using UIMotionComposer.Inspector;
using UnityEngine;

namespace UIMotionComposer
{
    [CreateAssetMenu(fileName = "UIAnimationPreset", menuName = "ScriptableObjects/UI/Animation Preset")]
    public class UIAnimationPresetSO : ScriptableObject
    {
        [HideLabel] public AnimationData AnimationData;
    }
}
