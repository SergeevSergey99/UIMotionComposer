using System;
using UnityEngine;

namespace UIMotionComposer
{
    internal enum TweenInitialValueType
    {
        Float,
        Vector2,
        Vector3,
        Color
    }

    /// <summary>A stable, serialized authoring value used by Initial and Offset From Initial.</summary>
    [Serializable]
    internal sealed class TweenInitialValue
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
    }
}
