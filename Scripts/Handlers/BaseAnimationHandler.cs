using UIMotionComposer.Inspector;
using UIMotionComposer.Tweening;
using UnityEngine;

namespace UIMotionComposer
{
    [System.Serializable]
    public abstract class BaseAnimationHandler : IAnimationHandler
    {
        [HideLabel]
        public AnimationMode Mode = AnimationMode.Disabled;

        [ShowIf(nameof(IsUnified)), HideLabel, InlineProperty]
        public AnimationProcessData Unified = new AnimationProcessData();

        public Color AnimationColor => IsEnabled ? Color.white : Color.red;

        public bool IsEnabled => Mode != AnimationMode.Disabled;
        public bool IsUnified => Mode == AnimationMode.Unified;
        public bool IsSeparate => Mode == AnimationMode.Separate;

        public abstract void AddToSequence(IUISequence sequence, in UIAnimationContext context);
    }
}
