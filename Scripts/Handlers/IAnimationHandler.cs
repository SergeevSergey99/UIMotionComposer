using UIPanelSystem.Tweening;
using UnityEngine;

namespace UIPanelSystem
{
    /// <summary>
    /// One animated aspect of a panel (alpha, position, scale, ...).
    ///
    /// Handler instances are shared: a preset asset hands the same instances to every controller
    /// that references it. They must therefore stay stateless during playback -- read the config,
    /// push tweens into the sequence, keep nothing.
    /// </summary>
    public interface IAnimationHandler
    {
        bool IsEnabled { get; }
        Color AnimationColor { get; }
        void AddToSequence(IUISequence sequence, in UIAnimationContext context);
    }
}
