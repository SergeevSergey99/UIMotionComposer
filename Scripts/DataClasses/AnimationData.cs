using System;
using System.Collections.Generic;
using UIPanelSystem.Inspector;
using UnityEngine;

[Serializable]
public class AnimationData
{
    [BoxGroup("Settings")]
    [LabelText("Duration"), SerializeField]
    public float Duration = 0.5f;

    [TabGroup("Alpha", TextColor = "@this.Alpha.AnimationColor"), HideLabel, SerializeField]
    public AlphaAnimationHandler Alpha = new();

    [TabGroup("Position", TextColor = "@this.Position.AnimationColor"), HideLabel, SerializeField]
    public PositionAnimationHandler Position = new();

    [TabGroup("Rotation", TextColor = "@this.Rotation.AnimationColor"), HideLabel, SerializeField]
    public RotationAnimationHandler Rotation = new();

    [TabGroup("Scale", TextColor = "@this.Scale.AnimationColor"), HideLabel, SerializeField]
    public ScaleAnimationHandler Scale = new();

    [TabGroup("Size", TextColor = "@this.Size.AnimationColor"), HideLabel, SerializeField]
    public SizeAnimationHandler Size = new();

    [TabGroup("Pivot", TextColor = "@this.Pivot.AnimationColor"), HideLabel, SerializeField]
    public PivotAnimationHandler Pivot = new();

    public List<IAnimationHandler> GetHandlers()
    {
        return new List<IAnimationHandler> { Alpha, Position, Rotation, Scale, Size, Pivot };
    }
}
