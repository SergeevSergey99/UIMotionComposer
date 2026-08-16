using UnityEngine;

namespace UIPanelSystem
{
    [System.Serializable]
    public class PivotAnimationHandler : Transform2DAnimationHandler
    {
        protected override Vector2 GetCurrentValue(RectTransform rectTransform) => rectTransform.pivot;
        protected override Vector2 GetStartValue(TempValues startValues) => startValues.pivot;
        protected override void ApplyValue(RectTransform rectTransform, Vector2 value) => rectTransform.pivot = value;
    }
}
