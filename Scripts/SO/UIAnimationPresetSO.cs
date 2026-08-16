using UIPanelSystem.Inspector;
using UnityEngine;

namespace UIPanelSystem
{
    [CreateAssetMenu(fileName = "UIAnimationPreset", menuName = "ScriptableObjects/UI/Animation Preset")]
    public class UIAnimationPresetSO : ScriptableObject
    {
        [HideLabel] public AnimationData AnimationData;
    }
}
