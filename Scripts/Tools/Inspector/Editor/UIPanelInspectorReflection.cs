#if UNITY_EDITOR && !ODIN_INSPECTOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace UIPanelSystem.Inspector.Editor
{
    /// <summary>
    /// Reflection helpers the fallback inspector needs to answer two questions a SerializedProperty
    /// cannot: which field declared it, and which object instance owns that field. Conditions like
    /// ShowIf(nameof(IsEnabled)) are evaluated against that instance.
    /// </summary>
    internal static class UIPanelInspectorReflection
    {
        private const BindingFlags MemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        public static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;

                type = type.BaseType;
            }

            return null;
        }

        /// <summary>Walks the property path from the serialized target down to the property's value.</summary>
        public static object GetPropertyValue(SerializedProperty property)
        {
            if (property == null)
                return null;

            object current = property.serializedObject.targetObject;
            string path = property.propertyPath.Replace(".Array.data[", "[");
            string[] elements = path.Split('.');

            for (int i = 0; i < elements.Length && current != null; i++)
            {
                string element = elements[i];
                int bracket = element.IndexOf('[');

                if (bracket < 0)
                {
                    current = GetMemberValue(current, element);
                    continue;
                }

                string memberName = element.Substring(0, bracket);
                string indexText = element.Substring(bracket).Replace("[", string.Empty).Replace("]", string.Empty);

                if (!int.TryParse(indexText, out int index))
                    return null;

                current = GetElementAt(GetMemberValue(current, memberName), index);
            }

            return current;
        }

        /// <summary>The object that declares <paramref name="property"/>, i.e. its parent value.</summary>
        public static object GetOwner(SerializedProperty property)
        {
            if (property == null)
                return null;

            int lastSeparator = property.propertyPath.LastIndexOf('.');
            if (lastSeparator < 0)
                return property.serializedObject.targetObject;

            SerializedProperty parent = property.serializedObject.FindProperty(
                property.propertyPath.Substring(0, lastSeparator));

            return parent == null ? property.serializedObject.targetObject : GetPropertyValue(parent);
        }

        /// <summary>
        /// Reads a field, property or parameterless method by name. Used for both ShowIf conditions
        /// and for the "@this.Member.Member" color expressions carried by TabGroup.
        /// </summary>
        public static bool TryGetMemberValue(object owner, string memberName, out object value)
        {
            value = null;
            if (owner == null || string.IsNullOrEmpty(memberName))
                return false;

            Type type = owner.GetType();

            while (type != null)
            {
                FieldInfo field = type.GetField(memberName, MemberFlags | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    value = field.GetValue(owner);
                    return true;
                }

                PropertyInfo property = type.GetProperty(memberName, MemberFlags | BindingFlags.DeclaredOnly);
                if (property != null && property.CanRead)
                {
                    value = property.GetValue(owner, null);
                    return true;
                }

                MethodInfo method = type.GetMethod(memberName, MemberFlags | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (method != null && method.ReturnType != typeof(void))
                {
                    value = method.Invoke(owner, null);
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        /// <summary>Resolves a dotted member path such as "Alpha.AnimationColor" from a root object.</summary>
        public static bool TryResolvePath(object root, string path, out object value)
        {
            value = root;
            if (root == null || string.IsNullOrEmpty(path))
                return false;

            string[] parts = path.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryGetMemberValue(value, parts[i], out value))
                    return false;
            }

            return true;
        }

        public static IEnumerable<MethodInfo> GetButtonMethods(Type type)
        {
            var seen = new HashSet<string>();
            var result = new List<MethodInfo>();

            while (type != null && type != typeof(UnityEngine.Object))
            {
                MethodInfo[] methods = type.GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.GetParameters().Length != 0)
                        continue;

                    if (!Attribute.IsDefined(method, typeof(ButtonAttribute), true))
                        continue;

                    if (seen.Add(method.Name))
                        result.Add(method);
                }

                type = type.BaseType;
            }

            // Base class buttons first, matching declaration order top to bottom.
            result.Reverse();
            return result;
        }

        private static object GetMemberValue(object owner, string memberName)
        {
            return TryGetMemberValue(owner, memberName, out object value) ? value : null;
        }

        private static object GetElementAt(object collection, int index)
        {
            if (collection is IList list)
                return index >= 0 && index < list.Count ? list[index] : null;

            if (collection is IEnumerable enumerable)
            {
                IEnumerator enumerator = enumerable.GetEnumerator();
                for (int i = 0; i <= index; i++)
                {
                    if (!enumerator.MoveNext())
                        return null;
                }

                return enumerator.Current;
            }

            return null;
        }
    }
}
#endif
