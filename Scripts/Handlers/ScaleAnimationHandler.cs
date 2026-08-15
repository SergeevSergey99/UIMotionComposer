using UIPanelSystem.Tweening;
using UnityEngine;

[System.Serializable]
public class ScaleAnimationHandler : TransformAnimationHandler
{
    protected override Vector3 GetCurrentValue(RectTransform rectTransform) => rectTransform.localScale;
    protected override Vector3 GetStartValue(TempValues startValues) => startValues.localScale;

    protected override IUITweener CreateUnifiedTween(RectTransform rectTransform, Vector3 startValue, Vector3 targetValue, float duration)
    {
        rectTransform.localScale = startValue; // Ensure the start value is set
        return UITween.Normalized(duration, t =>
                rectTransform.localScale = Vector3.LerpUnclamped(startValue, targetValue, t))
            .Modify(Unified);
    }

    protected override void AnimateComponent(IUISequence sequence, RectTransform rectTransform,
        int componentIndex, float currentValue, float targetValue, float duration, float delay, AnimationProccesData animationProccesData)
    {
        sequence.Join(UITween.Float(currentValue, targetValue, duration, value => {
            var scale = rectTransform.localScale;
            scale[componentIndex] = value;
            rectTransform.localScale = scale;
        }).Modify(animationProccesData).SetDelay(delay));
    }
}
