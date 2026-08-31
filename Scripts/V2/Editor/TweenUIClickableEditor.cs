using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UIMotionComposer.V2.Editor
{
    [CustomEditor(typeof(TweenUIClickable))]
    [CanEditMultipleObjects]
    public sealed class TweenUIClickableEditor : UnityEditor.Editor
    {
        private bool _showReferences;
        private bool _showEvents;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            TweenPlayer player = ResolvePlayer();
            string[] ids = player?.Animations
                .Where(animation => animation != null && !string.IsNullOrWhiteSpace(animation.Id))
                .Select(animation => animation.Id)
                .Distinct()
                .ToArray() ?? Array.Empty<string>();

            EditorGUILayout.HelpBox(
                "Stateful button wrapper. It stops the previous state animation before starting the next one, including infinite Hover loops.",
                MessageType.Info);

            _showReferences = EditorGUILayout.Foldout(_showReferences, "References", true);
            if (_showReferences)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("player"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("canvasGroup"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("selectable"));
            }

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("State animations", EditorStyles.boldLabel);
            DrawAnimationId(serializedObject.FindProperty("normalAnimation"), "Normal", ids);
            DrawAnimationId(serializedObject.FindProperty("hoverAnimation"), "Hovered", ids);
            DrawAnimationId(serializedObject.FindProperty("pressedAnimation"), "Pressed", ids);
            DrawAnimationId(serializedObject.FindProperty("disabledAnimation"), "Disabled", ids);
            DrawAnimationId(serializedObject.FindProperty("interactableAnimation"), "Re-enabled", ids);

            if (player == null)
                EditorGUILayout.HelpBox("TweenPlayer is missing.", MessageType.Error);
            else if (ids.Length == 0)
                EditorGUILayout.HelpBox("Add state animations to the attached TweenPlayer.", MessageType.Warning);

            _showEvents = EditorGUILayout.Foldout(_showEvents, "Events", true);
            if (_showEvents)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onHoverStarted"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onHoverEnded"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onPressed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onReleased"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onInteractableChanged"));
            }

            if (Application.isPlaying && targets.Length == 1)
            {
                var clickable = (TweenUIClickable)target;
                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField($"Runtime state: {clickable.State}", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Enable")) clickable.SetInteractable(true);
                    if (GUILayout.Button("Disable")) clickable.SetInteractable(false);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private TweenPlayer ResolvePlayer()
        {
            SerializedProperty property = serializedObject.FindProperty("player");
            if (property.objectReferenceValue is TweenPlayer assigned)
                return assigned;
            return targets.Length == 1 ? ((TweenUIClickable)target).GetComponent<TweenPlayer>() : null;
        }

        private static void DrawAnimationId(SerializedProperty property, string label, string[] ids)
        {
            int existing = Array.IndexOf(ids, property.stringValue);
            bool missing = existing < 0 && !string.IsNullOrWhiteSpace(property.stringValue);
            string[] options = missing
                ? new[] { "— None —" }.Concat(ids).Concat(new[] { $"⚠ Missing: {property.stringValue}" }).ToArray()
                : new[] { "— None —" }.Concat(ids).ToArray();
            int current = string.IsNullOrWhiteSpace(property.stringValue)
                ? 0
                : missing ? options.Length - 1 : existing + 1;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                int next = EditorGUILayout.Popup(current, options);
                if (next != current)
                    property.stringValue = next == 0 ? string.Empty : next <= ids.Length ? ids[next - 1] : property.stringValue;

                if (missing)
                    GUILayout.Label("Missing", EditorStyles.miniLabel, GUILayout.Width(45f));
            }
        }
    }
}
