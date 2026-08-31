using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UIMotionComposer.V2.Editor
{
    /// <summary>Compact draggable timeline backed directly by each clip's Delay and Duration.</summary>
    internal static class TweenTimelineEditor
    {
        private const float LabelWidth = 138f;
        private const float HeaderHeight = 24f;
        private const float RowHeight = 32f;
        private const float HandleWidth = 6f;
        private const float MinimumBlockWidth = 7f;
        private const float MinimumDuration = 0.001f;

        private static readonly float[] SnapValues = { 0f, 0.01f, 0.05f, 0.1f, 0.25f };
        private static readonly string[] SnapLabels = { "Off", "0.01s", "0.05s", "0.10s", "0.25s" };
        private static readonly Dictionary<string, TimelineState> States = new Dictionary<string, TimelineState>();
        private const int StateCacheLimit = 64;

        private enum DragMode
        {
            None,
            Move,
            ResizeStart,
            ResizeEnd,
            Playhead
        }

        private sealed class TimelineState
        {
            public bool Expanded = true;
            public int SelectedClip = -1;
            public int ActiveControl;
            public int DragClip = -1;
            public DragMode Mode;
            public float StartMouseX;
            public float StartDelay;
            public float StartDuration;
            public float DragViewDuration;
            public int SnapIndex = 2;
            public int UndoGroup = -1;
            public Vector2 Scroll;
            public int[] TargetIds = Array.Empty<int>();
        }

        public static void Draw(SerializedObject owner, SerializedProperty clips,
            float normalizedPlayhead = -1f, Action<float> onScrub = null)
        {
            if (owner == null || clips == null)
                return;

            TimelineState state = GetState(owner, clips.propertyPath);
            float totalDuration = CalculateDuration(clips);
            bool hasInfiniteClip = HasInfiniteClip(clips);
            float viewDuration = state.ActiveControl != 0
                ? Mathf.Max(0.25f, state.DragViewDuration)
                : Mathf.Max(0.25f, totalDuration);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                state.Expanded = EditorGUILayout.Foldout(state.Expanded, "Visual Timeline", true,
                    EditorStyles.foldoutHeader);
                GUILayout.FlexibleSpace();
                GUILayout.Label(hasInfiniteClip ? "∞" : $"{totalDuration:0.###}s",
                    EditorStyles.miniLabel, GUILayout.Width(54f));
                GUILayout.Label("Snap", EditorStyles.miniLabel, GUILayout.Width(30f));
                state.SnapIndex = EditorGUILayout.Popup(state.SnapIndex, SnapLabels,
                    EditorStyles.toolbarPopup, GUILayout.Width(58f));
            }

            if (!state.Expanded)
                return;

            if (clips.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Add a clip to populate the timeline.", MessageType.None);
                return;
            }

            Rect header = EditorGUILayout.GetControlRect(false, HeaderHeight);
            DrawHeader(header, viewDuration, normalizedPlayhead, onScrub, state);

            float contentHeight = clips.arraySize * RowHeight;
            float viewportHeight = Mathf.Min(contentHeight + 2f, RowHeight * 7f + 2f);
            state.Scroll = EditorGUILayout.BeginScrollView(state.Scroll, false, clips.arraySize > 7,
                GUILayout.Height(viewportHeight));
            Rect rows = GUILayoutUtility.GetRect(1f, contentHeight, GUILayout.ExpandWidth(true));
            for (int i = 0; i < clips.arraySize; i++)
            {
                Rect row = new Rect(rows.x, rows.y + i * RowHeight, rows.width, RowHeight - 1f);
                DrawRow(owner, clips, i, row, viewDuration, normalizedPlayhead, onScrub, state);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.LabelField(
                "Drag to move • drag duration clip edges to resize • markers move only • Alt disables snapping",
                EditorStyles.centeredGreyMiniLabel);
        }

        private static void DrawHeader(Rect rect, float viewDuration, float normalizedPlayhead,
            Action<float> onScrub, TimelineState state)
        {
            Rect labelRect = new Rect(rect.x, rect.y, Mathf.Min(LabelWidth, rect.width * 0.4f), rect.height);
            Rect timeRect = new Rect(labelRect.xMax, rect.y, Mathf.Max(1f, rect.xMax - labelRect.xMax), rect.height);
            EditorGUI.DrawRect(labelRect, new Color(0.14f, 0.15f, 0.18f, 1f));
            EditorGUI.DrawRect(timeRect, new Color(0.105f, 0.115f, 0.14f, 1f));
            GUI.Label(new Rect(labelRect.x + 6f, labelRect.y + 2f, labelRect.width - 8f, labelRect.height),
                "Clips", EditorStyles.miniBoldLabel);

            float step = NiceStep(viewDuration / 5f);
            for (float time = 0f; time <= viewDuration + step * 0.25f; time += step)
            {
                float x = TimeToX(time, timeRect, viewDuration);
                Handles.color = new Color(1f, 1f, 1f, 0.18f);
                Handles.DrawLine(new Vector3(x, timeRect.yMax - 6f), new Vector3(x, timeRect.yMax));
                GUI.Label(new Rect(x + 2f, timeRect.y + 2f, 48f, 16f), $"{time:0.##}", EditorStyles.miniLabel);
            }

            DrawPlayhead(timeRect, normalizedPlayhead);
            HandlePlayhead(timeRect, normalizedPlayhead, onScrub, state, viewDuration);
        }

        private static void DrawRow(SerializedObject owner, SerializedProperty clips, int index,
            Rect row, float viewDuration, float normalizedPlayhead, Action<float> onScrub,
            TimelineState state)
        {
            SerializedProperty clip = clips.GetArrayElementAtIndex(index);
            if (clip.managedReferenceValue is not BaseTweenClip value)
                return;

            SerializedProperty delay = clip.FindPropertyRelative("Delay");
            SerializedProperty duration = clip.FindPropertyRelative("Duration");
            SerializedProperty enabled = clip.FindPropertyRelative("Enabled");
            float start = Mathf.Max(0f, delay.floatValue);
            bool isMarker = duration == null;
            float length = isMarker ? 0f : Mathf.Max(0f, duration.floatValue);
            bool repeated = value is DurationTweenClip durationClip &&
                            durationClip.RepeatMode != TweenLoopMode.None;
            bool infinite = value.IsInfinite;
            int repeatCount = value is DurationTweenClip repeatedClip
                ? repeatedClip.RepeatCount
                : 1;

            float actualLabelWidth = Mathf.Min(LabelWidth, row.width * 0.4f);
            Rect labelRect = new Rect(row.x, row.y, actualLabelWidth, row.height);
            Rect timeRect = new Rect(labelRect.xMax, row.y, Mathf.Max(1f, row.xMax - labelRect.xMax), row.height);
            Color rowColor = index % 2 == 0
                ? new Color(0.12f, 0.13f, 0.155f, 1f)
                : new Color(0.105f, 0.115f, 0.14f, 1f);
            EditorGUI.DrawRect(row, rowColor);
            if (state.SelectedClip == index)
                EditorGUI.DrawRect(labelRect, new Color(0.24f, 0.39f, 0.64f, 0.42f));

            string label = string.IsNullOrWhiteSpace(value.Label)
                ? ObjectNames.NicifyVariableName(value.GetType().Name.Replace("TweenClip", string.Empty))
                : value.Label;
            GUI.Label(new Rect(labelRect.x + 5f, labelRect.y + 2f, labelRect.width - 8f, 16f),
                new GUIContent(label, value.GetType().Name), EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(labelRect.x + 5f, labelRect.y + 16f, labelRect.width - 8f, 14f),
                isMarker ? $"at {start:0.###}" : infinite
                    ? $"{start:0.###}  →  ∞"
                    : repeated
                        ? $"{start:0.###}  →  {value.EndTime:0.###}  ×{Mathf.Max(1, repeatCount)}"
                        : $"{start:0.###}  →  {start + length:0.###}",
                EditorStyles.centeredGreyMiniLabel);

            DrawGrid(timeRect, viewDuration);
            float blockX = TimeToX(start, timeRect, viewDuration);
            float blockEndX = TimeToX(start + length, timeRect, viewDuration);
            float width = Mathf.Max(MinimumBlockWidth, blockEndX - blockX);
            Rect block = new Rect(blockX, row.y + 5f, width, row.height - 10f);
            block.xMin = Mathf.Clamp(block.xMin, timeRect.x, timeRect.xMax - MinimumBlockWidth);
            block.xMax = Mathf.Clamp(block.xMax, block.xMin + MinimumBlockWidth, timeRect.xMax);

            Color color = ClipColor(value.GetType());
            if (!enabled.boolValue)
                color = Color.Lerp(color, Color.gray, 0.65f);
            if (repeated && !isMarker)
            {
                float repeatEndX = infinite
                    ? timeRect.xMax
                    : TimeToX(value.EndTime, timeRect, viewDuration);
                Rect repeatRegion = new Rect(blockX, row.y + 5f,
                    Mathf.Max(MinimumBlockWidth, repeatEndX - blockX), row.height - 10f);
                repeatRegion.xMin = Mathf.Clamp(repeatRegion.xMin, timeRect.x, timeRect.xMax);
                repeatRegion.xMax = Mathf.Clamp(repeatRegion.xMax, repeatRegion.xMin, timeRect.xMax);
                DrawRepeatRegion(repeatRegion, color, infinite);
            }
            EditorGUI.DrawRect(block, color);
            if (isMarker)
            {
                GUI.Label(block, "◆", EditorStyles.centeredGreyMiniLabel);
                EditorGUIUtility.AddCursorRect(block, MouseCursor.MoveArrow);
            }
            else
            {
                EditorGUI.DrawRect(new Rect(block.x, block.y, HandleWidth, block.height), Color.Lerp(color, Color.white, 0.32f));
                EditorGUI.DrawRect(new Rect(block.xMax - HandleWidth, block.y, HandleWidth, block.height), Color.Lerp(color, Color.white, 0.32f));
                GUI.Label(block, length <= 0.001f ? "0s" : repeated
                    ? $"{length:0.##}s {(infinite ? "∞" : "↻")}" : $"{length:0.##}s",
                    EditorStyles.centeredGreyMiniLabel);
                EditorGUIUtility.AddCursorRect(new Rect(block.x, block.y, HandleWidth, block.height), MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(new Rect(block.xMax - HandleWidth, block.y, HandleWidth, block.height), MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(new Rect(block.x + HandleWidth, block.y,
                    Mathf.Max(0f, block.width - HandleWidth * 2f), block.height), MouseCursor.MoveArrow);
            }

            DrawPlayhead(timeRect, normalizedPlayhead);
            HandleRow(owner, clip, index, row, timeRect, block, viewDuration, onScrub, state,
                !isMarker);
        }

        private static void HandleRow(SerializedObject owner, SerializedProperty clip, int index,
            Rect row, Rect timeRect, Rect block, float viewDuration, Action<float> onScrub,
            TimelineState state, bool resizable)
        {
            Event current = Event.current;
            int control = GUIUtility.GetControlID((owner.targetObject.GetInstanceID() * 397) ^
                                                   clip.propertyPath.GetHashCode(), FocusType.Passive, row);

            if (current.type == EventType.MouseDown && current.button == 0 && block.Contains(current.mousePosition))
            {
                SerializedProperty delay = clip.FindPropertyRelative("Delay");
                SerializedProperty duration = clip.FindPropertyRelative("Duration");
                state.ActiveControl = control;
                state.DragClip = index;
                state.StartMouseX = current.mousePosition.x;
                state.StartDelay = delay.floatValue;
                state.StartDuration = duration?.floatValue ?? 0f;
                state.DragViewDuration = viewDuration;
                state.SelectedClip = index;
                state.Mode = !resizable
                    ? DragMode.Move
                    : current.mousePosition.x <= block.x + HandleWidth
                    ? DragMode.ResizeStart
                    : current.mousePosition.x >= block.xMax - HandleWidth
                        ? DragMode.ResizeEnd
                        : DragMode.Move;
                clip.isExpanded = true;

                Undo.IncrementCurrentGroup();
                state.UndoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Edit tween timeline");
                Undo.RecordObjects(owner.targetObjects, "Edit tween timeline");
                GUIUtility.hotControl = control;
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0 &&
                timeRect.Contains(current.mousePosition) && !block.Contains(current.mousePosition) && onScrub != null)
            {
                state.ActiveControl = control;
                state.Mode = DragMode.Playhead;
                state.DragViewDuration = viewDuration;
                GUIUtility.hotControl = control;
                Scrub(current.mousePosition.x, timeRect, onScrub);
                current.Use();
                return;
            }

            if (GUIUtility.hotControl != control || state.ActiveControl != control)
                return;

            if (current.type == EventType.MouseDrag)
            {
                if (state.Mode == DragMode.Playhead)
                {
                    Scrub(current.mousePosition.x, timeRect, onScrub);
                }
                else
                {
                    float delta = (current.mousePosition.x - state.StartMouseX) /
                                  Mathf.Max(1f, timeRect.width) * state.DragViewDuration;
                    ApplyDrag(owner, clip, delta, current.alt, state);
                }
                current.Use();
            }
            else if (current.type == EventType.MouseUp || current.type == EventType.Ignore)
            {
                FinishDrag(state, control);
                if (current.type == EventType.MouseUp)
                    current.Use();
            }
        }

        private static void ApplyDrag(SerializedObject owner, SerializedProperty clip, float delta,
            bool disableSnap, TimelineState state)
        {
            SerializedProperty delay = clip.FindPropertyRelative("Delay");
            SerializedProperty duration = clip.FindPropertyRelative("Duration");
            float snap = disableSnap ? 0f : SnapValues[Mathf.Clamp(state.SnapIndex, 0, SnapValues.Length - 1)];

            switch (state.Mode)
            {
                case DragMode.Move:
                    delay.floatValue = Snap(Mathf.Max(0f, state.StartDelay + delta), snap);
                    break;
                case DragMode.ResizeStart:
                {
                    if (duration == null)
                        break;
                    float originalEnd = state.StartDelay + state.StartDuration;
                    float nextDelay = Mathf.Clamp(state.StartDelay + delta, 0f,
                        Mathf.Max(0f, originalEnd - MinimumDuration));
                    nextDelay = Mathf.Clamp(Snap(nextDelay, snap), 0f,
                        Mathf.Max(0f, originalEnd - MinimumDuration));
                    delay.floatValue = nextDelay;
                    duration.floatValue = Mathf.Max(0f, originalEnd - nextDelay);
                    break;
                }
                case DragMode.ResizeEnd:
                    if (duration == null)
                        break;
                    duration.floatValue = Snap(Mathf.Max(0f, state.StartDuration + delta), snap);
                    break;
            }

            owner.ApplyModifiedPropertiesWithoutUndo();
            foreach (UnityEngine.Object target in owner.targetObjects)
            {
                EditorUtility.SetDirty(target);
                if (!EditorUtility.IsPersistent(target))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            GUI.changed = true;
        }

        private static void FinishDrag(TimelineState state, int control)
        {
            if (state.UndoGroup >= 0)
                Undo.CollapseUndoOperations(state.UndoGroup);
            state.UndoGroup = -1;
            state.ActiveControl = 0;
            state.DragClip = -1;
            state.Mode = DragMode.None;
            GUIUtility.hotControl = 0;
        }

        private static void HandlePlayhead(Rect timeRect, float normalizedPlayhead,
            Action<float> onScrub, TimelineState state, float viewDuration)
        {
            if (onScrub == null)
                return;

            Event current = Event.current;
            int control = GUIUtility.GetControlID("TweenTimelinePlayhead".GetHashCode(), FocusType.Passive, timeRect);
            if (current.type == EventType.MouseDown && current.button == 0 && timeRect.Contains(current.mousePosition))
            {
                state.ActiveControl = control;
                state.Mode = DragMode.Playhead;
                state.DragViewDuration = viewDuration;
                GUIUtility.hotControl = control;
                Scrub(current.mousePosition.x, timeRect, onScrub);
                current.Use();
            }
            else if (GUIUtility.hotControl == control && state.ActiveControl == control &&
                     current.type == EventType.MouseDrag)
            {
                Scrub(current.mousePosition.x, timeRect, onScrub);
                current.Use();
            }
            else if (GUIUtility.hotControl == control && state.ActiveControl == control &&
                     (current.type == EventType.MouseUp || current.type == EventType.Ignore))
            {
                FinishDrag(state, control);
                if (current.type == EventType.MouseUp)
                    current.Use();
            }
        }

        private static void Scrub(float mouseX, Rect timeRect, Action<float> onScrub)
        {
            onScrub?.Invoke(Mathf.InverseLerp(timeRect.x, timeRect.xMax, mouseX));
            GUI.changed = true;
        }

        private static void DrawGrid(Rect rect, float duration)
        {
            float step = NiceStep(duration / 5f);
            Handles.color = new Color(1f, 1f, 1f, 0.075f);
            for (float time = 0f; time <= duration + step * 0.25f; time += step)
            {
                float x = TimeToX(time, rect, duration);
                Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.yMax));
            }
        }

        private static void DrawPlayhead(Rect timeRect, float normalizedPlayhead)
        {
            if (normalizedPlayhead < 0f)
                return;

            float x = Mathf.Lerp(timeRect.x, timeRect.xMax, Mathf.Clamp01(normalizedPlayhead));
            Handles.color = new Color(1f, 0.38f, 0.3f, 0.95f);
            Handles.DrawLine(new Vector3(x, timeRect.y), new Vector3(x, timeRect.yMax));
        }

        private static float CalculateDuration(SerializedProperty clips)
        {
            float duration = 0f;
            for (int i = 0; i < clips.arraySize; i++)
            {
                SerializedProperty clip = clips.GetArrayElementAtIndex(i);
                SerializedProperty enabled = clip.FindPropertyRelative("Enabled");
                if (enabled?.boolValue == false || clip.managedReferenceValue is not BaseTweenClip value)
                    continue;
                float end = value.EndTime;
                if (value.IsInfinite && value is DurationTweenClip durationClip)
                    end += Mathf.Max(0.5f, durationClip.Duration * 0.75f);
                duration = Mathf.Max(duration, end);
            }
            return duration;
        }

        private static bool HasInfiniteClip(SerializedProperty clips)
        {
            for (int i = 0; i < clips.arraySize; i++)
            {
                SerializedProperty clip = clips.GetArrayElementAtIndex(i);
                if (clip.FindPropertyRelative("Enabled")?.boolValue != false &&
                    clip.managedReferenceValue is BaseTweenClip { IsInfinite: true })
                    return true;
            }

            return false;
        }

        private static void DrawRepeatRegion(Rect rect, Color color, bool infinite)
        {
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.28f));
            Handles.BeginGUI();
            Color old = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.16f);
            for (float x = rect.x - rect.height; x < rect.xMax; x += 9f)
            {
                float startX = Mathf.Max(rect.x, x);
                float endX = Mathf.Min(rect.xMax, x + rect.height);
                float startY = rect.yMax - (startX - x);
                float endY = rect.yMax - (endX - x);
                Handles.DrawLine(new Vector3(startX, startY), new Vector3(endX, endY));
            }
            Handles.color = old;
            Handles.EndGUI();

            if (infinite)
                GUI.Label(new Rect(rect.xMax - 22f, rect.y, 20f, rect.height), "∞",
                    EditorStyles.centeredGreyMiniLabel);
        }

        private static float TimeToX(float time, Rect rect, float duration)
        {
            return Mathf.Lerp(rect.x, rect.xMax, duration <= 0f ? 0f : time / duration);
        }

        private static float NiceStep(float raw)
        {
            if (raw <= 0f)
                return 0.1f;
            float power = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(raw)));
            float normalized = raw / power;
            float nice = normalized < 1.5f ? 1f : normalized < 3.5f ? 2f : normalized < 7.5f ? 5f : 10f;
            return nice * power;
        }

        private static float Snap(float value, float step)
        {
            return step <= 0f ? value : Mathf.Round(value / step) * step;
        }

        private static Color ClipColor(Type type)
        {
            int hash = type.FullName?.GetHashCode() ?? type.Name.GetHashCode();
            float hue = Mathf.Abs(hash % 997) / 997f;
            Color color = Color.HSVToRGB(hue, 0.48f, 0.82f);
            color.a = 1f;
            return color;
        }

        private static TimelineState GetState(SerializedObject owner, string propertyPath)
        {
            UnityEngine.Object[] targetObjects = owner.targetObjects;
            var ids = new int[targetObjects.Length];
            string targets = string.Empty;
            for (int i = 0; i < targetObjects.Length; i++)
            {
                ids[i] = targetObjects[i] == null ? 0 : targetObjects[i].GetInstanceID();
                targets += ids[i] + ";";
            }

            string key = targets + propertyPath;
            if (!States.TryGetValue(key, out TimelineState state))
            {
                PruneStates();
                state = new TimelineState();
                States.Add(key, state);
            }

            state.TargetIds = ids;
            return state;
        }

        /// <summary>
        /// Drops entries whose objects are all gone. Keyed by instance id, the cache would otherwise
        /// keep one entry per object ever inspected for the life of the editor session.
        /// </summary>
        private static void PruneStates()
        {
            if (States.Count < StateCacheLimit)
                return;

            var dead = new List<string>();
            foreach (KeyValuePair<string, TimelineState> pair in States)
            {
                bool alive = false;
                int[] ids = pair.Value.TargetIds;
                for (int i = 0; i < ids.Length; i++)
                {
                    if (ids[i] != 0 && EditorUtility.InstanceIDToObject(ids[i]) != null)
                    {
                        alive = true;
                        break;
                    }
                }

                if (!alive)
                    dead.Add(pair.Key);
            }

            for (int i = 0; i < dead.Count; i++)
                States.Remove(dead[i]);
        }
    }
}
