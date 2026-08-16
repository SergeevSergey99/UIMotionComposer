using UIPanelSystem.Inspector;
using UIPanelSystem.Tweening;
using UnityEngine;

namespace UIPanelSystem
{
    /// <summary>
    /// Shared logic for the Vector3 valued rect properties. Subclasses only say how to read, write
    /// and (if plain lerping is wrong) how to interpolate their value.
    /// </summary>
    [System.Serializable]
    public abstract class TransformAnimationHandler : BaseAnimationHandler
    {
        [ShowIf(nameof(IsEnabled)), LabelText("Initial Value Mode")]
        public InitialValueMode InitialMode = InitialValueMode.OffsetFromStored;

        [ShowIf(nameof(ShowInitialValue)), LabelText("Initial Value")]
        public Vector3 InitialValue = Vector3.zero;

        [ShowIf(nameof(ShowInitialOffset)), LabelText("Initial Offset")]
        public Vector3 InitialOffset = Vector3.zero;

        [ShowIf(nameof(IsEnabled)), LabelText("Target Value Mode")]
        public TargetValueMode TargetMode = TargetValueMode.StoredInitial;

        [ShowIf(nameof(ShowTargetValue)), LabelText("Target Value")]
        public Vector3 TargetValue = Vector3.zero;

        [ShowIf(nameof(ShowTargetOffset)), LabelText("Target Offset")]
        public Vector3 TargetOffset = Vector3.zero;

        [ShowIf(nameof(IsSeparate)), HideLabel]
        public SeparateAnimationData Separate = new();

        private bool ShowInitialValue => IsEnabled && InitialMode == InitialValueMode.Custom;
        private bool ShowInitialOffset => IsEnabled && InitialMode == InitialValueMode.OffsetFromStored;
        private bool ShowTargetValue => IsEnabled && TargetMode == TargetValueMode.Custom;
        private bool ShowTargetOffset => IsEnabled && TargetMode == TargetValueMode.OffsetFromStored;

        protected abstract Vector3 GetCurrentValue(RectTransform rectTransform);
        protected abstract Vector3 GetStartValue(TempValues startValues);
        protected abstract void ApplyValue(RectTransform rectTransform, Vector3 value);

        /// <summary>
        /// Writes the value at normalised time <paramref name="t"/>. Plain unclamped lerp, so
        /// overshooting eases reach the transform. Override where lerping components is wrong.
        /// </summary>
        protected virtual void ApplyInterpolated(RectTransform rectTransform, Vector3 from, Vector3 to, float t)
        {
            ApplyValue(rectTransform, Vector3.LerpUnclamped(from, to, t));
        }

        public override void AddToSequence(IUISequence sequence, in UIAnimationContext context)
        {
            if (!IsEnabled) return;

            RectTransform rectTransform = context.RectTransform;
            var startValue = CalculateStartValue(context);
            var targetValue = CalculateTargetValue(context.StartValues);

            // Applied up front in both modes: the initial value modes are meaningless if the
            // animation simply begins wherever the panel happens to sit.
            ApplyValue(rectTransform, startValue);

            if (IsUnified)
            {
                var timeline = Unified.Timeline.GetTimelineParams(context.Duration);
                sequence.Join(UITween
                    .Normalized(timeline.duration, t => ApplyInterpolated(rectTransform, startValue, targetValue, t))
                    .Modify(Unified)
                    .SetDelay(timeline.delay));
            }
            else if (IsSeparate)
            {
                AnimateSeparately(sequence, rectTransform, startValue, targetValue, context.Duration);
            }
        }

        private Vector3 CalculateStartValue(in UIAnimationContext context)
        {
            if (context.StartFromCurrent)
                return GetCurrentValue(context.RectTransform);

            return InitialMode switch
            {
                InitialValueMode.Current => GetCurrentValue(context.RectTransform),
                InitialValueMode.Custom => InitialValue,
                InitialValueMode.OffsetFromStored => GetStartValue(context.StartValues) + InitialOffset,
                _ => GetCurrentValue(context.RectTransform)
            };
        }

        private Vector3 CalculateTargetValue(TempValues startValues)
        {
            return TargetMode switch
            {
                TargetValueMode.StoredInitial => GetStartValue(startValues),
                TargetValueMode.Custom => TargetValue,
                TargetValueMode.OffsetFromStored => GetStartValue(startValues) + TargetOffset,
                _ => GetStartValue(startValues)
            };
        }

        private void AnimateSeparately(IUISequence sequence, RectTransform rectTransform, Vector3 startValue, Vector3 targetValue, float duration)
        {
            AnimateComponent(sequence, rectTransform, 0, startValue.x, targetValue.x, duration, Separate.XAxis);
            AnimateComponent(sequence, rectTransform, 1, startValue.y, targetValue.y, duration, Separate.YAxis);
            AnimateComponent(sequence, rectTransform, 2, startValue.z, targetValue.z, duration, Separate.ZAxis);
        }

        private void AnimateComponent(IUISequence sequence, RectTransform rectTransform,
            int componentIndex, float startValue, float targetValue, float duration, AnimationProcessData processData)
        {
            var timeline = processData.Timeline.GetTimelineParams(duration);

            sequence.Join(UITween.Float(startValue, targetValue, timeline.duration, value =>
            {
                // Re-read every step: the other axes have their own timelines and move independently.
                var current = GetCurrentValue(rectTransform);
                current[componentIndex] = value;
                ApplyValue(rectTransform, current);
            }).Modify(processData).SetDelay(timeline.delay));
        }
    }
}
