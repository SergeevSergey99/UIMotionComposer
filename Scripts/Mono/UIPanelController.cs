
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

public class UIPanelController : BaseUIPanelController
{
    [BoxGroup("SO Settings")]
    [LabelText("Show Animation Preset"), SerializeField]
    protected UIAnimationPresetSO showAnimationPresetSo;
    [BoxGroup("SO Settings")]
    [LabelText("Hide Animation Preset"), SerializeField]
    protected UIAnimationPresetSO hideAnimationPresetSo;
    
    bool showPresetSetted => showAnimationPresetSo != null;
    bool hidePresetSetted => hideAnimationPresetSo != null;
    
    [HideIf(nameof(showPresetSetted)), BoxGroup("Show Duration"), SerializeField]
    [HideLabel] protected AnimationData ShowAnimationData;
    [HideIf(nameof(hidePresetSetted)), BoxGroup("Hide Duration"), SerializeField]
    [HideLabel] protected AnimationData HideAnimationData;

    public AnimationData CurrentShowAnimationData => showPresetSetted ? showAnimationPresetSo.AnimationData : ShowAnimationData;
    public AnimationData CurrentHideAnimationData => hidePresetSetted ? hideAnimationPresetSo.AnimationData : HideAnimationData;
    
    protected override void InitializeHandlers()
    {
        _showHandlers = CurrentShowAnimationData.GetHandlers();
        _hideHandlers = CurrentHideAnimationData.GetHandlers();
    }

    protected override float GetShowDuration() => CurrentShowAnimationData.Duration;
    protected override float GetHideDuration() => CurrentHideAnimationData.Duration;
}
