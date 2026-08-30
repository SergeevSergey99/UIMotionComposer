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

        private readonly Dictionary<string, object> _initialValues = new Dictionary<string, object>();
        private TweenPlayback _preview;

        public IReadOnlyList<TweenAnimation> Animations => animations;
        public IReadOnlyList<TweenTargetOverride> TargetOverrides => targetOverrides;

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
                return TweenHandle.Invalid;

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
        /// Samples an animation without side effects. The first call captures a snapshot; call
        /// <see cref="StopPreview"/> to restore it. Intended for custom inspectors and tooling.
        /// </summary>
        public bool Preview(string animationId, float normalizedTime)
        {
            TweenAnimation animation = FindAnimation(animationId);
            if (animation == null)
                return false;

            if (_preview == null || _preview.Animation != animation)
            {
                StopPreview();
                _preview = TweenPlayback.Create(this, animation, true);
            }

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

        public void ClearCapturedInitialValues()
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
            }

            return directTarget != null ? directTarget : gameObject;
        }

        internal T GetOrCaptureInitial<T>(string bindingKey, T current)
        {
            if (string.IsNullOrEmpty(bindingKey))
                return current;

            if (_initialValues.TryGetValue(bindingKey, out object value) && value is T typed)
                return typed;

            _initialValues[bindingKey] = current;
            return current;
        }

        internal void NotifyCompleted(TweenAnimation animation)
        {
            animation?.OnCompleted?.Invoke();
        }

        internal void NotifyCancelled(TweenAnimation animation)
        {
            animation?.OnCancelled?.Invoke();
        }
    }

    public sealed class TweenHandle
    {
        internal static readonly TweenHandle Invalid = new TweenHandle(null);
        private readonly TweenPlayback _playback;

        internal TweenHandle(TweenPlayback playback)
        {
            _playback = playback;
        }

        public bool IsValid => _playback != null;
        public bool IsActive => _playback != null && _playback.IsActive;
        public bool IsPaused => _playback != null && _playback.IsPaused;
        public float NormalizedTime => _playback?.NormalizedTime ?? 0f;

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
            if (!IsActive || IsPaused)
                return;

            TweenPlaybackSettings settings = Animation.Playback ?? new TweenPlaybackSettings();
            float delta = settings.UnscaledTime ? unscaledDelta : scaledDelta;

            if (Duration <= 0f)
            {
                Stop(true);
                return;
            }

            _previousTime = _time;
            _time += delta * _direction;

            if (_direction > 0 && _time >= Duration)
            {
                _time = Duration;
                Sample(_previousTime, _time, true);
                if (!TryStartNextPass())
                    Finish();
                return;
            }

            if (_direction < 0 && _time <= 0f)
            {
                _time = 0f;
                Sample(_previousTime, _time, true);
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
            Player.NotifyCancelled(Animation);
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
            Player.NotifyCompleted(Animation);
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
