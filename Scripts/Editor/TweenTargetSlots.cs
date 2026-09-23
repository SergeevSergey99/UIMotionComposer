using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UIMotionComposer.Editor
{
    /// <summary>What a named target slot must resolve to, derived from the clips that consume it.</summary>
    internal sealed class TweenTargetSlot
    {
        public string Key;
        public readonly List<string> Consumers = new List<string>();
        public readonly List<TweenTargetRequirement> Requirements = new List<TweenTargetRequirement>();

        public string Expected => string.Join(" + ", Requirements.Select(item => item.Label).Distinct());

        public IEnumerable<Type> ComponentTypes => Requirements
            .SelectMany(item => item.Types ?? Array.Empty<Type>())
            .Where(type => type != null && typeof(Component).IsAssignableFrom(type))
            .Distinct();

        public bool IsSatisfiedBy(UnityEngine.Object target)
        {
            if (target == null)
                return false;

            for (int i = 0; i < Requirements.Count; i++)
            {
                TweenTargetRequirement requirement = Requirements[i];
                if (requirement.Types == null || requirement.Types.Length == 0)
                    continue;
                if (!requirement.Types.Any(type => ResolvesAs(target, type)))
                    return false;
            }

            return true;
        }

        private static bool ResolvesAs(UnityEngine.Object target, Type type)
        {
            if (target == null || type == null)
                return false;
            if (type.IsInstanceOfType(target))
                return true;
            if (type == typeof(GameObject))
                return target is Component;

            GameObject gameObject = target switch
            {
                GameObject direct => direct,
                Component component => component.gameObject,
                _ => null
            };
            return gameObject != null && typeof(Component).IsAssignableFrom(type) && gameObject.GetComponent(type) != null;
        }
    }

    internal sealed class TweenTargetRequirement
    {
        public string Label;
        public Type[] Types;
    }

    /// <summary>Slot analysis and binding edits shared by the animation and binding drawers.</summary>
    internal static class TweenTargetSlots
    {
        public static List<TweenTargetSlot> Collect(IEnumerable<BaseTweenClip> clips)
        {
            var result = new List<TweenTargetSlot>();
            var byKey = new Dictionary<string, TweenTargetSlot>(StringComparer.Ordinal);
            if (clips == null)
                return result;

            foreach (BaseTweenClip item in clips)
            {
                if (item is not TargetedTweenClip clip)
                    continue;
                string key = clip.TargetKey?.Trim();
                if (string.IsNullOrEmpty(key))
                    continue;

                if (!byKey.TryGetValue(key, out TweenTargetSlot slot))
                {
                    slot = new TweenTargetSlot { Key = key };
                    byKey.Add(key, slot);
                    result.Add(slot);
                }

                slot.Consumers.Add(string.IsNullOrWhiteSpace(clip.Label)
                    ? ObjectNames.NicifyVariableName(clip.GetType().Name.Replace("TweenClip", string.Empty))
                    : clip.Label);
                TweenTargetRequirement requirement = RequirementFor(clip);
                if (!slot.Requirements.Any(existing => existing.Label == requirement.Label))
                    slot.Requirements.Add(requirement);
            }

            return result;
        }

        public static List<TweenTargetSlot> Collect(SerializedProperty clips)
        {
            if (clips == null || !clips.isArray)
                return new List<TweenTargetSlot>();

            var values = new List<BaseTweenClip>(clips.arraySize);
            for (int i = 0; i < clips.arraySize; i++)
                values.Add(clips.GetArrayElementAtIndex(i).managedReferenceValue as BaseTweenClip);
            return Collect(values);
        }

        /// <summary>Every requirement placed on <paramref name="key"/> by any animation of the player.</summary>
        public static TweenTargetSlot ForKey(TweenPlayer player, string key)
        {
            var merged = new TweenTargetSlot { Key = key };
            if (player == null || string.IsNullOrWhiteSpace(key))
                return merged;

            foreach (TweenAnimation animation in player.Animations)
            {
                if (animation == null)
                    continue;
                TweenTargetSlot slot = Collect(animation.EffectiveClips)
                    .FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
                if (slot == null)
                    continue;
                merged.Consumers.AddRange(slot.Consumers);
                foreach (TweenTargetRequirement requirement in slot.Requirements)
                    if (!merged.Requirements.Any(existing => existing.Label == requirement.Label))
                        merged.Requirements.Add(requirement);
            }

            return merged;
        }

        public static UnityEngine.Object FindBestTarget(TweenPlayer player, TweenTargetSlot slot)
        {
            Transform byPath = player.transform.Find(slot.Key);
            if (byPath != null && slot.IsSatisfiedBy(byPath.gameObject))
                return byPath.gameObject;

            Transform[] descendants = player.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (string.Equals(descendants[i].name, slot.Key, StringComparison.OrdinalIgnoreCase) &&
                    slot.IsSatisfiedBy(descendants[i].gameObject))
                    return descendants[i].gameObject;
            }

            // A type-only guess is safe only when it is unambiguous. In particular, never bind a
            // Transform slot to the player root merely because every GameObject has a Transform.
            UnityEngine.Object unique = null;
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] == player.transform || !slot.IsSatisfiedBy(descendants[i].gameObject))
                    continue;
                if (unique != null)
                    return null;
                unique = descendants[i].gameObject;
            }

            return unique;
        }

        public static int Find(SerializedProperty bindings, string key)
        {
            if (bindings == null)
                return -1;
            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty entry = bindings.GetArrayElementAtIndex(i);
                if (string.Equals(entry.FindPropertyRelative("Key").stringValue, key, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        public static SerializedProperty Ensure(SerializedProperty bindings, string key)
        {
            int index = Find(bindings, key);
            if (index >= 0)
                return bindings.GetArrayElementAtIndex(index);

            index = bindings.arraySize;
            bindings.InsertArrayElementAtIndex(index);
            SerializedProperty entry = bindings.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("Key").stringValue = key;
            Reset(entry);
            return entry;
        }

        public static void CopyRule(SerializedProperty source, SerializedProperty destination)
        {
            destination.FindPropertyRelative("Mode").enumValueIndex = source.FindPropertyRelative("Mode").enumValueIndex;
            destination.FindPropertyRelative("Target").objectReferenceValue = source.FindPropertyRelative("Target").objectReferenceValue;
            destination.FindPropertyRelative("Query").stringValue = source.FindPropertyRelative("Query").stringValue;
            destination.FindPropertyRelative("ComponentType").stringValue = source.FindPropertyRelative("ComponentType").stringValue;
        }

        public static void Reset(SerializedProperty entry)
        {
            entry.FindPropertyRelative("Mode").enumValueIndex = (int)TweenTargetBindingMode.Direct;
            entry.FindPropertyRelative("Target").objectReferenceValue = null;
            entry.FindPropertyRelative("Query").stringValue = string.Empty;
            entry.FindPropertyRelative("ComponentType").stringValue = string.Empty;
        }

        public static void BindDirect(SerializedProperty entry, UnityEngine.Object target)
        {
            Reset(entry);
            entry.FindPropertyRelative("Target").objectReferenceValue = target;
        }

        private static TweenTargetRequirement RequirementFor(TargetedTweenClip clip)
        {
            switch (clip)
            {
                case AnchorPositionTweenClip:
                case AnchorPosition3DTweenClip:
                case SizeDeltaTweenClip:
                case PivotTweenClip:
                case PunchAnchorPositionTweenClip:
                case JumpAnchorPositionTweenClip:
                    return Requirement("RectTransform", typeof(RectTransform));
                case ShakeTweenClip shake when shake.UseAnchoredPosition:
                    return Requirement("RectTransform", typeof(RectTransform));
                case MoveTweenClip:
                case ScaleTweenClip:
                case RotateTweenClip:
                case PunchScaleTweenClip:
                case ShakeTweenClip:
                    return Requirement("Transform", typeof(Transform));
                case FillAmountTweenClip:
                    return Requirement("Image", typeof(Image));
                case PlayTweenAnimationClip:
                    return Requirement("TweenPlayer", typeof(TweenPlayer));
                case ToggleObjectTweenClip:
                    return Requirement("GameObject", typeof(GameObject));
                case FadeTweenClip fade:
                    return fade.FadeTarget switch
                    {
                        TweenFadeTarget.CanvasGroup => Requirement("CanvasGroup", typeof(CanvasGroup)),
                        TweenFadeTarget.Graphic => Requirement("Graphic", typeof(Graphic)),
                        TweenFadeTarget.SpriteRenderer => Requirement("SpriteRenderer", typeof(SpriteRenderer)),
                        _ => Requirement("CanvasGroup / Graphic / SpriteRenderer", typeof(CanvasGroup), typeof(Graphic), typeof(SpriteRenderer))
                    };
                case ColorTweenClip color:
                    return color.ColorTarget switch
                    {
                        TweenColorTarget.Graphic => Requirement("Graphic", typeof(Graphic)),
                        TweenColorTarget.SpriteRenderer => Requirement("SpriteRenderer", typeof(SpriteRenderer)),
                        TweenColorTarget.Renderer => Requirement("Renderer", typeof(Renderer)),
                        _ => Requirement("Graphic / SpriteRenderer / Renderer", typeof(Graphic), typeof(SpriteRenderer), typeof(Renderer))
                    };
                case TextRevealTweenClip:
                case TextCounterTweenClip:
                    return Requirement("Text / TMP_Text", TextTypes());
                default:
                    return Requirement("Object", typeof(UnityEngine.Object));
            }
        }

        private static TweenTargetRequirement Requirement(string label, params Type[] types)
        {
            return new TweenTargetRequirement { Label = label, Types = types };
        }

        private static Type[] TextTypes()
        {
            Type tmp = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", false);
            return tmp != null ? new[] { typeof(Text), tmp } : new[] { typeof(Text) };
        }
    }
}
