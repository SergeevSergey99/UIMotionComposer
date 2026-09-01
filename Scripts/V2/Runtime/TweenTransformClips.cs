using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace UIMotionComposer.V2
{
    [MovedFrom(true, "UIMotionComposer.V2", "Assembly-CSharp-firstpass", "MoveTweenClip")]
    [Serializable, TweenClipMenu("Transform/Move")]
    public sealed class MoveTweenClip : Vector3TweenClip
    {
        public bool LocalSpace = true;

        protected override string PropertyId => LocalSpace ? "Transform.LocalPosition" : "Transform.Position";

        protected override UnityEngine.Object ResolveTarget(TweenPlayer player)
        {
            return ResolveComponent<Transform>(ResolveConfiguredTarget(player));
        }

        protected override Vector3 Read(UnityEngine.Object target)
        {
            var transform = (Transform)target;
            return LocalSpace ? transform.localPosition : transform.position;
        }

        protected override void Write(UnityEngine.Object target, Vector3 value)
        {
            var transform = (Transform)target;
            if (LocalSpace) transform.localPosition = value;
            else transform.position = value;
        }
    }

    [MovedFrom(true, "UIMotionComposer.V2", "Assembly-CSharp-firstpass", "ScaleTweenClip")]
    [Serializable, TweenClipMenu("Transform/Scale")]
    public sealed class ScaleTweenClip : Vector3TweenClip
    {
        protected override string PropertyId => "Transform.LocalScale";

        protected override UnityEngine.Object ResolveTarget(TweenPlayer player)
        {
            return ResolveComponent<Transform>(ResolveConfiguredTarget(player));
        }

        protected override Vector3 Read(UnityEngine.Object target) => ((Transform)target).localScale;
        protected override void Write(UnityEngine.Object target, Vector3 value) => ((Transform)target).localScale = value;
    }

    [MovedFrom(true, "UIMotionComposer.V2", "Assembly-CSharp-firstpass", "RotateTweenClip")]
    [Serializable, TweenClipMenu("Transform/Rotate")]
    public sealed class RotateTweenClip : Vector3TweenClip
    {
        public bool LocalSpace = true;
        public bool ShortestPath = true;

        protected override string PropertyId => LocalSpace ? "Transform.LocalRotation" : "Transform.Rotation";

        protected override UnityEngine.Object ResolveTarget(TweenPlayer player)
        {
            return ResolveComponent<Transform>(ResolveConfiguredTarget(player));
        }

        protected override Vector3 Read(UnityEngine.Object target)
        {
            var transform = (Transform)target;
            return LocalSpace ? transform.localEulerAngles : transform.eulerAngles;
        }

        protected override void Write(UnityEngine.Object target, Vector3 value)
        {
            var transform = (Transform)target;
            if (LocalSpace) transform.localRotation = Quaternion.Euler(value);
            else transform.rotation = Quaternion.Euler(value);
        }

        protected override Vector3 Interpolate(Vector3 from, Vector3 to, float progress)
        {
            if (ShortestPath && Components == TweenVectorComponents.All)
                return Quaternion.SlerpUnclamped(Quaternion.Euler(from), Quaternion.Euler(to), progress).eulerAngles;

            return base.Interpolate(from, to, progress);
        }
    }

    [MovedFrom(true, "UIMotionComposer.V2", "Assembly-CSharp-firstpass", "AnchorPositionTweenClip")]
    [Serializable, TweenClipMenu("Rect Transform/Anchor Position")]
    public sealed class AnchorPositionTweenClip : Vector2TweenClip
    {
        protected override string PropertyId => "RectTransform.AnchoredPosition";

        protected override UnityEngine.Object ResolveTarget(TweenPlayer player)
        {
            return ResolveComponent<RectTransform>(ResolveConfiguredTarget(player));
        }

        protected override Vector2 Read(UnityEngine.Object target) => ((RectTransform)target).anchoredPosition;
        protected override void Write(UnityEngine.Object target, Vector2 value) => ((RectTransform)target).anchoredPosition = value;
    }

    [MovedFrom(true, "UIMotionComposer.V2", "Assembly-CSharp-firstpass", "AnchorPosition3DTweenClip")]
    [Serializable, TweenClipMenu("Rect Transform/Anchor Position 3D")]
    public sealed class AnchorPosition3DTweenClip : Vector3TweenClip
    {
        protected override string PropertyId => "RectTransform.AnchoredPosition3D";

        protected override UnityEngine.Object ResolveTarget(TweenPlayer player)
        {
            return ResolveComponent<RectTransform>(ResolveConfiguredTarget(player));
        }

        protected override Vector3 Read(UnityEngine.Object target) => ((RectTransform)target).anchoredPosition3D;
        protected override void Write(UnityEngine.Object target, Vector3 value) => ((RectTransform)target).anchoredPosition3D = value;
    }

    [MovedFrom(true, "UIMotionComposer.V2", "Assembly-CSharp-firstpass", "SizeDeltaTweenClip")]
    [Serializable, TweenClipMenu("Rect Transform/Size Delta")]
    public sealed class SizeDeltaTweenClip : Vector2TweenClip
    {
        protected override string PropertyId => "RectTransform.SizeDelta";

        protected override UnityEngine.Object ResolveTarget(TweenPlayer player)
        {
            return ResolveComponent<RectTransform>(ResolveConfiguredTarget(player));
        }

        protected override Vector2 Read(UnityEngine.Object target) => ((RectTransform)target).sizeDelta;
        protected override void Write(UnityEngine.Object target, Vector2 value) => ((RectTransform)target).sizeDelta = value;
    }

    [MovedFrom(true, "UIMotionComposer.V2", "Assembly-CSharp-firstpass", "PivotTweenClip")]
    [Serializable, TweenClipMenu("Rect Transform/Pivot")]
    public sealed class PivotTweenClip : Vector2TweenClip
    {
        protected override string PropertyId => "RectTransform.Pivot";

        protected override UnityEngine.Object ResolveTarget(TweenPlayer player)
        {
            return ResolveComponent<RectTransform>(ResolveConfiguredTarget(player));
        }

        protected override Vector2 Read(UnityEngine.Object target) => ((RectTransform)target).pivot;
        protected override void Write(UnityEngine.Object target, Vector2 value) => ((RectTransform)target).pivot = value;
    }
}
