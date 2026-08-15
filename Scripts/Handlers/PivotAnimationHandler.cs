using DG.Tweening;
using UnityEngine;

[System.Serializable]
public class PivotAnimationHandler : Transform2DAnimationHandler
{
    protected override Vector2 GetCurrentValue(RectTransform rectTransform) => rectTransform.pivot;
    protected override Vector2 GetStartValue(TempValues startValues) => startValues.pivot;

    protected override Tween CreateUnifiedTween(RectTransform rectTransform, Vector2 startValue, Vector2 targetValue, float duration)
    {
        rectTransform.pivot = startValue; // Ensure the start value is set
        return DOTween.To(() => rectTransform.pivot, x => rectTransform.pivot = x, targetValue, duration).Modify(Unified);
    }

    protected override void AnimateComponent(Sequence sequence, RectTransform rectTransform, 
        int componentIndex, float currentValue, float targetValue, float duration, float delay, AnimationProccesData animationProccesData)
    {
        sequence.Join(
            DOVirtual.Float(currentValue, targetValue, duration, value => {
            var pivot = rectTransform.pivot;
            pivot[componentIndex] = value;
            rectTransform.pivot = pivot;
        }).Modify(animationProccesData).SetDelay(delay));
    }
}