using UIMotionComposer.Inspector;

namespace UIMotionComposer
{
    [System.Serializable, InlineProperty]
    public class SeparateAnimationData
    {
        [HideLabel, FoldoutGroup("X Axis")]
        public AnimationProcessData XAxis = new();

        [HideLabel, FoldoutGroup("Y Axis")]
        public AnimationProcessData YAxis = new();

        [HideLabel, FoldoutGroup("Z Axis")]
        public AnimationProcessData ZAxis = new();
    }
}
