using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public class AlphaAnimationHandler : IAnimationHandler
{
    [HideLabel, HorizontalGroup("Mode")]
    public SimpleAnimationMode Mode = SimpleAnimationMode.Disabled;

    [ShowIf(nameof(IsUnified)), HideLabel, InlineProperty]
    public AnimationProccesData Unified = new();

    [ShowIf(nameof(IsEnabled)), LabelText("Initial Value Mode")]
    public InitialValueMode InitialMode = InitialValueMode.Custom;
    
    [ShowIf("@IsEnabled && InitialMode == InitialValueMode.Custom"), LabelText("Initial Value"), Range(0f, 1f)]
    public float InitialValue = 0f;
    
    [ShowIf("@IsEnabled && InitialMode == InitialValueMode.OffsetFromStored"), LabelText("Initial Offset"), Range(-1f, 1f)]
    public float InitialOffset = 0f;

    [ShowIf(nameof(IsEnabled)), LabelText("Target Value Mode")]
    public TargetValueMode TargetMode = TargetValueMode.Custom;
    
    [ShowIf("@IsEnabled && TargetMode == TargetValueMode.Custom"), LabelText("Target Value"), Range(0f, 1f)]
    public float TargetValue = 1f;
    
    [ShowIf("@IsEnabled && TargetMode == TargetValueMode.OffsetFromStored"), LabelText("Target Offset"), Range(-1f, 1f)]
    public float TargetOffset = 0f;

    public Color AnimationColor => IsEnabled ? Color.white : Color.red;
    public Sequence CurrentSequence { get; set; }
    public bool IsEnabled => Mode != SimpleAnimationMode.Disabled;
    public bool IsUnified => Mode == SimpleAnimationMode.Unified;
    
    public void AddToSequence(Sequence sequence, TempValues startValues, RectTransform rectTransform, CanvasGroup canvasGroup, float duration)
    {
        if (!IsEnabled) return;

        canvasGroup.alpha = InitialMode switch
        {
            InitialValueMode.Current => canvasGroup.alpha,
            InitialValueMode.Custom => InitialValue,
            InitialValueMode.OffsetFromStored => Mathf.Clamp01(startValues.alpha + InitialOffset),
            _ => startValues.alpha
        };
        var targetValue = CalculateTargetValue(canvasGroup, startValues);

        if (IsUnified)
        {
            var timeline = Unified.Timeline.GetTimelineParams(duration);
            sequence.Join(canvasGroup.DOFade(targetValue, timeline.duration)
                .Modify(Unified)
                .SetUpdate(true)
                .SetDelay(timeline.delay));
        }
        CurrentSequence = sequence;
    }

    private float CalculateTargetValue(CanvasGroup canvasGroup, TempValues startValues)
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