using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UIMotionComposer.Editor
{
    [CustomPropertyDrawer(typeof(TweenInitialPose))]
    public sealed class TweenInitialPoseDrawer : PropertyDrawer
    {
        private const float HelpHeight = 40f;
        private const float Gap = 3f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
                return line;

            SerializedProperty values = property.FindPropertyRelative("values");
            float height = line + Gap + HelpHeight + Gap + line;
            if (values == null || values.arraySize == 0)
                return height;

            height += Gap + line;
            if (values.isExpanded)
            {
                for (int i = 0; i < values.arraySize; i++)
                    height += Gap + EditorGUI.GetPropertyHeight(values.GetArrayElementAtIndex(i), GUIContent.none, true);

                if (MissingCount(property) > 0)
                    height += Gap + line;
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            SerializedProperty captured = property.FindPropertyRelative("captured");
            SerializedProperty values = property.FindPropertyRelative("values");
            TweenPlayer player = property.serializedObject.targetObject as TweenPlayer;
            bool singlePlayer = property.serializedObject.targetObjects.Length == 1 && player != null;

            Rect row = Take(ref position, line);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (singlePlayer && player.HasCapturedInitialValues)
            {
                GUIContent count = new GUIContent($"{player.CapturedInitialValueCount} properties");
                Vector2 size = EditorStyles.miniLabel.CalcSize(count);
                Rect countRect = new Rect(row.xMax - size.x, row.y, size.x, row.height);
                EditorGUI.LabelField(countRect, count, EditorStyles.miniLabel);
            }

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            position.y += Gap;
            Rect help = Take(ref position, HelpHeight);
            int missing = MissingCount(property);
            if (captured == null || !captured.boolValue)
            {
                EditorGUI.HelpBox(help,
                    "No saved pose. Initial endpoints fall back to values captured at first playback.",
                    MessageType.Warning);
            }
            else if (missing > 0)
            {
                EditorGUI.HelpBox(help,
                    $"{missing} saved properties have a missing target or unsupported property type.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUI.HelpBox(help,
                    "Saved values are editable. Restore applies them without capturing a new pose.",
                    MessageType.None);
            }

            position.y += Gap;
            Rect buttons = Take(ref position, line);
            DrawActions(buttons, property, player, singlePlayer, captured?.boolValue == true,
                values?.arraySize ?? 0);

            if (values == null || values.arraySize == 0)
            {
                EditorGUI.EndProperty();
                return;
            }

            position.y += Gap;
            Rect valuesHeader = Take(ref position, line);
            values.isExpanded = EditorGUI.Foldout(valuesHeader, values.isExpanded,
                $"Saved properties ({values.arraySize})", true);
            if (values.isExpanded)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < values.arraySize; i++)
                {
                    position.y += Gap;
                    SerializedProperty entry = values.GetArrayElementAtIndex(i);
                    float entryHeight = EditorGUI.GetPropertyHeight(entry, GUIContent.none, true);
                    Rect entryRect = Take(ref position, entryHeight);
                    EditorGUI.PropertyField(entryRect, entry, GUIContent.none, true);
                }
                EditorGUI.indentLevel--;

                if (missing > 0)
                {
                    position.y += Gap;
                    Rect remove = Take(ref position, line);
                    using (new EditorGUI.DisabledScope(!singlePlayer || Application.isPlaying))
                    {
                        if (GUI.Button(remove, "Remove missing entries"))
                        {
                            Undo.RecordObject(player, "Remove missing UI Motion initial values");
                            player.RemoveMissingInitialValues();
                            MarkDirty(player);
                            property.serializedObject.Update();
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            EditorGUI.EndProperty();
        }

        private static void DrawActions(Rect rect, SerializedProperty property, TweenPlayer player,
            bool singlePlayer, bool captured, int count)
        {
            float clearWidth = 52f;
            float gap = 3f;
            float mainWidth = (rect.width - clearWidth - gap * 2f) * 0.5f;
            Rect capture = new Rect(rect.x, rect.y, mainWidth, rect.height);
            Rect restore = new Rect(capture.xMax + gap, rect.y, mainWidth, rect.height);
            Rect clear = new Rect(restore.xMax + gap, rect.y, clearWidth, rect.height);

            using (new EditorGUI.DisabledScope(!singlePlayer || Application.isPlaying))
            {
                if (GUI.Button(capture, captured ? "Recapture" : "Capture Pose"))
                {
                    property.serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(player, "Capture UI Motion initial pose");
                    player.CaptureInitialValues();
                    MarkDirty(player);
                    property.serializedObject.Update();
                    GUIUtility.ExitGUI();
                }

                using (new EditorGUI.DisabledScope(!captured || count == 0))
                {
                    if (GUI.Button(restore, "Restore Pose"))
                    {
                        property.serializedObject.ApplyModifiedProperties();
                        player.InvalidateAuthoringCache();
                        TweenInitialPoseEntryInfo[] entries = player.GetCapturedInitialPoseEntries();
                        UnityEngine.Object[] changed = entries.Where(item => item.Target != null)
                            .Select(item => item.Target).Distinct().ToArray();
                        if (changed.Length > 0)
                            Undo.RegisterCompleteObjectUndo(changed, "Restore UI Motion initial pose");
                        player.RestoreInitialValues();
                        MarkDirty(changed);
                        SceneView.RepaintAll();
                        property.serializedObject.Update();
                        GUIUtility.ExitGUI();
                    }

                    if (GUI.Button(clear, "Clear"))
                    {
                        property.serializedObject.ApplyModifiedProperties();
                        Undo.RecordObject(player, "Clear UI Motion initial pose");
                        player.ClearCapturedInitialValues();
                        MarkDirty(player);
                        property.serializedObject.Update();
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        private static int MissingCount(SerializedProperty property)
        {
            if (property.serializedObject.targetObject is not TweenPlayer player)
                return 0;
            return player.GetCapturedInitialPoseEntries().Count(item => item.Target == null || !item.CanRestore);
        }

        private static Rect Take(ref Rect remaining, float height)
        {
            Rect result = new Rect(remaining.x, remaining.y, remaining.width, height);
            remaining.y += height;
            return result;
        }

        internal static void MarkDirty(params UnityEngine.Object[] changed)
        {
            MarkDirty((IEnumerable<UnityEngine.Object>)changed);
        }

        internal static void MarkDirty(IEnumerable<UnityEngine.Object> changed)
        {
            foreach (UnityEngine.Object item in changed)
            {
                if (item == null)
                    continue;
                EditorUtility.SetDirty(item);
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
        }
    }

    [CustomPropertyDrawer(typeof(TweenInitialValue))]
    public sealed class TweenInitialValueDrawer : PropertyDrawer
    {
        private const float Gap = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 3f + Gap * 4f + 8f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);
            position = new Rect(position.x + 6f, position.y + 4f, position.width - 12f, position.height - 8f);

            float line = EditorGUIUtility.singleLineHeight;
            SerializedProperty target = property.FindPropertyRelative("target");
            SerializedProperty propertyId = property.FindPropertyRelative("propertyId");
            SerializedProperty valueType = property.FindPropertyRelative("valueType");
            SerializedProperty value = ValueProperty(property, valueType);
            TweenPlayer player = property.serializedObject.targetObject as TweenPlayer;

            Rect objectRow = Take(ref position, line);
            Rect objectField = new Rect(objectRow.x, objectRow.y, objectRow.width - 62f, objectRow.height);
            Rect restore = new Rect(objectField.xMax + 4f, objectRow.y, 58f, objectRow.height);
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.PropertyField(objectField, target, GUIContent.none);
            using (new EditorGUI.DisabledScope(player == null || target?.objectReferenceValue == null ||
                                                value == null || Application.isPlaying))
            {
                if (GUI.Button(restore, "Restore"))
                    RestoreEntry(property, player, target.objectReferenceValue);
            }

            position.y += Gap;
            Rect pathRow = Take(ref position, line);
            EditorGUI.LabelField(pathRow, TargetPath(player, target?.objectReferenceValue), EditorStyles.miniLabel);

            position.y += Gap;
            Rect valueRow = Take(ref position, line);
            string id = propertyId?.stringValue ?? string.Empty;
            if (value != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(valueRow, value,
                    new GUIContent(PropertyName(id), id));
                if (EditorGUI.EndChangeCheck())
                    player?.InvalidateAuthoringCache();
            }
            else
            {
                EditorGUI.HelpBox(valueRow, $"Unsupported initial value: {id}", MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        private static SerializedProperty ValueProperty(SerializedProperty property, SerializedProperty valueType)
        {
            if (valueType == null || valueType.enumValueIndex < 0 ||
                valueType.enumValueIndex >= valueType.enumNames.Length)
                return null;

            return valueType.enumNames[valueType.enumValueIndex] switch
            {
                "Float" => property.FindPropertyRelative("floatValue"),
                "Vector2" => property.FindPropertyRelative("vector2Value"),
                "Vector3" => property.FindPropertyRelative("vector3Value"),
                "Color" => property.FindPropertyRelative("colorValue"),
                _ => null
            };
        }

        private static void RestoreEntry(SerializedProperty property, TweenPlayer player,
            UnityEngine.Object changedTarget)
        {
            int index = ArrayIndex(property.propertyPath);
            if (index < 0)
                return;

            property.serializedObject.ApplyModifiedProperties();
            player.InvalidateAuthoringCache();
            Undo.RegisterCompleteObjectUndo(changedTarget, "Restore UI Motion initial value");
            if (player.RestoreInitialValueAt(index))
                TweenInitialPoseDrawer.MarkDirty(changedTarget);
            SceneView.RepaintAll();
            property.serializedObject.Update();
            GUIUtility.ExitGUI();
        }

        private static int ArrayIndex(string path)
        {
            int marker = path.LastIndexOf("data[", StringComparison.Ordinal);
            if (marker < 0)
                return -1;
            int start = marker + 5;
            int end = path.IndexOf(']', start);
            return end > start && int.TryParse(path.Substring(start, end - start), out int index)
                ? index
                : -1;
        }

        private static string TargetPath(TweenPlayer player, UnityEngine.Object target)
        {
            Transform transform = target switch
            {
                GameObject gameObject => gameObject.transform,
                Component component => component.transform,
                _ => null
            };
            if (transform == null)
                return "Missing target";
            if (player == null || transform == player.transform)
                return "This TweenPlayer";
            return transform.IsChildOf(player.transform)
                ? AnimationUtility.CalculateTransformPath(transform, player.transform)
                : transform.name + " (external)";
        }

        private static string PropertyName(string propertyId)
        {
            return propertyId switch
            {
                "Transform.LocalPosition" => "Local Position",
                "Transform.Position" => "World Position",
                "Transform.LocalScale" => "Local Scale",
                "Transform.LocalRotation" => "Local Rotation",
                "Transform.Rotation" => "World Rotation",
                "RectTransform.AnchoredPosition" => "Anchored Position",
                "RectTransform.AnchoredPosition3D" => "Anchored Position 3D",
                "RectTransform.SizeDelta" => "Size Delta",
                "RectTransform.Pivot" => "Pivot",
                "Visual.Alpha" => "Alpha",
                "Visual.Color" => "Color",
                "Image.FillAmount" => "Fill Amount",
                _ => string.IsNullOrEmpty(propertyId) ? "Unknown Property" : propertyId
            };
        }

        private static Rect Take(ref Rect remaining, float height)
        {
            Rect result = new Rect(remaining.x, remaining.y, remaining.width, height);
            remaining.y += height;
            return result;
        }
    }
}
