using System;
using System.Collections.Generic;
using UIPanelSystem.Inspector;
using UnityEngine;

namespace UIPanelSystem
{
    [Serializable]
    public class AnimationData
    {
        [BoxGroup("Settings")]
        [LabelText("Duration"), SerializeField]
        public float Duration = 0.5f;

        [BoxGroup("Settings")]
        [LabelText("Restart From Initial On Interrupt"), SerializeField]
        [Tooltip("Off: cutting an animation short and starting this one continues from wherever the " +
                 "panel currently is, which is what keeps fast hover in/out smooth. On: the initial " +
                 "value modes below are applied even mid-flight, which snaps.")]
        public bool RestartFromInitialOnInterrupt = false;

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

        /// <summary>
        /// The handlers are shared: when this data lives on a preset asset, every controller using
        /// that preset gets these very instances. They are read-only configuration during playback --
        /// all per-play state belongs to the sequence and to <see cref="UIAnimationContext"/>.
        /// </summary>
        public List<IAnimationHandler> GetHandlers()
        {
            return new List<IAnimationHandler> { Alpha, Position, Rotation, Scale, Size, Pivot };
        }
    }
}
