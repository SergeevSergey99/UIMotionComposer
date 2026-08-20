using System;
using UnityEngine;

namespace UIMotionComposer.Tweening
{
    /// <summary>
    /// A single tween scheduled inside a <see cref="IUISequence"/>.
    ///
    /// Everything the animation handlers need is expressed as one interpolating float, so the whole
    /// DOTween surface this package depends on is DOVirtual.Float plus a sequence. That keeps the
    /// built-in fallback and the DOTween backend visually identical instead of merely similar.
    /// </summary>
    public interface IUITweener
    {
        IUITweener SetEase(UIEase ease);
        IUITweener SetEase(AnimationCurve curve);
        IUITweener SetDelay(float delay);
    }

    /// <summary>
    /// A group of tweens that start together (each with its own delay) and report completion once.
    /// </summary>
    public interface IUISequence
    {
        /// <summary>Adds a tween that starts at the sequence's zero point, offset by its own delay.</summary>
        IUISequence Join(IUITweener tweener);

        /// <summary>True to run on unscaled time, mirroring DOTween's SetUpdate(isIndependentUpdate).</summary>
        IUISequence SetUpdate(bool isIndependentUpdate);

        IUISequence OnComplete(Action callback);

        void Play();

        /// <summary>Stops the sequence where it is. The completion callback does not fire.</summary>
        void Kill();

        bool IsActive();
        bool IsPlaying();
    }
}
