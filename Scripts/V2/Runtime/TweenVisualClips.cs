using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;

namespace UIMotionComposer.V2
{
    [MovedFrom(true, "UIMotionComposer.V2", "Assembly-CSharp-firstpass", "FadeTweenClip")]
    [Serializable, TweenClipMenu("Visual/Fade")]
    public sealed class FadeTweenClip : FloatTweenClip
    {
        public TweenFadeTarget FadeTarget = TweenFadeTarget.Auto;

        protected override string PropertyId => "Visual.Alpha";

        protected override UnityEngine.Object ResolveTarget(TweenPlayer player)
        {
            UnityEngine.Object source = ResolveConfiguredTarget(player);

            if (FadeTarget == TweenFadeTarget.CanvasGroup)
                return ResolveComponent<CanvasGroup>(source);
            if (FadeTarget == TweenFadeTarget.Graphic)
                return ResolveComponent<Graphic>(source);
            if (FadeTarget == TweenFadeTarget.SpriteRenderer)
                return ResolveComponent<SpriteRenderer>(source);

            return (UnityEngine.Object)ResolveComponent<CanvasGroup>(source) ??
                   (UnityEngine.Object)ResolveComponent<Graphic>(source) ??
                   ResolveComponent<SpriteRenderer>(source);
        }

        protected override float Read(UnityEngine.Object target)
        {
            return target switch
            {
                CanvasGroup canvasGroup => canvasGroup.alpha,
                Graphic graphic => graphic.color.a,
                SpriteRenderer spriteRenderer => spriteRenderer.color.a,
                _ => 1f
            };
        }

        protected override void Write(UnityEngine.Object target, float value)
        {
            switch (target)
            {
                case CanvasGroup canvasGroup:
                    canvasGroup.alpha = value;
                    break;
                case Graphic graphic:
                {
                    Color color = graphic.color;
                    color.a = value;
                    graphic.color = color;
                    break;
                }
                case SpriteRenderer spriteRenderer:
                {
                    Color color = spriteRenderer.color;
                    color.a = value;
                    spriteRenderer.color = color;
                    break;
                }
            }
        }
    }

    [MovedFrom(true, "UIMotionComposer.V2", "Assembly-CSharp-firstpass", "ColorTweenClip")]
    [Serializable, TweenClipMenu("Visual/Color")]
    public sealed class ColorTweenClip : DurationTweenClip
    {
        public TweenColorTarget ColorTarget = TweenColorTarget.Auto;

        public TweenEndpointMode FromMode = TweenEndpointMode.Current;
        public Color FromValue = Color.white;
        public Color FromOffset = Color.clear;

        public TweenEndpointMode ToMode = TweenEndpointMode.Custom;
        public Color ToValue = Color.white;
        public Color ToOffset = Color.clear;

        private sealed class RendererMetadata
        {
            public int PropertyId;
            public MaterialPropertyBlock OriginalBlock;
        }

        internal override TweenClipState Capture(TweenPlayer player)
        {
            UnityEngine.Object target = ResolveColorTarget(player);
            if (target == null)
                return null;

            Color current = Read(target, out RendererMetadata metadata);
            string key = MakeBindingKey(target, "Visual.Color");
            Color initial = player.GetOrCaptureInitial(target, "Visual.Color", key, current);

            return new TweenClipState
            {
                Clip = this,
                Target = target,
                BindingKey = key,
                Original = current,
                Initial = initial,
                From = ResolveColor(FromMode, FromValue, FromOffset, current, initial),
                To = ResolveColor(ToMode, ToValue, ToOffset, current, initial),
                Metadata = metadata
            };
        }

        internal override void Evaluate(TweenPlayer player, TweenClipState state, in TweenSampleInfo sample)
        {
            if (state?.Target == null || !ShouldApply(sample))
                return;

            Color from = (Color)state.From;
            Color to = (Color)state.To;
            Color value = Color.LerpUnclamped(from, to, EaseProgress(Progress(sample.Time)));
            Write(state.Target, value, state.Metadata as RendererMetadata);
        }

        internal override void Restore(TweenPlayer player, TweenClipState state)
        {
            if (state?.Target == null)
                return;

            if (state.Target is Renderer renderer && state.Metadata is RendererMetadata metadata)
            {
                renderer.SetPropertyBlock(metadata.OriginalBlock);
                return;
            }

            Write(state.Target, (Color)state.Original, null);
        }

        private UnityEngine.Object ResolveColorTarget(TweenPlayer player)
        {
            UnityEngine.Object source = ResolveConfiguredTarget(player);
            if (ColorTarget == TweenColorTarget.Graphic)
                return ResolveComponent<Graphic>(source);
            if (ColorTarget == TweenColorTarget.SpriteRenderer)
                return ResolveComponent<SpriteRenderer>(source);
            if (ColorTarget == TweenColorTarget.Renderer)
                return ResolveComponent<Renderer>(source);

            return (UnityEngine.Object)ResolveComponent<Graphic>(source) ??
                   ResolveComponent<SpriteRenderer>(source) ??
                   ResolveComponent<Renderer>(source);
        }

        private static Color Read(UnityEngine.Object target, out RendererMetadata metadata)
        {
            metadata = null;
            switch (target)
            {
                case Graphic graphic:
                    return graphic.color;
                case SpriteRenderer spriteRenderer:
                    return spriteRenderer.color;
                case Renderer renderer:
                {
                    Material material = renderer.sharedMaterial;
                    if (material == null)
                        return Color.white;

                    int propertyId = material.HasProperty("_Color")
                        ? Shader.PropertyToID("_Color")
                        : material.HasProperty("_BaseColor")
                            ? Shader.PropertyToID("_BaseColor")
                            : -1;

                    var originalBlock = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(originalBlock);
                    metadata = new RendererMetadata
                    {
                        PropertyId = propertyId,
                        OriginalBlock = originalBlock
                    };

                    return propertyId >= 0 ? material.GetColor(propertyId) : Color.white;
                }
                default:
                    return Color.white;
            }
        }

        private static void Write(UnityEngine.Object target, Color value, RendererMetadata metadata)
        {
            switch (target)
            {
                case Graphic graphic:
                    graphic.color = value;
                    break;
                case SpriteRenderer spriteRenderer:
                    spriteRenderer.color = value;
                    break;
                case Renderer renderer when metadata != null && metadata.PropertyId >= 0:
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor(metadata.PropertyId, value);
                    renderer.SetPropertyBlock(block);
                    break;
                }
            }
        }
    }

    [MovedFrom(true, "UIMotionComposer.V2", "Assembly-CSharp-firstpass", "FillAmountTweenClip")]
    [Serializable, TweenClipMenu("Visual/Fill Amount")]
    public sealed class FillAmountTweenClip : FloatTweenClip
    {
        protected override string PropertyId => "Image.FillAmount";

        protected override UnityEngine.Object ResolveTarget(TweenPlayer player)
        {
            return ResolveComponent<Image>(ResolveConfiguredTarget(player));
        }

        protected override float Read(UnityEngine.Object target) => ((Image)target).fillAmount;
        protected override void Write(UnityEngine.Object target, float value) => ((Image)target).fillAmount = value;
    }
}
