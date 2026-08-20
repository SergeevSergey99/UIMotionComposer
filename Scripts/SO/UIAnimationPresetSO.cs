using UIMotionComposer.Inspector;
using UnityEngine;

namespace UIMotionComposer
{
    [CreateAssetMenu(fileName = "UIAnimationPreset", menuName = "UI Motion Composer/Animation Preset")]
    public class UIAnimationPresetSO : ScriptableObject
    {
        [HideLabel] public AnimationData AnimationData;
    }
}
