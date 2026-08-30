using System;
using System.Collections.Generic;
using System.Linq;
using UIMotionComposer.Tweening;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
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
        private bool _previewActive;
        private string _previewAnimationId;
        private int _previewFingerprint;
        private SerializedObject _previewAssetSource;
        private TweenPreviewAnimationMode _previewAnimationMode;
        private bool _previewBlockedWarned;

        private TweenPlayer Player => (TweenPlayer)target;

        private void OnEnable()
        {
            _animations = serializedObject.FindProperty("animations");
            _targetOverrides = serializedObject.FindProperty("targetOverrides");
            _playOnEnable = serializedObject.FindProperty("playOnEnable");
            EditorApplication.update += UpdatePreview;
            EditorApplication.focusChanged += OnEditorFocusChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePreview;
            EditorApplication.focusChanged -= OnEditorFocusChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
            Undo.undoRedoPerformed -= OnUndoRedo;
            StopPreview();
            _previewAnimationMode?.Dispose();
            _previewAnimationMode = null;
            DisposeAssetSource();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Add named animations, then compose each one from independent clips. Clips may overlap; Delay is measured from the animation start.",
                MessageType.Info);

            DrawInitialValuesSnapshot();
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
            }

            serializedObject.ApplyModifiedProperties();
            RefreshPreviewIfAuthoringChanged();
        }

        private void DrawInitialValuesSnapshot()
        {
            EditorGUILayout.Space(3f);
            string status = Player.HasCapturedInitialValues
                ? $"Initial pose saved ({Player.CapturedInitialValueCount} properties). Initial and Offset From Initial use this serialized snapshot."
                : "Initial pose is not saved yet. Until it is captured, Initial falls back to the value at first playback.";
            EditorGUILayout.HelpBox(status,
                Player.HasCapturedInitialValues ? MessageType.None : MessageType.Warning);

            using (new EditorGUI.DisabledScope(targets.Length != 1 || Application.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Player.HasCapturedInitialValues
                        ? "Recapture Initial Pose"
                        : "Capture Initial Pose"))
                {
                    StopPreview();
                    Undo.RecordObject(Player, "Capture UI Motion initial pose");
                    Player.CaptureInitialValues();
                    EditorUtility.SetDirty(Player);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(Player);
                    serializedObject.Update();
                }

                using (new EditorGUI.DisabledScope(!Player.HasCapturedInitialValues))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(70f)))
                    {
                        StopPreview();
                        Undo.RecordObject(Player, "Clear UI Motion initial pose");
                        Player.ClearCapturedInitialValues();
                        EditorUtility.SetDirty(Player);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(Player);
                        serializedObject.Update();
                    }
                }
            }
        }

        private void DrawAnimationSelector()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string[] names = Enumerable.Range(0, _animations.arraySize)
                    .Select(i => AnimationLabel(_animations.GetArrayElementAtIndex(i), i))
                    .ToArray();

                using (new EditorGUI.DisabledScope(names.Length == 0))
                {
                    EditorGUI.BeginChangeCheck();
                    _selectedAnimation = EditorGUILayout.Popup(_selectedAnimation, names, EditorStyles.toolbarPopup);
                    if (EditorGUI.EndChangeCheck())
                        StopPreview();
                }

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
                    var sharedAsset = (TweenAnimationAsset)asset.objectReferenceValue;
                    SerializedObject assetSource = GetAssetSource(sharedAsset);
                    SerializedProperty sharedClips = assetSource?.FindProperty("Clips");
                    if (sharedClips != null)
                    {
                        TweenTimelineEditor.Draw(assetSource, sharedClips, _previewTime,
                            normalized => ScrubTimeline(id.stringValue, normalized));
                    }
                    EditorGUILayout.HelpBox("Timing comes from the shared asset. Dragging its timeline edits that asset for every player using it.", MessageType.None);
                    if (GUILayout.Button("Select shared animation asset"))
                        Selection.activeObject = asset.objectReferenceValue;
                }
                else
                {
                    TweenTimelineEditor.Draw(serializedObject, clips, _previewTime,
                        normalized => ScrubTimeline(id.stringValue, normalized));
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
                    SamplePreview(animationId, _previewTime);
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
                            SamplePreview(animationId, _previewTime);
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
            SamplePreview(CurrentAnimationId(), _previewTime);
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
            _previewAnimationMode?.Stop();
            _previewActive = false;
            _previewAnimationId = null;
            _previewFingerprint = 0;
            SceneView.RepaintAll();
        }

        private void SamplePreview(string animationId, float normalizedTime)
        {
            if (!_previewActive || !string.Equals(_previewAnimationId, animationId, StringComparison.Ordinal))
            {
                StopPreview();
                UnityEngine.Object[] affectedTargets = Player.PreparePreview(animationId);
                if (affectedTargets.Length == 0)
                    return;

                _previewAnimationMode ??= new TweenPreviewAnimationMode();
                if (!_previewAnimationMode.TryStart())
                {
                    Player.StopPreview();

                    // Once per blocked stretch: every scrub and every Play press comes back here
                    // while the Animation window stays open.
                    if (!_previewBlockedWarned)
                    {
                        _previewBlockedWarned = true;
                        Debug.LogWarning("[UI Motion Composer] Preview cannot start while another Animation Mode driver is active.", Player);
                    }
                    return;
                }

                _previewBlockedWarned = false;

                _previewAnimationMode.RegisterTargets(affectedTargets);
                _previewActive = true;
                _previewAnimationId = animationId;
                _previewFingerprint = AuthoringFingerprint(animationId);
            }

            Player.SamplePreparedPreview(normalizedTime);
        }

        private void ScrubTimeline(string animationId, float normalizedTime)
        {
            serializedObject.ApplyModifiedProperties();
            _previewTime = Mathf.Clamp01(normalizedTime);
            _previewPlaying = false;
            SamplePreview(animationId, _previewTime);
            Repaint();
            SceneView.RepaintAll();
        }

        private void RefreshPreviewIfAuthoringChanged()
        {
            if (!_previewActive || target == null)
                return;

            if (AuthoringFingerprint(_previewAnimationId) == _previewFingerprint)
                return;

            RebuildPreview(_previewTime);
        }

        /// <summary>
        /// Restores the pose, recaptures against the edited clips and resamples at the same point.
        /// Deliberately not routed through <see cref="StopPreview"/>: the session continues, so the
        /// Undo entry taken when it began still describes the pose to return to.
        /// </summary>
        private void RebuildPreview(float normalizedTime)
        {
            string animationId = _previewAnimationId;

            // PreparePreview restores the captured pose before recapturing, so From/To resolved as
            // Current read the authored pose rather than wherever the last sample left the object.
            UnityEngine.Object[] affectedTargets = Player.PreparePreview(animationId);
            if (affectedTargets.Length == 0)
            {
                StopPreview();
                return;
            }

            // Retargeting a clip mid-session can pull in an object the session never registered.
            _previewAnimationMode?.RegisterTargets(affectedTargets);
            _previewFingerprint = AuthoringFingerprint(animationId);
            Player.SamplePreparedPreview(normalizedTime);
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Hash of everything one preview sample reads: the selected animation's clips, its playback
        /// settings, and the shared asset's clips when one is assigned.
        ///
        /// Walks the SerializedProperty tree without allocating a JSON representation every
        /// inspector repaint, and explicitly includes managed-reference types and shared assets.
        /// </summary>
        private int AuthoringFingerprint(string animationId)
        {
            int hash = 17;

            int index = IndexOfAnimation(animationId);
            if (index >= 0)
                hash = TweenAuthoringFingerprint.Combine(hash,
                    TweenAuthoringFingerprint.Of(_animations.GetArrayElementAtIndex(index)));

            TweenAnimation animation = Player.FindAnimation(animationId);
            SerializedObject assetSource = GetAssetSource(animation?.Asset);
            if (assetSource != null)
                hash = TweenAuthoringFingerprint.Combine(hash,
                    TweenAuthoringFingerprint.Of(assetSource.FindProperty("Clips")));

            return hash;
        }

        private int IndexOfAnimation(string animationId)
        {
            if (_animations == null)
                return -1;

            for (int i = 0; i < _animations.arraySize; i++)
            {
                string id = _animations.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue;
                if (string.Equals(id, animationId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private SerializedObject GetAssetSource(TweenAnimationAsset asset)
        {
            if (asset == null)
            {
                DisposeAssetSource();
                return null;
            }

            if (_previewAssetSource == null || _previewAssetSource.targetObject != asset)
            {
                DisposeAssetSource();
                _previewAssetSource = new SerializedObject(asset);
            }

            _previewAssetSource.Update();
            return _previewAssetSource;
        }

        private void DisposeAssetSource()
        {
            _previewAssetSource?.Dispose();
            _previewAssetSource = null;
        }

        private void OnEditorFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
                StopPreview();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            StopPreview();
        }

        private void OnUndoRedo()
        {
            StopPreview();
            if (target != null)
                Player.InvalidateAuthoringCache();
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
            SerializedProperty clips = serializedObject.FindProperty("Clips");
            TweenTimelineEditor.Draw(serializedObject, clips);
            TweenClipEditorUtility.DrawClipList(serializedObject, clips, "Shared clips");
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
                    DrawEasePreview(clip);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private static void ShowAddMenu(SerializedObject owner, SerializedProperty clips)
        {
            Type[] types = TypeCache.GetTypesDerivedFrom<BaseTweenClip>()
                .Where(type => !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(MenuPath)
                .ToArray();
            var dropdown = new TweenClipDropdown(new AdvancedDropdownState(), owner, clips.propertyPath, types);
            dropdown.Show(GUILayoutUtility.GetLastRect());
        }

        private static void DrawEasePreview(SerializedProperty clip)
        {
            SerializedProperty useCustom = clip.FindPropertyRelative("UseCustomCurve");
            SerializedProperty custom = clip.FindPropertyRelative("CustomCurve");
            SerializedProperty ease = clip.FindPropertyRelative("Ease");
            if (useCustom == null || custom == null || ease == null)
                return;

            Rect rect = EditorGUILayout.GetControlRect(false, 54f);
            rect = EditorGUI.IndentedRect(rect);
            EditorGUI.DrawRect(rect, new Color(0.10f, 0.11f, 0.13f, 0.75f));

            const int samples = 48;
            var values = new float[samples + 1];
            float min = 0f;
            float max = 1f;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                values[i] = useCustom.boolValue
                    ? custom.animationCurveValue.Evaluate(t)
                    : UIEaseEvaluator.Evaluate((UIEase)ease.enumValueIndex, t);
                min = Mathf.Min(min, values[i]);
                max = Mathf.Max(max, values[i]);
            }

            float range = Mathf.Max(0.001f, max - min);
            Handles.BeginGUI();
            Color oldColor = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.13f);
            float zeroY = Mathf.Lerp(rect.yMax - 3f, rect.y + 3f, (0f - min) / range);
            float oneY = Mathf.Lerp(rect.yMax - 3f, rect.y + 3f, (1f - min) / range);
            Handles.DrawLine(new Vector3(rect.x + 3f, zeroY), new Vector3(rect.xMax - 3f, zeroY));
            Handles.DrawLine(new Vector3(rect.x + 3f, oneY), new Vector3(rect.xMax - 3f, oneY));

            var points = new Vector3[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                float x = Mathf.Lerp(rect.x + 3f, rect.xMax - 3f, i / (float)samples);
                float y = Mathf.Lerp(rect.yMax - 3f, rect.y + 3f, (values[i] - min) / range);
                points[i] = new Vector3(x, y);
            }

            Handles.color = new Color(0.36f, 0.62f, 1f, 1f);
            Handles.DrawAAPolyLine(2f, points);
            Handles.color = oldColor;
            Handles.EndGUI();
        }

        private sealed class TweenClipDropdown : AdvancedDropdown
        {
            private readonly SerializedObject _owner;
            private readonly string _path;
            private readonly Type[] _types;

            public TweenClipDropdown(AdvancedDropdownState state, SerializedObject owner,
                string path, Type[] types) : base(state)
            {
                _owner = owner;
                _path = path;
                _types = types;
                minimumSize = new Vector2(310f, 360f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Add Tween Clip");
                foreach (Type type in _types)
                    root.AddChild(new TweenClipDropdownItem(MenuPath(type), type));
                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is TweenClipDropdownItem clipItem)
                    Add(_owner, _path, clipItem.Type);
            }
        }

        private sealed class TweenClipDropdownItem : AdvancedDropdownItem
        {
            public Type Type { get; }

            public TweenClipDropdownItem(string name, Type type) : base(name)
            {
                Type = type;
            }
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

    /// <summary>
    /// Content hash of a SerializedProperty subtree.
    ///
    /// A property walk avoids allocating an intermediate JSON string on every inspector repaint
    /// and lets the preview explicitly include managed-reference types and shared assets.
    /// </summary>
    internal static class TweenAuthoringFingerprint
    {
        /// <summary>
        /// Folds every leaf under <paramref name="root"/> into one hash. Uses Next rather than
        /// NextVisible so a collapsed foldout does not hide its fields from the hash.
        /// </summary>
        public static int Of(SerializedProperty root)
        {
            if (root == null)
                return 0;

            int hash = 17;
            SerializedProperty iterator = root.Copy();
            SerializedProperty end = root.GetEndProperty();

            while (iterator.Next(true) && !SerializedProperty.EqualContents(iterator, end))
            {
                hash = Combine(hash, iterator.propertyPath.GetHashCode());
                hash = Combine(hash, HashValue(iterator));
            }

            return hash;
        }

        public static int Combine(int hash, int value)
        {
            unchecked
            {
                return hash * 31 + value;
            }
        }

        private static int HashValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.ArraySize:
                    return property.intValue;
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? 1 : 0;
                case SerializedPropertyType.Float:
                    return property.floatValue.GetHashCode();
                case SerializedPropertyType.String:
                    return property.stringValue?.GetHashCode() ?? 0;
                case SerializedPropertyType.Color:
                    return property.colorValue.GetHashCode();
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceInstanceIDValue;
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex;
                case SerializedPropertyType.Vector2:
                    return property.vector2Value.GetHashCode();
                case SerializedPropertyType.Vector3:
                    return property.vector3Value.GetHashCode();
                case SerializedPropertyType.Vector4:
                    return property.vector4Value.GetHashCode();
                case SerializedPropertyType.Quaternion:
                    return property.quaternionValue.GetHashCode();
                case SerializedPropertyType.Vector2Int:
                    return property.vector2IntValue.GetHashCode();
                case SerializedPropertyType.Vector3Int:
                    return property.vector3IntValue.GetHashCode();
                case SerializedPropertyType.Rect:
                    return property.rectValue.GetHashCode();
                case SerializedPropertyType.Bounds:
                    return property.boundsValue.GetHashCode();
                case SerializedPropertyType.AnimationCurve:
                    return HashCurve(property.animationCurveValue);

                // Swapping a clip for another type keeps the same property path, so the type name
                // is the only thing that tells them apart.
                case SerializedPropertyType.ManagedReference:
                    return property.managedReferenceFullTypename?.GetHashCode() ?? 0;

                // Generic containers carry nothing of their own; their leaves are visited next.
                default:
                    return 0;
            }
        }

        private static int HashCurve(AnimationCurve curve)
        {
            if (curve == null)
                return 0;

            int hash = 17;
            hash = Combine(hash, (int)curve.preWrapMode);
            hash = Combine(hash, (int)curve.postWrapMode);
            Keyframe[] keys = curve.keys;
            hash = Combine(hash, keys.Length);

            for (int i = 0; i < keys.Length; i++)
            {
                hash = Combine(hash, keys[i].time.GetHashCode());
                hash = Combine(hash, keys[i].value.GetHashCode());
                hash = Combine(hash, keys[i].inTangent.GetHashCode());
                hash = Combine(hash, keys[i].outTangent.GetHashCode());
                hash = Combine(hash, keys[i].inWeight.GetHashCode());
                hash = Combine(hash, keys[i].outWeight.GetHashCode());
                hash = Combine(hash, (int)keys[i].weightedMode);
            }

            return hash;
        }
    }

    /// <summary>
    /// Owns an isolated Unity Animation Mode driver for inspector preview. Registered animatable
    /// properties are restored by Unity without adding no-op entries to the user's Undo history;
    /// non-animatable values are still restored by TweenPlayback's exact captured state.
    /// </summary>
    internal sealed class TweenPreviewAnimationMode : IDisposable
    {
        private AnimationModeDriver _driver;
        private readonly HashSet<UnityEngine.Object> _registeredTargets = new HashSet<UnityEngine.Object>();

        public bool IsActive => _driver != null && UnityEditor.AnimationMode.InAnimationMode(_driver);

        public bool TryStart()
        {
            if (IsActive)
                return true;

            // Never steal or stop the Animation window, Timeline, or another custom previewer.
            if (UnityEditor.AnimationMode.InAnimationMode())
                return false;

            DestroyDriver();
            _registeredTargets.Clear();
            _driver = ScriptableObject.CreateInstance<AnimationModeDriver>();
            _driver.name = "UI Motion Composer Preview";
            _driver.hideFlags = HideFlags.HideAndDontSave;
            UnityEditor.AnimationMode.StartAnimationMode(_driver);
            return IsActive;
        }

        public void RegisterTargets(IEnumerable<UnityEngine.Object> targets)
        {
            if (!IsActive || targets == null)
                return;

            foreach (UnityEngine.Object target in targets)
            {
                if (target == null || !_registeredTargets.Add(target))
                    continue;

                GameObject gameObject = target switch
                {
                    GameObject direct => direct,
                    Component component => component.gameObject,
                    _ => null
                };
                if (gameObject == null)
                    continue;

                EditorCurveBinding[] bindings = AnimationUtility.GetAnimatableBindings(gameObject, gameObject);
                for (int i = 0; i < bindings.Length; i++)
                {
                    UnityEngine.Object animatedObject = AnimationUtility.GetAnimatedObject(gameObject, bindings[i]);
                    if (animatedObject == target || target is GameObject && animatedObject == gameObject)
                        UnityEditor.AnimationMode.AddEditorCurveBinding(gameObject, bindings[i]);
                }
            }
        }

        public void Stop()
        {
            if (IsActive)
                UnityEditor.AnimationMode.StopAnimationMode(_driver);
            _registeredTargets.Clear();
        }

        public void Dispose()
        {
            Stop();
            DestroyDriver();
        }

        private void DestroyDriver()
        {
            if (_driver == null)
                return;

            UnityEngine.Object.DestroyImmediate(_driver);
            _driver = null;
        }
    }
}
