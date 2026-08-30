using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UIMotionComposer.V2.Editor
{
    [CustomEditor(typeof(TweenPlayer))]
    [CanEditMultipleObjects]
    public sealed class TweenPlayerEditor : UnityEditor.Editor
    {
        private SerializedProperty _animations;
        private SerializedProperty _targetOverrides;
        private SerializedProperty _playOnEnable;
        private int _selectedAnimation;
        private bool _showAdvanced;
        private float _previewTime;
        private bool _previewPlaying;
        private double _previewStartedAt;
        private float _previewStartedFrom;

        private TweenPlayer Player => (TweenPlayer)target;

        private void OnEnable()
        {
            _animations = serializedObject.FindProperty("animations");
            _targetOverrides = serializedObject.FindProperty("targetOverrides");
            _playOnEnable = serializedObject.FindProperty("playOnEnable");
            EditorApplication.update += UpdatePreview;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePreview;
            StopPreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Add named animations, then compose each one from independent clips. Clips may overlap; Delay is measured from the animation start.",
                MessageType.Info);

            DrawAnimationSelector();

            DrawValidationMessages();

            if (_animations.arraySize > 0)
            {
                _selectedAnimation = Mathf.Clamp(_selectedAnimation, 0, _animations.arraySize - 1);
                DrawSelectedAnimation(_animations.GetArrayElementAtIndex(_selectedAnimation));
            }
            else
            {
                EditorGUILayout.HelpBox("Create an animation to begin.", MessageType.None);
            }

            EditorGUILayout.Space(6f);
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Targets and automatic playback", true);
            if (_showAdvanced)
            {
                EditorGUILayout.PropertyField(_targetOverrides, new GUIContent("Target overrides"), true);
                EditorGUILayout.PropertyField(_playOnEnable, new GUIContent("Play on enable"), true);
                if (GUILayout.Button("Clear captured initial values"))
                    Player.ClearCapturedInitialValues();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAnimationSelector()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string[] names = Enumerable.Range(0, _animations.arraySize)
                    .Select(i => AnimationLabel(_animations.GetArrayElementAtIndex(i), i))
                    .ToArray();

                using (new EditorGUI.DisabledScope(names.Length == 0))
                    _selectedAnimation = EditorGUILayout.Popup(_selectedAnimation, names, EditorStyles.toolbarPopup);

                if (GUILayout.Button("+ Animation", EditorStyles.toolbarButton, GUILayout.Width(84f)))
                    AddAnimation();

                using (new EditorGUI.DisabledScope(_animations.arraySize == 0))
                {
                    if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                        DuplicateAnimation();
                    if (GUILayout.Button("Remove", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                        RemoveAnimation();
                }
            }
        }

        private void DrawSelectedAnimation(SerializedProperty animation)
        {
            SerializedProperty id = animation.FindPropertyRelative("Id");
            SerializedProperty asset = animation.FindPropertyRelative("Asset");
            SerializedProperty playback = animation.FindPropertyRelative("Playback");
            SerializedProperty clips = animation.FindPropertyRelative("Clips");

            EditorGUILayout.Space(5f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(id, new GUIContent("Animation ID"));
                DrawIdShortcuts(id);
                EditorGUILayout.PropertyField(asset, new GUIContent("Shared clip asset"));

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);
                DrawPlayback(playback);

                EditorGUILayout.Space(4f);
                if (asset.objectReferenceValue != null)
                {
                    EditorGUILayout.HelpBox("Clips are read from the shared asset. Select it to edit the clip stack.", MessageType.None);
                    if (GUILayout.Button("Select shared animation asset"))
                        Selection.activeObject = asset.objectReferenceValue;
                }
                else
                {
                    TweenClipEditorUtility.DrawClipList(serializedObject, clips, "Clips");
                }

                DrawPreview(id.stringValue);

                SerializedProperty started = animation.FindPropertyRelative("OnStarted");
                SerializedProperty completed = animation.FindPropertyRelative("OnCompleted");
                SerializedProperty cancelled = animation.FindPropertyRelative("OnCancelled");
                animation.isExpanded = EditorGUILayout.Foldout(animation.isExpanded, "Events", true);
                if (animation.isExpanded)
                {
                    EditorGUILayout.PropertyField(started);
                    EditorGUILayout.PropertyField(completed);
                    EditorGUILayout.PropertyField(cancelled);
                }
            }
        }

        private void DrawValidationMessages()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _animations.arraySize; i++)
            {
                string id = _animations.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue;
                if (string.IsNullOrWhiteSpace(id))
                {
                    EditorGUILayout.HelpBox("Every animation needs a non-empty ID.", MessageType.Warning);
                    return;
                }

                if (!ids.Add(id))
                {
                    EditorGUILayout.HelpBox($"Animation ID '{id}' is duplicated. Play() will use the first match.", MessageType.Warning);
                    return;
                }
            }
        }

        private static void DrawPlayback(SerializedProperty playback)
        {
            EditorGUILayout.PropertyField(playback.FindPropertyRelative("UnscaledTime"));
            EditorGUILayout.PropertyField(playback.FindPropertyRelative("BlendMode"));
            EditorGUILayout.PropertyField(playback.FindPropertyRelative("KillBehavior"));
            EditorGUILayout.PropertyField(playback.FindPropertyRelative("AllowSelfOverride"));
            EditorGUILayout.PropertyField(playback.FindPropertyRelative("LoopMode"));

            SerializedProperty loopMode = playback.FindPropertyRelative("LoopMode");
            if (loopMode.enumValueIndex != (int)TweenLoopMode.None)
                EditorGUILayout.PropertyField(playback.FindPropertyRelative("LoopCount"));
        }

        private static void DrawIdShortcuts(SerializedProperty id)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Quick IDs");
                string[] ids = { TweenIds.Show, TweenIds.Hide, TweenIds.Hover, TweenIds.Click, TweenIds.Unhover };
                foreach (string value in ids)
                {
                    if (GUILayout.Button(value, EditorStyles.miniButton))
                        id.stringValue = value;
                }
            }
        }

        private void DrawPreview(string animationId)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Edit-mode preview", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(targets.Length != 1 || Application.isPlaying || string.IsNullOrWhiteSpace(animationId)))
            {
                EditorGUI.BeginChangeCheck();
                _previewTime = EditorGUILayout.Slider("Timeline", _previewTime, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    _previewPlaying = false;
                    Player.Preview(animationId, _previewTime);
                    SceneView.RepaintAll();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(_previewPlaying ? "Pause" : "Play preview"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        if (_previewPlaying)
                            _previewPlaying = false;
                        else
                        {
                            _previewStartedFrom = _previewTime >= 0.999f ? 0f : _previewTime;
                            _previewTime = _previewStartedFrom;
                            _previewStartedAt = EditorApplication.timeSinceStartup;
                            _previewPlaying = true;
                        }
                    }

                    if (GUILayout.Button("Restore"))
                        StopPreview();
                }
            }
        }

        private void UpdatePreview()
        {
            if (!_previewPlaying || target == null || Application.isPlaying)
                return;

            float duration = Mathf.Max(0.01f, Player.GetDuration(CurrentAnimationId()));
            _previewTime = Mathf.Clamp01(_previewStartedFrom +
                                         (float)(EditorApplication.timeSinceStartup - _previewStartedAt) / duration);
            Player.Preview(CurrentAnimationId(), _previewTime);
            Repaint();
            SceneView.RepaintAll();

            if (_previewTime >= 1f)
                _previewPlaying = false;
        }

        private void StopPreview()
        {
            _previewPlaying = false;
            if (target != null)
                Player.StopPreview();
            SceneView.RepaintAll();
        }

        private string CurrentAnimationId()
        {
            if (_animations == null || _animations.arraySize == 0)
                return string.Empty;

            _selectedAnimation = Mathf.Clamp(_selectedAnimation, 0, _animations.arraySize - 1);
            return _animations.GetArrayElementAtIndex(_selectedAnimation).FindPropertyRelative("Id").stringValue;
        }

        private void AddAnimation()
        {
            Undo.RecordObjects(targets, "Add tween animation");
            foreach (TweenPlayer player in targets)
                player.AnimationDefinitions.Add(new TweenAnimation { Id = UniqueId(player, "Animation") });
            serializedObject.Update();
            _selectedAnimation = Mathf.Max(0, _animations.arraySize - 1);
        }

        private void DuplicateAnimation()
        {
            if (_animations.arraySize == 0)
                return;

            Undo.RecordObjects(targets, "Duplicate tween animation");
            foreach (TweenPlayer player in targets)
            {
                int index = Mathf.Clamp(_selectedAnimation, 0, player.AnimationDefinitions.Count - 1);
                TweenAnimation source = player.AnimationDefinitions[index];
                var copy = new TweenAnimation();
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), copy);
                copy.Clips = source.Clips.Select(TweenClipEditorUtility.CloneClip).ToList();
                copy.Id = UniqueId(player, source.Id + " Copy");
                player.AnimationDefinitions.Insert(index + 1, copy);
            }
            serializedObject.Update();
            _selectedAnimation++;
        }

        private void RemoveAnimation()
        {
            if (_animations.arraySize == 0)
                return;

            string name = AnimationLabel(_animations.GetArrayElementAtIndex(_selectedAnimation), _selectedAnimation);
            if (!EditorUtility.DisplayDialog("Remove animation?", $"Remove '{name}' and all of its inline clips?", "Remove", "Cancel"))
                return;

            StopPreview();
            Undo.RecordObjects(targets, "Remove tween animation");
            foreach (TweenPlayer player in targets)
            {
                if (_selectedAnimation < player.AnimationDefinitions.Count)
                    player.AnimationDefinitions.RemoveAt(_selectedAnimation);
            }
            serializedObject.Update();
            _selectedAnimation = Mathf.Clamp(_selectedAnimation, 0, Mathf.Max(0, _animations.arraySize - 1));
        }

        private static string UniqueId(TweenPlayer player, string basis)
        {
            string id = basis;
            int suffix = 2;
            while (player.FindAnimation(id) != null)
                id = $"{basis} {suffix++}";
            return id;
        }

        private static string AnimationLabel(SerializedProperty animation, int index)
        {
            string id = animation.FindPropertyRelative("Id").stringValue;
            return string.IsNullOrWhiteSpace(id) ? $"Animation {index + 1}" : id;
        }
    }

    [CustomEditor(typeof(TweenAnimationAsset))]
    public sealed class TweenAnimationAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Reusable clip stack. Timing is relative to the beginning of any animation that references this asset.", MessageType.Info);
            TweenClipEditorUtility.DrawClipList(serializedObject, serializedObject.FindProperty("Clips"), "Shared clips");
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(TweenUITrigger))]
    public sealed class TweenUITriggerEditor : UnityEditor.Editor
    {
        private static readonly string[] Fields =
        {
            "PointerEnter", "PointerExit", "PointerDown", "PointerUp",
            "Select", "Deselect", "Submit", "Cancel"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("player"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("interactabilitySource"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("logMissingAnimations"));

            var trigger = (TweenUITrigger)target;
            TweenPlayer player = trigger.Player != null ? trigger.Player : trigger.GetComponent<TweenPlayer>();
            string[] ids = player == null
                ? Array.Empty<string>()
                : player.Animations.Where(animation => animation != null && !string.IsNullOrWhiteSpace(animation.Id))
                    .Select(animation => animation.Id).Distinct().ToArray();

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox("Choose which named animation is played for each UI event. Empty means no action.", MessageType.Info);
            foreach (string field in Fields)
                DrawAnimationId(serializedObject.FindProperty(field), ObjectNames.NicifyVariableName(field), ids);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawAnimationId(SerializedProperty property, string label, string[] ids)
        {
            string[] options = new[] { "— None —" }.Concat(ids).ToArray();
            int current = string.IsNullOrWhiteSpace(property.stringValue)
                ? 0
                : Mathf.Max(0, Array.IndexOf(ids, property.stringValue) + 1);

            using (new EditorGUILayout.HorizontalScope())
            {
                int next = EditorGUILayout.Popup(label, current, options);
                if (next != current)
                    property.stringValue = next == 0 ? string.Empty : ids[next - 1];

                if (!string.IsNullOrWhiteSpace(property.stringValue) && !ids.Contains(property.stringValue))
                {
                    GUILayout.Label("Missing", EditorStyles.miniLabel, GUILayout.Width(45f));
                }
            }
        }
    }

    internal static class TweenClipEditorUtility
    {
        private static string _clipboardJson;
        private static Type _clipboardType;

        public static void DrawClipList(SerializedObject owner, SerializedProperty clips, string title)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{title} ({clips.arraySize})", EditorStyles.boldLabel);
                if (GUILayout.Button("+ Add clip", GUILayout.Width(86f)))
                    ShowAddMenu(owner, clips);
                using (new EditorGUI.DisabledScope(_clipboardType == null))
                {
                    if (GUILayout.Button("Paste", GUILayout.Width(48f)))
                        Paste(owner, clips, clips.arraySize);
                }
            }

            if (clips.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No clips yet. Add Move, Fade, Scale or another clip.", MessageType.None);
                return;
            }

            for (int i = 0; i < clips.arraySize; i++)
            {
                SerializedProperty clip = clips.GetArrayElementAtIndex(i);
                DrawClip(owner, clips, clip, i);
            }
        }

        private static void DrawClip(SerializedObject owner, SerializedProperty clips, SerializedProperty clip, int index)
        {
            if (clip.managedReferenceValue == null)
            {
                EditorGUILayout.HelpBox($"Clip {index + 1} has a missing type.", MessageType.Warning);
                if (GUILayout.Button("Remove missing clip"))
                    Delete(owner, clips, index);
                return;
            }

            BaseTweenClip value = (BaseTweenClip)clip.managedReferenceValue;
            string label = string.IsNullOrWhiteSpace(value.Label) ? Nicify(value.GetType().Name) : value.Label;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    SerializedProperty enabled = clip.FindPropertyRelative("Enabled");
                    enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(18f));
                    clip.isExpanded = EditorGUILayout.Foldout(clip.isExpanded, $"{index + 1}. {label}", true, EditorStyles.foldoutHeader);

                    using (new EditorGUI.DisabledScope(index == 0))
                        if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24f)))
                            Move(owner, clips, index, index - 1);
                    using (new EditorGUI.DisabledScope(index == clips.arraySize - 1))
                        if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24f)))
                            Move(owner, clips, index, index + 1);
                    if (GUILayout.Button("⋮", EditorStyles.miniButtonRight, GUILayout.Width(24f)))
                        ShowContext(owner, clips, index, value);
                }

                if (clip.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    SerializedProperty child = clip.Copy();
                    SerializedProperty end = clip.GetEndProperty();
                    bool enterChildren = true;
                    while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                    {
                        enterChildren = false;
                        if (child.name == "Enabled")
                            continue;
                        if (!ShouldDraw(clip, child.name))
                            continue;
                        EditorGUILayout.PropertyField(child, true);
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }

        private static void ShowAddMenu(SerializedObject owner, SerializedProperty clips)
        {
            var menu = new GenericMenu();
            IEnumerable<Type> types = TypeCache.GetTypesDerivedFrom<BaseTweenClip>()
                .Where(type => !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(MenuPath);

            foreach (Type type in types)
            {
                Type captured = type;
                menu.AddItem(new GUIContent(MenuPath(type)), false, () => Add(owner, clips.propertyPath, captured));
            }
            menu.ShowAsContext();
        }

        private static void ShowContext(SerializedObject owner, SerializedProperty clips, int index, BaseTweenClip value)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Duplicate"), false, () => Duplicate(owner, clips.propertyPath, index));
            menu.AddItem(new GUIContent("Copy"), false, () => Copy(value));
            if (_clipboardType != null)
                menu.AddItem(new GUIContent("Paste after"), false, () => Paste(owner, owner.FindProperty(clips.propertyPath), index + 1));
            else
                menu.AddDisabledItem(new GUIContent("Paste after"));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Remove"), false, () => Delete(owner, owner.FindProperty(clips.propertyPath), index));
            menu.ShowAsContext();
        }

        private static void Add(SerializedObject owner, string path, Type type)
        {
            owner.Update();
            SerializedProperty clips = owner.FindProperty(path);
            Undo.RecordObjects(owner.targetObjects, "Add tween clip");
            int index = clips.arraySize;
            clips.arraySize++;
            clips.GetArrayElementAtIndex(index).managedReferenceValue = Activator.CreateInstance(type);
            owner.ApplyModifiedProperties();
            MarkDirty(owner);
        }

        private static void Duplicate(SerializedObject owner, string path, int index)
        {
            owner.Update();
            SerializedProperty clips = owner.FindProperty(path);
            BaseTweenClip source = (BaseTweenClip)clips.GetArrayElementAtIndex(index).managedReferenceValue;
            Copy(source);
            Paste(owner, clips, index + 1);
        }

        private static void Copy(BaseTweenClip clip)
        {
            _clipboardType = clip.GetType();
            _clipboardJson = EditorJsonUtility.ToJson(clip);
        }

        internal static BaseTweenClip CloneClip(BaseTweenClip clip)
        {
            if (clip == null)
                return null;

            var copy = (BaseTweenClip)Activator.CreateInstance(clip.GetType());
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(clip), copy);
            return copy;
        }

        private static void Paste(SerializedObject owner, SerializedProperty clips, int index)
        {
            if (_clipboardType == null)
                return;

            Undo.RecordObjects(owner.targetObjects, "Paste tween clip");
            object copy = Activator.CreateInstance(_clipboardType);
            EditorJsonUtility.FromJsonOverwrite(_clipboardJson, copy);
            int appended = clips.arraySize;
            clips.arraySize++;
            clips.GetArrayElementAtIndex(appended).managedReferenceValue = copy;
            if (index < appended)
                clips.MoveArrayElement(appended, index);
            owner.ApplyModifiedProperties();
            MarkDirty(owner);
        }

        private static void Delete(SerializedObject owner, SerializedProperty clips, int index)
        {
            Undo.RecordObjects(owner.targetObjects, "Remove tween clip");
            clips.DeleteArrayElementAtIndex(index);
            owner.ApplyModifiedProperties();
            MarkDirty(owner);
        }

        private static void Move(SerializedObject owner, SerializedProperty clips, int from, int to)
        {
            Undo.RecordObjects(owner.targetObjects, "Reorder tween clip");
            clips.MoveArrayElement(from, to);
            owner.ApplyModifiedProperties();
            MarkDirty(owner);
        }

        private static void MarkDirty(SerializedObject owner)
        {
            foreach (UnityEngine.Object target in owner.targetObjects)
                EditorUtility.SetDirty(target);
        }

        private static string MenuPath(Type type)
        {
            return type.GetCustomAttributes(typeof(TweenClipMenuAttribute), false)
                       .OfType<TweenClipMenuAttribute>()
                       .FirstOrDefault()?.Path ?? Nicify(type.Name);
        }

        private static bool ShouldDraw(SerializedProperty clip, string fieldName)
        {
            if (fieldName == "CustomCurve")
                return clip.FindPropertyRelative("UseCustomCurve")?.boolValue == true;
            if (fieldName == "Ease")
                return clip.FindPropertyRelative("UseCustomCurve")?.boolValue != true;

            SerializedProperty fromMode = clip.FindPropertyRelative("FromMode");
            if (fieldName == "FromValue" && fromMode != null)
                return fromMode.enumValueIndex == (int)TweenEndpointMode.Custom;
            if (fieldName == "FromOffset" && fromMode != null)
                return fromMode.enumValueIndex == (int)TweenEndpointMode.OffsetFromInitial;

            SerializedProperty toMode = clip.FindPropertyRelative("ToMode");
            if (fieldName == "ToValue" && toMode != null)
                return toMode.enumValueIndex == (int)TweenEndpointMode.Custom;
            if (fieldName == "ToOffset" && toMode != null)
                return toMode.enumValueIndex == (int)TweenEndpointMode.OffsetFromInitial;

            return true;
        }

        private static string Nicify(string typeName)
        {
            return ObjectNames.NicifyVariableName(typeName.Replace("TweenClip", string.Empty));
        }
    }
}
