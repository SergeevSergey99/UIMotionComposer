using UIPanelSystem.Tweening;
using UnityEngine;

[System.Serializable]
public class SizeAnimationHandler : Transform2DAnimationHandler
{
    protected override Vector2 GetCurrentValue(RectTransform rectTransform) => rectTransform.sizeDelta;
    protected override Vector2 GetStartValue(TempValues startValues) => startValues.sizeDelta;

    protected override IUITweener CreateUnifiedTween(RectTransform rectTransform, Vector2 startValue, Vector2 targetValue, float duration)
    {
        rectTransform.sizeDelta = startValue; // Ensure the start value is set
        return UITween.Normalized(duration, t =>
                rectTransform.sizeDelta = Vector2.LerpUnclamped(startValue, targetValue, t))
            .Modify(Unified);
    }

    protected override void AnimateComponent(IUISequence sequence, RectTransform rectTransform,
        int componentIndex, float currentValue, float targetValue, float duration, float delay, AnimationProccesData animationProccesData)
    {
        sequence.Join(UITween.Float(currentValue, targetValue, duration, value => {
            var size = rectTransform.sizeDelta;
            size[componentIndex] = value;
            rectTransform.sizeDelta = size;
        }).Modify(animationProccesData).SetDelay(delay));
    }
}
