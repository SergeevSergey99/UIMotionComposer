using UIPanelSystem.Tweening;
using UnityEngine;

[System.Serializable]
public class RotationAnimationHandler : TransformAnimationHandler
{
    protected override Vector3 GetCurrentValue(RectTransform rectTransform) => rectTransform.localRotation.eulerAngles;
    protected override Vector3 GetStartValue(TempValues startValues) => startValues.localRotation;

    protected override IUITweener CreateUnifiedTween(RectTransform rectTransform, Vector3 startValue, Vector3 targetValue, float duration)
    {
        // Slerped as quaternions, matching DOLocalRotateQuaternion: interpolating the euler angles
        // instead would take the long way round whenever an axis crosses 180 degrees.
        var startRotation = Quaternion.Euler(startValue);
        var targetRotation = Quaternion.Euler(targetValue);

        rectTransform.localRotation = startRotation; // Ensure the start value is set
        return UITween.Normalized(duration, t =>
                rectTransform.localRotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, t))
            .Modify(Unified);
    }

    protected override void AnimateComponent(IUISequence sequence, RectTransform rectTransform,
        int componentIndex, float currentValue, float targetValue, float duration, float delay, AnimationProccesData animationProccesData)
    {
        sequence.Join(UITween.Float(currentValue, targetValue, duration, value => {
            var euler = rectTransform.localRotation.eulerAngles;
            euler[componentIndex] = value;
            rectTransform.localRotation = Quaternion.Euler(euler);
        }).Modify(animationProccesData).SetDelay(delay));
    }
}
