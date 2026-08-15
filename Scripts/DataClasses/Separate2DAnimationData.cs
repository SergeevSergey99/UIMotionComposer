using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable, InlineProperty]
public class Separate2DAnimationData
{
    [HideLabel, FoldoutGroup("X Axis")]
    public AnimationProccesData XAxis = new();
    [HideLabel, FoldoutGroup("Y Axis")]
    public AnimationProccesData YAxis = new();
}