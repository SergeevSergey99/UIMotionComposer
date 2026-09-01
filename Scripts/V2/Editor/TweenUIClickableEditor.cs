using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UIMotionComposer.V2.Editor
{
    [CustomEditor(typeof(TweenUIClickable))]
    [CanEditMultipleObjects]
    public sealed class TweenUIClickableEditor : UnityEditor.Editor
    {
        private const float PriorityWidth = 48f;
        private const float StateWidth = 76f;
        private const float ActionWidth = 58f;

        private bool _showReferences;
        private bool _showTransitions = true;
        private bool _showEvents;
        private bool _previewBlocked;
        private TweenPreviewAnimationMode _previewMode;
        private TweenPlayer _previewPlayer;
        private string _previewAnimation;

        private static readonly StateRow[] StateRows =
        {
            new StateRow(TweenClickableState.Disabled, "Disabled", "disabledAnimation", 5,
                "Highest priority. Entered whenever CanvasGroup or Selectable is not interactable."),
            new StateRow(TweenClickableState.Pressed, "Pressed", "pressedAnimation", 4,
                "Pointer down or Submit while the control is interactable."),
            new StateRow(TweenClickableState.Selected, "Selected", "selectedAnimation", 3,
                "Selected by keyboard, gamepad, or EventSystem. Takes priority over pointer hover."),
            new StateRow(TweenClickableState.Hovered, "Hovered", "hoverAnimation", 2,
                "Pointer is inside and the control is neither pressed nor selected."),
            new StateRow(TweenClickableState.Normal, "Normal", "normalAnimation", 1,
                "Fallback state when no higher-priority condition is active.")
        };

        private readonly struct StateRow
        {
            public readonly TweenClickableState State;
            public readonly string Label;
            public readonly string Property;
            public readonly int Priority;
            public readonly string Tooltip;

            public StateRow(TweenClickableState state, string label, string property, int priority,
                string tooltip)
            {
                State = state;
                Label = label;
                Property = property;
                Priority = priority;
                Tooltip = tooltip;
            }
        }

        private void OnDisable()
        {
            StopPreview();
            _previewMode?.Dispose();
            _previewMode = null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            TweenPlayer player = ResolvePlayer();
            string[] ids = CollectAnimationIds(player);

            EditorGUILayout.HelpBox(
                "Stateful UI control. Only one state animation owns the control at a time, so an " +
                "infinite Hover loop is stopped before Pressed, Selected, Normal, or Disabled starts.",
                MessageType.Info);

            DrawReferences();
            DrawStateEditor(player, ids);
            DrawTransitions();
            DrawEvents();
            DrawRuntimeDiagnostics();

            if (player == null)
                EditorGUILayout.HelpBox("TweenPlayer is missing.", MessageType.Error);
            else if (ids.Length == 0)
                EditorGUILayout.HelpBox("Add state animations to the attached TweenPlayer.", MessageType.Warning);
            else
                DrawMissingSummary(ids);

            if (_previewBlocked)
                EditorGUILayout.HelpBox(
                    "Preview is unavailable while the Animation window, Timeline, or another preview driver is active.",
                    MessageType.Warning);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawReferences()
        {
            _showReferences = EditorGUILayout.Foldout(_showReferences, "References", true);
            if (!_showReferences)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("player"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("canvasGroup"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("selectable"));
            }
        }

        private void DrawStateEditor(TweenPlayer player, string[] ids)
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("State editor", EditorStyles.boldLabel);
                if (GUILayout.Button("Conventional IDs", EditorStyles.miniButton, GUILayout.Width(112f)))
                    ApplyConventionalIds();
                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(42f)))
                    ClearMappings();
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Priority", EditorStyles.miniLabel, GUILayout.Width(PriorityWidth));
                GUILayout.Label("State", EditorStyles.miniLabel, GUILayout.Width(StateWidth));
                GUILayout.Label("Animation on enter", EditorStyles.miniLabel);
                GUILayout.Space(ActionWidth);
            }

            TweenUIClickable clickable = targets.Length == 1 ? (TweenUIClickable)target : null;
            for (int i = 0; i < StateRows.Length; i++)
                DrawStateRow(StateRows[i], clickable, player, ids);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty reenabled = serializedObject.FindProperty("interactableAnimation");
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Exit", EditorStyles.miniBoldLabel, GUILayout.Width(PriorityWidth));
                    GUILayout.Label(new GUIContent("Disabled", "Played when an inactive control becomes interactable again."),
                        GUILayout.Width(StateWidth));
                    DrawAnimationPopup(reenabled, ids);
                    DrawAnimationAction(player, null, reenabled.stringValue);
                }
                EditorGUILayout.LabelField(
                    "Re-enabled is a transition animation. If the control is already Selected or Hovered, " +
                    "that state's entry animation is used instead.", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawStateRow(StateRow row, TweenUIClickable clickable, TweenPlayer player,
            string[] ids)
        {
            SerializedProperty property = serializedObject.FindProperty(row.Property);
            bool active = Application.isPlaying && clickable != null && clickable.State == row.State;
            bool missing = IsMissing(property, ids);

            Color previous = GUI.backgroundColor;
            if (active)
                GUI.backgroundColor = new Color(0.55f, 0.9f, 0.65f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = previous;
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent($"{row.Priority}", row.Tooltip),
                        active ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel,
                        GUILayout.Width(PriorityWidth));
                    GUILayout.Label(new GUIContent(active ? $"● {row.Label}" : row.Label, row.Tooltip),
                        active ? EditorStyles.boldLabel : EditorStyles.label,
                        GUILayout.Width(StateWidth));
                    DrawAnimationPopup(property, ids);
                    DrawAnimationAction(player, row, property.stringValue);
                }

                if (missing)
                    EditorGUILayout.LabelField($"Animation '{property.stringValue}' is missing on TweenPlayer.",
                        EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawAnimationAction(TweenPlayer player, StateRow? row, string animationId)
        {
            bool available = targets.Length == 1 && player != null &&
                             !string.IsNullOrWhiteSpace(animationId) &&
                             player.FindAnimation(animationId) != null;
            string label = Application.isPlaying ? "Play" :
                (_previewPlayer == player && _previewAnimation == animationId ? "Restore" : "Preview");

            using (new EditorGUI.DisabledScope(!available))
            {
                if (!GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(ActionWidth)))
                    return;

                serializedObject.ApplyModifiedProperties();
                if (Application.isPlaying)
                {
                    if (row.HasValue)
                        ((TweenUIClickable)target).PlayStateAnimation(row.Value.State);
                    else
                        player.Play(animationId);
                }
                else if (_previewPlayer == player && _previewAnimation == animationId)
                {
                    StopPreview();
                }
                else
                {
                    Preview(player, animationId);
                }
            }
        }

        private static void DrawAnimationPopup(SerializedProperty property, string[] ids)
        {
            bool mixed = property.hasMultipleDifferentValues;
            int existing = mixed ? -1 : Array.IndexOf(ids, property.stringValue);
            bool missing = !mixed && existing < 0 && !string.IsNullOrWhiteSpace(property.stringValue);
            string[] options = missing
                ? new[] { "— None —" }.Concat(ids).Concat(new[] { $"⚠ Missing: {property.stringValue}" }).ToArray()
                : new[] { "— None —" }.Concat(ids).ToArray();
            int current = mixed ? 0 : string.IsNullOrWhiteSpace(property.stringValue)
                ? 0
                : missing ? options.Length - 1 : existing + 1;

            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.Popup(current, options);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = next == 0 ? string.Empty : next <= ids.Length ? ids[next - 1] : property.stringValue;
            EditorGUI.showMixedValue = false;
        }

        private void DrawTransitions()
        {
            EditorGUILayout.Space(2f);
            _showTransitions = EditorGUILayout.Foldout(_showTransitions, "Resolved transitions", true);
            if (!_showTransitions)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                DrawTransition("Interactable = false", "Any", "Disabled");
                DrawTransition("Pointer Down / Submit", "Normal · Hovered · Selected", "Pressed");
                DrawTransition("Select", "Normal · Hovered", "Selected");
                DrawTransition("Pointer Enter", "Normal", "Hovered");
                DrawTransition("Pointer Up", "Pressed", "Selected · Hovered · Normal");
                DrawTransition("Deselect / Pointer Exit", "Selected · Hovered", "Hovered · Normal");
            }
        }

        private static void DrawTransition(string input, string from, string to)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(input, GUILayout.MinWidth(135f));
                GUILayout.Label(from, EditorStyles.miniLabel, GUILayout.MinWidth(145f));
                GUILayout.Label("→", GUILayout.Width(18f));
                GUILayout.Label(to, EditorStyles.miniBoldLabel, GUILayout.MinWidth(100f));
            }
        }

        private void DrawEvents()
        {
            EditorGUILayout.Space(2f);
            _showEvents = EditorGUILayout.Foldout(_showEvents, "Events", true);
            if (!_showEvents)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onHoverStarted"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onHoverEnded"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onPressed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onReleased"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("onInteractableChanged"));
            }
        }

        private void DrawRuntimeDiagnostics()
        {
            if (!Application.isPlaying || targets.Length != 1)
                return;

            var clickable = (TweenUIClickable)target;
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"State: {clickable.State}   Pointer: {OnOff(clickable.IsPointerInside)}   " +
                $"Pressed: {OnOff(clickable.IsPressed)}   Selected: {OnOff(clickable.IsSelected)}",
                EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Enable")) clickable.SetInteractable(true);
                if (GUILayout.Button("Disable")) clickable.SetInteractable(false);
                if (GUILayout.Button("Replay state")) clickable.PlayCurrentState();
            }
            Repaint();
        }

        private void DrawMissingSummary(string[] ids)
        {
            var missing = new HashSet<string>(StringComparer.Ordinal);
            string[] properties = StateRows.Select(row => row.Property)
                .Concat(new[] { "interactableAnimation" }).ToArray();
            for (int i = 0; i < properties.Length; i++)
            {
                SerializedProperty property = serializedObject.FindProperty(properties[i]);
                if (IsMissing(property, ids))
                    missing.Add(property.stringValue);
            }

            if (missing.Count > 0)
                EditorGUILayout.HelpBox("Missing animations: " + string.Join(", ", missing), MessageType.Warning);
        }

        private void ApplyConventionalIds()
        {
            Set("normalAnimation", TweenIds.Unhover);
            Set("hoverAnimation", TweenIds.Hover);
            Set("pressedAnimation", TweenIds.Click);
            Set("selectedAnimation", TweenIds.Hover);
            Set("disabledAnimation", TweenIds.Disabled);
            Set("interactableAnimation", TweenIds.Interactable);
            StopPreview();
        }

        private void ClearMappings()
        {
            for (int i = 0; i < StateRows.Length; i++)
                Set(StateRows[i].Property, string.Empty);
            Set("interactableAnimation", string.Empty);
            StopPreview();
        }

        private void Set(string propertyName, string value)
        {
            serializedObject.FindProperty(propertyName).stringValue = value;
        }

        private void Preview(TweenPlayer player, string animationId)
        {
            StopPreview();
            UnityEngine.Object[] affected = player.PreparePreview(animationId);
            if (affected.Length == 0)
                return;

            _previewMode ??= new TweenPreviewAnimationMode();
            if (!_previewMode.TryStart())
            {
                player.StopPreview();
                _previewBlocked = true;
                return;
            }

            _previewBlocked = false;
            _previewMode.RegisterTargets(affected);
            player.SamplePreparedPreview(1f);
            _previewPlayer = player;
            _previewAnimation = animationId;
            SceneView.RepaintAll();
        }

        private void StopPreview()
        {
            if (_previewPlayer != null)
                _previewPlayer.StopPreview();
            _previewMode?.Stop();
            _previewPlayer = null;
            _previewAnimation = null;
            _previewBlocked = false;
            SceneView.RepaintAll();
        }

        private TweenPlayer ResolvePlayer()
        {
            SerializedProperty property = serializedObject.FindProperty("player");
            if (property.objectReferenceValue is TweenPlayer assigned)
                return assigned;
            return targets.Length == 1 ? ((TweenUIClickable)target).GetComponent<TweenPlayer>() : null;
        }

        private static string[] CollectAnimationIds(TweenPlayer player)
        {
            return player?.Animations
                .Where(animation => animation != null && !string.IsNullOrWhiteSpace(animation.Id))
                .Select(animation => animation.Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
        }

        private static bool IsMissing(SerializedProperty property, string[] ids)
        {
            return !property.hasMultipleDifferentValues &&
                   !string.IsNullOrWhiteSpace(property.stringValue) &&
                   Array.IndexOf(ids, property.stringValue) < 0;
        }

        private static string OnOff(bool value) => value ? "yes" : "no";
    }
}
