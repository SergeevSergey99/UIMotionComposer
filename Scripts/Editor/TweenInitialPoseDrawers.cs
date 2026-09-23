using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using Image = UnityEngine.UI.Image;
using Toggle = UnityEngine.UIElements.Toggle;

namespace UIMotionComposer.Editor
{
    /// <summary>
    /// The saved pose: capture/restore/clear, then its entries grouped by the object they belong
    /// to. Fields can be added to or removed from each object, and objects added as a whole.
    /// </summary>
    [CustomPropertyDrawer(typeof(TweenInitialPose))]
    public sealed class TweenInitialPoseDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new TweenInitialPoseView(property, preferredLabel);
        }
    }

    internal sealed class TweenInitialPoseView : VisualElement
    {
        private static readonly HashSet<int> CollapsedObjects = new HashSet<int>();

        private readonly SerializedObject _owner;
        private readonly string _path;
        private readonly TweenPlayer _player;
        private readonly Foldout _foldout;
        private readonly Label _summary;
        private readonly HelpBox _status;
        private readonly Button _capture;
        private readonly Button _restore;
        private readonly Button _clear;
        private readonly VisualElement _groups;
        private readonly ObjectField _addObject;
        private readonly Button _removeMissing;
        private string _signature;

        public TweenInitialPoseView(SerializedProperty property, string label)
        {
            _owner = property.serializedObject;
            _path = property.propertyPath;
            _player = _owner.targetObjects.Length == 1 ? _owner.targetObject as TweenPlayer : null;

            _foldout = new Foldout { text = label, value = property.isExpanded };
            _foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == _foldout && Property() is { } pose)
                    pose.isExpanded = evt.newValue;
            });
            _summary = TweenInspectorUI.Mini(string.Empty);
            _summary.style.marginLeft = StyleKeyword.Auto;
            _summary.pickingMode = PickingMode.Ignore;
            _foldout.Q<Toggle>().Add(_summary);
            Add(_foldout);

            _status = new HelpBox(string.Empty, HelpBoxMessageType.None);
            _foldout.Add(_status);

            VisualElement actions = TweenInspectorUI.Row();
            _capture = TweenInspectorUI.Button("Capture Pose", Capture);
            _capture.style.flexGrow = 1f;
            _restore = TweenInspectorUI.Button("Restore Pose", () => Restore(null));
            _restore.style.flexGrow = 1f;
            _clear = TweenInspectorUI.Button("Clear", ClearPose, 56f);
            actions.Add(_capture);
            actions.Add(_restore);
            actions.Add(_clear);
            _foldout.Add(actions);

            _groups = new VisualElement();
            _foldout.Add(_groups);

            _addObject = new ObjectField("Add object")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = true,
                tooltip = "Saves the active state and Transform of the dropped object."
            };
            _addObject.AddToClassList(BaseField<UnityEngine.Object>.alignedFieldUssClassName);
            _addObject.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is GameObject gameObject)
                    Edit("Add UI Motion initial object", () => _player.AddInitialObject(gameObject));
                _addObject.SetValueWithoutNotify(null);
            });
            _foldout.Add(_addObject);

            _removeMissing = TweenInspectorUI.Button("Remove missing entries", () =>
                Edit("Remove missing UI Motion initial values", () => _player.RemoveMissingInitialValues()));
            _foldout.Add(_removeMissing);

            RegisterCallback<AttachToPanelEvent>(_ => Refresh());
            TweenInspectorUI.Poll(this, _owner, 250, Refresh);
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

        private void Refresh()
        {
            SerializedProperty pose = Property();
            if (pose == null)
                return;

            bool captured = pose.FindPropertyRelative("captured").boolValue;
            SerializedProperty values = pose.FindPropertyRelative("values");
            string signature = Signature(captured, values);
            if (signature != _signature)
            {
                _signature = signature;
                RebuildGroups(values);
            }

            int missing = _player != null
                ? _player.GetCapturedInitialPoseEntries().Count(item => item.Target == null || !item.CanRestore)
                : 0;
            int objects = Groups(values).Count(group => group.Key != null);
            _summary.text = captured ? $"{objects} objects · {values.arraySize} values" : "not captured";
            _capture.text = captured ? "Recapture" : "Capture Pose";

            if (!captured)
                SetStatus(HelpBoxMessageType.Warning,
                    "No saved pose. Initial endpoints fall back to values captured at first playback.");
            else if (missing > 0)
                SetStatus(HelpBoxMessageType.Warning,
                    $"{missing} saved values have a missing target or unsupported property type.");
            else
                SetStatus(HelpBoxMessageType.None,
                    "Applied on Awake or the first Play. Values are editable; Restore applies them without capturing a new pose.");

            bool editable = _player != null && !Application.isPlaying;
            _capture.SetEnabled(editable);
            _restore.SetEnabled(editable && values.arraySize > 0);
            _clear.SetEnabled(editable && captured);
            _addObject.SetEnabled(editable);
            _removeMissing.SetEnabled(editable);
            TweenInspectorUI.Show(_removeMissing, missing > 0);
        }

        private void SetStatus(HelpBoxMessageType type, string text)
        {
            _status.messageType = type;
            _status.text = text;
        }

        private static string Signature(bool captured, SerializedProperty values)
        {
            var signature = new System.Text.StringBuilder().Append(captured).Append('|');
            for (int i = 0; i < values.arraySize; i++)
            {
                SerializedProperty entry = values.GetArrayElementAtIndex(i);
                signature.Append(entry.FindPropertyRelative("target").objectReferenceInstanceIDValue).Append(':')
                    .Append(entry.FindPropertyRelative("propertyId").stringValue).Append('|');
            }
            return signature.ToString();
        }

        /// <summary>Entry indices grouped by the GameObject their target belongs to, in list order.</summary>
        private static List<KeyValuePair<GameObject, List<int>>> Groups(SerializedProperty values)
        {
            var groups = new List<KeyValuePair<GameObject, List<int>>>();
            for (int i = 0; i < values.arraySize; i++)
            {
                UnityEngine.Object target = values.GetArrayElementAtIndex(i).FindPropertyRelative("target").objectReferenceValue;
                GameObject owner = target switch
                {
                    GameObject gameObject => gameObject,
                    Component component => component.gameObject,
                    _ => null
                };
                int group = groups.FindIndex(item => item.Key == owner);
                if (group < 0)
                    groups.Add(new KeyValuePair<GameObject, List<int>>(owner, new List<int> { i }));
                else
                    groups[group].Value.Add(i);
            }
            return groups;
        }

        private void RebuildGroups(SerializedProperty values)
        {
            _groups.Clear();
            foreach (KeyValuePair<GameObject, List<int>> group in Groups(values))
                _groups.Add(BuildGroup(group.Key, group.Value, values));
            _groups.Bind(_owner);
        }

        private VisualElement BuildGroup(GameObject gameObject, List<int> indices, SerializedProperty values)
        {
            VisualElement box = TweenInspectorUI.Box();
            VisualElement header = TweenInspectorUI.Row();
            int key = gameObject != null ? gameObject.GetInstanceID() : 0;
            var foldout = new Foldout
            {
                text = gameObject != null ? TargetPath(_player, gameObject) : "Missing target",
                value = !CollapsedObjects.Contains(key)
            };
            foldout.style.flexGrow = 1f;
            foldout.style.flexShrink = 1f;
            header.Add(foldout);

            bool editable = _player != null && !Application.isPlaying;
            if (gameObject != null)
            {
                Button add = TweenInspectorUI.Button("+", null, 24f);
                add.tooltip = "Save another property of this object.";
                add.clicked += () => ShowAddMenu(gameObject);
                add.SetEnabled(editable);
                header.Add(add);

                Button restore = TweenInspectorUI.Button("Restore", () => Restore(gameObject), 62f);
                restore.tooltip = "Apply the saved values of this object.";
                restore.SetEnabled(editable);
                header.Add(restore);
            }

            Button remove = TweenInspectorUI.Button("✕", () => Edit("Remove UI Motion initial object", () =>
            {
                for (int i = indices.Count - 1; i >= 0; i--)
                    _player.RemoveInitialValueAt(indices[i]);
            }), 24f);
            remove.tooltip = "Stop saving this object.";
            remove.SetEnabled(editable);
            header.Add(remove);
            box.Add(header);

            var entries = new VisualElement();
            foreach (int index in indices.OrderBy(index => DisplayOrder(values.GetArrayElementAtIndex(index)
                         .FindPropertyRelative("propertyId").stringValue)).ThenBy(index => index))
                entries.Add(new PropertyField(values.GetArrayElementAtIndex(index)));
            box.Add(entries);

            void Collapse(bool expanded)
            {
                TweenInspectorUI.Show(entries, expanded);
                if (expanded)
                    CollapsedObjects.Remove(key);
                else
                    CollapsedObjects.Add(key);
            }
            Collapse(foldout.value);
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout)
                    Collapse(evt.newValue);
            });
            return box;
        }

        /// <summary>The order an object's fields are listed in, whatever order they were saved in.</summary>
        private static int DisplayOrder(string propertyId)
        {
            return propertyId switch
            {
                TweenPoseProperties.Active => 0,
                TweenPoseProperties.AnchoredPosition or TweenPoseProperties.AnchoredPosition3D or
                    TweenPoseProperties.LocalPosition or TweenPoseProperties.Position => 1,
                TweenPoseProperties.LocalRotation or TweenPoseProperties.Rotation => 2,
                TweenPoseProperties.LocalScale => 3,
                TweenPoseProperties.SizeDelta => 4,
                TweenPoseProperties.AnchorMin => 5,
                TweenPoseProperties.AnchorMax => 6,
                TweenPoseProperties.Pivot => 7,
                _ => 8
            };
        }

        private void ShowAddMenu(GameObject gameObject)
        {
            var menu = new GenericMenu();
            foreach ((string label, UnityEngine.Object target, string propertyId) in AddableProperties(gameObject))
            {
                var content = new GUIContent(label);
                if (_player.HasInitialValue(target, propertyId))
                    menu.AddDisabledItem(content, true);
                else
                    menu.AddItem(content, false, () => Edit("Add UI Motion initial value",
                        () => _player.AddInitialValue(target, propertyId)));
            }
            menu.ShowAsContext();
        }

        private static IEnumerable<(string Label, UnityEngine.Object Target, string PropertyId)> AddableProperties(
            GameObject gameObject)
        {
            yield return ("Active", gameObject, TweenPoseProperties.Active);
            Transform transform = gameObject.transform;
            if (transform is RectTransform rect)
            {
                yield return ("RectTransform/Anchored Position", rect, TweenPoseProperties.AnchoredPosition);
                yield return ("RectTransform/Anchored Position 3D", rect, TweenPoseProperties.AnchoredPosition3D);
                yield return ("RectTransform/Size Delta", rect, TweenPoseProperties.SizeDelta);
                yield return ("RectTransform/Pivot", rect, TweenPoseProperties.Pivot);
                yield return ("RectTransform/Anchor Min", rect, TweenPoseProperties.AnchorMin);
                yield return ("RectTransform/Anchor Max", rect, TweenPoseProperties.AnchorMax);
            }

            yield return ("Transform/Local Position", transform, TweenPoseProperties.LocalPosition);
            yield return ("Transform/World Position", transform, TweenPoseProperties.Position);
            yield return ("Transform/Local Rotation", transform, TweenPoseProperties.LocalRotation);
            yield return ("Transform/World Rotation", transform, TweenPoseProperties.Rotation);
            yield return ("Transform/Local Scale", transform, TweenPoseProperties.LocalScale);

            foreach (Component component in gameObject.GetComponents<Component>())
            {
                string type = component.GetType().Name;
                switch (component)
                {
                    case CanvasGroup:
                        yield return ($"{type}/Alpha", component, TweenPoseProperties.Alpha);
                        break;
                    case Graphic:
                        yield return ($"{type}/Alpha", component, TweenPoseProperties.Alpha);
                        yield return ($"{type}/Color", component, TweenPoseProperties.Color);
                        if (component is Image)
                            yield return ($"{type}/Fill Amount", component, TweenPoseProperties.FillAmount);
                        break;
                    case SpriteRenderer:
                        yield return ($"{type}/Alpha", component, TweenPoseProperties.Alpha);
                        yield return ($"{type}/Color", component, TweenPoseProperties.Color);
                        break;
                    case Renderer:
                        yield return ($"{type}/Color", component, TweenPoseProperties.Color);
                        break;
                }
            }
        }

        private void Capture()
        {
            Edit("Capture UI Motion initial pose", () => _player.CaptureInitialValues());
        }

        private void ClearPose()
        {
            Edit("Clear UI Motion initial pose", () => _player.ClearCapturedInitialValues());
        }

        private void Restore(GameObject only)
        {
            _owner.ApplyModifiedProperties();
            _player.InvalidateAuthoringCache();
            UnityEngine.Object[] changed = _player.GetCapturedInitialPoseEntries()
                .Select(entry => entry.Target)
                .Where(target => target != null && (only == null || target == only ||
                                                    target is Component component && component.gameObject == only))
                .Distinct().ToArray();
            if (changed.Length > 0)
                Undo.RegisterCompleteObjectUndo(changed, "Restore UI Motion initial pose");
            if (only != null)
                _player.RestoreInitialValuesOf(only);
            else
                _player.RestoreInitialValues();
            TweenInspectorUI.MarkDirty(changed);
            SceneView.RepaintAll();
            Changed();
        }

        /// <summary>Records one change to the player's pose as an Undo step and redraws.</summary>
        internal void Edit(string undoName, Action change)
        {
            if (_player == null)
                return;
            _owner.ApplyModifiedProperties();
            Undo.RecordObject(_player, undoName);
            change();
            _player.InvalidateAuthoringCache();
            TweenInspectorUI.MarkDirty(new UnityEngine.Object[] { _player });
            Changed();
        }

        private void Changed()
        {
            _owner.Update();
            _signature = null;
            Refresh();
        }

        internal static string TargetPath(TweenPlayer player, UnityEngine.Object target)
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
                return $"{transform.name} (this TweenPlayer)";
            return transform.IsChildOf(player.transform)
                ? AnimationUtility.CalculateTransformPath(transform, player.transform)
                : transform.name + " (external)";
        }
    }

    /// <summary>One saved property of one object: its value, Restore and remove.</summary>
    [CustomPropertyDrawer(typeof(TweenInitialValue))]
    public sealed class TweenInitialValueDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedObject owner = property.serializedObject;
            string path = property.propertyPath;
            var player = owner.targetObjects.Length == 1 ? owner.targetObject as TweenPlayer : null;
            UnityEngine.Object target = property.FindPropertyRelative("target").objectReferenceValue;
            string id = property.FindPropertyRelative("propertyId").stringValue ?? string.Empty;
            SerializedProperty value = ValueProperty(property, property.FindPropertyRelative("valueType"));
            bool editable = player != null && !Application.isPlaying;

            VisualElement row = TweenInspectorUI.Row();
            if (value != null)
            {
                var field = new PropertyField(value, Label(id, target)) { tooltip = Tooltip(id, target, player) };
                field.style.flexGrow = 1f;
                field.style.flexShrink = 1f;
                row.Add(field);
            }
            else
            {
                var warning = new HelpBox($"Unsupported initial value: {id}", HelpBoxMessageType.Warning);
                warning.style.flexGrow = 1f;
                row.Add(warning);
            }

            Button restore = TweenInspectorUI.Button("↺", () => Restore(owner, path, player), 24f);
            restore.tooltip = "Apply this saved value.";
            restore.SetEnabled(editable && target != null && value != null);
            row.Add(restore);

            Button remove = TweenInspectorUI.Button("✕", null, 24f);
            remove.tooltip = "Stop saving this property.";
            remove.clicked += () =>
            {
                int index = TweenInspectorUI.ArrayIndex(path);
                row.GetFirstAncestorOfType<TweenInitialPoseView>()?.Edit("Remove UI Motion initial value",
                    () => player.RemoveInitialValueAt(index));
            };
            remove.SetEnabled(editable);
            row.Add(remove);

            row.RegisterCallback<SerializedPropertyChangeEvent>(_ => player?.InvalidateAuthoringCache());
            return row;
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
                "Bool" => property.FindPropertyRelative("boolValue"),
                _ => null
            };
        }

        private static void Restore(SerializedObject owner, string path, TweenPlayer player)
        {
            int index = TweenInspectorUI.ArrayIndex(path);
            UnityEngine.Object changed = owner.FindProperty(path)?.FindPropertyRelative("target").objectReferenceValue;
            if (player == null || index < 0 || changed == null)
                return;

            owner.ApplyModifiedProperties();
            player.InvalidateAuthoringCache();
            Undo.RegisterCompleteObjectUndo(changed, "Restore UI Motion initial value");
            if (player.RestoreInitialValueAt(index))
                TweenInspectorUI.MarkDirty(new[] { changed });
            SceneView.RepaintAll();
        }

        /// <summary>Property name, plus the component when it is not the Transform or the GameObject.</summary>
        private static string Label(string propertyId, UnityEngine.Object target)
        {
            string name = PropertyName(propertyId);
            return target is Component component && component is not Transform
                ? $"{name} · {component.GetType().Name}"
                : name;
        }

        private static string Tooltip(string propertyId, UnityEngine.Object target, TweenPlayer player)
        {
            if (propertyId == TweenPoseProperties.Active && player != null && target == player.gameObject)
                return "Play mode applies it only while the scene loads: a player left active in the scene is switched off. Once code activates the object later, the pose no longer switches it off.";
            return propertyId;
        }

        private static string PropertyName(string propertyId)
        {
            return propertyId switch
            {
                TweenPoseProperties.Active => "Active",
                TweenPoseProperties.LocalPosition => "Local Position",
                TweenPoseProperties.Position => "World Position",
                TweenPoseProperties.LocalScale => "Local Scale",
                TweenPoseProperties.LocalRotation => "Local Rotation",
                TweenPoseProperties.Rotation => "World Rotation",
                TweenPoseProperties.AnchorMin => "Anchor Min",
                TweenPoseProperties.AnchorMax => "Anchor Max",
                TweenPoseProperties.AnchoredPosition => "Anchored Position",
                TweenPoseProperties.AnchoredPosition3D => "Anchored Position 3D",
                TweenPoseProperties.SizeDelta => "Size Delta",
                TweenPoseProperties.Pivot => "Pivot",
                TweenPoseProperties.Alpha => "Alpha",
                TweenPoseProperties.Color => "Color",
                TweenPoseProperties.FillAmount => "Fill Amount",
                _ => string.IsNullOrEmpty(propertyId) ? "Unknown Property" : propertyId
            };
        }
    }
}
