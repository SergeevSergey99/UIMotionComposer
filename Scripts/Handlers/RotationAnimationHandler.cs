using UnityEngine;

namespace UIPanelSystem
{
    [System.Serializable]
    public class RotationAnimationHandler : TransformAnimationHandler
    {
        protected override Vector3 GetCurrentValue(RectTransform rectTransform) => rectTransform.localRotation.eulerAngles;
        protected override Vector3 GetStartValue(TempValues startValues) => startValues.localRotation;
        protected override void ApplyValue(RectTransform rectTransform, Vector3 value) => rectTransform.localRotation = Quaternion.Euler(value);

        /// <summary>
        /// Slerped as quaternions rather than lerped per axis: interpolating euler angles takes the
        /// long way round whenever an axis crosses 180 degrees. Matches DOLocalRotateQuaternion.
        /// The separate-axis mode still moves one euler component at a time, by definition.
        /// </summary>
        protected override void ApplyInterpolated(RectTransform rectTransform, Vector3 from, Vector3 to, float t)
        {
            rectTransform.localRotation = Quaternion.SlerpUnclamped(Quaternion.Euler(from), Quaternion.Euler(to), t);
        }
    }
}
