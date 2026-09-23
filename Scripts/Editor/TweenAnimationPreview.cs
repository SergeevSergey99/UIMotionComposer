using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UIMotionComposer.Editor
{
    /// <summary>
    /// Edit-mode preview of one animation of one TweenPlayer, owned by that animation's drawer.
    /// A player holds a single preview snapshot, so starting one preview stops any other preview
    /// of the same player first and restores its pose.
    /// </summary>
    internal sealed class TweenAnimationPreview : IDisposable
    {
        private static readonly Dictionary<TweenPlayer, TweenAnimationPreview> Running =
            new Dictionary<TweenPlayer, TweenAnimationPreview>();

        private readonly TweenPlayer _player;
        private readonly SerializedObject _owner;
        private readonly string _animationPath;
        private TweenPreviewAnimationMode _animationMode;
        private SerializedObject _assetSource;
        private string _animationId;
        private int _fingerprint;
        private double _startedAt;
        private float _startedFrom;
        private bool _blockedWarned;
        private bool _hooked;

        public float Time { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool Loop { get; set; }
        public bool IsActive { get; private set; }

        /// <summary>Raised whenever the sampled time changes, so the drawer can move its playhead.</summary>
        public event Action Changed;

        public TweenAnimationPreview(SerializedObject owner, string animationPath)
        {
            _owner = owner;
            _animationPath = animationPath;
            _player = owner.targetObjects.Length == 1 ? owner.targetObject as TweenPlayer : null;
        }

        public bool CanPreview => _player != null && !Application.isPlaying &&
                                  !string.IsNullOrWhiteSpace(AnimationId());

        public void Hook()
        {
            if (_hooked)
                return;
            _hooked = true;
            EditorApplication.update += Update;
            EditorApplication.focusChanged += OnFocusChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        public void Dispose()
        {
            if (_hooked)
            {
                _hooked = false;
                EditorApplication.update -= Update;
                EditorApplication.focusChanged -= OnFocusChanged;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                AssemblyReloadEvents.beforeAssemblyReload -= Stop;
                Undo.undoRedoPerformed -= OnUndoRedo;
            }

            Stop();
            _animationMode?.Dispose();
            _animationMode = null;
            _assetSource?.Dispose();
            _assetSource = null;
        }

        public void Scrub(float normalizedTime)
        {
            _owner.ApplyModifiedProperties();
            IsPlaying = false;
            Time = Mathf.Clamp01(normalizedTime);
            Sample(Time);
        }

        public void Rewind() => Scrub(0f);

        public void TogglePlay()
        {
            _owner.ApplyModifiedProperties();
            if (IsPlaying)
            {
                IsPlaying = false;
                Changed?.Invoke();
                return;
            }

            _startedFrom = Time >= 0.999f ? 0f : Time;
            Time = _startedFrom;
            _startedAt = EditorApplication.timeSinceStartup;
            IsPlaying = Sample(Time);
        }

        /// <summary>Restores the pose that existed before preview started.</summary>
        public void Stop()
        {
            IsPlaying = false;
            if (IsActive && _player != null)
                _player.StopPreview();
            _animationMode?.Stop();
            if (_player != null && Running.TryGetValue(_player, out TweenAnimationPreview running) && running == this)
                Running.Remove(_player);
            IsActive = false;
            _animationId = null;
            _fingerprint = 0;
            SceneView.RepaintAll();
            Changed?.Invoke();
        }

        /// <summary>Resamples at the same point after the animation's authored data changed.</summary>
        public void RefreshIfAuthoringChanged()
        {
            if (!IsActive || _player == null || Fingerprint() == _fingerprint)
                return;

            // PreparePreview restores the captured pose before recapturing, so From/To resolved as
            // Current read the authored pose rather than wherever the last sample left the object.
            UnityEngine.Object[] affected = _player.PreparePreview(_animationId);
            if (affected.Length == 0)
            {
                Stop();
                return;
            }

            // Retargeting a clip mid-session can pull in an object the session never registered.
            _animationMode?.RegisterTargets(affected);
            _fingerprint = Fingerprint();
            _player.SamplePreparedPreview(Time);
            SceneView.RepaintAll();
        }

        private void Update()
        {
            if (!IsPlaying || _player == null || Application.isPlaying)
                return;

            string id = AnimationId();
            float duration = Mathf.Max(0.01f, _player.GetDuration(id));
            float raw = _startedFrom + (float)(EditorApplication.timeSinceStartup - _startedAt) / duration;
            bool infinite = _player.IsInfinite(id);
            Time = Loop || infinite ? Mathf.Repeat(raw, 1f) : Mathf.Clamp01(raw);
            if (infinite)
                SampleSeconds(Mathf.Max(0f, raw) * duration);
            else
                Sample(Time);

            if (!Loop && !infinite && Time >= 1f)
                IsPlaying = false;
        }

        private bool Sample(float normalizedTime)
        {
            bool sampled = Ensure() && _player.SamplePreparedPreview(normalizedTime);
            SceneView.RepaintAll();
            Changed?.Invoke();
            return sampled;
        }

        private void SampleSeconds(float seconds)
        {
            if (Ensure())
                _player.SamplePreparedPreviewTime(seconds);
            SceneView.RepaintAll();
            Changed?.Invoke();
        }

        private bool Ensure()
        {
            if (!CanPreview)
                return false;

            string id = AnimationId();
            if (IsActive && string.Equals(_animationId, id, StringComparison.Ordinal))
                return true;

            Stop();
            if (Running.TryGetValue(_player, out TweenAnimationPreview other) && other != this)
                other.Stop();

            UnityEngine.Object[] affected = _player.PreparePreview(id);
            if (affected.Length == 0)
                return false;

            _animationMode ??= new TweenPreviewAnimationMode();
            if (!_animationMode.TryStart())
            {
                _player.StopPreview();

                // Once per blocked stretch: every scrub and every Play press comes back here
                // while the Animation window stays open.
                if (!_blockedWarned)
                {
                    _blockedWarned = true;
                    Debug.LogWarning("[UI Motion Composer] Preview cannot start while another Animation Mode driver is active.", _player);
                }
                return false;
            }

            _blockedWarned = false;
            _animationMode.RegisterTargets(affected);
            Running[_player] = this;
            IsActive = true;
            _animationId = id;
            _fingerprint = Fingerprint();
            return true;
        }

        private string AnimationId()
        {
            SerializedProperty animation = Animation();
            return animation?.FindPropertyRelative("Id")?.stringValue ?? string.Empty;
        }

        private SerializedProperty Animation()
        {
            try
            {
                return _owner.targetObject != null ? _owner.FindProperty(_animationPath) : null;
            }
            catch (Exception)
            {
                // The SerializedObject was disposed together with its inspector.
                return null;
            }
        }

        /// <summary>
        /// Hash of everything one preview sample reads: the animation's clips and playback settings,
        /// and the shared asset's clips when one is assigned.
        /// </summary>
        private int Fingerprint()
        {
            SerializedProperty animation = Animation();
            if (animation == null)
                return 0;

            int hash = TweenAuthoringFingerprint.Of(animation);
            var asset = animation.FindPropertyRelative("Asset").objectReferenceValue as TweenAnimationAsset;
            if (asset == null)
                return hash;

            if (_assetSource == null || _assetSource.targetObject != asset)
            {
                _assetSource?.Dispose();
                _assetSource = new SerializedObject(asset);
            }
            _assetSource.Update();
            return TweenAuthoringFingerprint.Combine(hash, TweenAuthoringFingerprint.Of(_assetSource.FindProperty("Clips")));
        }

        private void OnFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
                Stop();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change) => Stop();

        private void OnUndoRedo()
        {
            Stop();
            if (_player != null)
                _player.InvalidateAuthoringCache();
        }
    }
}
