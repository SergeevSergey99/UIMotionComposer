using System;
using System.Collections.Generic;
using System.Linq;
using UIMotionComposer.Tweening;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UI;

namespace UIMotionComposer.Editor
{
    [CustomEditor(typeof(TweenPlayer))]
    [CanEditMultipleObjects]
    public sealed class TweenPlayerEditor : UnityEditor.Editor
    {
        private SerializedProperty _animations;
        private SerializedProperty _targetOverrides;
        private SerializedProperty _playOnEnable;
        private SerializedProperty _initialPose;
        private int _selectedAnimation;
        private bool _showAdvanced;
        private float _previewTime;
        private bool _previewPlaying;
        private bool _previewLoop;
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
            _initialPose = serializedObject.FindProperty("initialPose");
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

            EditorGUILayout.PropertyField(_initialPose, new GUIContent("Initial Pose"), true);
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
                EditorGUILayout.HelpBox(
                    "Advanced view of every local target binding, including unused entries. Shared assets declare slots; this player supplies the scene or prefab objects.",
                    MessageType.None);
                EditorGUILayout.PropertyField(_targetOverrides, new GUIContent("All target bindings"), true);
                EditorGUILayout.PropertyField(_playOnEnable, new GUIContent("Play on enable"), true);
            }

            serializedObject.ApplyModifiedProperties();
            RefreshPreviewIfAuthoringChanged();
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
                        DrawTargetBindings(animation, sharedClips);
                    }
                    EditorGUILayout.HelpBox(
                        "Timing and target slot names come from the shared asset. Their concrete objects are stored locally on this TweenPlayer.",
                        MessageType.None);
                    if (GUILayout.Button("Select shared animation asset"))
                        Selection.activeObject = asset.objectReferenceValue;
                }
                else
                {
                    TweenTimelineEditor.Draw(serializedObject, clips, _previewTime,
                        normalized => ScrubTimeline(id.stringValue, normalized));
                    TweenClipEditorUtility.DrawClipList(serializedObject, clips, "Clips");
                    DrawTargetBindings(animation, clips);
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

        private sealed class TargetRequirement
        {
            public string Label;
            public Type[] Types;
        }

        private sealed class TargetSlotInfo
        {
            public string Key;
            public readonly List<string> Consumers = new List<string>();
            public readonly List<TargetRequirement> Requirements = new List<TargetRequirement>();

            public string Expected => string.Join(" + ", Requirements.Select(item => item.Label).Distinct());
        }

        private void DrawTargetBindings(SerializedProperty animation, SerializedProperty clips)
        {
            List<TargetSlotInfo> slots = CollectTargetSlots(clips);
            if (slots.Count == 0)
                return;

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Target bindings ({slots.Count})", EditorStyles.boldLabel);
                if (GUILayout.Button("Auto Bind All", GUILayout.Width(96f)))
                    AutoBindAll(animation, slots);
            }
            EditorGUILayout.HelpBox(
                "Slots may use a direct object, this player, a child path/name, or a component search. Local overrides affect only this animation.",
                MessageType.Info);

            if (targets.Length != 1)
            {
                EditorGUILayout.HelpBox("Edit target bindings on one TweenPlayer at a time.", MessageType.None);
                return;
            }

            SerializedProperty localBindings = animation.FindPropertyRelative("TargetOverrides");
            TweenAnimation runtimeAnimation = CurrentAnimation();
            bool hasProblem = false;
            foreach (TargetSlotInfo slot in slots)
            {
                int localIndex = FindTargetBinding(localBindings, slot.Key);
                int globalIndex = FindTargetBinding(_targetOverrides, slot.Key);
                bool useLocal = localIndex >= 0;
                SerializedProperty bindings = useLocal ? localBindings : _targetOverrides;
                int index = useLocal ? localIndex : globalIndex;
                SerializedProperty entry = index >= 0 ? bindings.GetArrayElementAtIndex(index) : null;
                UnityEngine.Object resolved = Player.ResolveTargetBinding(slot.Key, runtimeAnimation);
                bool valid = resolved != null && TargetSatisfies(resolved, slot);
                hasProblem |= !valid;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Color previous = GUI.color;
                        GUI.color = resolved == null
                            ? new Color(1f, 0.55f, 0.35f)
                            : valid ? new Color(0.45f, 0.9f, 0.55f) : new Color(1f, 0.75f, 0.25f);
                        GUILayout.Label(resolved == null ? "● Missing" : valid ? "● Resolved" : "● Wrong type",
                            EditorStyles.miniBoldLabel, GUILayout.Width(82f));
                        GUI.color = previous;
                        GUILayout.Label(slot.Key, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        bool nextLocal = GUILayout.Toggle(useLocal, new GUIContent("Local", "Override this slot only for the selected animation."),
                            EditorStyles.miniButton, GUILayout.Width(48f));
                        if (nextLocal != useLocal)
                        {
                            if (nextLocal)
                                CopyOrCreateLocalBinding(localBindings, slot.Key, entry);
                            else
                                localBindings.DeleteArrayElementAtIndex(localIndex);
                            serializedObject.ApplyModifiedProperties();
                            Player.InvalidateAuthoringCache();
                            GUIUtility.ExitGUI();
                        }
                    }

                    EditorGUILayout.LabelField($"Expected: {slot.Expected}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Used by: {string.Join(", ", slot.Consumers)}", EditorStyles.miniLabel);

                    if (entry == null)
                    {
                        EditorGUILayout.HelpBox("No binding rule yet. Create one or let Auto Bind search the hierarchy.", MessageType.None);
                        if (GUILayout.Button("Create binding"))
                            EnsureBinding(bindings, slot.Key);
                    }
                    else
                    {
                        DrawBindingRule(entry, slot);
                        DrawBindingActions(entry, slot, runtimeAnimation, resolved);
                    }
                }
            }

            if (hasProblem)
                EditorGUILayout.HelpBox("Missing or incompatible slots are skipped during playback. Use Auto Bind All or inspect the highlighted rows.", MessageType.Warning);
        }

        private void DrawBindingRule(SerializedProperty entry, TargetSlotInfo slot)
        {
            SerializedProperty mode = entry.FindPropertyRelative("Mode");
            EditorGUILayout.PropertyField(mode, new GUIContent("Mode"));
            var bindingMode = (TweenTargetBindingMode)mode.enumValueIndex;

            switch (bindingMode)
            {
                case TweenTargetBindingMode.Direct:
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("Target"), new GUIContent("Object"));
                    break;
                case TweenTargetBindingMode.Self:
                    EditorGUILayout.ObjectField("Resolved from", Player.gameObject, typeof(GameObject), true);
                    break;
                case TweenTargetBindingMode.ChildPath:
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("Query"),
                        new GUIContent("Child path", $"Relative path. Empty uses '{slot.Key}'."));
                    break;
                case TweenTargetBindingMode.ChildName:
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("Query"),
                        new GUIContent("Child name", $"Descendant name. Empty uses '{slot.Key}'."));
                    break;
                case TweenTargetBindingMode.Component:
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("Query"),
                        new GUIContent("Under child", "Optional path or name. Empty searches the player and its descendants. A missing child leaves the slot unresolved."));
                    DrawComponentType(entry.FindPropertyRelative("ComponentType"), slot);
                    break;
            }
        }

        private static void DrawComponentType(SerializedProperty property, TargetSlotInfo slot)
        {
            List<Type> options = slot.Requirements
                .SelectMany(item => item.Types ?? Array.Empty<Type>())
                .Where(type => type != null && typeof(Component).IsAssignableFrom(type))
                .Distinct().ToList();
            if (options.Count == 0)
                options.Add(typeof(Transform));

            int selected = options.FindIndex(type => type.AssemblyQualifiedName == property.stringValue);
            string[] names = options.Select(type => type.Name).ToArray();
            int next = EditorGUILayout.Popup("Component type", Mathf.Max(0, selected), names);
            if (selected < 0 || next != selected)
                property.stringValue = options[next].AssemblyQualifiedName;
        }

        private void DrawBindingActions(SerializedProperty entry, TargetSlotInfo slot,
            TweenAnimation animation, UnityEngine.Object resolved)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Find"))
                {
                    string propertyPath = entry.propertyPath;
                    serializedObject.ApplyModifiedProperties();
                    Player.InvalidateTargetBindings();
                    resolved = Player.ResolveTargetBinding(slot.Key, animation);
                    if (resolved == null)
                    {
                        UnityEngine.Object found = FindBestTarget(slot);
                        if (found != null)
                        {
                            serializedObject.Update();
                            entry = serializedObject.FindProperty(propertyPath);
                            entry.FindPropertyRelative("Mode").enumValueIndex = (int)TweenTargetBindingMode.Direct;
                            entry.FindPropertyRelative("Target").objectReferenceValue = found;
                        }
                    }
                }

                using (new EditorGUI.DisabledScope(resolved == null))
                {
                    if (GUILayout.Button("Ping"))
                        EditorGUIUtility.PingObject(resolved);
                }

                if (GUILayout.Button("Clear"))
                    ResetBinding(entry);
            }
        }

        private void AutoBindAll(SerializedProperty animation, IReadOnlyList<TargetSlotInfo> slots)
        {
            SerializedProperty localBindings = animation.FindPropertyRelative("TargetOverrides");
            Undo.RecordObject(Player, "Auto bind UI Motion targets");
            for (int i = 0; i < slots.Count; i++)
            {
                TargetSlotInfo slot = slots[i];
                int localIndex = FindTargetBinding(localBindings, slot.Key);
                SerializedProperty bindings = localIndex >= 0 ? localBindings : _targetOverrides;
                int index = localIndex >= 0 ? localIndex : FindTargetBinding(bindings, slot.Key);
                SerializedProperty entry = index >= 0
                    ? bindings.GetArrayElementAtIndex(index)
                    : EnsureBinding(bindings, slot.Key);
                UnityEngine.Object current = Player.ResolveTargetBinding(slot.Key, CurrentAnimation());
                if (current != null && TargetSatisfies(current, slot))
                    continue;

                UnityEngine.Object found = FindBestTarget(slot);
                if (found == null)
                    continue;

                entry.FindPropertyRelative("Mode").enumValueIndex = (int)TweenTargetBindingMode.Direct;
                entry.FindPropertyRelative("Target").objectReferenceValue = found;
                entry.FindPropertyRelative("Query").stringValue = string.Empty;
                entry.FindPropertyRelative("ComponentType").stringValue = string.Empty;
            }

            serializedObject.ApplyModifiedProperties();
            Player.InvalidateAuthoringCache();
            EditorUtility.SetDirty(Player);
            serializedObject.Update();
        }

        private UnityEngine.Object FindBestTarget(TargetSlotInfo slot)
        {
            Transform byPath = Player.transform.Find(slot.Key);
            if (byPath != null && TargetSatisfies(byPath.gameObject, slot))
                return byPath.gameObject;

            Transform[] descendants = Player.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (string.Equals(descendants[i].name, slot.Key, StringComparison.OrdinalIgnoreCase) &&
                    TargetSatisfies(descendants[i].gameObject, slot))
                    return descendants[i].gameObject;
            }

            // A type-only guess is safe only when it is unambiguous. In particular, never bind a
            // Transform slot to the player root merely because every GameObject has a Transform.
            UnityEngine.Object unique = null;
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] == Player.transform || !TargetSatisfies(descendants[i].gameObject, slot))
                    continue;
                if (unique != null)
                    return null;
                unique = descendants[i].gameObject;
            }

            return unique;
        }

        private static bool TargetSatisfies(UnityEngine.Object target, TargetSlotInfo slot)
        {
            if (target == null)
                return false;

            for (int i = 0; i < slot.Requirements.Count; i++)
            {
                TargetRequirement requirement = slot.Requirements[i];
                if (requirement.Types == null || requirement.Types.Length == 0)
                    continue;
                if (!requirement.Types.Any(type => ResolvesAs(target, type)))
                    return false;
            }

            return true;
        }

        private static bool ResolvesAs(UnityEngine.Object target, Type type)
        {
            if (target == null || type == null)
                return false;
            if (type.IsInstanceOfType(target))
                return true;
            if (type == typeof(GameObject))
                return target is Component;

            GameObject gameObject = target switch
            {
                GameObject direct => direct,
                Component component => component.gameObject,
                _ => null
            };
            return gameObject != null && typeof(Component).IsAssignableFrom(type) && gameObject.GetComponent(type) != null;
        }

        private static int FindTargetBinding(SerializedProperty bindings, string key)
        {
            if (bindings == null)
                return -1;
            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty entry = bindings.GetArrayElementAtIndex(i);
                if (string.Equals(entry.FindPropertyRelative("Key").stringValue, key, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static SerializedProperty EnsureBinding(SerializedProperty bindings, string key)
        {
            int index = FindTargetBinding(bindings, key);
            if (index >= 0)
                return bindings.GetArrayElementAtIndex(index);

            index = bindings.arraySize;
            bindings.InsertArrayElementAtIndex(index);
            SerializedProperty entry = bindings.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("Key").stringValue = key;
            ResetBinding(entry);
            return entry;
        }

        private static void CopyOrCreateLocalBinding(SerializedProperty localBindings, string key,
            SerializedProperty source)
        {
            SerializedProperty entry = EnsureBinding(localBindings, key);
            if (source == null)
                return;
            entry.FindPropertyRelative("Mode").enumValueIndex = source.FindPropertyRelative("Mode").enumValueIndex;
            entry.FindPropertyRelative("Target").objectReferenceValue = source.FindPropertyRelative("Target").objectReferenceValue;
            entry.FindPropertyRelative("Query").stringValue = source.FindPropertyRelative("Query").stringValue;
            entry.FindPropertyRelative("ComponentType").stringValue = source.FindPropertyRelative("ComponentType").stringValue;
        }

        private static void ResetBinding(SerializedProperty entry)
        {
            entry.FindPropertyRelative("Mode").enumValueIndex = (int)TweenTargetBindingMode.Direct;
            entry.FindPropertyRelative("Target").objectReferenceValue = null;
            entry.FindPropertyRelative("Query").stringValue = string.Empty;
            entry.FindPropertyRelative("ComponentType").stringValue = string.Empty;
        }

        private TweenAnimation CurrentAnimation()
        {
            return _selectedAnimation >= 0 && _selectedAnimation < Player.AnimationDefinitions.Count
                ? Player.AnimationDefinitions[_selectedAnimation]
                : null;
        }

        private static List<TargetSlotInfo> CollectTargetSlots(SerializedProperty clips)
        {
            var result = new List<TargetSlotInfo>();
            var byKey = new Dictionary<string, TargetSlotInfo>(StringComparer.Ordinal);
            if (clips == null || !clips.isArray)
                return result;

            for (int i = 0; i < clips.arraySize; i++)
            {
                if (clips.GetArrayElementAtIndex(i).managedReferenceValue is not TargetedTweenClip clip)
                    continue;
                string key = clip.TargetKey?.Trim();
                if (string.IsNullOrEmpty(key))
                    continue;

                if (!byKey.TryGetValue(key, out TargetSlotInfo slot))
                {
                    slot = new TargetSlotInfo { Key = key };
                    byKey.Add(key, slot);
                    result.Add(slot);
                }

                string consumer = string.IsNullOrWhiteSpace(clip.Label)
                    ? ObjectNames.NicifyVariableName(clip.GetType().Name.Replace("TweenClip", string.Empty))
                    : clip.Label;
                slot.Consumers.Add(consumer);
                TargetRequirement requirement = RequirementFor(clip);
                if (!slot.Requirements.Any(item => item.Label == requirement.Label))
                    slot.Requirements.Add(requirement);
            }

            return result;
        }

        private static TargetRequirement RequirementFor(TargetedTweenClip clip)
        {
            switch (clip)
            {
                case AnchorPositionTweenClip:
                case AnchorPosition3DTweenClip:
                case SizeDeltaTweenClip:
                case PivotTweenClip:
                case PunchAnchorPositionTweenClip:
                case JumpAnchorPositionTweenClip:
                    return Requirement("RectTransform", typeof(RectTransform));
                case ShakeTweenClip shake when shake.UseAnchoredPosition:
                    return Requirement("RectTransform", typeof(RectTransform));
                case MoveTweenClip:
                case ScaleTweenClip:
                case RotateTweenClip:
                case PunchScaleTweenClip:
                case ShakeTweenClip:
                    return Requirement("Transform", typeof(Transform));
                case FillAmountTweenClip:
                    return Requirement("Image", typeof(Image));
                case PlayTweenAnimationClip:
                    return Requirement("TweenPlayer", typeof(TweenPlayer));
                case ToggleObjectTweenClip:
                    return Requirement("GameObject", typeof(GameObject));
                case FadeTweenClip fade:
                    return fade.FadeTarget switch
                    {
                        TweenFadeTarget.CanvasGroup => Requirement("CanvasGroup", typeof(CanvasGroup)),
                        TweenFadeTarget.Graphic => Requirement("Graphic", typeof(Graphic)),
                        TweenFadeTarget.SpriteRenderer => Requirement("SpriteRenderer", typeof(SpriteRenderer)),
                        _ => Requirement("CanvasGroup / Graphic / SpriteRenderer", typeof(CanvasGroup), typeof(Graphic), typeof(SpriteRenderer))
                    };
                case ColorTweenClip color:
                    return color.ColorTarget switch
                    {
                        TweenColorTarget.Graphic => Requirement("Graphic", typeof(Graphic)),
                        TweenColorTarget.SpriteRenderer => Requirement("SpriteRenderer", typeof(SpriteRenderer)),
                        TweenColorTarget.Renderer => Requirement("Renderer", typeof(Renderer)),
                        _ => Requirement("Graphic / SpriteRenderer / Renderer", typeof(Graphic), typeof(SpriteRenderer), typeof(Renderer))
                    };
                case TextRevealTweenClip:
                case TextCounterTweenClip:
                    return Requirement("Text / TMP_Text", TextTypes());
                default:
                    return Requirement("Object", typeof(UnityEngine.Object));
            }
        }

        private static TargetRequirement Requirement(string label, params Type[] types)
        {
            return new TargetRequirement { Label = label, Types = types };
        }

        private static Type[] TextTypes()
        {
            Type tmp = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", false);
            return tmp != null ? new[] { typeof(Text), tmp } : new[] { typeof(Text) };
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
            EditorGUILayout.PropertyField(playback.FindPropertyRelative("AllowSelfOverride"));
            EditorGUILayout.PropertyField(playback.FindPropertyRelative("LoopMode"));

            SerializedProperty loopMode = playback.FindPropertyRelative("LoopMode");
            if (loopMode.enumValueIndex != (int)TweenLoopMode.None)
                EditorGUILayout.PropertyField(playback.FindPropertyRelative("LoopCount"));
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
                    if (GUILayout.Button("|<", GUILayout.Width(34f)))
                    {
                        serializedObject.ApplyModifiedProperties();
                        _previewPlaying = false;
                        _previewTime = 0f;
                        SamplePreview(animationId, 0f);
                        SceneView.RepaintAll();
                    }

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

                    _previewLoop = GUILayout.Toggle(_previewLoop, "Loop", EditorStyles.miniButton,
                        GUILayout.Width(52f));

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
            float rawTime = _previewStartedFrom +
                            (float)(EditorApplication.timeSinceStartup - _previewStartedAt) / duration;
            bool infinite = Player.IsInfinite(CurrentAnimationId());
            _previewTime = _previewLoop || infinite ? Mathf.Repeat(rawTime, 1f) : Mathf.Clamp01(rawTime);
            if (infinite)
                SamplePreviewTime(CurrentAnimationId(), Mathf.Max(0f, rawTime) * duration);
            else
                SamplePreview(CurrentAnimationId(), _previewTime);
            Repaint();
            SceneView.RepaintAll();

            if (!_previewLoop && !infinite && _previewTime >= 1f)
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
            if (EnsurePreview(animationId))
                Player.SamplePreparedPreview(normalizedTime);
        }

        private void SamplePreviewTime(string animationId, float time)
        {
            if (EnsurePreview(animationId))
                Player.SamplePreparedPreviewTime(time);
        }

        private bool EnsurePreview(string animationId)
        {
            if (_previewActive && string.Equals(_previewAnimationId, animationId, StringComparison.Ordinal))
                return true;

            StopPreview();
            UnityEngine.Object[] affectedTargets = Player.PreparePreview(animationId);
            if (affectedTargets.Length == 0)
                return false;

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
                return false;
            }

            _previewBlockedWarned = false;
            _previewAnimationMode.RegisterTargets(affectedTargets);
            _previewActive = true;
            _previewAnimationId = animationId;
            _previewFingerprint = AuthoringFingerprint(animationId);
            return true;
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
            EditorGUILayout.HelpBox(
                "Reusable clip stack. A ScriptableObject cannot reference objects from a scene. Leave Target Slot empty to animate the TweenPlayer root, or enter a portable slot name (for example Content or Icon) and bind it on each TweenPlayer.",
                MessageType.Info);
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

        public static List<string> CollectTargetKeys(SerializedProperty clips)
        {
            var result = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            if (clips == null || !clips.isArray)
                return result;

            for (int i = 0; i < clips.arraySize; i++)
            {
                SerializedProperty key = clips.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("TargetKey");
                string value = key?.stringValue?.Trim();
                if (!string.IsNullOrEmpty(value) && unique.Add(value))
                    result.Add(value);
            }

            return result;
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
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField("Clip Type", MenuPath(value.GetType()).Replace("/", " / "));
                    }

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

                        bool sharedAsset = owner.targetObject is TweenAnimationAsset;
                        if (child.name == "Target" && sharedAsset)
                        {
                            if (child.objectReferenceValue != null)
                            {
                                EditorGUILayout.HelpBox(
                                    "This shared clip contains a non-portable direct target. Clear it and use Target Slot so every player can provide its own object.",
                                    MessageType.Warning);
                                using (new EditorGUI.DisabledScope(true))
                                    EditorGUILayout.PropertyField(child, new GUIContent("Direct target"));
                                if (GUILayout.Button("Clear direct target"))
                                    child.objectReferenceValue = null;
                            }
                            continue;
                        }

                        if (child.name == "TargetKey")
                        {
                            GUIContent labelContent = sharedAsset
                                ? new GUIContent("Target Slot", "Portable slot name bound on each TweenPlayer. Empty means the TweenPlayer root.")
                                : new GUIContent("Target Slot", "Optional named binding on this TweenPlayer. It overrides Direct Target.");
                            EditorGUILayout.PropertyField(child, labelContent, true);
                            continue;
                        }

                        if (child.name == "Target")
                        {
                            EditorGUILayout.PropertyField(child,
                                new GUIContent("Direct Target", "Inline-only target. Empty means the TweenPlayer root unless a Target Slot is set."), true);
                            continue;
                        }

                        if (child.name == "Mode" && value is PlayTweenAnimationClip)
                        {
                            EditorGUILayout.PropertyField(child, new GUIContent("Playback Mode"), true);
                            continue;
                        }

                        EditorGUILayout.PropertyField(child, true);
                    }
                    if (value is PlayTweenAnimationClip)
                        DrawNestedPlaybackHelp(clip);
                    if (value.IsInfinite)
                        EditorGUILayout.HelpBox(
                            "This clip repeats forever and keeps its animation handle active until it is stopped or replaced.",
                            MessageType.Info);
                    DrawEasePreview(clip);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private static void DrawNestedPlaybackHelp(SerializedProperty clip)
        {
            SerializedProperty modeProperty = clip.FindPropertyRelative("Mode");
            SerializedProperty animationId = clip.FindPropertyRelative("AnimationId");
            if (modeProperty == null)
                return;

            var mode = (TweenNestedPlaybackMode)modeProperty.enumValueIndex;
            string message = mode switch
            {
                TweenNestedPlaybackMode.Wait =>
                    "Wait pauses the parent at this marker until the child finishes. Cancelling the parent also cancels the child; an infinitely looping child will wait forever.",
                TweenNestedPlaybackMode.LinkLifetime =>
                    "Link Lifetime runs the child in parallel. Parent completion completes the child; parent cancellation stops it.",
                _ =>
                    "Fire And Forget starts the child independently. Parent pause, completion and cancellation do not affect it."
            };
            EditorGUILayout.HelpBox(message, MessageType.None);

            if (animationId != null && string.IsNullOrWhiteSpace(animationId.stringValue))
                EditorGUILayout.HelpBox("Animation ID is empty, so this marker cannot start anything.",
                    MessageType.Warning);
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
            SerializedProperty repeatMode = clip.FindPropertyRelative("RepeatMode");
            if ((fieldName == "RepeatCount" || fieldName == "RepeatDelay") && repeatMode != null &&
                repeatMode.enumValueIndex == (int)TweenLoopMode.None)
                return false;

            if (fieldName == "RepeatDelay" && repeatMode != null)
            {
                SerializedProperty repeatCount = clip.FindPropertyRelative("RepeatCount");
                if (repeatCount != null && repeatCount.intValue == 1)
                    return false;
            }

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
