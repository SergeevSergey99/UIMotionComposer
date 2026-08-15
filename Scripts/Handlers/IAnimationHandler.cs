using UIPanelSystem.Tweening;
using UnityEngine;

public interface IAnimationHandler
{
    bool IsEnabled { get; }
    Color AnimationColor { get; }
    void AddToSequence(IUISequence sequence, TempValues startValues, RectTransform rectTransform, CanvasGroup canvasGroup, float duration);
}
