using UIPanelSystem.Inspector;
using UIPanelSystem.Tweening;
using UnityEngine;

[System.Serializable]
public abstract class BaseAnimationHandler : IAnimationHandler
{
    [HideLabel]
    public AnimationMode Mode = AnimationMode.Disabled;

    [ShowIf(nameof(IsUnified)), HideLabel, InlineProperty]
    public AnimationProccesData Unified = new AnimationProccesData();

    public Color AnimationColor => IsEnabled ? Color.white : Color.red;

    public IUISequence CurrentSequence { get; set; }

    public bool IsEnabled => Mode != AnimationMode.Disabled;
    public bool IsUnified => Mode == AnimationMode.Unified;
    public bool IsSeparate => Mode == AnimationMode.Separate;

    public abstract void AddToSequence(IUISequence sequence, TempValues startValues, RectTransform rectTransform, CanvasGroup canvasGroup, float duration);
}
