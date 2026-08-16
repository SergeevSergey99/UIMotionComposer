using UnityEngine;

namespace UIPanelSystem
{
    [System.Serializable]
    public class SizeAnimationHandler : Transform2DAnimationHandler
    {
        protected override Vector2 GetCurrentValue(RectTransform rectTransform) => rectTransform.sizeDelta;
        protected override Vector2 GetStartValue(TempValues startValues) => startValues.sizeDelta;
        protected override void ApplyValue(RectTransform rectTransform, Vector2 value) => rectTransform.sizeDelta = value;
    }
}
