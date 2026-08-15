using DG.Tweening;
using UnityEngine;

[System.Serializable]
public class RotationAnimationHandler : TransformAnimationHandler
{
    protected override Vector3 GetCurrentValue(RectTransform rectTransform) => rectTransform.localRotation.eulerAngles;
    protected override Vector3 GetStartValue(TempValues startValues) => startValues.localRotation;
    protected override Tween CreateUnifiedTween(RectTransform rectTransform, Vector3 startValue, Vector3 targetValue, float duration)
    {
        rectTransform.localRotation = Quaternion.Euler(startValue); // Ensure the start value is set
        return rectTransform.DOLocalRotateQuaternion(Quaternion.Euler(targetValue), duration).Modify(Unified);
    }

    protected override void AnimateComponent(Sequence sequence, RectTransform rectTransform, 
        int componentIndex, float currentValue, float targetValue, float duration, float delay, AnimationProccesData animationProccesData)
    {
        sequence.Join(DOVirtual.Float(currentValue, targetValue, duration, value => {
            var euler = rectTransform.localRotation.eulerAngles;
            euler[componentIndex] = value;
            rectTransform.localRotation = Quaternion.Euler(euler);
        }).Modify(animationProccesData).SetDelay(delay));
    }
}