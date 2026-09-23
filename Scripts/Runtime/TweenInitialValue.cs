using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace UIMotionComposer
{
    /// <summary>
    /// The saved authored pose: one flat list of (target, property, value) entries. Clips add the
    /// properties they read for Initial endpoints; capture adds each object's active state and
    /// Transform. A property is stored once, so there is a single place to edit it.
    /// </summary>
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

    /// <summary>Property IDs an Initial Pose entry can hold, and which component each one lives on.</summary>
    public static class TweenPoseProperties
    {
        public const string Active = "GameObject.Active";
        public const string LocalPosition = "Transform.LocalPosition";
        public const string Position = "Transform.Position";
        public const string LocalRotation = "Transform.LocalRotation";
        public const string Rotation = "Transform.Rotation";
        public const string LocalScale = "Transform.LocalScale";
        public const string AnchorMin = "RectTransform.AnchorMin";
        public const string AnchorMax = "RectTransform.AnchorMax";
        public const string Pivot = "RectTransform.Pivot";
        public const string SizeDelta = "RectTransform.SizeDelta";
        public const string AnchoredPosition = "RectTransform.AnchoredPosition";
        public const string AnchoredPosition3D = "RectTransform.AnchoredPosition3D";
        public const string Alpha = "Visual.Alpha";
        public const string Color = "Visual.Color";
        public const string FillAmount = "Image.FillAmount";

        /// <summary>
        /// Properties that describe the same thing. A capture that already holds one member of a
        /// group for a target does not add another: position is either local, world or anchored.
        /// </summary>
        public static string Group(string propertyId)
        {
            return propertyId switch
            {
                LocalPosition or Position or AnchoredPosition or AnchoredPosition3D => "Position",
                LocalRotation or Rotation => "Rotation",
                _ => propertyId
            };
        }

        /// <summary>Anchors, pivot and size first: anchored position is measured against them.</summary>
        internal static int ApplyOrder(string propertyId)
        {
            return propertyId switch
            {
                AnchorMin or AnchorMax or Pivot => 0,
                SizeDelta => 1,
                Active => 3,
                _ => 2
            };
        }

        /// <summary>The object a property is read from on <paramref name="gameObject"/>, or null.</summary>
        public static UnityEngine.Object TargetFor(GameObject gameObject, string propertyId, Type componentType = null)
        {
            if (gameObject == null)
                return null;

            switch (propertyId)
            {
                case Active:
                    return gameObject;
                case LocalPosition or Position or LocalRotation or Rotation or LocalScale:
                    return gameObject.transform;
                case AnchorMin or AnchorMax or Pivot or SizeDelta or AnchoredPosition or AnchoredPosition3D:
                    return gameObject.transform as RectTransform;
                default:
                    return componentType != null ? gameObject.GetComponent(componentType) : null;
            }
        }

        /// <summary>Whole-object properties saved for every object in the pose.</summary>
        internal static IEnumerable<string> ObjectProperties(GameObject gameObject)
        {
            yield return Active;
            if (gameObject.transform is RectTransform)
            {
                yield return AnchorMin;
                yield return AnchorMax;
                yield return Pivot;
                yield return SizeDelta;
                yield return AnchoredPosition3D;
            }
            else
            {
                yield return LocalPosition;
            }

            yield return LocalRotation;
            yield return LocalScale;
        }
    }

    internal enum TweenInitialValueType
    {
        Float,
        Vector2,
        Vector3,
        Color,
        Bool
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
        [SerializeField] private bool boolValue;

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
                TweenInitialValueType.Bool => boolValue,
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
                case bool flag:
                    entry.valueType = TweenInitialValueType.Bool;
                    entry.boolValue = flag;
                    break;
                default:
                    throw new ArgumentException($"Unsupported initial value type {typeof(T).Name}.", nameof(value));
            }

            return entry;
        }

        /// <summary>Reads the current value of <paramref name="propertyId"/> from <paramref name="target"/>.</summary>
        public static TweenInitialValue Read(UnityEngine.Object target, string propertyId)
        {
            switch (propertyId)
            {
                case TweenPoseProperties.Active when target is GameObject gameObject:
                    return Create(target, propertyId, gameObject.activeSelf);
                case TweenPoseProperties.LocalPosition when target is Transform transform:
                    return Create(target, propertyId, transform.localPosition);
                case TweenPoseProperties.Position when target is Transform transform:
                    return Create(target, propertyId, transform.position);
                case TweenPoseProperties.LocalRotation when target is Transform transform:
                    return Create(target, propertyId, transform.localEulerAngles);
                case TweenPoseProperties.Rotation when target is Transform transform:
                    return Create(target, propertyId, transform.eulerAngles);
                case TweenPoseProperties.LocalScale when target is Transform transform:
                    return Create(target, propertyId, transform.localScale);
                case TweenPoseProperties.AnchorMin when target is RectTransform rect:
                    return Create(target, propertyId, rect.anchorMin);
                case TweenPoseProperties.AnchorMax when target is RectTransform rect:
                    return Create(target, propertyId, rect.anchorMax);
                case TweenPoseProperties.Pivot when target is RectTransform rect:
                    return Create(target, propertyId, rect.pivot);
                case TweenPoseProperties.SizeDelta when target is RectTransform rect:
                    return Create(target, propertyId, rect.sizeDelta);
                case TweenPoseProperties.AnchoredPosition when target is RectTransform rect:
                    return Create(target, propertyId, rect.anchoredPosition);
                case TweenPoseProperties.AnchoredPosition3D when target is RectTransform rect:
                    return Create(target, propertyId, rect.anchoredPosition3D);
                case TweenPoseProperties.Alpha when target is CanvasGroup canvasGroup:
                    return Create(target, propertyId, canvasGroup.alpha);
                case TweenPoseProperties.Alpha when target is Graphic graphic:
                    return Create(target, propertyId, graphic.color.a);
                case TweenPoseProperties.Alpha when target is SpriteRenderer spriteRenderer:
                    return Create(target, propertyId, spriteRenderer.color.a);
                case TweenPoseProperties.Color when target is Graphic graphic:
                    return Create(target, propertyId, graphic.color);
                case TweenPoseProperties.Color when target is SpriteRenderer spriteRenderer:
                    return Create(target, propertyId, spriteRenderer.color);
                case TweenPoseProperties.Color when target is Renderer renderer && renderer.sharedMaterial != null:
                    return Create(target, propertyId, renderer.sharedMaterial.color);
                case TweenPoseProperties.FillAmount when target is Image image:
                    return Create(target, propertyId, image.fillAmount);
                default:
                    return null;
            }
        }

        public bool TryApply()
        {
            if (!CanApply())
                return false;

            switch (propertyId)
            {
                case TweenPoseProperties.Active:
                    if (((GameObject)target).activeSelf != boolValue)
                        ((GameObject)target).SetActive(boolValue);
                    break;
                case TweenPoseProperties.LocalPosition:
                    ((Transform)target).localPosition = vector3Value;
                    break;
                case TweenPoseProperties.Position:
                    ((Transform)target).position = vector3Value;
                    break;
                case TweenPoseProperties.LocalScale:
                    ((Transform)target).localScale = vector3Value;
                    break;
                case TweenPoseProperties.LocalRotation:
                    ((Transform)target).localRotation = Quaternion.Euler(vector3Value);
                    break;
                case TweenPoseProperties.Rotation:
                    ((Transform)target).rotation = Quaternion.Euler(vector3Value);
                    break;
                case TweenPoseProperties.AnchorMin:
                    ((RectTransform)target).anchorMin = vector2Value;
                    break;
                case TweenPoseProperties.AnchorMax:
                    ((RectTransform)target).anchorMax = vector2Value;
                    break;
                case TweenPoseProperties.AnchoredPosition:
                    ((RectTransform)target).anchoredPosition = vector2Value;
                    break;
                case TweenPoseProperties.AnchoredPosition3D:
                    ((RectTransform)target).anchoredPosition3D = vector3Value;
                    break;
                case TweenPoseProperties.SizeDelta:
                    ((RectTransform)target).sizeDelta = vector2Value;
                    break;
                case TweenPoseProperties.Pivot:
                    ((RectTransform)target).pivot = vector2Value;
                    break;
                case TweenPoseProperties.Alpha:
                    ApplyAlpha(floatValue);
                    break;
                case TweenPoseProperties.Color:
                    ApplyColor(colorValue);
                    break;
                case TweenPoseProperties.FillAmount:
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
                TweenPoseProperties.Active => valueType == TweenInitialValueType.Bool && target is GameObject,
                TweenPoseProperties.LocalPosition or TweenPoseProperties.Position or TweenPoseProperties.LocalScale or
                TweenPoseProperties.LocalRotation or TweenPoseProperties.Rotation =>
                    valueType == TweenInitialValueType.Vector3 && target is Transform,
                TweenPoseProperties.AnchoredPosition or TweenPoseProperties.SizeDelta or TweenPoseProperties.Pivot or
                TweenPoseProperties.AnchorMin or TweenPoseProperties.AnchorMax =>
                    valueType == TweenInitialValueType.Vector2 && target is RectTransform,
                TweenPoseProperties.AnchoredPosition3D =>
                    valueType == TweenInitialValueType.Vector3 && target is RectTransform,
                TweenPoseProperties.Alpha => valueType == TweenInitialValueType.Float &&
                                             target is CanvasGroup or Graphic or SpriteRenderer,
                TweenPoseProperties.Color => valueType == TweenInitialValueType.Color &&
                                             target is Graphic or SpriteRenderer or Renderer,
                TweenPoseProperties.FillAmount => valueType == TweenInitialValueType.Float && target is Image,
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
                TweenInitialValueType.Bool => boolValue ? "On" : "Off",
                _ => "—"
            };
        }

        private static string Number(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
