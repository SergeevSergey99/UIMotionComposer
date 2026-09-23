using System;
using System.Collections.Generic;
using System.Linq;
using UIMotionComposer.Tweening;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIMotionComposer.Editor
{
    /// <summary>
    /// Declares only that TweenPlayer is drawn with UI Toolkit. It draws nothing itself: every
    /// serialized field has its own property drawer (Initial Pose, animations, bindings), and the
    /// declaration keeps inspector replacements such as Odin from redrawing the component in IMGUI,
    /// where those drawers do not run.
    /// </summary>
    [CustomEditor(typeof(TweenPlayer))]
    [CanEditMultipleObjects]
    public sealed class TweenPlayerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
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
