using System;
using UnityEngine;

[Serializable]
public class TempValues
{
    public Vector3 position; 
    public Vector3 localRotation;
    public Vector3 localScale;
    public Vector2 sizeDelta;
    public Vector2 pivot;
    public float alpha;
    
    public void SetInitialState(RectTransform rectTransform, CanvasGroup canvasGroup)
    {
        position = rectTransform.anchoredPosition3D;
        localRotation = rectTransform.localEulerAngles;
        localScale = rectTransform.localScale;
        sizeDelta = rectTransform.sizeDelta;
        pivot = rectTransform.pivot;
        alpha = canvasGroup.alpha;
    }
    
    public void ApplyTo(RectTransform rectTransform, CanvasGroup canvasGroup)
    {
        rectTransform.anchoredPosition3D = position;
        rectTransform.localEulerAngles = localRotation;
        rectTransform.localScale = localScale;
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.pivot = pivot;
        canvasGroup.alpha = alpha;
    }
}