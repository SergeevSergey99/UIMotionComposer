using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UIMotionComposer.V2.Editor
{
    [CustomEditor(typeof(TweenUIPanel))]
    [CanEditMultipleObjects]
    public sealed class TweenUIPanelEditor : UnityEditor.Editor
    {
        private SerializedProperty _player;
        private SerializedProperty _canvasGroup;
        private SerializedProperty _showAnimation;
        private SerializedProperty _hideAnimation;
        private SerializedProperty _hideOnAwake;
        private SerializedProperty _deactivateWhenHidden;
        private SerializedProperty _manageInteractability;
        private SerializedProperty _interactableWhileShowing;
        private bool _showEvents;

        private void OnEnable()
        {
            _player = serializedObject.FindProperty("player");
            _canvasGroup = serializedObject.FindProperty("canvasGroup");
            _showAnimation = serializedObject.FindProperty("showAnimation");
            _hideAnimation = serializedObject.FindProperty("hideAnimation");
            _hideOnAwake = serializedObject.FindProperty("hideOnAwake");
            _deactivateWhenHidden = serializedObject.FindProperty("deactivateWhenHidden");
            _manageInteractability = serializedObject.FindProperty("manageInteractability");
            _interactableWhileShowing = serializedObject.FindProperty("interactableWhileShowing");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "Panel lifecycle wrapper. TweenPlayer composes motion; this component controls activation, input and Show/Hide callbacks.",
                MessageType.Info);

            EditorGUILayout.PropertyField(_player);
            EditorGUILayout.PropertyField(_canvasGroup);

            TweenPlayer player = ResolvePlayer();
            string[] animationIds = player == null
                ? Array.Empty<string>()
                : player.Animations
                    .Where(animation => animation != null && !string.IsNullOrWhiteSpace(animation.Id))
                    .Select(animation => animation.Id)
                    .Distinct()
                    .ToArray();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Animations", EditorStyles.boldLabel);
            DrawAnimationId(_showAnimation, "Show", animationIds);
            DrawAnimationId(_hideAnimation, "Hide", animationIds);

            if (player == null)
                EditorGUILayout.HelpBox("Assign a TweenPlayer.", MessageType.Error);
            else
            {
                DrawMissingMessage(player, _showAnimation.stringValue, "Show");
                DrawMissingMessage(player, _hideAnimation.stringValue, "Hide");
                DrawInfiniteLoopMessage(player, _showAnimation.stringValue, "Show");
                DrawInfiniteLoopMessage(player, _hideAnimation.stringValue, "Hide");
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Behaviour", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_hideOnAwake, new GUIContent("Hide on Awake"));
            EditorGUILayout.PropertyField(_deactivateWhenHidden, new GUIContent("Deactivate when hidden"));
            if (_hideOnAwake.boolValue && !_deactivateWhenHidden.boolValue)
                EditorGUILayout.HelpBox("Hide on Awake will disable input but keep this GameObject active. Ensure its authored pose is visually hidden.", MessageType.Info);
            EditorGUILayout.PropertyField(_manageInteractability, new GUIContent("Manage interactability"));
            if (_manageInteractability.boolValue)
                EditorGUILayout.PropertyField(_interactableWhileShowing, new GUIContent("Interactable while showing"));

            _showEvents = EditorGUILayout.Foldout(_showEvents, "Events", true);
            if (_showEvents)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onShowStarted"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onShowCompleted"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onShowCancelled"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onHideStarted"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onHideCompleted"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onHideCancelled"));
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(5f);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || targets.Length != 1))
            {
                var panel = (TweenUIPanel)target;
                EditorGUILayout.LabelField(Application.isPlaying ? $"State: {panel.State}" : "Runtime controls", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Show")) panel.Show();
                    if (GUILayout.Button("Hide")) panel.Hide();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Instant Show")) panel.InstantShow();
                    if (GUILayout.Button("Instant Hide")) panel.InstantHide();
                }
            }
        }

        private TweenPlayer ResolvePlayer()
        {
            if (_player.objectReferenceValue is TweenPlayer assigned)
                return assigned;
            return targets.Length == 1 ? ((TweenUIPanel)target).GetComponent<TweenPlayer>() : null;
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

                if (!string.IsNullOrWhiteSpace(property.stringValue) && !ids.Contains(property.stringValue))
                    GUILayout.Label("Missing", EditorStyles.miniLabel, GUILayout.Width(45f));
            }
        }

        private static void DrawMissingMessage(TweenPlayer player, string id, string role)
        {
            if (!string.IsNullOrWhiteSpace(id) && player.FindAnimation(id) == null)
                EditorGUILayout.HelpBox($"{role} animation '{id}' does not exist on this player.", MessageType.Warning);
        }

        private static void DrawInfiniteLoopMessage(TweenPlayer player, string id, string role)
        {
            // Not a null-conditional chain: lifting the enum makes "null != None" true, so a missing
            // animation would fall straight through into dereferencing it.
            if (player.IsInfinite(id))
                EditorGUILayout.HelpBox($"{role} uses an infinite loop, so the panel transition will never complete.", MessageType.Warning);
        }
    }
}
