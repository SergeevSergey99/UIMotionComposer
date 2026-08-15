using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public interface IAnimationHandler
{
    bool IsEnabled { get; }
    Color AnimationColor { get; }
    void AddToSequence(Sequence sequence, TempValues startValues, RectTransform rectTransform, CanvasGroup canvasGroup, float duration);
}