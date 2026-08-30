using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UIMotionComposer.V2
{
    [Serializable, TweenClipMenu("Utility/Event")]
    public sealed class EventTweenClip : BaseTweenClip
    {
        public UnityEvent Event = new UnityEvent();
        public bool FireOnReverse;

        public override bool HasSideEffects => true;

        internal override TweenClipState Capture(TweenPlayer player)
        {
            return new TweenClipState { Clip = this, BindingKey = string.Empty };
        }

        internal override void Evaluate(TweenPlayer player, TweenClipState state, in TweenSampleInfo sample)
        {
            if (!sample.AllowSideEffects || state.LastTriggeredPass == sample.Pass)
                return;

            float trigger = Mathf.Max(0f, Delay);
            bool crossedForward = sample.Forward && sample.PreviousTime <= trigger && sample.Time >= trigger;
            bool crossedReverse = FireOnReverse && !sample.Forward &&
                                  sample.PreviousTime >= trigger && sample.Time <= trigger;
            if (!crossedForward && !crossedReverse)
                return;

            state.LastTriggeredPass = sample.Pass;
            Event?.Invoke();
        }

        internal override void Restore(TweenPlayer player, TweenClipState state) { }
    }

    [Serializable, TweenClipMenu("Utility/Toggle Object")]
    public sealed class ToggleObjectTweenClip : BaseTweenClip
    {
        public bool Active = true;
        public bool FireOnReverse;

        public override bool HasSideEffects => true;

        internal override TweenClipState Capture(TweenPlayer player)
        {
            GameObject target = ResolveGameObject(ResolveConfiguredTarget(player));
            if (target == null)
                return null;

            return new TweenClipState
            {
                Clip = this,
                Target = target,
                BindingKey = MakeBindingKey(target, "GameObject.Active"),
                Original = target.activeSelf
            };
        }

        internal override void Evaluate(TweenPlayer player, TweenClipState state, in TweenSampleInfo sample)
        {
            if (!sample.AllowSideEffects || state?.Target is not GameObject target ||
                state.LastTriggeredPass == sample.Pass)
                return;

            float trigger = Mathf.Max(0f, Delay);
            bool crossedForward = sample.Forward && sample.PreviousTime <= trigger && sample.Time >= trigger;
            bool crossedReverse = FireOnReverse && !sample.Forward &&
                                  sample.PreviousTime >= trigger && sample.Time <= trigger;
            if (!crossedForward && !crossedReverse)
                return;

            target.SetActive(sample.Forward ? Active : !Active);
            state.LastTriggeredPass = sample.Pass;
        }

        internal override void Restore(TweenPlayer player, TweenClipState state)
        {
            if (state?.Target is GameObject target && state.Original is bool active)
                target.SetActive(active);
        }
    }

    [Serializable, TweenClipMenu("Utility/Play Tween Animation")]
    public sealed class PlayTweenAnimationClip : BaseTweenClip
    {
        public string AnimationId = TweenIds.Show;
        public bool FireOnReverse;

        public override bool HasSideEffects => true;

        internal override TweenClipState Capture(TweenPlayer player)
        {
            TweenPlayer target = ResolveComponent<TweenPlayer>(ResolveConfiguredTarget(player));
            if (target == null)
                target = player;

            return new TweenClipState
            {
                Clip = this,
                Target = target,
                BindingKey = string.Empty
            };
        }

        internal override void Evaluate(TweenPlayer player, TweenClipState state, in TweenSampleInfo sample)
        {
            if (!sample.AllowSideEffects || state?.Target is not TweenPlayer target ||
                state.LastTriggeredPass == sample.Pass)
                return;

            float trigger = Mathf.Max(0f, Delay);
            bool crossedForward = sample.Forward && sample.PreviousTime <= trigger && sample.Time >= trigger;
            bool crossedReverse = FireOnReverse && !sample.Forward &&
                                  sample.PreviousTime >= trigger && sample.Time <= trigger;
            if (!crossedForward && !crossedReverse)
                return;

            state.LastTriggeredPass = sample.Pass;
            if (!target.IsPlaying(AnimationId))
                target.Play(AnimationId);
        }

        internal override void Restore(TweenPlayer player, TweenClipState state) { }
    }

    [Serializable, TweenClipMenu("Text/Text Reveal")]
    public sealed class TextRevealTweenClip : BaseTweenClip
    {
        [Min(0)] public int FromCharacters;
        [Tooltip("Use -1 to reveal the whole string.")]
        public int ToCharacters = -1;

        private sealed class RevealMetadata
        {
            public Text Text;
            public Component Tmp;
            public PropertyInfo TmpTextProperty;
            public PropertyInfo MaxVisibleProperty;
            public string OriginalText;
            public int OriginalMaxVisible;
        }

        internal override TweenClipState Capture(TweenPlayer player)
        {
            RevealMetadata metadata = TextTargetUtility.ResolveReveal(ResolveConfiguredTarget(player));
            if (metadata == null)
                return null;

            UnityEngine.Object target = (UnityEngine.Object)metadata.Text ?? metadata.Tmp;
            return new TweenClipState
            {
                Clip = this,
                Target = target,
                BindingKey = MakeBindingKey(target, "Text.Reveal"),
                Original = metadata.OriginalText,
                Metadata = metadata
            };
        }

        internal override void Evaluate(TweenPlayer player, TweenClipState state, in TweenSampleInfo sample)
        {
            if (state?.Target == null || state.Metadata is not RevealMetadata metadata || !ShouldApply(sample.Time))
                return;

            int fullLength = metadata.OriginalText?.Length ?? 0;
            int to = ToCharacters < 0 ? fullLength : Mathf.Clamp(ToCharacters, 0, fullLength);
            int visible = Mathf.RoundToInt(Mathf.LerpUnclamped(
                Mathf.Clamp(FromCharacters, 0, fullLength), to, EaseProgress(Progress(sample.Time))));

            if (metadata.Text != null)
                metadata.Text.text = metadata.OriginalText.Substring(0, Mathf.Clamp(visible, 0, fullLength));
            else if (metadata.MaxVisibleProperty != null && metadata.Tmp != null)
                metadata.MaxVisibleProperty.SetValue(metadata.Tmp, visible, null);
        }

        internal override void Restore(TweenPlayer player, TweenClipState state)
        {
            if (state?.Metadata is not RevealMetadata metadata)
                return;

            if (metadata.Text != null)
                metadata.Text.text = metadata.OriginalText;
            else if (metadata.Tmp != null)
            {
                metadata.TmpTextProperty?.SetValue(metadata.Tmp, metadata.OriginalText, null);
                metadata.MaxVisibleProperty?.SetValue(metadata.Tmp, metadata.OriginalMaxVisible, null);
            }
        }

        private static class TextTargetUtility
        {
            public static RevealMetadata ResolveReveal(UnityEngine.Object source)
            {
                Text text = ResolveComponent<Text>(source);
                if (text != null)
                {
                    return new RevealMetadata
                    {
                        Text = text,
                        OriginalText = text.text ?? string.Empty
                    };
                }

                Component tmp = FindTextComponent(source);
                if (tmp == null)
                    return null;

                PropertyInfo textProperty = tmp.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                PropertyInfo maxVisible = tmp.GetType().GetProperty("maxVisibleCharacters", BindingFlags.Instance | BindingFlags.Public);
                if (textProperty == null || maxVisible == null)
                    return null;

                return new RevealMetadata
                {
                    Tmp = tmp,
                    TmpTextProperty = textProperty,
                    MaxVisibleProperty = maxVisible,
                    OriginalText = textProperty.GetValue(tmp, null) as string ?? string.Empty,
                    OriginalMaxVisible = Convert.ToInt32(maxVisible.GetValue(tmp, null))
                };
            }

            private static Component FindTextComponent(UnityEngine.Object source)
            {
                GameObject gameObject = ResolveGameObject(source);
                if (gameObject == null)
                    return null;

                Component[] components = gameObject.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component != null && component.GetType().GetProperty("maxVisibleCharacters") != null &&
                        component.GetType().GetProperty("text") != null)
                        return component;
                }

                return null;
            }
        }
    }

    [Serializable, TweenClipMenu("Text/Text Counter")]
    public sealed class TextCounterTweenClip : BaseTweenClip
    {
        public float FromValue;
        public float ToValue = 100f;
        public bool WholeNumbers = true;
        public string Format = "{0}";

        private sealed class CounterMetadata
        {
            public Component Target;
            public PropertyInfo TextProperty;
            public string OriginalText;
        }

        internal override TweenClipState Capture(TweenPlayer player)
        {
            CounterMetadata metadata = ResolveText(ResolveConfiguredTarget(player));
            if (metadata == null)
                return null;

            return new TweenClipState
            {
                Clip = this,
                Target = metadata.Target,
                BindingKey = MakeBindingKey(metadata.Target, "Text.Value"),
                Original = metadata.OriginalText,
                Metadata = metadata
            };
        }

        internal override void Evaluate(TweenPlayer player, TweenClipState state, in TweenSampleInfo sample)
        {
            if (state?.Target == null || state.Metadata is not CounterMetadata metadata || !ShouldApply(sample.Time))
                return;

            float value = Mathf.LerpUnclamped(FromValue, ToValue, EaseProgress(Progress(sample.Time)));
            object formattedValue = WholeNumbers ? Mathf.RoundToInt(value) : value;
            string format = string.IsNullOrEmpty(Format) ? "{0}" : Format;
            string text;
            try
            {
                text = string.Format(format, formattedValue);
            }
            catch (FormatException)
            {
                text = formattedValue.ToString();
            }

            metadata.TextProperty.SetValue(metadata.Target, text, null);
        }

        internal override void Restore(TweenPlayer player, TweenClipState state)
        {
            if (state?.Metadata is CounterMetadata metadata && metadata.Target != null)
                metadata.TextProperty.SetValue(metadata.Target, metadata.OriginalText, null);
        }

        private static CounterMetadata ResolveText(UnityEngine.Object source)
        {
            GameObject gameObject = ResolveGameObject(source);
            if (gameObject == null)
                return null;

            Component[] components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                PropertyInfo property = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                if (property == null || !property.CanRead || !property.CanWrite || property.PropertyType != typeof(string))
                    continue;

                return new CounterMetadata
                {
                    Target = component,
                    TextProperty = property,
                    OriginalText = property.GetValue(component, null) as string ?? string.Empty
                };
            }

            return null;
        }
    }
}
