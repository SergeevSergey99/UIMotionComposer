using UIPanelSystem.Inspector;
using UIPanelSystem.Tweening;
using UnityEngine;

namespace UIPanelSystem
{
    [System.Serializable]
    public class AlphaAnimationHandler : IAnimationHandler
    {
        [HideLabel]
        public SimpleAnimationMode Mode = SimpleAnimationMode.Disabled;

        [ShowIf(nameof(IsUnified)), HideLabel, InlineProperty]
        public AnimationProcessData Unified = new();

        [ShowIf(nameof(IsEnabled)), LabelText("Initial Value Mode")]
        public InitialValueMode InitialMode = InitialValueMode.Custom;

        [ShowIf(nameof(ShowInitialValue)), LabelText("Initial Value"), Range(0f, 1f)]
        public float InitialValue = 0f;

        [ShowIf(nameof(ShowInitialOffset)), LabelText("Initial Offset"), Range(-1f, 1f)]
        public float InitialOffset = 0f;

        [ShowIf(nameof(IsEnabled)), LabelText("Target Value Mode")]
        public TargetValueMode TargetMode = TargetValueMode.Custom;

        [ShowIf(nameof(ShowTargetValue)), LabelText("Target Value"), Range(0f, 1f)]
        public float TargetValue = 1f;

        [ShowIf(nameof(ShowTargetOffset)), LabelText("Target Offset"), Range(-1f, 1f)]
        public float TargetOffset = 0f;

        public Color AnimationColor => IsEnabled ? Color.white : Color.red;
        public bool IsEnabled => Mode != SimpleAnimationMode.Disabled;
        public bool IsUnified => Mode == SimpleAnimationMode.Unified;

        private bool ShowInitialValue => IsEnabled && InitialMode == InitialValueMode.Custom;
        private bool ShowInitialOffset => IsEnabled && InitialMode == InitialValueMode.OffsetFromStored;
        private bool ShowTargetValue => IsEnabled && TargetMode == TargetValueMode.Custom;
        private bool ShowTargetOffset => IsEnabled && TargetMode == TargetValueMode.OffsetFromStored;

        public void AddToSequence(IUISequence sequence, in UIAnimationContext context)
        {
            if (!IsEnabled) return;

            CanvasGroup canvasGroup = context.CanvasGroup;
            canvasGroup.alpha = CalculateStartValue(canvasGroup, context);

            var startValue = canvasGroup.alpha;
            var targetValue = CalculateTargetValue(context.StartValues);

            if (IsUnified)
            {
                var timeline = Unified.Timeline.GetTimelineParams(context.Duration);
                sequence.Join(UITween
                    .Float(startValue, targetValue, timeline.duration, value => canvasGroup.alpha = value)
                    .Modify(Unified)
                    .SetDelay(timeline.delay));
            }
        }

        private float CalculateStartValue(CanvasGroup canvasGroup, in UIAnimationContext context)
        {
            if (context.StartFromCurrent)
                return canvasGroup.alpha;

            return InitialMode switch
            {
                InitialValueMode.Current => canvasGroup.alpha,
                InitialValueMode.Custom => InitialValue,
                InitialValueMode.OffsetFromStored => Mathf.Clamp01(context.StartValues.alpha + InitialOffset),
                _ => context.StartValues.alpha
            };
        }

        private float CalculateTargetValue(TempValues startValues)
        {
            return TargetMode switch
            {
                TargetValueMode.StoredInitial => startValues.alpha,
                TargetValueMode.Custom => TargetValue,
                TargetValueMode.OffsetFromStored => Mathf.Clamp01(startValues.alpha + TargetOffset),
                _ => startValues.alpha
            };
        }
    }
}
