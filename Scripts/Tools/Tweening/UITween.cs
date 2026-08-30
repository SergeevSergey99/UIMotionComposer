using System;

namespace UIMotionComposer.Tweening
{
    /// <summary>
    /// Entry point for every tween the UI panel system creates.
    ///
    /// V1 tween facade. When DOTween is installed (UIMOTION_DOTWEEN, set automatically by
    /// DefineSymbols) these calls are forwarded to DOTween so legacy animations live in the same engine as the rest of the
    /// project -- DOTween.KillAll, DOTween.timeScale and the DOTween inspector all see them. Without
    /// DOTween a small coroutine driven engine takes over and nothing else changes.
    /// </summary>
    public static class UITween
    {
        public static IUISequence CreateSequence()
        {
#if UIMOTION_DOTWEEN
            return new DoTweenSequence();
#else
            return new UITweenSequence();
#endif
        }

        /// <summary>
        /// Interpolates a single float and hands each step to <paramref name="onUpdate"/>.
        /// Values may leave the [from..to] range: overshooting eases (Back, Elastic, Bounce) are
        /// expected to overshoot, and callers lerp unclamped so the overshoot reaches the transform.
        /// </summary>
        public static IUITweener Float(float from, float to, float duration, Action<float> onUpdate)
        {
            if (onUpdate == null)
                throw new ArgumentNullException(nameof(onUpdate));

#if UIMOTION_DOTWEEN
            return new DoTweenTweener(from, to, duration, onUpdate);
#else
            return new UITweenStep(from, to, duration, onUpdate);
#endif
        }

        /// <summary>
        /// Normalised 0..1 tween, the shape used whenever a whole vector or quaternion is animated:
        /// the eased value drives an unclamped lerp on the caller's side.
        /// </summary>
        public static IUITweener Normalized(float duration, Action<float> onUpdate)
        {
            return Float(0f, 1f, duration, onUpdate);
        }

        /// <summary>True when the DOTween backend is active. Useful for editor diagnostics.</summary>
        public static bool IsUsingDoTween
        {
            get
            {
#if UIMOTION_DOTWEEN
                return true;
#else
                return false;
#endif
            }
        }
    }
}
