using UIPanelSystem.Inspector;
using UIPanelSystem.Tweening;
using UnityEngine;

[System.Serializable, InlineProperty]
public class AnimationProccesData
{
    [LabelText("Timeline"), MinMaxSlider(0, 1, true)]
    public Vector2 Timeline = new Vector2(0.0f, 1f);

    [LabelText("Curve Mode")]
    public CurveMode CurveMode = CurveMode.Ease;

    [ShowIf(nameof(isEase)), LabelText("Ease")]
    public UIEase Ease = UIEase.OutBack;

    [ShowIf(nameof(isCurve)), LabelText("Curve")]
    public AnimationCurve Curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private bool isEase => CurveMode == CurveMode.Ease;
    private bool isCurve => CurveMode == CurveMode.Curve;

    public IUITweener ModifyTweener(IUITweener tweener)
    {
        if (CurveMode == CurveMode.Curve)
        {
            return tweener.SetEase(Curve);
        }

        return tweener.SetEase(Ease);
    }
}

public enum CurveMode
{
    Ease,
    Curve
}

public static class AnimationProccesDataExtentinon
{
    public static IUITweener Modify(this IUITweener tweener, AnimationProccesData animationProccesData)
    {
        return animationProccesData.ModifyTweener(tweener);
    }
}
