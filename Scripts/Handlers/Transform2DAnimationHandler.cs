using UIMotionComposer.Inspector;
using UIMotionComposer.Tweening;
using UnityEngine;

namespace UIMotionComposer
{
    /// <summary>
    /// Vector2 counterpart of <see cref="TransformAnimationHandler"/>, for sizeDelta and pivot.
    /// </summary>
    [System.Serializable]
    public abstract class Transform2DAnimationHandler : BaseAnimationHandler
    {
        [ShowIf(nameof(IsEnabled)), LabelText("Initial Value Mode")]
        public InitialValueMode InitialMode = InitialValueMode.OffsetFromStored;

        [ShowIf(nameof(ShowInitialValue)), LabelText("Initial Value")]
        public Vector2 InitialValue = Vector2.zero;

        [ShowIf(nameof(ShowInitialOffset)), LabelText("Initial Offset")]
        public Vector2 InitialOffset = Vector2.zero;

        [ShowIf(nameof(IsEnabled)), LabelText("Target Value Mode")]
        public TargetValueMode TargetMode = TargetValueMode.StoredInitial;

        [ShowIf(nameof(ShowTargetValue)), LabelText("Target Value")]
        public Vector2 TargetValue = Vector2.zero;

        [ShowIf(nameof(ShowTargetOffset)), LabelText("Target Offset")]
        public Vector2 TargetOffset = Vector2.zero;

        [ShowIf(nameof(IsSeparate)), HideLabel]
        public Separate2DAnimationData Separate2D = new Separate2DAnimationData();

        private bool ShowInitialValue => IsEnabled && InitialMode == InitialValueMode.Custom;
        private bool ShowInitialOffset => IsEnabled && InitialMode == InitialValueMode.OffsetFromStored;
        private bool ShowTargetValue => IsEnabled && TargetMode == TargetValueMode.Custom;
        private bool ShowTargetOffset => IsEnabled && TargetMode == TargetValueMode.OffsetFromStored;

        protected abstract Vector2 GetCurrentValue(RectTransform rectTransform);
        protected abstract Vector2 GetStartValue(TempValues startValues);
        protected abstract void ApplyValue(RectTransform rectTransform, Vector2 value);

        public override void AddToSequence(IUISequence sequence, in UIAnimationContext context)
        {
            if (!IsEnabled) return;

            RectTransform rectTransform = context.RectTransform;
            var startValue = CalculateStartValue(context);
            var targetValue = CalculateTargetValue(context.StartValues);

            ApplyValue(rectTransform, startValue);

            if (IsUnified)
            {
                var timeline = Unified.Timeline.GetTimelineParams(context.Duration);
                sequence.Join(UITween
                    .Normalized(timeline.duration,
                        t => ApplyValue(rectTransform, Vector2.LerpUnclamped(startValue, targetValue, t)))
                    .Modify(Unified)
                    .SetDelay(timeline.delay));
            }
            else if (IsSeparate)
            {
                AnimateSeparately(sequence, rectTransform, startValue, targetValue, context.Duration);
            }
        }

        private Vector2 CalculateStartValue(in UIAnimationContext context)
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

        private Vector2 CalculateTargetValue(TempValues startValues)
        {
            return TargetMode switch
            {
                TargetValueMode.StoredInitial => GetStartValue(startValues),
                TargetValueMode.Custom => TargetValue,
                TargetValueMode.OffsetFromStored => GetStartValue(startValues) + TargetOffset,
                _ => GetStartValue(startValues)
            };
        }

        private void AnimateSeparately(IUISequence sequence, RectTransform rectTransform, Vector2 startValue, Vector2 targetValue, float duration)
        {
            AnimateComponent(sequence, rectTransform, 0, startValue.x, targetValue.x, duration, Separate2D.XAxis);
            AnimateComponent(sequence, rectTransform, 1, startValue.y, targetValue.y, duration, Separate2D.YAxis);
        }

        private void AnimateComponent(IUISequence sequence, RectTransform rectTransform,
            int componentIndex, float startValue, float targetValue, float duration, AnimationProcessData processData)
        {
            var timeline = processData.Timeline.GetTimelineParams(duration);

            sequence.Join(UITween.Float(startValue, targetValue, timeline.duration, value =>
            {
                // Re-read every step: the other axis has its own timeline and moves independently.
                var current = GetCurrentValue(rectTransform);
                current[componentIndex] = value;
                ApplyValue(rectTransform, current);
            }).Modify(processData).SetDelay(timeline.delay));
        }
    }
}
