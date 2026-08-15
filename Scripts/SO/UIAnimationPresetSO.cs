using UIPanelSystem.Inspector;
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "UIAnimationPreset", menuName = "ScriptableObjects/UI/Animation Preset")]
public class UIAnimationPresetSO : ScriptableObject
{
    [HideLabel] public AnimationData AnimationData;
}