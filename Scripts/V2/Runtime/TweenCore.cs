using System;
using System.Collections.Generic;
using UIMotionComposer.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace UIMotionComposer.V2
{
    public enum TweenLoopMode
    {
        None,
        Restart,
        PingPong
    }

    public enum TweenBlendMode
    {
        Override,
        Additive
    }

    public enum TweenKillBehavior
    {
        Cancel,
        Complete
    }

    public enum TweenNestedPlaybackMode
    {
        [Tooltip("Start the child animation and let it continue independently.")]
        FireAndForget,

        [Tooltip("Pause the parent timeline at this marker until the child animation finishes.")]
        Wait,

        [Tooltip("Run in parallel, but cancel or complete the child together with the parent.")]
        LinkLifetime
    }

    public enum TweenEndpointMode
    {
        Current,
        Initial,
        Custom,
        OffsetFromInitial
    }

    [Flags]
    public enum TweenVectorComponents
    {
        None = 0,
        X = 1,
        Y = 2,
        Z = 4,
        All2D = X | Y,
        All = X | Y | Z
    }

    public enum TweenFadeTarget
    {
        Auto,
        CanvasGroup,
        Graphic,
        SpriteRenderer
    }

    public enum TweenColorTarget
    {
        Auto,
        Graphic,
        SpriteRenderer,
        Renderer
    }

    [Serializable]
    public sealed class TweenPlaybackSettings
    {
        [Tooltip("Use Time.unscaledDeltaTime. Useful for pause menus.")]
        public bool UnscaledTime = true;

        public TweenBlendMode BlendMode = TweenBlendMode.Override;
        public TweenKillBehavior KillBehavior = TweenKillBehavior.Cancel;

        [Tooltip("When disabled, Play ignores a request for the same animation while it is active.")]
        public bool AllowSelfOverride = true;

        public TweenLoopMode LoopMode = TweenLoopMode.None;

        [Tooltip("Total number of passes. Use -1 for infinite looping.")]
        public int LoopCount = 1;
    }

    [Serializable]
    public sealed class TweenAnimation
    {
        [Tooltip("Built-in names such as Show, Hide, Hover and Click are conventions, not restrictions.")]
        public string Id = "Show";

        [Tooltip("When assigned, clips come from this shared asset. Playback settings and events remain local.")]
        public TweenAnimationAsset Asset;

        public TweenPlaybackSettings Playback = new TweenPlaybackSettings();

        [SerializeReference]
        public List<BaseTweenClip> Clips = new List<BaseTweenClip>();

        public UnityEvent OnStarted = new UnityEvent();
        public UnityEvent OnCompleted = new UnityEvent();
        public UnityEvent OnCancelled = new UnityEvent();

        public IReadOnlyList<BaseTweenClip> EffectiveClips =>
            Asset != null ? Asset.Clips : Clips;
    }

    [Serializable]
    public sealed class TweenTargetOverride
    {
        public string Key;
        public UnityEngine.Object Target;
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class TweenClipMenuAttribute : Attribute
    {
        public string Path { get; }

        public TweenClipMenuAttribute(string path)
        {
            Path = path;
        }
    }

    public static class TweenIds
    {
        public const string Normal = "Normal";
        public const string Show = "Show";
        public const string Hide = "Hide";
        public const string Idle = "Idle";
        public const string Highlight = "Highlight";
        public const string Click = "Click";
        public const string Hover = "Hover";
        public const string Unhover = "Unhover";
        public const string Interactable = "Interactable";
        public const string Disabled = "Disabled";
        public const string Success = "Success";
        public const string Error = "Error";
        public const string Warning = "Warning";
    }

    internal sealed class TweenClipState
    {
        public BaseTweenClip Clip;
        public UnityEngine.Object Target;
        public string BindingKey;
        public object Original;
        public object Initial;
        public object From;
        public object To;
        public object Extra;
        public object Metadata;
        public int LastTriggeredPass = -1;
    }

    internal readonly struct TweenSampleInfo
    {
        public readonly float PreviousTime;
        public readonly float Time;
        public readonly int Pass;
        public readonly bool Forward;
        public readonly bool AllowSideEffects;
        public readonly bool Additive;

        public TweenSampleInfo(float previousTime, float time, int pass, bool forward,
            bool allowSideEffects, bool additive)
        {
            PreviousTime = previousTime;
            Time = time;
            Pass = pass;
            Forward = forward;
            AllowSideEffects = allowSideEffects;
            Additive = additive;
        }
    }

    [Serializable]
    public abstract class BaseTweenClip
    {
        public bool Enabled = true;
        public string Label;

        [Min(0f)] public float Delay;

        /// <summary>Trigger clips end at their marker; duration clips override this.</summary>
        public virtual float EndTime => Mathf.Max(0f, Delay);
        public virtual bool IsInfinite => false;
        public virtual bool HasSideEffects => false;

        internal abstract TweenClipState Capture(TweenPlayer player);
        internal abstract void Evaluate(TweenPlayer player, TweenClipState state, in TweenSampleInfo sample);
        internal abstract void Restore(TweenPlayer player, TweenClipState state);

        internal virtual void ResetPass(TweenClipState state)
        {
            state.LastTriggeredPass = -1;
        }

        protected static T ResolveComponent<T>(UnityEngine.Object source) where T : Component
        {
            switch (source)
            {
                case T component:
                    return component;
                case GameObject gameObject:
                    return gameObject.GetComponent<T>();
                case Component other:
                    return other.GetComponent<T>();
                default:
                    return null;
            }
        }

        protected static GameObject ResolveGameObject(UnityEngine.Object source)
        {
            return source switch
            {
                GameObject gameObject => gameObject,
                Component component => component.gameObject,
                _ => null
            };
        }

        protected static string MakeBindingKey(UnityEngine.Object target, string property)
        {
            return target == null ? string.Empty : target.GetInstanceID() + ":" + property;
        }

        protected static Vector3 ResolveVector3(TweenEndpointMode mode, Vector3 custom, Vector3 offset,
            Vector3 current, Vector3 initial)
        {
            return mode switch
            {
                TweenEndpointMode.Current => current,
                TweenEndpointMode.Initial => initial,
                TweenEndpointMode.Custom => custom,
                TweenEndpointMode.OffsetFromInitial => initial + offset,
                _ => current
            };
        }

        protected static Vector2 ResolveVector2(TweenEndpointMode mode, Vector2 custom, Vector2 offset,
            Vector2 current, Vector2 initial)
        {
            return mode switch
            {
                TweenEndpointMode.Current => current,
                TweenEndpointMode.Initial => initial,
                TweenEndpointMode.Custom => custom,
                TweenEndpointMode.OffsetFromInitial => initial + offset,
                _ => current
            };
        }

        protected static float ResolveFloat(TweenEndpointMode mode, float custom, float offset,
            float current, float initial)
        {
            return mode switch
            {
                TweenEndpointMode.Current => current,
                TweenEndpointMode.Initial => initial,
                TweenEndpointMode.Custom => custom,
                TweenEndpointMode.OffsetFromInitial => initial + offset,
                _ => current
            };
        }

        protected static Color ResolveColor(TweenEndpointMode mode, Color custom, Color offset,
            Color current, Color initial)
        {
            return mode switch
            {
                TweenEndpointMode.Current => current,
                TweenEndpointMode.Initial => initial,
                TweenEndpointMode.Custom => custom,
                TweenEndpointMode.OffsetFromInitial => initial + offset,
                _ => current
            };
        }
    }

    /// <summary>A clip whose effect is applied to a player-local or directly assigned object.</summary>
    [Serializable]
    public abstract class TargetedTweenClip : BaseTweenClip
    {
        [Tooltip("Optional direct target for an inline clip. Shared assets should use Target Slot instead. When both fields are empty, the TweenPlayer object is used.")]
        public UnityEngine.Object Target;

        [Tooltip("Portable target slot resolved by each TweenPlayer. It takes priority over Direct Target. Use this for clips stored in a shared asset.")]
        public string TargetKey;

        internal UnityEngine.Object ResolveConfiguredTarget(TweenPlayer player)
        {
            return player.ResolveTarget(TargetKey, Target);
        }
    }

    /// <summary>A continuous, eased clip with a start and an end on the timeline.</summary>
    [Serializable]
    public abstract class DurationTweenClip : TargetedTweenClip
    {
        [Min(0f)] public float Duration = 0.3f;

        [Tooltip("Repeat this clip independently. Animation-level Loop still repeats the whole composition.")]
        public TweenLoopMode RepeatMode = TweenLoopMode.None;

        [Tooltip("Total number of clip passes. Use -1 for an infinite repeat.")]
        public int RepeatCount = 1;

        [Min(0f), Tooltip("Pause between clip passes.")]
        public float RepeatDelay;

        public UIEase Ease = UIEase.OutQuad;
        public bool UseCustomCurve;
        public AnimationCurve CustomCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Apply the configured From value while this clip is waiting for its delay.")]
        public bool ApplyFromBeforeDelay = true;

        public override bool IsInfinite => RepeatMode != TweenLoopMode.None && RepeatCount < 0 && Duration > 0f;

        public override float EndTime
        {
            get
            {
                float delay = Mathf.Max(0f, Delay);
                float duration = Mathf.Max(0f, Duration);
                if (RepeatMode == TweenLoopMode.None || duration <= 0f || IsInfinite)
                    return delay + duration;

                int count = Mathf.Max(1, RepeatCount);
                return delay + duration * count + Mathf.Max(0f, RepeatDelay) * (count - 1);
            }
        }

        protected float Progress(float time)
        {
            float delay = Mathf.Max(0f, Delay);
            if (time < delay)
                return 0f;

            float duration = Mathf.Max(0f, Duration);
            if (duration <= 0f)
                return 1f;

            float elapsed = Mathf.Max(0f, time - delay);
            if (RepeatMode == TweenLoopMode.None)
                return Mathf.Clamp01(elapsed / duration);

            int count = RepeatCount < 0 ? int.MaxValue : Mathf.Max(1, RepeatCount);
            float repeatDelay = Mathf.Max(0f, RepeatDelay);
            float span = duration + repeatDelay;
            float total = duration * count + repeatDelay * Mathf.Max(0, count - 1);
            if (RepeatCount >= 0 && elapsed >= total)
                return PassEndProgress(count - 1);

            int pass = span <= 0f ? 0 : Mathf.Max(0, Mathf.FloorToInt(elapsed / span));
            if (RepeatCount >= 0)
                pass = Mathf.Min(pass, count - 1);
            float passTime = elapsed - pass * span;
            if (passTime >= duration)
                return PassEndProgress(pass);

            float progress = Mathf.Clamp01(passTime / duration);
            return RepeatMode == TweenLoopMode.PingPong && (pass & 1) == 1
                ? 1f - progress
                : progress;
        }

        protected bool ShouldApply(float time)
        {
            return ApplyFromBeforeDelay || time >= Mathf.Max(0f, Delay);
        }

        protected float EaseProgress(float progress)
        {
            return UseCustomCurve && CustomCurve != null
                ? CustomCurve.Evaluate(progress)
                : UIEaseEvaluator.Evaluate(Ease, progress);
        }

        private float PassEndProgress(int pass)
        {
            return RepeatMode == TweenLoopMode.PingPong && (pass & 1) == 1 ? 0f : 1f;
        }
    }

    /// <summary>A targetless, one-shot marker such as a UnityEvent.</summary>
    [Serializable]
    public abstract class TriggerTweenClip : BaseTweenClip
    {
        public bool FireOnReverse;
        public sealed override bool HasSideEffects => true;

        internal sealed override void Evaluate(TweenPlayer player, TweenClipState state,
            in TweenSampleInfo sample)
        {
            if (!sample.AllowSideEffects || state == null || state.LastTriggeredPass == sample.Pass ||
                !Crossed(sample))
                return;

            state.LastTriggeredPass = sample.Pass;
            Fire(player, state, sample.Forward);
        }

        internal abstract void Fire(TweenPlayer player, TweenClipState state, bool forward);

        private bool Crossed(in TweenSampleInfo sample)
        {
            float trigger = Mathf.Max(0f, Delay);
            return sample.Forward
                ? sample.PreviousTime <= trigger && sample.Time >= trigger
                : FireOnReverse && sample.PreviousTime >= trigger && sample.Time <= trigger;
        }
    }

    /// <summary>A one-shot marker that resolves a concrete object through Direct Target or Target Slot.</summary>
    [Serializable]
    public abstract class TargetedTriggerTweenClip : TargetedTweenClip
    {
        public bool FireOnReverse;
        public sealed override bool HasSideEffects => true;

        internal sealed override void Evaluate(TweenPlayer player, TweenClipState state,
            in TweenSampleInfo sample)
        {
            if (!sample.AllowSideEffects || state == null || state.LastTriggeredPass == sample.Pass ||
                !Crossed(sample))
                return;

            state.LastTriggeredPass = sample.Pass;
            Fire(player, state, sample.Forward);
        }

        internal abstract void Fire(TweenPlayer player, TweenClipState state, bool forward);

        private bool Crossed(in TweenSampleInfo sample)
        {
            float trigger = Mathf.Max(0f, Delay);
            return sample.Forward
                ? sample.PreviousTime <= trigger && sample.Time >= trigger
                : FireOnReverse && sample.PreviousTime >= trigger && sample.Time <= trigger;
        }
    }

    [Serializable]
    public abstract class Vector3TweenClip : DurationTweenClip
    {
        public TweenEndpointMode FromMode = TweenEndpointMode.Current;
        public Vector3 FromValue;
        public Vector3 FromOffset;

        public TweenEndpointMode ToMode = TweenEndpointMode.Custom;
        public Vector3 ToValue;
        public Vector3 ToOffset;

        public TweenVectorComponents Components = TweenVectorComponents.All;

        protected abstract string PropertyId { get; }
        protected abstract UnityEngine.Object ResolveTarget(TweenPlayer player);
        protected abstract Vector3 Read(UnityEngine.Object target);
        protected abstract void Write(UnityEngine.Object target, Vector3 value);

        protected virtual Vector3 Interpolate(Vector3 from, Vector3 to, float progress)
        {
            return Vector3.LerpUnclamped(from, to, progress);
        }

        internal override TweenClipState Capture(TweenPlayer player)
        {
            UnityEngine.Object target = ResolveTarget(player);
            if (target == null)
                return null;

            Vector3 current = Read(target);
            string key = MakeBindingKey(target, PropertyId);
            Vector3 initial = player.GetOrCaptureInitial(target, PropertyId, key, current);

            return new TweenClipState
            {
                Clip = this,
                Target = target,
                BindingKey = key,
                Original = current,
                Initial = initial,
                From = ResolveVector3(FromMode, FromValue, FromOffset, current, initial),
                To = ResolveVector3(ToMode, ToValue, ToOffset, current, initial)
            };
        }

        internal override void Evaluate(TweenPlayer player, TweenClipState state, in TweenSampleInfo sample)
        {
            if (state?.Target == null || !ShouldApply(sample.Time))
                return;

            Vector3 from = (Vector3)state.From;
            Vector3 to = (Vector3)state.To;
            float eased = EaseProgress(Progress(sample.Time));
            Vector3 value = Interpolate(from, to, eased);
            Vector3 current = Read(state.Target);

            if (sample.Additive)
            {
                Vector3 delta = value - from;
                Vector3 previousDelta = state.Extra is Vector3 stored ? stored : Vector3.zero;
                value = current + delta - previousDelta;
                state.Extra = delta;
            }

            if ((Components & TweenVectorComponents.X) != 0) current.x = value.x;
            if ((Components & TweenVectorComponents.Y) != 0) current.y = value.y;
            if ((Components & TweenVectorComponents.Z) != 0) current.z = value.z;

            Write(state.Target, current);
        }

        internal override void Restore(TweenPlayer player, TweenClipState state)
        {
            if (state?.Target == null)
                return;

            if (state.Extra is Vector3 delta)
            {
                Vector3 current = Read(state.Target);
                if ((Components & TweenVectorComponents.X) != 0) current.x -= delta.x;
                if ((Components & TweenVectorComponents.Y) != 0) current.y -= delta.y;
                if ((Components & TweenVectorComponents.Z) != 0) current.z -= delta.z;
                Write(state.Target, current);
                return;
            }

            Write(state.Target, (Vector3)state.Original);
        }
    }

    [Serializable]
    public abstract class Vector2TweenClip : DurationTweenClip
    {
        public TweenEndpointMode FromMode = TweenEndpointMode.Current;
        public Vector2 FromValue;
        public Vector2 FromOffset;

        public TweenEndpointMode ToMode = TweenEndpointMode.Custom;
        public Vector2 ToValue;
        public Vector2 ToOffset;

        public TweenVectorComponents Components = TweenVectorComponents.All2D;

        protected abstract string PropertyId { get; }
        protected abstract UnityEngine.Object ResolveTarget(TweenPlayer player);
        protected abstract Vector2 Read(UnityEngine.Object target);
        protected abstract void Write(UnityEngine.Object target, Vector2 value);

        protected virtual Vector2 Interpolate(Vector2 from, Vector2 to, float progress)
        {
            return Vector2.LerpUnclamped(from, to, progress);
        }

        internal override TweenClipState Capture(TweenPlayer player)
        {
            UnityEngine.Object target = ResolveTarget(player);
            if (target == null)
                return null;

            Vector2 current = Read(target);
            string key = MakeBindingKey(target, PropertyId);
            Vector2 initial = player.GetOrCaptureInitial(target, PropertyId, key, current);

            return new TweenClipState
            {
                Clip = this,
                Target = target,
                BindingKey = key,
                Original = current,
                Initial = initial,
                From = ResolveVector2(FromMode, FromValue, FromOffset, current, initial),
                To = ResolveVector2(ToMode, ToValue, ToOffset, current, initial)
            };
        }

        internal override void Evaluate(TweenPlayer player, TweenClipState state, in TweenSampleInfo sample)
        {
            if (state?.Target == null || !ShouldApply(sample.Time))
                return;

            Vector2 from = (Vector2)state.From;
            Vector2 to = (Vector2)state.To;
            Vector2 value = Interpolate(from, to, EaseProgress(Progress(sample.Time)));
            Vector2 current = Read(state.Target);

            if (sample.Additive)
            {
                Vector2 delta = value - from;
                Vector2 previousDelta = state.Extra is Vector2 stored ? stored : Vector2.zero;
                value = current + delta - previousDelta;
                state.Extra = delta;
            }

            if ((Components & TweenVectorComponents.X) != 0) current.x = value.x;
            if ((Components & TweenVectorComponents.Y) != 0) current.y = value.y;

            Write(state.Target, current);
        }

        internal override void Restore(TweenPlayer player, TweenClipState state)
        {
            if (state?.Target == null)
                return;

            if (state.Extra is Vector2 delta)
            {
                Vector2 current = Read(state.Target);
                if ((Components & TweenVectorComponents.X) != 0) current.x -= delta.x;
                if ((Components & TweenVectorComponents.Y) != 0) current.y -= delta.y;
                Write(state.Target, current);
                return;
            }

            Write(state.Target, (Vector2)state.Original);
        }
    }

    [Serializable]
    public abstract class FloatTweenClip : DurationTweenClip
    {
        public TweenEndpointMode FromMode = TweenEndpointMode.Current;
        public float FromValue;
        public float FromOffset;

        public TweenEndpointMode ToMode = TweenEndpointMode.Custom;
        public float ToValue = 1f;
        public float ToOffset;

        protected abstract string PropertyId { get; }
        protected abstract UnityEngine.Object ResolveTarget(TweenPlayer player);
        protected abstract float Read(UnityEngine.Object target);
        protected abstract void Write(UnityEngine.Object target, float value);

        internal override TweenClipState Capture(TweenPlayer player)
        {
            UnityEngine.Object target = ResolveTarget(player);
            if (target == null)
                return null;

            float current = Read(target);
            string key = MakeBindingKey(target, PropertyId);
            float initial = player.GetOrCaptureInitial(target, PropertyId, key, current);

            return new TweenClipState
            {
                Clip = this,
                Target = target,
                BindingKey = key,
                Original = current,
                Initial = initial,
                From = ResolveFloat(FromMode, FromValue, FromOffset, current, initial),
                To = ResolveFloat(ToMode, ToValue, ToOffset, current, initial)
            };
        }

        internal override void Evaluate(TweenPlayer player, TweenClipState state, in TweenSampleInfo sample)
        {
            if (state?.Target == null || !ShouldApply(sample.Time))
                return;

            float from = (float)state.From;
            float to = (float)state.To;
            float value = Mathf.LerpUnclamped(from, to, EaseProgress(Progress(sample.Time)));
            if (sample.Additive)
            {
                float delta = value - from;
                float previousDelta = state.Extra is float stored ? stored : 0f;
                value = Read(state.Target) + delta - previousDelta;
                state.Extra = delta;
            }

            Write(state.Target, value);
        }

        internal override void Restore(TweenPlayer player, TweenClipState state)
        {
            if (state?.Target == null)
                return;

            if (state.Extra is float delta)
            {
                Write(state.Target, Read(state.Target) - delta);
                return;
            }

            Write(state.Target, (float)state.Original);
        }
    }
}
