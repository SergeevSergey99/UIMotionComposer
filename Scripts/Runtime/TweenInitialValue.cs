using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace UIMotionComposer
{
    [Serializable]
    public sealed class TweenInitialPose
    {
        [SerializeField] private bool captured;
        [SerializeField] private List<TweenInitialValue> values = new List<TweenInitialValue>();

        public bool IsCaptured => captured;
        public int Count => values?.Count ?? 0;

        internal bool Captured
        {
            get => captured;
            set => captured = value;
        }

        internal List<TweenInitialValue> Values => values ??= new List<TweenInitialValue>();

        internal void ImportLegacy(bool wasCaptured, List<TweenInitialValue> legacyValues)
        {
            if (captured || values is { Count: > 0 })
                return;
            captured = wasCaptured;
            values = legacyValues != null
                ? new List<TweenInitialValue>(legacyValues)
                : new List<TweenInitialValue>();
        }
    }

    public readonly struct TweenInitialPoseEntryInfo
    {
        public int Index { get; }
        public UnityEngine.Object Target { get; }
        public string PropertyId { get; }
        public string ValueType { get; }
        public string Value { get; }
        public bool CanRestore { get; }

        internal TweenInitialPoseEntryInfo(int index, UnityEngine.Object target, string propertyId,
            string valueType, string value, bool canRestore)
        {
            Index = index;
            Target = target;
            PropertyId = propertyId;
            ValueType = valueType;
            Value = value;
            CanRestore = canRestore;
        }
    }

    internal enum TweenInitialValueType
    {
        Float,
        Vector2,
        Vector3,
        Color
    }

    /// <summary>A stable, serialized authoring value used by Initial and Offset From Initial.</summary>
    [Serializable]
    public sealed class TweenInitialValue
    {
        [SerializeField] private UnityEngine.Object target;
        [SerializeField] private string propertyId;
        [SerializeField] private TweenInitialValueType valueType;
        [SerializeField] private float floatValue;
        [SerializeField] private Vector2 vector2Value;
        [SerializeField] private Vector3 vector3Value;
        [SerializeField] private Color colorValue;

        public UnityEngine.Object Target => target;
        public string PropertyId => propertyId;

        public TweenInitialPoseEntryInfo Describe(int index)
        {
            return new TweenInitialPoseEntryInfo(index, target, propertyId, valueType.ToString(),
                FormatValue(), CanApply());
        }

        public bool Matches(UnityEngine.Object candidate, string property)
        {
            return target == candidate && string.Equals(propertyId, property, StringComparison.Ordinal);
        }

        public bool TryGet<T>(out T value)
        {
            object boxed = valueType switch
            {
                TweenInitialValueType.Float => floatValue,
                TweenInitialValueType.Vector2 => vector2Value,
                TweenInitialValueType.Vector3 => vector3Value,
                TweenInitialValueType.Color => colorValue,
                _ => null
            };

            if (boxed is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        public static TweenInitialValue Create<T>(UnityEngine.Object target, string propertyId, T value)
        {
            var entry = new TweenInitialValue
            {
                target = target,
                propertyId = propertyId
            };

            switch (value)
            {
                case float number:
                    entry.valueType = TweenInitialValueType.Float;
                    entry.floatValue = number;
                    break;
                case Vector2 vector2:
                    entry.valueType = TweenInitialValueType.Vector2;
                    entry.vector2Value = vector2;
                    break;
                case Vector3 vector3:
                    entry.valueType = TweenInitialValueType.Vector3;
                    entry.vector3Value = vector3;
                    break;
                case Color color:
                    entry.valueType = TweenInitialValueType.Color;
                    entry.colorValue = color;
                    break;
                default:
                    throw new ArgumentException($"Unsupported initial value type {typeof(T).Name}.", nameof(value));
            }

            return entry;
        }

        public bool TryApply()
        {
            if (!CanApply())
                return false;

            switch (propertyId)
            {
                case "Transform.LocalPosition":
                    ((Transform)target).localPosition = vector3Value;
                    break;
                case "Transform.Position":
                    ((Transform)target).position = vector3Value;
                    break;
                case "Transform.LocalScale":
                    ((Transform)target).localScale = vector3Value;
                    break;
                case "Transform.LocalRotation":
                    ((Transform)target).localRotation = Quaternion.Euler(vector3Value);
                    break;
                case "Transform.Rotation":
                    ((Transform)target).rotation = Quaternion.Euler(vector3Value);
                    break;
                case "RectTransform.AnchoredPosition":
                    ((RectTransform)target).anchoredPosition = vector2Value;
                    break;
                case "RectTransform.AnchoredPosition3D":
                    ((RectTransform)target).anchoredPosition3D = vector3Value;
                    break;
                case "RectTransform.SizeDelta":
                    ((RectTransform)target).sizeDelta = vector2Value;
                    break;
                case "RectTransform.Pivot":
                    ((RectTransform)target).pivot = vector2Value;
                    break;
                case "Visual.Alpha":
                    ApplyAlpha(floatValue);
                    break;
                case "Visual.Color":
                    ApplyColor(colorValue);
                    break;
                case "Image.FillAmount":
                    ((Image)target).fillAmount = floatValue;
                    break;
                default:
                    return false;
            }

            return true;
        }

        private bool CanApply()
        {
            if (target == null || string.IsNullOrEmpty(propertyId))
                return false;

            return propertyId switch
            {
                "Transform.LocalPosition" or "Transform.Position" or "Transform.LocalScale" or
                "Transform.LocalRotation" or "Transform.Rotation" =>
                    valueType == TweenInitialValueType.Vector3 && target is Transform,
                "RectTransform.AnchoredPosition" or "RectTransform.SizeDelta" or "RectTransform.Pivot" =>
                    valueType == TweenInitialValueType.Vector2 && target is RectTransform,
                "RectTransform.AnchoredPosition3D" =>
                    valueType == TweenInitialValueType.Vector3 && target is RectTransform,
                "Visual.Alpha" => valueType == TweenInitialValueType.Float &&
                                  target is CanvasGroup or Graphic or SpriteRenderer,
                "Visual.Color" => valueType == TweenInitialValueType.Color &&
                                  target is Graphic or SpriteRenderer or Renderer,
                "Image.FillAmount" => valueType == TweenInitialValueType.Float && target is Image,
                _ => false
            };
        }

        private void ApplyAlpha(float value)
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

        private void ApplyColor(Color value)
        {
            switch (target)
            {
                case Graphic graphic:
                    graphic.color = value;
                    break;
                case SpriteRenderer spriteRenderer:
                    spriteRenderer.color = value;
                    break;
                case Renderer renderer:
                {
                    Material material = renderer.sharedMaterial;
                    if (material == null)
                        break;
                    int id = material.HasProperty("_Color")
                        ? Shader.PropertyToID("_Color")
                        : material.HasProperty("_BaseColor") ? Shader.PropertyToID("_BaseColor") : -1;
                    if (id < 0)
                        break;
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor(id, value);
                    renderer.SetPropertyBlock(block);
                    break;
                }
            }
        }

        private string FormatValue()
        {
            return valueType switch
            {
                TweenInitialValueType.Float => Number(floatValue),
                TweenInitialValueType.Vector2 => $"({Number(vector2Value.x)}, {Number(vector2Value.y)})",
                TweenInitialValueType.Vector3 =>
                    $"({Number(vector3Value.x)}, {Number(vector3Value.y)}, {Number(vector3Value.z)})",
                TweenInitialValueType.Color =>
                    $"RGBA({Number(colorValue.r)}, {Number(colorValue.g)}, {Number(colorValue.b)}, {Number(colorValue.a)})",
                _ => "—"
            };
        }

        private static string Number(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
