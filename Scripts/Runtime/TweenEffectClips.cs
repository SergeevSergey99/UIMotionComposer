using System;
using UnityEngine;

namespace UIMotionComposer
{
    [Serializable, TweenClipMenu("Effects/Punch Scale")]
    public sealed class PunchScaleTweenClip : DurationTweenClip
    {
        public Vector3 Strength = new Vector3(0.15f, 0.15f, 0.15f);
        [Range(1, 30)] public int Vibrato = 8;
        [Range(0f, 1f)] public float Elasticity = 0.8f;

        internal override TweenClipState Capture(TweenPlayer player)
        {
            Transform target = ResolveComponent<Transform>(ResolveConfiguredTarget(player));
            if (target == null)
                return null;

            Vector3 current = target.localScale;
            string key = MakeBindingKey(target, "Transform.LocalScale");
            player.GetOrCaptureInitial(target, "Transform.LocalScale", key, current);
            return new TweenClipState
            {
                Clip = this,
                Target = target,
                BindingKey = key,
                Original = current,
                From = current,
                Extra = Vector3.zero
            };
        }

        internal override void EvaluateProgress(TweenClipState state, float progress, bool additive)
        {
            var transform = (Transform)state.Target;
            // Oscillation phase is bounded even when an ease or custom curve overshoots.
            progress = Mathf.Clamp01(progress);
            float damping = Mathf.Pow(1f - progress, Mathf.Lerp(1f, 3f, 1f - Elasticity));
            float wave = Mathf.Sin(progress * Mathf.PI * Mathf.Max(1, Vibrato));
            Vector3 offset = Vector3.Scale(Strength, Vector3.one * (wave * damping));
            Vector3 previous = state.Extra is Vector3 stored ? stored : Vector3.zero;
            transform.localScale += offset - previous;
            state.Extra = offset;
        }

        internal override void Restore(TweenPlayer player, TweenClipState state)
        {
            if (state?.Target is not Transform transform)
                return;

            Vector3 previous = state.Extra is Vector3 stored ? stored : Vector3.zero;
            transform.localScale -= previous;
            state.Extra = Vector3.zero;
        }
    }

    [Serializable, TweenClipMenu("Effects/Punch Anchor Position")]
    public sealed class PunchAnchorPositionTweenClip : DurationTweenClip
    {
        public Vector2 Strength = new Vector2(16f, 0f);
        [Range(1, 30)] public int Vibrato = 8;
        [Range(0f, 1f)] public float Elasticity = 0.8f;

        internal override TweenClipState Capture(TweenPlayer player)
        {
            RectTransform target = ResolveComponent<RectTransform>(ResolveConfiguredTarget(player));
            if (target == null)
                return null;

            Vector2 current = target.anchoredPosition;
            string key = MakeBindingKey(target, "RectTransform.AnchoredPosition");
            player.GetOrCaptureInitial(target, "RectTransform.AnchoredPosition", key, current);
            return new TweenClipState
            {
                Clip = this,
                Target = target,
                BindingKey = key,
                Original = current,
                Extra = Vector2.zero
            };
        }

        internal override void EvaluateProgress(TweenClipState state, float progress, bool additive)
        {
            var rectTransform = (RectTransform)state.Target;
            progress = Mathf.Clamp01(progress);
            float damping = Mathf.Pow(1f - progress, Mathf.Lerp(1f, 3f, 1f - Elasticity));
            float wave = Mathf.Sin(progress * Mathf.PI * Mathf.Max(1, Vibrato));
            Vector2 offset = Strength * (wave * damping);
            Vector2 previous = state.Extra is Vector2 stored ? stored : Vector2.zero;
            rectTransform.anchoredPosition += offset - previous;
            state.Extra = offset;
        }

        internal override void Restore(TweenPlayer player, TweenClipState state)
        {
            if (state?.Target is not RectTransform rectTransform)
                return;

            Vector2 previous = state.Extra is Vector2 stored ? stored : Vector2.zero;
            rectTransform.anchoredPosition -= previous;
            state.Extra = Vector2.zero;
        }
    }

    [Serializable, TweenClipMenu("Effects/Shake")]
    public sealed class ShakeTweenClip : DurationTweenClip
    {
        public Vector3 Strength = new Vector3(10f, 10f, 0f);
        [Range(1, 50)] public int Vibrato = 12;
        [Range(0f, 180f)] public float Randomness = 90f;
        public bool FadeOut = true;
        public bool UseAnchoredPosition = true;
        public int Seed = 1337;

        internal override TweenClipState Capture(TweenPlayer player)
        {
            UnityEngine.Object source = ResolveConfiguredTarget(player);
            UnityEngine.Object target = UseAnchoredPosition
                ? ResolveComponent<RectTransform>(source)
                : ResolveComponent<Transform>(source);
            if (target == null)
                return null;

            string property = UseAnchoredPosition ? "RectTransform.AnchoredPosition" : "Transform.LocalPosition";
            string key = MakeBindingKey(target, property);
            object current = UseAnchoredPosition
                ? ((RectTransform)target).anchoredPosition
                : ((Transform)target).localPosition;
            player.GetOrCaptureInitial(target, property, key, current);

            return new TweenClipState
            {
                Clip = this,
                Target = target,
                BindingKey = key,
                Original = current,
                Extra = UseAnchoredPosition ? (object)Vector2.zero : Vector3.zero
            };
        }

        internal override void EvaluateProgress(TweenClipState state, float progress, bool additive)
        {
            progress = Mathf.Clamp01(progress);
            float fade = FadeOut ? 1f - progress : 1f;
            Vector3 noise = new Vector3(
                Noise(progress, Seed),
                Noise(progress, Seed + 31),
                Noise(progress, Seed + 67));
            float randomScale = Mathf.Lerp(1f, 0.35f, Randomness / 180f);
            Vector3 offset3 = Vector3.Scale(noise, Strength) * fade * randomScale;

            if (UseAnchoredPosition && state.Target is RectTransform rectTransform)
            {
                Vector2 offset = offset3;
                Vector2 previous = state.Extra is Vector2 stored ? stored : Vector2.zero;
                rectTransform.anchoredPosition += offset - previous;
                state.Extra = offset;
            }
            else if (state.Target is Transform transform)
            {
                Vector3 previous = state.Extra is Vector3 stored ? stored : Vector3.zero;
                transform.localPosition += offset3 - previous;
                state.Extra = offset3;
            }
        }

        internal override void Restore(TweenPlayer player, TweenClipState state)
        {
            if (state?.Target is RectTransform rectTransform && state.Extra is Vector2 offset2)
            {
                rectTransform.anchoredPosition -= offset2;
                state.Extra = Vector2.zero;
            }
            else if (state?.Target is Transform transform && state.Extra is Vector3 offset3)
            {
                transform.localPosition -= offset3;
                state.Extra = Vector3.zero;
            }
        }

        private float Noise(float progress, int seed)
        {
            float frequency = Mathf.Max(1, Vibrato);
            float a = Mathf.Sin((progress * frequency + seed * 0.017f) * 12.9898f);
            float b = Mathf.Sin((progress * frequency * 1.73f + seed * 0.031f) * 78.233f);
            return Mathf.Clamp(a * 0.65f + b * 0.35f, -1f, 1f);
        }
    }

    [Serializable, TweenClipMenu("Effects/Jump Anchor Position")]
    public sealed class JumpAnchorPositionTweenClip : Vector2TweenClip
    {
        [Min(0f)] public float JumpPower = 40f;
        [Min(1)] public int Jumps = 1;

        protected override string PropertyId => "RectTransform.AnchoredPosition";

        protected override UnityEngine.Object ResolveTarget(TweenPlayer player)
        {
            return ResolveComponent<RectTransform>(ResolveConfiguredTarget(player));
        }

        protected override Vector2 Read(UnityEngine.Object target) => ((RectTransform)target).anchoredPosition;
        protected override void Write(UnityEngine.Object target, Vector2 value) => ((RectTransform)target).anchoredPosition = value;

        protected override Vector2 Interpolate(Vector2 from, Vector2 to, float progress)
        {
            Vector2 value = Vector2.LerpUnclamped(from, to, progress);
            value.y += Mathf.Abs(Mathf.Sin(progress * Mathf.PI * Mathf.Max(1, Jumps))) * JumpPower;
            return value;
        }
    }
}
