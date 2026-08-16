using UnityEngine;

namespace UIPanelSystem
{
    [System.Serializable]
    public class ScaleAnimationHandler : TransformAnimationHandler
    {
        protected override Vector3 GetCurrentValue(RectTransform rectTransform) => rectTransform.localScale;
        protected override Vector3 GetStartValue(TempValues startValues) => startValues.localScale;
        protected override void ApplyValue(RectTransform rectTransform, Vector3 value) => rectTransform.localScale = value;
    }
}
