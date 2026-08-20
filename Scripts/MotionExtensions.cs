using UnityEngine;
using UnityEngine.UI;

namespace UIMotionComposer
{
    public static class MotionExtensions
    {
        public static (float delay, float duration) GetTimelineParams(this Vector2 timeline, float totalDuration)
        {
            float delay = timeline.x * totalDuration;
            float end = timeline.y * totalDuration;
            float duration = end - delay;
            return (delay, duration);
        }

        /// <summary>
        /// Settles any layout that drives this rect before its values are read.
        ///
        /// Without this, a panel inside a LayoutGroup captures its authored position and size rather
        /// than the ones the layout is about to give it: the layout rebuild happens at the end of the
        /// frame, well after Awake. Costs nothing for panels no layout touches.
        /// </summary>
        public static void RebuildDrivenLayout(this RectTransform rectTransform)
        {
            if (rectTransform == null)
                return;

            // A ContentSizeFitter or similar on the panel itself.
            if (rectTransform.GetComponent<ILayoutController>() != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

            // A LayoutGroup on the parent, which is what positions the panel.
            if (rectTransform.parent is RectTransform parent && parent.GetComponent<ILayoutGroup>() != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }
    }
}
