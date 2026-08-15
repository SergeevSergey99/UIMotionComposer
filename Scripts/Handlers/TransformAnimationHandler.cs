using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public abstract class TransformAnimationHandler : BaseAnimationHandler
{
    [ShowIf(nameof(IsEnabled)), LabelText("Initial Value Mode")]
    public InitialValueMode InitialMode = InitialValueMode.OffsetFromStored;
    
    [ShowIf("@IsEnabled && InitialMode == InitialValueMode.Custom"), LabelText("Initial Value")]
    public Vector3 InitialValue = Vector3.zero;
    
    [ShowIf("@IsEnabled && InitialMode == InitialValueMode.OffsetFromStored"), LabelText("Initial Offset")]
    public Vector3 InitialOffset = Vector3.zero;

    [ShowIf(nameof(IsEnabled)), LabelText("Target Value Mode")]
    public TargetValueMode TargetMode = TargetValueMode.StoredInitial;
    
    [ShowIf("@IsEnabled && TargetMode == TargetValueMode.Custom"), LabelText("Target Value")]
    public Vector3 TargetValue = Vector3.zero;
    
    [ShowIf("@IsEnabled && TargetMode == TargetValueMode.OffsetFromStored"), LabelText("Target Offset")]
    public Vector3 TargetOffset = Vector3.zero;

    [ShowIf(nameof(IsSeparate)), HideLabel]
    public SeparateAnimationData Separate = new();

    protected abstract Vector3 GetCurrentValue(RectTransform rectTransform);
    protected abstract Vector3 GetStartValue(TempValues startValues);
    protected abstract Tween CreateUnifiedTween(RectTransform rectTransform, Vector3 startValue, Vector3 targetValue, float duration);
    protected abstract void AnimateComponent(Sequence sequence, RectTransform rectTransform, 
        int componentIndex, float currentValue, float targetValue, float duration, float delay, AnimationProccesData animationProccesData);

    public override void AddToSequence(Sequence sequence, TempValues startValues, RectTransform rectTransform, CanvasGroup canvasGroup, float duration)
    {
        if (!IsEnabled) return;

        var startValue = CalculateStartValue(rectTransform, startValues);
        var targetValue = CalculateTargetValue(rectTransform, startValues);

        if (IsUnified)
        {
            var timeline = Unified.Timeline.GetTimelineParams(duration);
            var tween = CreateUnifiedTween(rectTransform, startValue, targetValue, timeline.duration);
            sequence.Join(tween.SetDelay(timeline.delay));
        }
        else if (IsSeparate)
        {
            AnimateSeparately(sequence, rectTransform, targetValue, duration);
        }
        CurrentSequence = sequence;
    }

    private Vector3 CalculateStartValue(RectTransform rectTransform, TempValues startValues)
    {
        return InitialMode switch
        {
            InitialValueMode.Current => GetCurrentValue(rectTransform),
            InitialValueMode.Custom => InitialValue,
            InitialValueMode.OffsetFromStored => GetStartValue(startValues) + InitialOffset,
            _ => GetCurrentValue(rectTransform)
        };
    }

    private Vector3 CalculateTargetValue(RectTransform rectTransform, TempValues startValues)
    {
        return TargetMode switch
        {
            TargetValueMode.StoredInitial => GetStartValue(startValues),
            TargetValueMode.Custom => TargetValue,
            TargetValueMode.OffsetFromStored => GetStartValue(startValues) + TargetOffset,
            _ => GetStartValue(startValues)
        };
    }

    private void AnimateSeparately(Sequence sequence, RectTransform rectTransform, Vector3 targetValue, float duration)
    {
        var currentValue = GetCurrentValue(rectTransform);
        
        // X компонент
        var xTimeline = Separate.XAxis.Timeline.GetTimelineParams(duration);
        AnimateComponent(sequence, rectTransform, 0, currentValue.x, targetValue.x, 
            xTimeline.duration, xTimeline.delay, Separate.XAxis);

        // Y компонент
        var yTimeline = Separate.YAxis.Timeline.GetTimelineParams(duration);
        AnimateComponent(sequence, rectTransform, 1, currentValue.y, targetValue.y, 
            yTimeline.duration, yTimeline.delay, Separate.YAxis);

        // Z компонент
        var zTimeline = Separate.ZAxis.Timeline.GetTimelineParams(duration);
        AnimateComponent(sequence, rectTransform, 2, currentValue.z, targetValue.z, 
            zTimeline.duration, zTimeline.delay, Separate.ZAxis);
    }
}