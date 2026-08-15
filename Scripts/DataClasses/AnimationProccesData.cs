using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable, InlineProperty]
public class AnimationProccesData
{
    
    [LabelText("Timeline"), MinMaxSlider(0, 1, true)]
    public Vector2 Timeline = new Vector2(0.0f, 1f);

    [LabelText("Curve Mode")]
    public CurveMode CurveMode = CurveMode.Ease;
    
    [ShowIf(nameof(isEase)), LabelText("Ease")]
    public Ease Ease = Ease.OutBack;
    
    [ShowIf(nameof(isCurve)), LabelText("Curve")]
    public AnimationCurve Curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private bool isEase => CurveMode == CurveMode.Ease;
    private bool isCurve => CurveMode == CurveMode.Curve;

    public Tweener ModifyTweener(Tweener tweener)
    {
        if (CurveMode == CurveMode.Ease)
        {
            tweener.SetEase(Ease);
        }
        else if (CurveMode == CurveMode.Curve)
        {
            tweener.SetEase(Curve);
        }

        return tweener;
    }
    
}
public enum CurveMode
{
    Ease,
    Curve
}

public static class AnimationProccesDataExtentinon
{
    public static Tweener Modify(this Tweener tweener, AnimationProccesData animationProccesData)
    {
        return animationProccesData.ModifyTweener(tweener);
    }
}