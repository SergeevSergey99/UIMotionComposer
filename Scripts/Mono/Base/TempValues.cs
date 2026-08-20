using System;
using UnityEngine;

namespace UIMotionComposer
{
    /// <summary>
    /// The authored pose of a panel, captured once so animations can offset from it or return to it.
    ///
    /// Plain serialization, not [SerializeReference]: there is no polymorphism here, and a managed
    /// reference would put the type name into every prefab's YAML for nothing. Whether it has been
    /// captured is tracked by a separate flag on the controller.
    /// </summary>
    [Serializable]
    public class TempValues
    {
        public Vector3 position;
        public Vector3 localRotation;
        public Vector3 localScale;
        public Vector2 sizeDelta;
        public Vector2 pivot;
        public float alpha;

        public void SetInitialState(RectTransform rectTransform, CanvasGroup canvasGroup)
        {
            position = rectTransform.anchoredPosition3D;
            localRotation = rectTransform.localEulerAngles;
            localScale = rectTransform.localScale;
            sizeDelta = rectTransform.sizeDelta;
            pivot = rectTransform.pivot;
            alpha = canvasGroup == null ? 1f : canvasGroup.alpha;
        }

        public void ApplyTo(RectTransform rectTransform, CanvasGroup canvasGroup)
        {
            rectTransform.anchoredPosition3D = position;
            rectTransform.localEulerAngles = localRotation;
            rectTransform.localScale = localScale;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.pivot = pivot;

            if (canvasGroup != null)
                canvasGroup.alpha = alpha;
        }
    }
}
