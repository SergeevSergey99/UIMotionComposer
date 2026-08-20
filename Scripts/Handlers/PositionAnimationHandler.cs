using UnityEngine;

namespace UIMotionComposer
{
    [System.Serializable]
    public class PositionAnimationHandler : TransformAnimationHandler
    {
        protected override Vector3 GetCurrentValue(RectTransform rectTransform) => rectTransform.anchoredPosition3D;
        protected override Vector3 GetStartValue(TempValues startValues) => startValues.position;
        protected override void ApplyValue(RectTransform rectTransform, Vector3 value) => rectTransform.anchoredPosition3D = value;
    }
}
