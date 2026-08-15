using UIPanelSystem.Tweening;
using UnityEngine;

[System.Serializable]
public class PositionAnimationHandler : TransformAnimationHandler
{
    protected override Vector3 GetCurrentValue(RectTransform rectTransform) => rectTransform.anchoredPosition3D;
    protected override Vector3 GetStartValue(TempValues startValues) => startValues.position;

    protected override IUITweener CreateUnifiedTween(RectTransform rectTransform, Vector3 startValue, Vector3 targetValue, float duration)
    {
        rectTransform.anchoredPosition3D = startValue; // Ensure the start value is set
        return UITween.Normalized(duration, t =>
                rectTransform.anchoredPosition3D = Vector3.LerpUnclamped(startValue, targetValue, t))
            .Modify(Unified);
    }

    protected override void AnimateComponent(IUISequence sequence, RectTransform rectTransform,
        int componentIndex, float currentValue, float targetValue, float duration, float delay, AnimationProccesData animationProccesData)
    {
        sequence.Join(
            UITween.Float(currentValue, targetValue, duration, value => {
            var pos = rectTransform.anchoredPosition3D;
            pos[componentIndex] = value;
            rectTransform.anchoredPosition3D = pos;
        }).Modify(animationProccesData).SetDelay(delay));
    }
}
