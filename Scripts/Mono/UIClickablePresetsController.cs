using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections.Generic;

public class UIClickablePresetsController : BaseUIClickableController
{
    [BoxGroup("Animation Settings")]
    [LabelText("Hover Preset"), SerializeField]
    protected UIAnimationPresetSO hoverPresetSo;

    [BoxGroup("Animation Settings")]
    [LabelText("Click Preset"), SerializeField]
    protected UIAnimationPresetSO clickPresetSo;

    [BoxGroup("Animation Settings")]
    [LabelText("Disable Preset"), SerializeField]
    protected UIAnimationPresetSO disablePresetSo;
    
    [BoxGroup("Return Animation Settings")]
    [LabelText("Return From Hover Preset"), SerializeField]
    protected UIAnimationPresetSO returnFromHoverPresetSo;

    [BoxGroup("Return Animation Settings")]
    [LabelText("Return From Click Preset"), SerializeField]
    protected UIAnimationPresetSO returnFromClickPresetSo;

    [BoxGroup("Return Animation Settings")]
    [LabelText("Return From Disable Preset"), SerializeField]
    protected UIAnimationPresetSO returnFromDisablePresetSo;

    protected override void InitializeHandlers()
    {
        _hoverHandlers = hoverPresetSo != null ? hoverPresetSo.AnimationData.GetHandlers() : new List<IAnimationHandler>();
        _clickHandlers = clickPresetSo != null ? clickPresetSo.AnimationData.GetHandlers() : new List<IAnimationHandler>();
        _disableHandlers = disablePresetSo != null ? disablePresetSo.AnimationData.GetHandlers() : new List<IAnimationHandler>();
        _returnFromHoverHandlers = returnFromHoverPresetSo != null ? returnFromHoverPresetSo.AnimationData.GetHandlers() : new List<IAnimationHandler>();
        _returnFromClickHandlers = returnFromClickPresetSo != null ? returnFromClickPresetSo.AnimationData.GetHandlers() : new List<IAnimationHandler>();
        _returnFromDisableHandlers = returnFromDisablePresetSo != null ? returnFromDisablePresetSo.AnimationData.GetHandlers() : new List<IAnimationHandler>();
    }

    protected override float GetHoverDuration()
    {
        return hoverPresetSo != null ? hoverPresetSo.AnimationData.Duration : 0.2f;
    }

    protected override float GetClickDuration()
    {
        return clickPresetSo != null ? clickPresetSo.AnimationData.Duration : 0.1f;
    }

    protected override float GetDisableDuration()
    {
        return disablePresetSo != null ? disablePresetSo.AnimationData.Duration : 0.3f;
    }
    
    protected override float GetReturnFromHoverDuration()
    {
        return returnFromHoverPresetSo != null ? returnFromHoverPresetSo.AnimationData.Duration : 0.2f;
    }

    protected override float GetReturnFromClickDuration()
    {
        return returnFromClickPresetSo != null ? returnFromClickPresetSo.AnimationData.Duration : 0.1f;
    }

    protected override float GetReturnFromDisableDuration()
    {
        return returnFromDisablePresetSo != null ? returnFromDisablePresetSo.AnimationData.Duration : 0.3f;
    }
}