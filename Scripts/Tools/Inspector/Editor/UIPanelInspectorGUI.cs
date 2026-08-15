#if UNITY_EDITOR && !ODIN_INSPECTOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UIPanelSystem.Inspector.Editor
{
    /// <summary>
    /// Fallback inspector used when Odin is not installed.
    ///
    /// It walks the serialized tree itself instead of leaning on PropertyDrawers, because the layout
    /// attributes this package uses are about arranging several fields at once -- boxes, tabs,
    /// conditional visibility -- which a per field drawer cannot express. Everything it does not
    /// recognise falls through to Unity's own drawing, so [Range], [Tooltip] and custom drawers on
    /// nested types keep working.
    /// </summary>
    internal static class UIPanelInspectorGUI
    {
        private enum GroupKind
        {
            None,
            Box,
            Foldout,
            Tabs
        }

        private sealed class MemberEntry
        {
            public SerializedProperty Property;
            public FieldInfo Field;
        }

        private sealed class TabEntry
        {
            public string Name;
            public string ColorExpression;
            public readonly List<MemberEntry> Members = new List<MemberEntry>();
        }

        private sealed class Group
        {
            public GroupKind Kind;
            public string Key;
            public bool ShowLabel = true;
            public bool DefaultExpanded = true;
            public readonly List<MemberEntry> Members = new List<MemberEntry>();
            public readonly List<TabEntry> Tabs = new List<TabEntry>();
        }

        private static readonly Dictionary<string, bool> FoldoutStates = new Dictionary<string, bool>();
        private static readonly Dictionary<string, int> TabStates = new Dictionary<string, int>();

        /// <summary>Draws every visible field of the object, then its [Button] methods.</summary>
        public static void DrawInspector(SerializedObject serializedObject)
        {
            UnityEngine.Object target = serializedObject.targetObject;
            if (target == null)
                return;

            serializedObject.Update();

            DrawScriptField(serializedObject);

            List<MemberEntry> members = CollectRootMembers(serializedObject, target.GetType());
            DrawGroups(BuildGroups(members), target, StateKeyRoot(target));

            serializedObject.ApplyModifiedProperties();

            DrawButtons(serializedObject);
        }

        private static void DrawButtons(SerializedObject serializedObject)
        {
            UnityEngine.Object target = serializedObject.targetObject;
            var methods = new List<MethodInfo>(UIPanelInspectorReflection.GetButtonMethods(target.GetType()));
            if (methods.Count == 0)
                return;

            EditorGUILayout.Space();

            for (int i = 0; i < methods.Count; i++)
            {
                MethodInfo method = methods[i];
                var attribute = (ButtonAttribute)Attribute.GetCustomAttribute(method, typeof(ButtonAttribute), true);
                string label = string.IsNullOrEmpty(attribute.Label) ? ObjectNames.NicifyVariableName(method.Name) : attribute.Label;

                if (!GUILayout.Button(label))
                    continue;

                // Every target, so the button behaves the same in a multi-selection.
                foreach (UnityEngine.Object selected in serializedObject.targetObjects)
                {
                    method.Invoke(selected, null);
                    EditorUtility.SetDirty(selected);
                }
            }
        }

        // ---------------------------------------------------------------- collecting

        private static void DrawScriptField(SerializedObject serializedObject)
        {
            SerializedProperty script = serializedObject.FindProperty("m_Script");
            if (script == null)
                return;

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(script);
        }

        private static List<MemberEntry> CollectRootMembers(SerializedObject serializedObject, Type ownerType)
        {
            var members = new List<MemberEntry>();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.propertyPath == "m_Script")
                    continue;

                members.Add(new MemberEntry
                {
                    Property = iterator.Copy(),
                    Field = UIPanelInspectorReflection.FindField(ownerType, iterator.name)
                });
            }

            return members;
        }

        private static List<MemberEntry> CollectChildMembers(SerializedProperty parent, Type ownerType)
        {
            var members = new List<MemberEntry>();
            SerializedProperty iterator = parent.Copy();
            SerializedProperty end = parent.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                members.Add(new MemberEntry
                {
                    Property = iterator.Copy(),
                    Field = ownerType == null ? null : UIPanelInspectorReflection.FindField(ownerType, iterator.name)
                });
            }

            return members;
        }

        private static List<Group> BuildGroups(List<MemberEntry> members)
        {
            var groups = new List<Group>();

            for (int i = 0; i < members.Count; i++)
            {
                MemberEntry member = members[i];
                FieldInfo field = member.Field;

                var box = GetAttribute<BoxGroupAttribute>(field);
                if (box != null)
                {
                    Group group = FindOrCreate(groups, GroupKind.Box, box.GroupName);
                    group.ShowLabel = box.ShowLabel;
                    group.Members.Add(member);
                    continue;
                }

                var foldout = GetAttribute<FoldoutGroupAttribute>(field);
                if (foldout != null)
                {
                    Group group = FindOrCreate(groups, GroupKind.Foldout, foldout.GroupName);
                    group.DefaultExpanded = foldout.Expanded;
                    group.Members.Add(member);
                    continue;
                }

                var tab = GetAttribute<TabGroupAttribute>(field);
                if (tab != null)
                {
                    Group group = FindOrCreate(groups, GroupKind.Tabs, tab.GroupId);
                    TabEntry tabEntry = group.Tabs.Find(t => t.Name == tab.TabName);

                    if (tabEntry == null)
                    {
                        tabEntry = new TabEntry { Name = tab.TabName, ColorExpression = tab.TextColor };
                        group.Tabs.Add(tabEntry);
                    }

                    tabEntry.Members.Add(member);
                    continue;
                }

                // Ungrouped fields keep their position relative to the groups around them.
                Group ungrouped = groups.Count > 0 && groups[groups.Count - 1].Kind == GroupKind.None
                    ? groups[groups.Count - 1]
                    : NewGroup(groups, GroupKind.None, string.Empty);

                ungrouped.Members.Add(member);
            }

            return groups;
        }

        private static Group FindOrCreate(List<Group> groups, GroupKind kind, string key)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Kind == kind && groups[i].Key == key)
                    return groups[i];
            }

            return NewGroup(groups, kind, key);
        }

        private static Group NewGroup(List<Group> groups, GroupKind kind, string key)
        {
            var group = new Group { Kind = kind, Key = key };
            groups.Add(group);
            return group;
        }

        // ---------------------------------------------------------------- drawing

        private static void DrawGroups(List<Group> groups, object owner, string stateKey)
        {
            for (int i = 0; i < groups.Count; i++)
                DrawGroup(groups[i], owner, stateKey);
        }

        private static void DrawGroup(Group group, object owner, string stateKey)
        {
            switch (group.Kind)
            {
                case GroupKind.None:
                    DrawMembers(group.Members, owner, stateKey);
                    break;

                case GroupKind.Box:
                    if (!AnyVisible(group.Members, owner))
                        return;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    if (group.ShowLabel && !string.IsNullOrEmpty(group.Key))
                        EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);

                    DrawMembers(group.Members, owner, stateKey);
                    EditorGUILayout.EndVertical();
                    break;

                case GroupKind.Foldout:
                    DrawFoldoutGroup(group, owner, stateKey);
                    break;

                case GroupKind.Tabs:
                    DrawTabGroup(group, owner, stateKey);
                    break;
            }
        }

        private static void DrawFoldoutGroup(Group group, object owner, string stateKey)
        {
            if (!AnyVisible(group.Members, owner))
                return;

            string key = stateKey + "/foldout/" + group.Key;
            bool expanded = GetFoldout(key, group.DefaultExpanded);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            expanded = EditorGUILayout.Foldout(expanded, group.Key, true);
            FoldoutStates[key] = expanded;

            if (expanded)
            {
                EditorGUI.indentLevel++;
                DrawMembers(group.Members, owner, stateKey);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawTabGroup(Group group, object owner, string stateKey)
        {
            if (group.Tabs.Count == 0)
                return;

            string key = stateKey + "/tabs/" + group.Key;
            int selected = Mathf.Clamp(GetTab(key), 0, group.Tabs.Count - 1);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < group.Tabs.Count; i++)
            {
                TabEntry tab = group.Tabs[i];
                GUIStyle style = group.Tabs.Count == 1
                    ? EditorStyles.miniButton
                    : i == 0
                        ? EditorStyles.miniButtonLeft
                        : i == group.Tabs.Count - 1
                            ? EditorStyles.miniButtonRight
                            : EditorStyles.miniButtonMid;

                Color previousColor = GUI.contentColor;
                if (TryResolveColor(tab.ColorExpression, owner, out Color tabColor))
                    GUI.contentColor = tabColor;

                if (GUILayout.Toggle(selected == i, tab.Name, style))
                    selected = i;

                GUI.contentColor = previousColor;
            }

            EditorGUILayout.EndHorizontal();
            TabStates[key] = selected;

            EditorGUILayout.Space(2f);
            DrawMembers(group.Tabs[selected].Members, owner, stateKey);
            EditorGUILayout.EndVertical();
        }

        private static void DrawMembers(List<MemberEntry> members, object owner, string stateKey)
        {
            for (int i = 0; i < members.Count; i++)
                DrawMember(members[i], owner, stateKey);
        }

        private static void DrawMember(MemberEntry member, object owner, string stateKey)
        {
            if (!IsVisible(member.Field, owner))
                return;

            SerializedProperty property = member.Property;
            GUIContent label = BuildLabel(member);

            var minMax = GetAttribute<MinMaxSliderAttribute>(member.Field);
            if (minMax != null && property.propertyType == SerializedPropertyType.Vector2)
            {
                DrawMinMaxSlider(property, label, minMax);
                return;
            }

            if (property.propertyType == SerializedPropertyType.Generic && !property.isArray)
            {
                DrawNested(member, label, stateKey);
                return;
            }

            EditorGUILayout.PropertyField(property, label, true);
        }

        private static void DrawNested(MemberEntry member, GUIContent label, string stateKey)
        {
            SerializedProperty property = member.Property;
            object value = UIPanelInspectorReflection.GetPropertyValue(property);
            Type valueType = value?.GetType() ?? member.Field?.FieldType;

            if (valueType == null)
            {
                EditorGUILayout.PropertyField(property, label, true);
                return;
            }

            List<MemberEntry> children = CollectChildMembers(property, valueType);
            if (children.Count == 0)
            {
                EditorGUILayout.PropertyField(property, label, true);
                return;
            }

            string childStateKey = stateKey + "/" + property.propertyPath;
            List<Group> groups = BuildGroups(children);

            if (IsInline(member.Field, valueType) || label == GUIContent.none)
            {
                DrawGroups(groups, value, childStateKey);
                return;
            }

            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, label, true);
            if (!property.isExpanded)
                return;

            EditorGUI.indentLevel++;
            DrawGroups(groups, value, childStateKey);
            EditorGUI.indentLevel--;
        }

        private static void DrawMinMaxSlider(SerializedProperty property, GUIContent label, MinMaxSliderAttribute attribute)
        {
            Vector2 range = property.vector2Value;
            float min = range.x;
            float max = range.y;

            EditorGUILayout.BeginHorizontal();

            if (label != GUIContent.none)
                EditorGUILayout.PrefixLabel(label);

            if (attribute.ShowFields)
                min = EditorGUILayout.FloatField(min, GUILayout.Width(48f));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.MinMaxSlider(ref min, ref max, attribute.Min, attribute.Max);
            bool sliderChanged = EditorGUI.EndChangeCheck();

            if (attribute.ShowFields)
                max = EditorGUILayout.FloatField(max, GUILayout.Width(48f));

            EditorGUILayout.EndHorizontal();

            min = Mathf.Clamp(min, attribute.Min, attribute.Max);
            max = Mathf.Clamp(max, min, attribute.Max);

            if (sliderChanged || !Mathf.Approximately(min, range.x) || !Mathf.Approximately(max, range.y))
                property.vector2Value = new Vector2(min, max);
        }

        // ---------------------------------------------------------------- attribute lookups

        private static GUIContent BuildLabel(MemberEntry member)
        {
            if (GetAttribute<HideLabelAttribute>(member.Field) != null)
                return GUIContent.none;

            var labelText = GetAttribute<LabelTextAttribute>(member.Field);
            string text = labelText != null && !string.IsNullOrEmpty(labelText.Text)
                ? labelText.Text
                : member.Property.displayName;

            var tooltip = GetAttribute<TooltipAttribute>(member.Field);
            return new GUIContent(text, tooltip?.tooltip ?? string.Empty);
        }

        private static bool IsInline(FieldInfo field, Type valueType)
        {
            if (field != null && Attribute.IsDefined(field, typeof(InlinePropertyAttribute), true))
                return true;

            return valueType != null && Attribute.IsDefined(valueType, typeof(InlinePropertyAttribute), true);
        }

        private static bool AnyVisible(List<MemberEntry> members, object owner)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (IsVisible(members[i].Field, owner))
                    return true;
            }

            return false;
        }

        private static bool IsVisible(FieldInfo field, object owner)
        {
            if (field == null)
                return true;

            var showIf = GetAttribute<ShowIfAttribute>(field);
            if (showIf != null && !Matches(owner, showIf.MemberName, showIf.ExpectedValue))
                return false;

            var hideIf = GetAttribute<HideIfAttribute>(field);
            if (hideIf != null && Matches(owner, hideIf.MemberName, hideIf.ExpectedValue))
                return false;

            return true;
        }

        private static bool Matches(object owner, string memberName, object expectedValue)
        {
            // An unresolvable condition shows the field rather than hiding it: a typo should be
            // visible in the inspector, not silently swallow the data behind it.
            if (!UIPanelInspectorReflection.TryGetMemberValue(owner, memberName, out object value))
                return true;

            if (expectedValue != null)
                return Equals(value, expectedValue);

            switch (value)
            {
                case null:
                    return false;
                case bool boolValue:
                    return boolValue;
                case UnityEngine.Object unityObject:
                    return unityObject != null;
                default:
                    return true;
            }
        }

        private static bool TryResolveColor(string expression, object owner, out Color color)
        {
            color = Color.white;

            const string prefix = "@this.";
            if (string.IsNullOrEmpty(expression) || !expression.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            string path = expression.Substring(prefix.Length);
            if (!UIPanelInspectorReflection.TryResolvePath(owner, path, out object value) || !(value is Color resolved))
                return false;

            color = resolved;
            return true;
        }

        private static T GetAttribute<T>(FieldInfo field) where T : Attribute
        {
            return field == null ? null : Attribute.GetCustomAttribute(field, typeof(T), true) as T;
        }

        // ---------------------------------------------------------------- persisted view state

        private static string StateKeyRoot(UnityEngine.Object target)
        {
            return target.GetInstanceID().ToString();
        }

        private static bool GetFoldout(string key, bool defaultValue)
        {
            return FoldoutStates.TryGetValue(key, out bool value) ? value : defaultValue;
        }

        private static int GetTab(string key)
        {
            return TabStates.TryGetValue(key, out int value) ? value : 0;
        }
    }
}
#endif
