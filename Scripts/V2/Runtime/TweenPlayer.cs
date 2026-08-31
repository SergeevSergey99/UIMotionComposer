using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIMotionComposer.V2
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/UI Motion Composer V2/Tween Player")]
    public sealed class TweenPlayer : MonoBehaviour
    {
        [SerializeField]
        private List<TweenAnimation> animations = new List<TweenAnimation>();

        [SerializeField]
        private List<TweenTargetOverride> targetOverrides = new List<TweenTargetOverride>();

        [SerializeField]
        private List<string> playOnEnable = new List<string>();

        [SerializeField, HideInInspector]
        private bool hasCapturedInitialValues;

        [SerializeField, HideInInspector]
        private List<TweenInitialValue> capturedInitialValues = new List<TweenInitialValue>();

        private readonly Dictionary<string, object> _initialValues = new Dictionary<string, object>();
        private bool _isCapturingInitialValues;
        private TweenPlayback _preview;

        public IReadOnlyList<TweenAnimation> Animations => animations;
        public IReadOnlyList<TweenTargetOverride> TargetOverrides => targetOverrides;
        public bool HasCapturedInitialValues => hasCapturedInitialValues;
        public int CapturedInitialValueCount => capturedInitialValues?.Count ?? 0;

        /// <summary>
        /// Mutable authoring API used by editor tooling and importers. Runtime code should normally
        /// prefer <see cref="Animations"/> and <see cref="FindAnimation"/>.
        /// </summary>
        public List<TweenAnimation> AnimationDefinitions => animations;

        public List<TweenTargetOverride> TargetOverrideDefinitions => targetOverrides;
        public List<string> PlayOnEnableAnimations => playOnEnable;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            for (int i = 0; i < playOnEnable.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(playOnEnable[i]))
                    Play(playOnEnable[i]);
            }
        }

        private void OnDestroy()
        {
            StopPreview();
            TweenRuntimeRunner.StopAll(this, false);
        }

        public TweenHandle Play(string animationId)
        {
            TweenAnimation animation = FindAnimation(animationId);
            if (animation == null)
            {
                Debug.LogWarning($"[UI Motion Composer] Animation '{animationId}' was not found on {name}.", this);
                return TweenHandle.Invalid;
            }

            TweenPlayback playback = TweenPlayback.Create(this, animation, false);
            if (playback == null)
            {
                // Distinct from "not found": the animation exists but no enabled clip resolved a
                // target, so callers waiting on the handle would otherwise see a silent no-op.
                Debug.LogWarning(
                    $"[UI Motion Composer] Animation '{animationId}' on {name} has no enabled clip " +
                    "that resolves a target.", this);
                return TweenHandle.Invalid;
            }

            if (!TweenRuntimeRunner.TryRegister(playback))
                return TweenHandle.Invalid;

            animation.OnStarted?.Invoke();
            return playback.Handle;
        }

        public TweenHandle PlayShow() => Play(TweenIds.Show);
        public TweenHandle PlayHide() => Play(TweenIds.Hide);

        /// <summary>Void wrapper intended for Button/UnityEvent persistent listeners.</summary>
        public void PlayAnimation(string animationId)
        {
            Play(animationId);
        }

        /// <summary>Stops the matching playback at its current value and starts it again.</summary>
        public void RestartAnimation(string animationId)
        {
            Stop(animationId);
            Play(animationId);
        }

        public void Stop(string animationId, bool complete = false)
        {
            TweenRuntimeRunner.Stop(this, animationId, complete);
        }

        public void StopAll(bool complete = false)
        {
            TweenRuntimeRunner.StopAll(this, complete);
        }

        public void Complete(string animationId)
        {
            Stop(animationId, true);
        }

        public bool IsPlaying(string animationId)
        {
            return TweenRuntimeRunner.IsPlaying(this, animationId);
        }

        public float GetDuration(string animationId)
        {
            TweenAnimation animation = FindAnimation(animationId);
            if (animation == null)
                return 0f;

            return TweenPlayback.CalculateDuration(animation.EffectiveClips);
        }

        /// <summary>
        /// Returns authored clips that write the same resolved property during overlapping timeline
        /// ranges. Evaluation remains deterministic (the later clip wins), but surfacing it in the
        /// inspector prevents accidental double-authoring.
        /// </summary>
        public string[] GetBindingConflicts(string animationId)
        {
            TweenAnimation animation = FindAnimation(animationId);
            TweenPlayback playback = TweenPlayback.Create(this, animation, true);
            if (playback == null)
                return Array.Empty<string>();

            var conflicts = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<TweenClipState> states = playback.States;
            for (int i = 0; i < states.Count; i++)
            {
                TweenClipState first = states[i];
                if (first?.Clip == null || string.IsNullOrEmpty(first.BindingKey))
                    continue;

                for (int j = i + 1; j < states.Count; j++)
                {
                    TweenClipState second = states[j];
                    if (second?.Clip == null || first.BindingKey != second.BindingKey ||
                        !AuthoredRangesOverlap(first.Clip, second.Clip))
                        continue;

                    string property = first.BindingKey.Substring(first.BindingKey.IndexOf(':') + 1);
                    string targetName = first.Target != null ? first.Target.name : name;
                    conflicts.Add($"{targetName} · {property}: {ClipName(first.Clip)} ↔ {ClipName(second.Clip)}");
                }
            }

            var result = new string[conflicts.Count];
            conflicts.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        /// <summary>
        /// Samples an animation without side effects. The first call captures a snapshot; call
        /// <see cref="StopPreview"/> to restore it. Intended for custom inspectors and tooling.
        /// </summary>
        public bool Preview(string animationId, float normalizedTime)
        {
            if (_preview == null || _preview.Animation != FindAnimation(animationId))
                PreparePreview(animationId);

            return SamplePreparedPreview(normalizedTime);
        }

        /// <summary>
        /// Rebuilds the preview snapshot without writing an animated value. Editor tooling uses the
        /// returned targets to register Undo before the first sample is applied.
        /// </summary>
        public UnityEngine.Object[] PreparePreview(string animationId)
        {
            TweenAnimation animation = FindAnimation(animationId);
            StopPreview();
            if (animation == null)
                return Array.Empty<UnityEngine.Object>();

            _preview = TweenPlayback.Create(this, animation, true);
            return _preview?.GetAffectedTargets() ?? Array.Empty<UnityEngine.Object>();
        }

        public bool SamplePreparedPreview(float normalizedTime)
        {
            if (_preview == null)
                return false;

            _preview.SampleManual(Mathf.Clamp01(normalizedTime));
            return true;
        }

        public void StopPreview()
        {
            if (_preview == null)
                return;

            _preview.Restore();
            _preview = null;
        }

        /// <summary>
        /// Captures the authored pose after driven layouts have settled. Initial endpoints keep
        /// using this serialized pose across play sessions and scene reloads.
        /// </summary>
        [ContextMenu("Capture Initial Values")]
        public void CaptureInitialValues()
        {
            StopPreview();
            RebuildDrivenLayouts();

            _initialValues.Clear();
            capturedInitialValues ??= new List<TweenInitialValue>();
            capturedInitialValues.Clear();
            hasCapturedInitialValues = true;
            _isCapturingInitialValues = true;

            try
            {
                for (int animationIndex = 0; animationIndex < animations.Count; animationIndex++)
                {
                    TweenAnimation animation = animations[animationIndex];
                    if (animation == null)
                        continue;

                    IReadOnlyList<BaseTweenClip> clips = animation.EffectiveClips;
                    for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
                    {
                        BaseTweenClip clip = clips[clipIndex];
                        if (clip != null && clip.Enabled)
                            clip.Capture(this);
                    }
                }
            }
            finally
            {
                _isCapturingInitialValues = false;
            }
        }

        /// <summary>Imports the authored pose stored by a legacy V1 controller.</summary>
        public void ImportLegacyInitialValues(TempValues legacyValues)
        {
            CaptureInitialValues();
            if (legacyValues == null)
                return;

            RectTransform rect = transform as RectTransform;
            if (rect != null)
            {
                StoreCapturedInitial(rect, "RectTransform.AnchoredPosition",
                    (Vector2)legacyValues.position);
                StoreCapturedInitial(rect, "RectTransform.AnchoredPosition3D", legacyValues.position);
                StoreCapturedInitial(rect, "Transform.LocalScale", legacyValues.localScale);
                StoreCapturedInitial(rect, "Transform.LocalRotation", legacyValues.localRotation);
                StoreCapturedInitial(rect, "RectTransform.SizeDelta", legacyValues.sizeDelta);
                StoreCapturedInitial(rect, "RectTransform.Pivot", legacyValues.pivot);
            }

            CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                StoreCapturedInitial(canvasGroup, "Visual.Alpha", legacyValues.alpha);

            _initialValues.Clear();
        }

        [ContextMenu("Clear Captured Initial Values")]
        public void ClearCapturedInitialValues()
        {
            StopPreview();
            _initialValues.Clear();
            capturedInitialValues?.Clear();
            hasCapturedInitialValues = false;
        }

        /// <summary>Drops non-serialized preview/runtime caches after an editor Undo or import.</summary>
        public void InvalidateAuthoringCache()
        {
            StopPreview();
            _initialValues.Clear();
        }

        public TweenAnimation FindAnimation(string animationId)
        {
            if (string.IsNullOrWhiteSpace(animationId))
                return null;

            for (int i = 0; i < animations.Count; i++)
            {
                TweenAnimation animation = animations[i];
                if (animation != null && string.Equals(animation.Id, animationId, StringComparison.Ordinal))
                    return animation;
            }

            return null;
        }

        internal UnityEngine.Object ResolveTarget(string key, UnityEngine.Object directTarget)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                for (int i = 0; i < targetOverrides.Count; i++)
                {
                    TweenTargetOverride entry = targetOverrides[i];
                    if (entry != null && entry.Key == key && entry.Target != null)
                        return entry.Target;
                }

                // A named slot is an explicit target contract. Falling back to this GameObject when
                // a shared asset forgot its binding animates the wrong object and is very difficult
                // to diagnose. Inline clips may still provide a direct fallback deliberately.
                return directTarget;
            }

            return directTarget != null ? directTarget : gameObject;
        }

        internal T GetOrCaptureInitial<T>(UnityEngine.Object target, string propertyId,
            string bindingKey, T current)
        {
            if (string.IsNullOrEmpty(bindingKey))
                return current;

            if (_initialValues.TryGetValue(bindingKey, out object value) && value is T typed)
                return typed;

            if (!_isCapturingInitialValues && hasCapturedInitialValues && capturedInitialValues != null)
            {
                for (int i = 0; i < capturedInitialValues.Count; i++)
                {
                    TweenInitialValue entry = capturedInitialValues[i];
                    if (entry != null && entry.Matches(target, propertyId) && entry.TryGet(out T stored))
                    {
                        _initialValues[bindingKey] = stored;
                        return stored;
                    }
                }
            }

            _initialValues[bindingKey] = current;
            if (_isCapturingInitialValues)
                StoreCapturedInitial(target, propertyId, current);
            return current;
        }

        private void StoreCapturedInitial<T>(UnityEngine.Object target, string propertyId, T value)
        {
            if (target == null || string.IsNullOrEmpty(propertyId))
                return;

            for (int i = 0; i < capturedInitialValues.Count; i++)
            {
                TweenInitialValue existing = capturedInitialValues[i];
                if (existing != null && existing.Matches(target, propertyId))
                {
                    capturedInitialValues[i] = TweenInitialValue.Create(target, propertyId, value);
                    return;
                }
            }

            capturedInitialValues.Add(TweenInitialValue.Create(target, propertyId, value));
        }

        private void RebuildDrivenLayouts()
        {
            Canvas.ForceUpdateCanvases();
            var rects = new HashSet<RectTransform>();
            CollectRect(gameObject, rects);

            for (int i = 0; i < targetOverrides.Count; i++)
                CollectRect(targetOverrides[i]?.Target, rects);

            for (int animationIndex = 0; animationIndex < animations.Count; animationIndex++)
            {
                TweenAnimation animation = animations[animationIndex];
                if (animation == null)
                    continue;

                IReadOnlyList<BaseTweenClip> clips = animation.EffectiveClips;
                for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
                {
                    BaseTweenClip clip = clips[clipIndex];
                    if (clip is TargetedTweenClip { Enabled: true } targeted)
                        CollectRect(targeted.ResolveConfiguredTarget(this), rects);
                }
            }

            foreach (RectTransform rect in rects)
                rect.RebuildDrivenLayout();
            Canvas.ForceUpdateCanvases();
        }

        private static void CollectRect(UnityEngine.Object source, ISet<RectTransform> output)
        {
            RectTransform rect = source switch
            {
                RectTransform direct => direct,
                GameObject gameObject => gameObject.transform as RectTransform,
                Component component => component.transform as RectTransform,
                _ => null
            };

            if (rect != null)
                output.Add(rect);
        }

        internal void NotifyCompleted(TweenAnimation animation)
        {
            animation?.OnCompleted?.Invoke();
        }

        internal void NotifyCancelled(TweenAnimation animation)
        {
            animation?.OnCancelled?.Invoke();
        }

        private static bool AuthoredRangesOverlap(BaseTweenClip first, BaseTweenClip second)
        {
            float firstStart = Mathf.Max(0f, first.Delay);
            float secondStart = Mathf.Max(0f, second.Delay);
            float firstEnd = Mathf.Max(firstStart, first.EndTime);
            float secondEnd = Mathf.Max(secondStart, second.EndTime);

            bool firstMarker = Mathf.Approximately(firstStart, firstEnd);
            bool secondMarker = Mathf.Approximately(secondStart, secondEnd);
            if (firstMarker || secondMarker)
                return Mathf.Abs(firstStart - secondStart) < 0.0001f;

            return Mathf.Max(firstStart, secondStart) < Mathf.Min(firstEnd, secondEnd) - 0.0001f;
        }

        private static string ClipName(BaseTweenClip clip)
        {
            return string.IsNullOrWhiteSpace(clip.Label)
                ? clip.GetType().Name.Replace("TweenClip", string.Empty)
                : clip.Label;
        }
    }

    public sealed class TweenHandle
    {
        internal static readonly TweenHandle Invalid = new TweenHandle(null);
        private readonly TweenPlayback _playback;
        private Action _completedCallbacks;
        private Action _cancelledCallbacks;
        private bool _completed;
        private bool _cancelled;

        internal TweenHandle(TweenPlayback playback)
        {
            _playback = playback;
        }

        public bool IsValid => _playback != null;
        public bool IsActive => _playback != null && _playback.IsActive;
        public bool IsPaused => _playback != null && _playback.IsPaused;
        public float NormalizedTime => _playback?.NormalizedTime ?? 0f;
        public bool WasCompleted => _completed;
        public bool WasCancelled => _cancelled;

        /// <summary>Registers a completion callback and returns this handle for fluent setup.</summary>
        public TweenHandle OnCompleted(Action callback)
        {
            if (callback == null || !IsValid)
                return this;

            if (_completed)
                callback.Invoke();
            else if (!_cancelled)
                _completedCallbacks += callback;
            return this;
        }

        /// <summary>Registers a cancellation callback and returns this handle for fluent setup.</summary>
        public TweenHandle OnCancelled(Action callback)
        {
            if (callback == null || !IsValid)
                return this;

            if (_cancelled)
                callback.Invoke();
            else if (!_completed)
                _cancelledCallbacks += callback;
            return this;
        }

        public void Pause()
        {
            if (_playback != null)
                _playback.IsPaused = true;
        }

        public void Resume()
        {
            if (_playback != null)
                _playback.IsPaused = false;
        }

        public void Stop() => _playback?.Stop(false);
        public void Complete() => _playback?.Stop(true);

        internal void NotifyCompleted()
        {
            if (_completed || _cancelled)
                return;

            _completed = true;
            Action callbacks = _completedCallbacks;
            _completedCallbacks = null;
            _cancelledCallbacks = null;
            callbacks?.Invoke();
        }

        internal void NotifyCancelled()
        {
            if (_completed || _cancelled)
                return;

            _cancelled = true;
            Action callbacks = _cancelledCallbacks;
            _completedCallbacks = null;
            _cancelledCallbacks = null;
            callbacks?.Invoke();
        }
    }

    internal sealed class TweenPlayback
    {
        private readonly List<TweenClipState> _states = new List<TweenClipState>();
        private float _time;
        private float _previousTime;
        private int _pass;
        private int _direction = 1;

        public TweenPlayer Player { get; }
        public TweenAnimation Animation { get; }
        public TweenHandle Handle { get; }
        public float Duration { get; }
        public bool IsPreview { get; }
        public bool IsActive { get; private set; } = true;
        public bool IsPaused { get; set; }
        public float NormalizedTime => Duration <= 0f ? 1f : Mathf.Clamp01(_time / Duration);

        public IReadOnlyList<TweenClipState> States => _states;

        public UnityEngine.Object[] GetAffectedTargets()
        {
            var targets = new List<UnityEngine.Object>();
            for (int i = 0; i < _states.Count; i++)
            {
                UnityEngine.Object target = _states[i].Target;
                if (target != null && !targets.Contains(target))
                    targets.Add(target);
            }

            return targets.ToArray();
        }

        private TweenPlayback(TweenPlayer player, TweenAnimation animation, bool preview)
        {
            Player = player;
            Animation = animation;
            IsPreview = preview;
            Duration = CalculateDuration(animation.EffectiveClips);
            Handle = new TweenHandle(this);
        }

        public static TweenPlayback Create(TweenPlayer player, TweenAnimation animation, bool preview)
        {
            if (player == null || animation == null)
                return null;

            var playback = new TweenPlayback(player, animation, preview);
            IReadOnlyList<BaseTweenClip> clips = animation.EffectiveClips;

            // Capture every property before any clip writes its From value. This keeps parallel
            // clips deterministic and lets preview restore the exact authored state.
            for (int i = 0; i < clips.Count; i++)
            {
                BaseTweenClip clip = clips[i];
                if (clip == null || !clip.Enabled)
                    continue;

                TweenClipState state = clip.Capture(player);
                if (state != null)
                    playback._states.Add(state);
            }

            if (playback._states.Count == 0)
                return null;

            return playback;
        }

        public void Begin()
        {
            if (IsActive)
                Sample(0f, 0f, false);
        }

        public static float CalculateDuration(IReadOnlyList<BaseTweenClip> clips)
        {
            float duration = 0f;
            if (clips == null)
                return duration;

            for (int i = 0; i < clips.Count; i++)
            {
                BaseTweenClip clip = clips[i];
                if (clip != null && clip.Enabled)
                    duration = Mathf.Max(duration, clip.EndTime);
            }

            return duration;
        }

        public bool Overlaps(TweenPlayback other)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                string ownKey = _states[i].BindingKey;
                if (string.IsNullOrEmpty(ownKey))
                    continue;

                for (int j = 0; j < other._states.Count; j++)
                {
                    if (ownKey == other._states[j].BindingKey)
                        return true;
                }
            }

            return false;
        }

        public void Tick(float scaledDelta, float unscaledDelta)
        {
            if (!IsActive || IsPaused || IsWaitingForNestedAnimation())
                return;

            TweenPlaybackSettings settings = Animation.Playback ?? new TweenPlaybackSettings();
            float delta = settings.UnscaledTime ? unscaledDelta : scaledDelta;

            if (Duration <= 0f)
            {
                Stop(true);
                return;
            }

            _previousTime = _time;
            float proposedTime = _time + delta * _direction;
            _time = ClampToNextWaitMarker(proposedTime);

            if (_direction > 0 && _time >= Duration)
            {
                _time = Duration;
                Sample(_previousTime, _time, true);
                if (IsWaitingForNestedAnimation())
                    return;
                if (!TryStartNextPass())
                    Finish();
                return;
            }

            if (_direction < 0 && _time <= 0f)
            {
                _time = 0f;
                Sample(_previousTime, _time, true);
                if (IsWaitingForNestedAnimation())
                    return;
                if (!TryStartNextPass())
                    Finish();
                return;
            }

            Sample(_previousTime, _time, true);
        }

        public void SampleManual(float normalizedTime)
        {
            float next = Mathf.Clamp01(normalizedTime) * Duration;
            Sample(_time, next, false);
            _previousTime = _time;
            _time = next;
        }

        public void Restore()
        {
            for (int i = _states.Count - 1; i >= 0; i--)
                _states[i].Clip.Restore(Player, _states[i]);
        }

        public void Stop(bool complete)
        {
            if (!IsActive)
                return;

            if (complete)
            {
                float end = _direction > 0 ? Duration : 0f;
                Sample(_time, end, true);
                _time = end;
                Finish();
                return;
            }

            IsActive = false;
            ReleaseNestedAnimations(false);
            try
            {
                Player.NotifyCancelled(Animation);
            }
            finally
            {
                Handle.NotifyCancelled();
            }
        }

        private bool TryStartNextPass()
        {
            TweenPlaybackSettings settings = Animation.Playback ?? new TweenPlaybackSettings();
            if (settings.LoopMode == TweenLoopMode.None)
                return false;

            int loopCount = settings.LoopCount == 0 ? 1 : settings.LoopCount;
            if (loopCount > 0 && _pass + 1 >= loopCount)
                return false;

            _pass++;
            for (int i = 0; i < _states.Count; i++)
                _states[i].Clip.ResetPass(_states[i]);

            if (settings.LoopMode == TweenLoopMode.PingPong)
            {
                _direction *= -1;
                _previousTime = _time;
            }
            else
            {
                _direction = 1;
                _previousTime = Duration;
                _time = 0f;
                Sample(_previousTime, _time, true);
            }

            return true;
        }

        private void Sample(float previousTime, float time, bool allowSideEffects)
        {
            TweenPlaybackSettings settings = Animation.Playback ?? new TweenPlaybackSettings();
            var sample = new TweenSampleInfo(previousTime, time, _pass, _direction > 0,
                allowSideEffects && !IsPreview, settings.BlendMode == TweenBlendMode.Additive);

            for (int i = 0; i < _states.Count; i++)
                _states[i].Clip.Evaluate(Player, _states[i], sample);
        }

        private void Finish()
        {
            if (!IsActive)
                return;

            IsActive = false;
            ReleaseNestedAnimations(true);
            try
            {
                Player.NotifyCompleted(Animation);
            }
            finally
            {
                Handle.NotifyCompleted();
            }
        }

        private float ClampToNextWaitMarker(float proposedTime)
        {
            float result = proposedTime;
            bool found = false;
            bool forward = _direction > 0;
            for (int i = 0; i < _states.Count; i++)
            {
                TweenClipState state = _states[i];
                if (state.Clip is not PlayTweenAnimationClip nested ||
                    !nested.TryGetWaitMarker(state, _pass, forward, _time, proposedTime,
                        out float marker))
                    continue;

                result = !found
                    ? marker
                    : forward ? Mathf.Min(result, marker) : Mathf.Max(result, marker);
                found = true;
            }

            return result;
        }

        private bool IsWaitingForNestedAnimation()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                TweenClipState state = _states[i];
                if (state.Clip is PlayTweenAnimationClip nested && nested.IsWaiting(state))
                    return true;
            }

            return false;
        }

        private void ReleaseNestedAnimations(bool complete)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                TweenClipState state = _states[i];
                if (state.Clip is PlayTweenAnimationClip nested)
                    nested.ReleaseNested(state, complete);
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class TweenRuntimeRunner : MonoBehaviour
    {
        private static TweenRuntimeRunner _instance;
        private readonly List<TweenPlayback> _playbacks = new List<TweenPlayback>();

        private static TweenRuntimeRunner Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                var host = new GameObject("[UI Motion Composer V2 Runner]")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (Application.isPlaying)
                    DontDestroyOnLoad(host);
                _instance = host.AddComponent<TweenRuntimeRunner>();
                return _instance;
            }
        }

        public static bool TryRegister(TweenPlayback incoming)
        {
            TweenRuntimeRunner runner = Instance;
            TweenPlaybackSettings settings = incoming.Animation.Playback ?? new TweenPlaybackSettings();

            for (int i = runner._playbacks.Count - 1; i >= 0; i--)
            {
                TweenPlayback active = runner._playbacks[i];
                if (!active.IsActive)
                    continue;

                bool sameAnimation = active.Player == incoming.Player &&
                                     active.Animation == incoming.Animation;
                if (sameAnimation && !settings.AllowSelfOverride)
                    return false;

                if (settings.BlendMode != TweenBlendMode.Override || !incoming.Overlaps(active))
                    continue;

                TweenKillBehavior behavior = active.Animation.Playback?.KillBehavior ?? TweenKillBehavior.Cancel;
                active.Stop(behavior == TweenKillBehavior.Complete);
            }

            runner._playbacks.Add(incoming);
            incoming.Begin();
            return true;
        }

        public static void Stop(TweenPlayer player, string animationId, bool complete)
        {
            if (_instance == null)
                return;

            for (int i = _instance._playbacks.Count - 1; i >= 0; i--)
            {
                TweenPlayback playback = _instance._playbacks[i];
                if (playback.Player == player && playback.Animation.Id == animationId)
                    playback.Stop(complete);
            }
        }

        public static void StopAll(TweenPlayer player, bool complete)
        {
            if (_instance == null)
                return;

            for (int i = _instance._playbacks.Count - 1; i >= 0; i--)
            {
                TweenPlayback playback = _instance._playbacks[i];
                if (playback.Player == player)
                    playback.Stop(complete);
            }
        }

        public static bool IsPlaying(TweenPlayer player, string animationId)
        {
            if (_instance == null)
                return false;

            for (int i = 0; i < _instance._playbacks.Count; i++)
            {
                TweenPlayback playback = _instance._playbacks[i];
                if (playback.IsActive && playback.Player == player && playback.Animation.Id == animationId)
                    return true;
            }

            return false;
        }

        private void Update()
        {
            for (int i = _playbacks.Count - 1; i >= 0; i--)
            {
                TweenPlayback playback = _playbacks[i];
                if (playback.Player == null)
                {
                    _playbacks.RemoveAt(i);
                    continue;
                }

                playback.Tick(Time.deltaTime, Time.unscaledDeltaTime);
                if (!playback.IsActive)
                    _playbacks.RemoveAt(i);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
