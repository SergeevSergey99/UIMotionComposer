using System.Collections.Generic;
using UIPanelSystem.Inspector;
using UnityEngine;

public class UIClickableController : BaseUIClickableController
{
    [BoxGroup("Hover Settings")]
    [LabelText("Hover Animation Preset"), SerializeField]
    protected UIAnimationPresetSO hoverAnimationPresetSo;
    
    [BoxGroup("Click Settings")]
    [LabelText("Click Animation Preset"), SerializeField]
    protected UIAnimationPresetSO clickAnimationPresetSo;
    
    [BoxGroup("Disable Settings")]
    [LabelText("Disable Animation Preset"), SerializeField]
    protected UIAnimationPresetSO disableAnimationPresetSo;
    
    [BoxGroup("Return Animations")]
    [LabelText("Return From Hover Preset"), SerializeField]
    protected UIAnimationPresetSO returnFromHoverPresetSo;
    
    [BoxGroup("Return Animations")]
    [LabelText("Return From Click Preset"), SerializeField]
    protected UIAnimationPresetSO returnFromClickPresetSo;
    
    [BoxGroup("Return Animations")]
    [LabelText("Return From Disable Preset"), SerializeField]
    protected UIAnimationPresetSO returnFromDisablePresetSo;
    
    bool hoverPresetSetted => hoverAnimationPresetSo != null;
    bool clickPresetSetted => clickAnimationPresetSo != null;
    bool disablePresetSetted => disableAnimationPresetSo != null;
    bool returnFromHoverPresetSetted => returnFromHoverPresetSo != null;
    bool returnFromClickPresetSetted => returnFromClickPresetSo != null;
    bool returnFromDisablePresetSetted => returnFromDisablePresetSo != null;
    
    [HideIf(nameof(hoverPresetSetted)), BoxGroup("Hover Settings"), SerializeField]
    [HideLabel] protected AnimationData HoverAnimationData;
    
    [HideIf(nameof(clickPresetSetted)), BoxGroup("Click Settings"), SerializeField]
    [HideLabel] protected AnimationData ClickAnimationData;
    
    [HideIf(nameof(disablePresetSetted)), BoxGroup("Disable Settings"), SerializeField]
    [HideLabel] protected AnimationData DisableAnimationData;
    
    [HideIf(nameof(returnFromHoverPresetSetted)), BoxGroup("Return Animations"), SerializeField]
    [HideLabel] protected AnimationData ReturnFromHoverAnimationData;
    
    [HideIf(nameof(returnFromClickPresetSetted)), BoxGroup("Return Animations"), SerializeField]
    [HideLabel] protected AnimationData ReturnFromClickAnimationData;
    
    [HideIf(nameof(returnFromDisablePresetSetted)), BoxGroup("Return Animations"), SerializeField]
    [HideLabel] protected AnimationData ReturnFromDisableAnimationData;
    
    public AnimationData CurrentHoverAnimationData => hoverPresetSetted ? hoverAnimationPresetSo.AnimationData : HoverAnimationData;
    public AnimationData CurrentClickAnimationData => clickPresetSetted ? clickAnimationPresetSo.AnimationData : ClickAnimationData;
    public AnimationData CurrentDisableAnimationData => disablePresetSetted ? disableAnimationPresetSo.AnimationData : DisableAnimationData;
    public AnimationData CurrentReturnFromHoverAnimationData => returnFromHoverPresetSetted ? returnFromHoverPresetSo.AnimationData : ReturnFromHoverAnimationData;
    public AnimationData CurrentReturnFromClickAnimationData => returnFromClickPresetSetted ? returnFromClickPresetSo.AnimationData : ReturnFromClickAnimationData;
    public AnimationData CurrentReturnFromDisableAnimationData => returnFromDisablePresetSetted ? returnFromDisablePresetSo.AnimationData : ReturnFromDisableAnimationData;
    
    protected override void InitializeHandlers()
    {
        _hoverHandlers = CurrentHoverAnimationData?.GetHandlers() ?? new List<IAnimationHandler>();
        _clickHandlers = CurrentClickAnimationData?.GetHandlers() ?? new List<IAnimationHandler>();
        _disableHandlers = CurrentDisableAnimationData?.GetHandlers() ?? new List<IAnimationHandler>();
        _returnFromHoverHandlers = CurrentReturnFromHoverAnimationData?.GetHandlers() ?? new List<IAnimationHandler>();
        _returnFromClickHandlers = CurrentReturnFromClickAnimationData?.GetHandlers() ?? new List<IAnimationHandler>();
        _returnFromDisableHandlers = CurrentReturnFromDisableAnimationData?.GetHandlers() ?? new List<IAnimationHandler>();
    }
    
    protected override float GetHoverDuration() => CurrentHoverAnimationData?.Duration ?? 0.2f;
    protected override float GetClickDuration() => CurrentClickAnimationData?.Duration ?? 0.1f;
    protected override float GetDisableDuration() => CurrentDisableAnimationData?.Duration ?? 0.3f;
    protected override float GetReturnFromHoverDuration() => CurrentReturnFromHoverAnimationData?.Duration ?? 0.2f;
    protected override float GetReturnFromClickDuration() => CurrentReturnFromClickAnimationData?.Duration ?? 0.1f;
    protected override float GetReturnFromDisableDuration() => CurrentReturnFromDisableAnimationData?.Duration ?? 0.3f;
}