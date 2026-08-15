using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

public class UIPanelPresetsController : BaseUIPanelController
{
    [FormerlySerializedAs("_showPreset")]
    [BoxGroup("Animation Settings")]
    [LabelText("Show Preset"), SerializeField]
    protected UIAnimationPresetSO showPresetSo;

    [FormerlySerializedAs("_hidePreset")]
    [BoxGroup("Animation Settings")]
    [LabelText("Hide Preset"), SerializeField]
    protected UIAnimationPresetSO hidePresetSo;

    protected override void InitializeHandlers()
    {
        _showHandlers = showPresetSo != null ? showPresetSo.AnimationData.GetHandlers() : new List<IAnimationHandler>();
        _hideHandlers = hidePresetSo != null ? hidePresetSo.AnimationData.GetHandlers() : new List<IAnimationHandler>();
    }

    protected override float GetShowDuration()
    {
        return showPresetSo != null ? showPresetSo.AnimationData.Duration : 0.5f;
    }

    protected override float GetHideDuration()
    {
        return hidePresetSo != null ? hidePresetSo.AnimationData.Duration : 0.5f;
    }
    protected override void OnValidate()
    {
        base.OnValidate();
        
        if (Application.isPlaying && (showPresetSo != null || hidePresetSo != null))
        {
            InitializeHandlers();
        }
    }
}