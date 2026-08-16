using UnityEngine;

namespace UIPanelSystem
{
    /// <summary>
    /// Everything a handler needs for one play. Passed by reference so adding a knob later does not
    /// grow the signature of every handler again.
    /// </summary>
    public readonly struct UIAnimationContext
    {
        /// <summary>The authored pose captured when the controller first initialised.</summary>
        public readonly TempValues StartValues;

        public readonly RectTransform RectTransform;
        public readonly CanvasGroup CanvasGroup;
        public readonly float Duration;

        /// <summary>
        /// True when this play cut a running animation short and should pick up from wherever the
        /// panel currently is, rather than jumping to the configured initial value first.
        /// </summary>
        public readonly bool StartFromCurrent;

        public UIAnimationContext(
            TempValues startValues,
            RectTransform rectTransform,
            CanvasGroup canvasGroup,
            float duration,
            bool startFromCurrent)
        {
            StartValues = startValues;
            RectTransform = rectTransform;
            CanvasGroup = canvasGroup;
            Duration = duration;
            StartFromCurrent = startFromCurrent;
        }
    }
}
