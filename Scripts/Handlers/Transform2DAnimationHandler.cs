using UIPanelSystem.Inspector;
using UIPanelSystem.Tweening;
using UnityEngine;

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
    protected abstract IUITweener CreateUnifiedTween(RectTransform rectTransform, Vector2 startValue, Vector2 targetValue, float duration);
    protected abstract void AnimateComponent(IUISequence sequence, RectTransform rectTransform,
        int componentIndex, float currentValue, float targetValue, float duration, float delay, AnimationProccesData animationProccesData);

    public override void AddToSequence(IUISequence sequence, TempValues startValues, RectTransform rectTransform, CanvasGroup canvasGroup, float duration)
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

    private Vector2 CalculateStartValue(RectTransform rectTransform, TempValues startValues)
    {
        return InitialMode switch
        {
            InitialValueMode.Current => GetCurrentValue(rectTransform),
            InitialValueMode.Custom => InitialValue,
            InitialValueMode.OffsetFromStored => GetStartValue(startValues) + InitialOffset,
            _ => GetCurrentValue(rectTransform)
        };
    }

    private Vector2 CalculateTargetValue(RectTransform rectTransform, TempValues startValues)
    {
        return TargetMode switch
        {
            TargetValueMode.StoredInitial => GetStartValue(startValues),
            TargetValueMode.Custom => TargetValue,
            TargetValueMode.OffsetFromStored => GetStartValue(startValues) + TargetOffset,
            _ => GetStartValue(startValues)
        };
    }

    private void AnimateSeparately(IUISequence sequence, RectTransform rectTransform, Vector2 targetValue, float duration)
    {
        var currentValue = GetCurrentValue(rectTransform);

        // X компонент
        var xTimeline = Separate2D.XAxis.Timeline.GetTimelineParams(duration);
        AnimateComponent(sequence, rectTransform, 0, currentValue.x, targetValue.x,
            xTimeline.duration, xTimeline.delay, Separate2D.XAxis);

        // Y компонент
        var yTimeline = Separate2D.YAxis.Timeline.GetTimelineParams(duration);
        AnimateComponent(sequence, rectTransform, 1, currentValue.y, targetValue.y,
            yTimeline.duration, yTimeline.delay, Separate2D.YAxis);
    }
}
