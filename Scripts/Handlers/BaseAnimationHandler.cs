using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public abstract class BaseAnimationHandler : IAnimationHandler
{
    [HideLabel, HorizontalGroup("Mode")]
    public AnimationMode Mode = AnimationMode.Disabled;

    [ShowIf(nameof(IsUnified)), HideLabel, InlineProperty]
    public AnimationProccesData Unified = new AnimationProccesData();

    public Color AnimationColor => IsEnabled ? Color.white : Color.red;
    
    public Sequence CurrentSequence { get; set; }
    
    public bool IsEnabled => Mode != AnimationMode.Disabled;
    public bool IsUnified => Mode == AnimationMode.Unified;
    public bool IsSeparate => Mode == AnimationMode.Separate;

    public abstract void AddToSequence(Sequence sequence, TempValues startValues, RectTransform rectTransform, CanvasGroup canvasGroup, float duration);
}