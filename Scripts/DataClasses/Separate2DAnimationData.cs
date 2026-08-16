using UIPanelSystem.Inspector;

namespace UIPanelSystem
{
    [System.Serializable, InlineProperty]
    public class Separate2DAnimationData
    {
        [HideLabel, FoldoutGroup("X Axis")]
        public AnimationProcessData XAxis = new();

        [HideLabel, FoldoutGroup("Y Axis")]
        public AnimationProcessData YAxis = new();
    }
}
