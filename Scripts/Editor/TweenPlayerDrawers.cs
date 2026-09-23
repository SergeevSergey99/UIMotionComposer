using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIMotionComposer.Editor
{
    /// <summary>One animation of a TweenPlayer: ID, playback, clips and timeline, slots, preview, events.</summary>
    [CustomPropertyDrawer(typeof(TweenAnimation))]
    public sealed class TweenAnimationDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new TweenAnimationView(property);
        }
    }

    [CustomPropertyDrawer(typeof(TweenPlaybackSettings))]
    public sealed class TweenPlaybackSettingsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.Add(TweenInspectorUI.Header(preferredLabel));
            root.Add(new PropertyField(property.FindPropertyRelative("UnscaledTime")));
            root.Add(new PropertyField(property.FindPropertyRelative("BlendMode")));
            root.Add(new PropertyField(property.FindPropertyRelative("AllowSelfOverride")));

            SerializedProperty loopMode = property.FindPropertyRelative("LoopMode");
            var loopModeField = new PropertyField(loopMode);
            var loopCount = new PropertyField(property.FindPropertyRelative("LoopCount"));
            root.Add(loopModeField);
            root.Add(loopCount);

            void Refresh(int mode) => TweenInspectorUI.Show(loopCount, mode != (int)TweenLoopMode.None);
            Refresh(loopMode.enumValueIndex);
            loopModeField.RegisterValueChangeCallback(evt => Refresh(evt.changedProperty.enumValueIndex));
            return root;
        }
    }

    /// <summary>A binding rule of the TweenPlayer-wide table: slot name, rule and what it resolves to.</summary>
    [CustomPropertyDrawer(typeof(TweenTargetOverride))]
    public sealed class TweenTargetOverrideDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedObject owner = property.serializedObject;
            var player = owner.targetObjects.Length == 1 ? owner.targetObject as TweenPlayer : null;
            SerializedProperty key = property.FindPropertyRelative("Key");

            VisualElement box = TweenInspectorUI.Box();
            box.Add(new PropertyField(key, "Slot"));
            box.Add(BuildRule(property, player, () => TweenTargetSlots.ForKey(player, key.stringValue)));

            if (player != null)
            {
                Label status = TweenInspectorUI.Mini(string.Empty);
                box.Add(status);
                box.schedule.Execute(() =>
                {
                    string slotKey = SafeString(owner, key.propertyPath);
                    if (slotKey == null)
                        return;
                    UnityEngine.Object resolved = player.ResolveTargetBinding(slotKey);
                    status.text = resolved != null ? $"Resolves to {Describe(resolved)}" : "Resolves to nothing";
                }).Every(300);
            }

            return box;
        }

        /// <summary>
        /// Mode plus the fields that mode reads. Shared by the table above and by each slot row of an
        /// animation, which already shows the slot name in its own header.
        /// </summary>
        internal static VisualElement BuildRule(SerializedProperty entry, TweenPlayer player,
            Func<TweenTargetSlot> slot)
        {
            SerializedObject owner = entry.serializedObject;
            SerializedProperty mode = entry.FindPropertyRelative("Mode");
            string componentPath = entry.FindPropertyRelative("ComponentType").propertyPath;
            string key = entry.FindPropertyRelative("Key").stringValue;

            var root = new VisualElement();
            var modeField = new PropertyField(mode, "Mode");
            var target = new PropertyField(entry.FindPropertyRelative("Target"), "Object");
            var self = new ObjectField("Resolved from") { objectType = typeof(GameObject), value = player != null ? player.gameObject : null };
            self.SetEnabled(false);
            var query = new PropertyField(entry.FindPropertyRelative("Query"));
            var component = new DropdownField("Component type");
            root.Add(modeField);
            root.Add(target);
            root.Add(self);
            root.Add(query);
            root.Add(component);

            component.RegisterValueChangedCallback(evt =>
            {
                Type type = ComponentOptions(slot()).FirstOrDefault(item => item.Name == evt.newValue);
                SerializedProperty stored = owner.FindProperty(componentPath);
                if (type == null || stored == null)
                    return;
                stored.stringValue = type.AssemblyQualifiedName;
                owner.ApplyModifiedProperties();
                player?.InvalidateTargetBindings();
            });

            void Refresh(TweenTargetBindingMode value)
            {
                TweenInspectorUI.Show(target, value == TweenTargetBindingMode.Direct);
                TweenInspectorUI.Show(self, value == TweenTargetBindingMode.Self);
                TweenInspectorUI.Show(query, value is TweenTargetBindingMode.ChildPath or
                    TweenTargetBindingMode.ChildName or TweenTargetBindingMode.Component);
                TweenInspectorUI.Show(component, value == TweenTargetBindingMode.Component);

                string slotName = string.IsNullOrWhiteSpace(key) ? "the slot name" : $"'{key}'";
                (query.label, query.tooltip) = value switch
                {
                    TweenTargetBindingMode.ChildPath => ("Child path", $"Relative path. Empty uses {slotName}."),
                    TweenTargetBindingMode.ChildName => ("Child name", $"Descendant name. Empty uses {slotName}."),
                    _ => ("Under child", "Optional path or name. Empty searches the player and its descendants. A missing child leaves the slot unresolved.")
                };

                if (value != TweenTargetBindingMode.Component)
                    return;

                List<Type> options = ComponentOptions(slot());
                component.choices = options.Select(type => type.Name).ToList();
                SerializedProperty stored = owner.FindProperty(componentPath);
                Type current = options.FirstOrDefault(type => type.AssemblyQualifiedName == stored?.stringValue);
                if (current == null && stored != null)
                {
                    current = options[0];
                    stored.stringValue = current.AssemblyQualifiedName;
                    owner.ApplyModifiedProperties();
                }
                component.SetValueWithoutNotify(current?.Name);
            }

            Refresh((TweenTargetBindingMode)mode.enumValueIndex);
            modeField.RegisterValueChangeCallback(evt =>
            {
                Refresh((TweenTargetBindingMode)evt.changedProperty.enumValueIndex);
                player?.InvalidateTargetBindings();
            });
            root.RegisterCallback<SerializedPropertyChangeEvent>(_ => player?.InvalidateTargetBindings());
            return root;
        }

        private static List<Type> ComponentOptions(TweenTargetSlot slot)
        {
            List<Type> options = slot?.ComponentTypes.ToList() ?? new List<Type>();
            if (options.Count == 0)
                options.Add(typeof(Transform));
            return options;
        }

        internal static string Describe(UnityEngine.Object target)
        {
            return target switch
            {
                GameObject gameObject => gameObject.name,
                Component component => $"{component.gameObject.name} ({component.GetType().Name})",
                _ => target.name
            };
        }

        private static string SafeString(SerializedObject owner, string path)
        {
            try
            {
                return owner.targetObject != null ? owner.FindProperty(path)?.stringValue : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>A string chosen among the animation IDs of the TweenPlayer that owns the field.</summary>
    [CustomPropertyDrawer(typeof(TweenAnimationIdAttribute))]
    public sealed class TweenAnimationIdDrawer : PropertyDrawer
    {
        private const string None = "— None —";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedObject owner = property.serializedObject;
            string path = property.propertyPath;
            var field = new DropdownField(preferredLabel);
            field.AddToClassList(BaseField<string>.alignedFieldUssClassName);

            void Refresh()
            {
                SerializedProperty current = owner.FindProperty(path);
                if (current == null)
                    return;
                List<string> choices = Choices(owner.targetObject as TweenPlayer, current.stringValue);
                field.choices = choices;
                field.SetValueWithoutNotify(ChoiceFor(current.stringValue));
            }

            field.RegisterValueChangedCallback(evt =>
            {
                SerializedProperty current = owner.FindProperty(path);
                if (current == null || evt.newValue == null)
                    return;
                current.stringValue = evt.newValue == None ? string.Empty : evt.newValue;
                owner.ApplyModifiedProperties();
            });
            field.TrackPropertyValue(property, _ => Refresh());
            field.RegisterCallback<AttachToPanelEvent>(_ => Refresh());
            field.RegisterCallback<FocusInEvent>(_ => Refresh());
            return field;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            List<string> choices = Choices(property.serializedObject.targetObject as TweenPlayer, property.stringValue);
            int index = Mathf.Max(0, choices.IndexOf(ChoiceFor(property.stringValue)));
            EditorGUI.BeginProperty(position, label, property);
            int next = EditorGUI.Popup(position, label.text, index, choices.ToArray());
            if (next != index)
                property.stringValue = next == 0 ? string.Empty : choices[next];
            EditorGUI.EndProperty();
        }

        private static List<string> Choices(TweenPlayer player, string value)
        {
            var choices = new List<string> { None };
            if (player != null)
                choices.AddRange(player.Animations
                    .Where(animation => animation != null && !string.IsNullOrWhiteSpace(animation.Id))
                    .Select(animation => animation.Id).Distinct());
            if (!string.IsNullOrEmpty(value) && !choices.Contains(value))
                choices.Add(ChoiceFor(value));
            return choices;
        }

        private static string ChoiceFor(string value)
        {
            return string.IsNullOrEmpty(value) ? None : value;
        }

    }

    /// <summary>The body of <see cref="TweenAnimationDrawer"/>.</summary>
    internal sealed class TweenAnimationView : VisualElement
    {
        private readonly SerializedObject _owner;
        private readonly string _path;
        private readonly TweenPlayer _player;
        private readonly Foldout _foldout;
        private readonly Label _summary;
        private readonly HelpBox _validation;
        private readonly IMGUIContainer _clips;
        private readonly VisualElement _bindings;
        private readonly HelpBox _bindingWarning;
        private readonly List<SlotRow> _slotRows = new List<SlotRow>();
        private readonly TweenAnimationPreview _preview;
        private readonly Slider _previewSlider;
        private readonly Button _previewPlay;
        private readonly VisualElement _previewControls;
        private SerializedObject _assetSource;
        private string _bindingSignature;

        private sealed class SlotRow
        {
            public TweenTargetSlot Slot;
            public Label Status;
            public Button Ping;
        }

        public TweenAnimationView(SerializedProperty property)
        {
            _owner = property.serializedObject;
            _path = property.propertyPath;
            _player = _owner.targetObjects.Length == 1 ? _owner.targetObject as TweenPlayer : null;

            _foldout = new Foldout { value = property.isExpanded };
            _foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == _foldout && Property() is { } animation)
                    animation.isExpanded = evt.newValue;
            });
            _summary = TweenInspectorUI.Mini(string.Empty);
            _summary.style.marginLeft = StyleKeyword.Auto;
            _summary.pickingMode = PickingMode.Ignore;
            _foldout.Q<Toggle>().Add(_summary);
            Add(_foldout);

            _validation = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            TweenInspectorUI.Show(_validation, false);
            _foldout.Add(_validation);

            VisualElement idRow = TweenInspectorUI.Row();
            var id = new PropertyField(property.FindPropertyRelative("Id"), "Animation ID");
            id.style.flexGrow = 1f;
            idRow.Add(id);
            Button duplicate = TweenInspectorUI.Button("Duplicate", Duplicate);
            duplicate.SetEnabled(_player != null);
            idRow.Add(duplicate);
            _foldout.Add(idRow);

            _foldout.Add(new PropertyField(property.FindPropertyRelative("Asset"), "Shared clip asset"));
            _foldout.Add(new PropertyField(property.FindPropertyRelative("Playback"), "Playback"));

            _clips = new IMGUIContainer(DrawClips);
            _clips.style.marginTop = 4f;
            _foldout.Add(_clips);

            _bindings = new VisualElement();
            _foldout.Add(_bindings);
            _bindingWarning = new HelpBox(
                "Missing or incompatible slots are skipped during playback. Use Auto Bind All or inspect the highlighted rows.",
                HelpBoxMessageType.Warning);

            _preview = new TweenAnimationPreview(_owner, _path);
            _foldout.Add(TweenInspectorUI.Header("Edit-mode preview"));
            _previewControls = new VisualElement();
            _previewSlider = new Slider("Timeline", 0f, 1f);
            _previewSlider.AddToClassList(BaseField<float>.alignedFieldUssClassName);
            _previewSlider.RegisterValueChangedCallback(evt => _preview.Scrub(evt.newValue));
            _previewControls.Add(_previewSlider);
            VisualElement buttons = TweenInspectorUI.Row();
            buttons.Add(TweenInspectorUI.Button("|<", _preview.Rewind, 34f));
            _previewPlay = TweenInspectorUI.Button("Play preview", _preview.TogglePlay);
            _previewPlay.style.flexGrow = 1f;
            buttons.Add(_previewPlay);
            var loop = new Toggle("Loop");
            loop.RegisterValueChangedCallback(evt => _preview.Loop = evt.newValue);
            loop.style.marginLeft = 6f;
            loop.labelElement.style.minWidth = StyleKeyword.Auto;
            buttons.Add(loop);
            Button restore = TweenInspectorUI.Button("Restore", _preview.Stop);
            restore.style.flexGrow = 1f;
            buttons.Add(restore);
            _previewControls.Add(buttons);
            _foldout.Add(_previewControls);
            _preview.Changed += OnPreviewChanged;

            var events = new Foldout { text = "Events", value = false };
            events.Add(new PropertyField(property.FindPropertyRelative("OnStarted")));
            events.Add(new PropertyField(property.FindPropertyRelative("OnCompleted")));
            events.Add(new PropertyField(property.FindPropertyRelative("OnCancelled")));
            _foldout.Add(events);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _preview.Hook();
                SeparateDuplicatedClips();
                Refresh();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                _preview.Dispose();
                _assetSource?.Dispose();
                _assetSource = null;
            });
            schedule.Execute(Refresh).Every(200);
        }

        private SerializedProperty Property()
        {
            try
            {
                return _owner.targetObject != null ? _owner.FindProperty(_path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private int Index => TweenInspectorUI.ArrayIndex(_path);

        private TweenAnimation RuntimeAnimation()
        {
            int index = Index;
            return _player != null && index >= 0 && index < _player.AnimationDefinitions.Count
                ? _player.AnimationDefinitions[index]
                : null;
        }

        private void Refresh()
        {
            SerializedProperty animation = Property();
            if (animation == null)
                return;

            string id = animation.FindPropertyRelative("Id").stringValue;
            var asset = animation.FindPropertyRelative("Asset").objectReferenceValue as TweenAnimationAsset;
            _foldout.text = string.IsNullOrWhiteSpace(id) ? $"Animation {Index + 1}" : id;
            _summary.text = Summary(animation, id, asset);
            RefreshValidation(id);

            SerializedProperty clips = ClipsOf(animation, asset);
            string signature = BindingSignature(animation, clips, asset);
            if (signature != _bindingSignature)
                RebuildBindings(animation, clips);
            else
                RefreshBindingStatus();

            _previewControls.SetEnabled(_preview.CanPreview);
            _preview.RefreshIfAuthoringChanged();
        }

        private string Summary(SerializedProperty animation, string id, TweenAnimationAsset asset)
        {
            int count = asset != null ? asset.Clips.Count : animation.FindPropertyRelative("Clips").arraySize;
            var player = _owner.targetObject as TweenPlayer;
            string duration = player == null ? string.Empty
                : player.IsInfinite(id) ? " · ∞" : $" · {player.GetDuration(id):0.##}s";
            string shared = asset != null ? " · shared" : string.Empty;
            return $"{count} clips{duration}{shared}";
        }

        private void RefreshValidation(string id)
        {
            string message = null;
            if (string.IsNullOrWhiteSpace(id))
                message = "Every animation needs a non-empty ID.";
            else if (_owner.FindProperty("animations") is { } list)
            {
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (i == Index)
                        continue;
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue == id)
                    {
                        message = $"Animation ID '{id}' is duplicated. Play() will use the first match.";
                        break;
                    }
                }
            }

            _validation.text = message ?? string.Empty;
            TweenInspectorUI.Show(_validation, message != null);
        }

        private void OnPreviewChanged()
        {
            _previewSlider.SetValueWithoutNotify(_preview.Time);
            _previewPlay.text = _preview.IsPlaying ? "Pause" : "Play preview";
            _clips.MarkDirtyRepaint();
        }

        private void DrawClips()
        {
            _owner.Update();
            SerializedProperty animation = Property();
            if (animation == null)
                return;

            var asset = animation.FindPropertyRelative("Asset").objectReferenceValue as TweenAnimationAsset;
            if (asset != null)
            {
                SerializedObject source = AssetSource(asset);
                TweenTimelineEditor.Draw(source, source.FindProperty("Clips"), _preview.Time, _preview.Scrub);
                source.ApplyModifiedProperties();
                EditorGUILayout.HelpBox(
                    "Timing and target slot names come from the shared asset. Their concrete objects are stored locally on this TweenPlayer.",
                    MessageType.None);
                if (GUILayout.Button("Select shared animation asset"))
                    Selection.activeObject = asset;
            }
            else
            {
                SerializedProperty clips = animation.FindPropertyRelative("Clips");
                TweenTimelineEditor.Draw(_owner, clips, _preview.Time, _preview.Scrub);
                TweenClipEditorUtility.DrawClipList(_owner, clips, "Clips");
            }

            _owner.ApplyModifiedProperties();
        }

        private SerializedObject AssetSource(TweenAnimationAsset asset)
        {
            if (_assetSource == null || _assetSource.targetObject != asset)
            {
                _assetSource?.Dispose();
                _assetSource = new SerializedObject(asset);
            }

            _assetSource.Update();
            return _assetSource;
        }

        private SerializedProperty ClipsOf(SerializedProperty animation, TweenAnimationAsset asset)
        {
            return asset != null
                ? AssetSource(asset).FindProperty("Clips")
                : animation.FindPropertyRelative("Clips");
        }

        private string BindingSignature(SerializedProperty animation, SerializedProperty clips,
            TweenAnimationAsset asset)
        {
            List<TweenTargetSlot> slots = TweenTargetSlots.Collect(clips);
            if (slots.Count == 0)
                return string.Empty;

            SerializedProperty local = animation.FindPropertyRelative("TargetOverrides");
            SerializedProperty global = _owner.FindProperty("targetOverrides");
            var signature = new System.Text.StringBuilder();
            signature.Append(asset != null ? asset.GetInstanceID() : 0).Append('|');
            foreach (TweenTargetSlot slot in slots)
            {
                signature.Append(slot.Key).Append(':').Append(slot.Expected).Append(':')
                    .Append(string.Join(",", slot.Consumers)).Append(':')
                    .Append(TweenTargetSlots.Find(local, slot.Key)).Append(':')
                    .Append(TweenTargetSlots.Find(global, slot.Key)).Append('|');
            }
            return signature.ToString();
        }

        private void RebuildBindings(SerializedProperty animation, SerializedProperty clips)
        {
            _bindings.Clear();
            _slotRows.Clear();
            _bindingSignature = BindingSignature(animation, clips,
                animation.FindPropertyRelative("Asset").objectReferenceValue as TweenAnimationAsset);

            List<TweenTargetSlot> slots = TweenTargetSlots.Collect(clips);
            if (slots.Count == 0)
                return;

            VisualElement header = TweenInspectorUI.Row();
            header.style.marginTop = 6f;
            Label title = TweenInspectorUI.Header($"Target bindings ({slots.Count})");
            title.style.flexGrow = 1f;
            title.style.marginTop = 0f;
            header.Add(title);
            Button autoBind = TweenInspectorUI.Button("Auto Bind All", () => AutoBindAll(slots));
            autoBind.SetEnabled(_player != null);
            header.Add(autoBind);
            _bindings.Add(header);
            _bindings.Add(TweenInspectorUI.Mini(
                "Slots may use a direct object, this player, a child path/name, or a component search. Local overrides affect only this animation."));

            if (_player == null)
            {
                _bindings.Add(new HelpBox("Edit target bindings on one TweenPlayer at a time.", HelpBoxMessageType.None));
                return;
            }

            SerializedProperty local = animation.FindPropertyRelative("TargetOverrides");
            SerializedProperty global = _owner.FindProperty("targetOverrides");
            foreach (TweenTargetSlot slot in slots)
                _bindings.Add(BuildSlot(slot, local, global));

            _bindings.Add(_bindingWarning);
            _bindings.Bind(_owner);
            RefreshBindingStatus();
        }

        private VisualElement BuildSlot(TweenTargetSlot slot, SerializedProperty local, SerializedProperty global)
        {
            int localIndex = TweenTargetSlots.Find(local, slot.Key);
            bool useLocal = localIndex >= 0;
            SerializedProperty bindings = useLocal ? local : global;
            int index = useLocal ? localIndex : TweenTargetSlots.Find(global, slot.Key);
            SerializedProperty entry = index >= 0 ? bindings.GetArrayElementAtIndex(index) : null;
            string bindingsPath = bindings.propertyPath;

            VisualElement box = TweenInspectorUI.Box();
            VisualElement header = TweenInspectorUI.Row();
            var row = new SlotRow { Slot = slot, Status = TweenInspectorUI.Mini(string.Empty) };
            row.Status.style.width = 82f;
            row.Status.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(row.Status);
            Label key = TweenInspectorUI.Header(slot.Key);
            key.style.marginTop = 0f;
            key.style.flexGrow = 1f;
            header.Add(key);
            var localToggle = new Toggle("Local") { value = useLocal, tooltip = "Override this slot only for this animation." };
            localToggle.labelElement.style.minWidth = StyleKeyword.Auto;
            localToggle.RegisterValueChangedCallback(evt => SetLocal(slot.Key, evt.newValue));
            header.Add(localToggle);
            box.Add(header);
            box.Add(TweenInspectorUI.Mini($"Expected: {slot.Expected}"));
            box.Add(TweenInspectorUI.Mini($"Used by: {string.Join(", ", slot.Consumers)}"));

            if (entry == null)
            {
                box.Add(TweenInspectorUI.Mini("No binding rule yet. Create one or let Auto Bind search the hierarchy."));
                box.Add(TweenInspectorUI.Button("Create binding", () => Edit(() =>
                    TweenTargetSlots.Ensure(_owner.FindProperty(bindingsPath), slot.Key))));
                _slotRows.Add(row);
                return box;
            }

            string entryPath = entry.propertyPath;
            box.Add(TweenTargetOverrideDrawer.BuildRule(entry, _player, () => slot));
            VisualElement actions = TweenInspectorUI.Row();
            Button find = TweenInspectorUI.Button("Find", () => FindTarget(entryPath, slot));
            find.style.flexGrow = 1f;
            actions.Add(find);
            row.Ping = TweenInspectorUI.Button("Ping", () =>
                EditorGUIUtility.PingObject(_player.ResolveTargetBinding(slot.Key, RuntimeAnimation())));
            row.Ping.style.flexGrow = 1f;
            actions.Add(row.Ping);
            Button clear = TweenInspectorUI.Button("Clear", () => Edit(() =>
                TweenTargetSlots.Reset(_owner.FindProperty(entryPath))));
            clear.style.flexGrow = 1f;
            actions.Add(clear);
            box.Add(actions);
            _slotRows.Add(row);
            return box;
        }

        private void RefreshBindingStatus()
        {
            if (_player == null || _slotRows.Count == 0)
                return;

            TweenAnimation runtime = RuntimeAnimation();
            bool problem = false;
            foreach (SlotRow row in _slotRows)
            {
                UnityEngine.Object resolved = _player.ResolveTargetBinding(row.Slot.Key, runtime);
                bool valid = row.Slot.IsSatisfiedBy(resolved);
                problem |= !valid;
                row.Status.text = resolved == null ? "● Missing" : valid ? "● Resolved" : "● Wrong type";
                row.Status.style.color = resolved == null ? TweenInspectorUI.Missing
                    : valid ? TweenInspectorUI.Resolved : TweenInspectorUI.WrongType;
                row.Status.tooltip = resolved != null ? TweenTargetOverrideDrawer.Describe(resolved) : string.Empty;
                row.Ping?.SetEnabled(resolved != null);
            }

            TweenInspectorUI.Show(_bindingWarning, problem);
        }

        private void SetLocal(string key, bool local)
        {
            Edit(() =>
            {
                SerializedProperty animation = Property();
                SerializedProperty localBindings = animation.FindPropertyRelative("TargetOverrides");
                int localIndex = TweenTargetSlots.Find(localBindings, key);
                if (!local)
                {
                    if (localIndex >= 0)
                        localBindings.DeleteArrayElementAtIndex(localIndex);
                    return;
                }

                SerializedProperty global = _owner.FindProperty("targetOverrides");
                int globalIndex = TweenTargetSlots.Find(global, key);
                SerializedProperty entry = TweenTargetSlots.Ensure(localBindings, key);
                if (globalIndex >= 0)
                    TweenTargetSlots.CopyRule(global.GetArrayElementAtIndex(globalIndex), entry);
            });
        }

        private void FindTarget(string entryPath, TweenTargetSlot slot)
        {
            _owner.ApplyModifiedProperties();
            _player.InvalidateTargetBindings();
            if (_player.ResolveTargetBinding(slot.Key, RuntimeAnimation()) != null)
                return;

            UnityEngine.Object found = TweenTargetSlots.FindBestTarget(_player, slot);
            if (found != null)
                Edit(() => TweenTargetSlots.BindDirect(_owner.FindProperty(entryPath), found));
        }

        private void AutoBindAll(IReadOnlyList<TweenTargetSlot> slots)
        {
            Edit(() =>
            {
                SerializedProperty animation = Property();
                SerializedProperty local = animation.FindPropertyRelative("TargetOverrides");
                SerializedProperty global = _owner.FindProperty("targetOverrides");
                foreach (TweenTargetSlot slot in slots)
                {
                    UnityEngine.Object current = _player.ResolveTargetBinding(slot.Key, RuntimeAnimation());
                    if (slot.IsSatisfiedBy(current))
                        continue;

                    UnityEngine.Object found = TweenTargetSlots.FindBestTarget(_player, slot);
                    if (found == null)
                        continue;

                    SerializedProperty bindings = TweenTargetSlots.Find(local, slot.Key) >= 0 ? local : global;
                    TweenTargetSlots.BindDirect(TweenTargetSlots.Ensure(bindings, slot.Key), found);
                }
            });
        }

        /// <summary>Applies one serialized edit as an Undo step, then redraws the slot rows.</summary>
        private void Edit(Action change)
        {
            _owner.Update();
            change();
            _owner.ApplyModifiedProperties();
            _player?.InvalidateAuthoringCache();
            _bindingSignature = null;
            Refresh();
        }

        private void Duplicate()
        {
            SerializedProperty list = _owner.FindProperty("animations");
            int index = Index;
            if (list == null || index < 0 || index >= list.arraySize)
                return;

            _owner.Update();
            list.InsertArrayElementAtIndex(index);
            SerializedProperty copy = list.GetArrayElementAtIndex(index + 1);
            GiveOwnClips(copy);
            copy.FindPropertyRelative("Id").stringValue =
                UniqueId(list, copy.FindPropertyRelative("Id").stringValue + " Copy");
            _owner.ApplyModifiedProperties();
            _player?.InvalidateAuthoringCache();
        }

        /// <summary>
        /// The list's + button and Duplicate copy the element's clip references, not the clips, so
        /// two animations would edit one clip. The later animation takes its own copies.
        /// </summary>
        private void SeparateDuplicatedClips()
        {
            if (_player == null)
                return;

            SerializedProperty list = _owner.FindProperty("animations");
            int index = Index;
            if (list == null || index <= 0 || index >= list.arraySize)
                return;

            var earlier = new HashSet<long>();
            for (int i = 0; i < index; i++)
            {
                SerializedProperty clips = list.GetArrayElementAtIndex(i).FindPropertyRelative("Clips");
                for (int j = 0; j < clips.arraySize; j++)
                    earlier.Add(clips.GetArrayElementAtIndex(j).managedReferenceId);
            }

            SerializedProperty animation = list.GetArrayElementAtIndex(index);
            SerializedProperty own = animation.FindPropertyRelative("Clips");
            bool shared = false;
            for (int j = 0; j < own.arraySize && !shared; j++)
            {
                SerializedProperty clip = own.GetArrayElementAtIndex(j);
                shared = clip.managedReferenceValue != null && earlier.Contains(clip.managedReferenceId);
            }
            if (!shared)
                return;

            GiveOwnClips(animation);
            SerializedProperty id = animation.FindPropertyRelative("Id");
            id.stringValue = UniqueId(list, id.stringValue);
            _owner.ApplyModifiedPropertiesWithoutUndo();
            _player.InvalidateAuthoringCache();
        }

        private static void GiveOwnClips(SerializedProperty animation)
        {
            SerializedProperty clips = animation.FindPropertyRelative("Clips");
            for (int i = 0; i < clips.arraySize; i++)
            {
                SerializedProperty clip = clips.GetArrayElementAtIndex(i);
                if (clip.managedReferenceValue is BaseTweenClip value)
                    clip.managedReferenceValue = TweenClipEditorUtility.CloneClip(value);
            }
        }

        private static string UniqueId(SerializedProperty list, string basis)
        {
            var taken = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < list.arraySize; i++)
                taken.Add(list.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue);

            // The element being renamed is in the list too, so its current ID counts as taken.
            string id = string.IsNullOrWhiteSpace(basis) ? "Animation" : basis;
            if (!taken.Contains(id))
                return id;
            int suffix = 2;
            string candidate = $"{id} {suffix}";
            while (taken.Contains(candidate))
                candidate = $"{id} {++suffix}";
            return candidate;
        }
    }

    /// <summary>Small layout helpers so the drawers share spacing and colors.</summary>
    internal static class TweenInspectorUI
    {
        public static readonly Color Missing = new Color(1f, 0.55f, 0.35f);
        public static readonly Color Resolved = new Color(0.45f, 0.9f, 0.55f);
        public static readonly Color WrongType = new Color(1f, 0.75f, 0.25f);

        public static VisualElement Box()
        {
            var box = new VisualElement();
            box.AddToClassList(HelpBox.ussClassName);
            box.style.flexDirection = FlexDirection.Column;
            box.style.alignItems = Align.Stretch;
            box.style.paddingLeft = 4f;
            box.style.paddingRight = 4f;
            box.style.paddingTop = 3f;
            box.style.paddingBottom = 3f;
            box.style.marginTop = 2f;
            box.style.marginBottom = 2f;
            return box;
        }

        public static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }

        public static Label Header(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 6f;
            label.style.marginLeft = 3f;
            label.style.marginBottom = 2f;
            return label;
        }

        public static Label Mini(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 10f;
            label.style.opacity = 0.8f;
            label.style.marginLeft = 3f;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        public static Button Button(string text, Action clicked, float width = -1f)
        {
            var button = new Button(clicked) { text = text };
            if (width > 0f)
                button.style.width = width;
            return button;
        }

        public static void Show(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Index of the innermost array element in a property path, or -1.</summary>
        public static int ArrayIndex(string path)
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

        public static void MarkDirty(IEnumerable<UnityEngine.Object> changed)
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
}
