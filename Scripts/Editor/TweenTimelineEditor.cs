using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UIMotionComposer.Editor
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
        private const float MinimumViewDuration = 0.05f;
        private const float MaximumZoom = 12f;

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
            Playhead,
            Marquee
        }

        private readonly struct ClipGeometry
        {
            public readonly int Index;
            public readonly Rect Block;

            public ClipGeometry(int index, Rect block)
            {
                Index = index;
                Block = block;
            }
        }

        private sealed class TimelineState
        {
            public bool Expanded = true;
            public readonly HashSet<int> SelectedClips = new HashSet<int>();
            public int SelectionAnchor = -1;
            public int ActiveControl;
            public int DragClip = -1;
            public DragMode Mode;
            public float StartMouseX;
            public float StartDelay;
            public float StartDuration;
            public float DragViewDuration;
            public float DragViewStart;
            public int[] DragSelection = Array.Empty<int>();
            public float[] DragStartDelays = Array.Empty<float>();
            public Vector2 MarqueeStart;
            public Vector2 MarqueeCurrent;
            public bool MarqueeAdditive;
            public readonly HashSet<int> MarqueeInitialSelection = new HashSet<int>();
            public int SnapIndex = 2;
            public int UndoGroup = -1;
            public float Zoom = 1f;
            public float ViewStart;
            public Vector2 Scroll;
            public int[] TargetIds = Array.Empty<int>();
        }

        public static void Draw(SerializedObject owner, SerializedProperty clips,
            float normalizedPlayhead = -1f, Action<float> onScrub = null)
        {
            if (owner == null || clips == null)
                return;

            TimelineState state = GetState(owner, clips.propertyPath);
            NormalizeSelection(state, clips.arraySize);
            float totalDuration = CalculateDuration(clips);
            bool hasInfiniteClip = HasInfiniteClip(clips);
            float fullDuration = Mathf.Max(0.25f, totalDuration);
            state.Zoom = Mathf.Clamp(state.Zoom, 1f, MaximumZoom);
            float viewDuration = state.ActiveControl != 0
                ? Mathf.Max(MinimumViewDuration, state.DragViewDuration)
                : Mathf.Max(MinimumViewDuration, fullDuration / state.Zoom);
            float viewStart = state.ActiveControl != 0
                ? state.DragViewStart
                : ClampViewStart(state.ViewStart, fullDuration, viewDuration);
            state.ViewStart = viewStart;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                state.Expanded = EditorGUILayout.Foldout(state.Expanded, "Visual Timeline", true,
                    EditorStyles.foldoutHeader);
                GUILayout.FlexibleSpace();
                GUILayout.Label(hasInfiniteClip ? "∞" : $"{totalDuration:0.###}s",
                    EditorStyles.miniLabel, GUILayout.Width(54f));
                GUILayout.Label("Zoom", EditorStyles.miniLabel, GUILayout.Width(32f));
                EditorGUI.BeginChangeCheck();
                float zoom = GUILayout.HorizontalSlider(state.Zoom, 1f, MaximumZoom,
                    GUILayout.Width(62f));
                if (EditorGUI.EndChangeCheck())
                    SetZoom(state, zoom, fullDuration, 0.5f);
                if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(30f)))
                {
                    state.Zoom = 1f;
                    state.ViewStart = 0f;
                    GUI.changed = true;
                }
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

            DrawSelectionToolbar(owner, clips, state);

            Rect header = EditorGUILayout.GetControlRect(false, HeaderHeight);
            DrawHeader(header, viewStart, viewDuration, fullDuration, normalizedPlayhead, onScrub, state);

            if (state.Zoom > 1.001f)
            {
                EditorGUI.BeginChangeCheck();
                float nextStart = GUILayout.HorizontalScrollbar(state.ViewStart, viewDuration,
                    0f, fullDuration, GUILayout.Height(12f));
                if (EditorGUI.EndChangeCheck())
                {
                    state.ViewStart = ClampViewStart(nextStart, fullDuration, viewDuration);
                    GUI.changed = true;
                }
            }

            float contentHeight = clips.arraySize * RowHeight;
            float viewportHeight = Mathf.Min(contentHeight + 2f, RowHeight * 7f + 2f);
            state.Scroll = EditorGUILayout.BeginScrollView(state.Scroll, false, clips.arraySize > 7,
                GUILayout.Height(viewportHeight));
            Rect rows = GUILayoutUtility.GetRect(1f, contentHeight, GUILayout.ExpandWidth(true));
            var geometries = new List<ClipGeometry>(clips.arraySize);
            for (int i = 0; i < clips.arraySize; i++)
            {
                Rect row = new Rect(rows.x, rows.y + i * RowHeight, rows.width, RowHeight - 1f);
                DrawRow(owner, clips, i, row, viewStart, viewDuration, fullDuration,
                    normalizedPlayhead, state, geometries);
            }
            HandleMarquee(owner, clips, rows, geometries, state);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.LabelField(
                "Click/drag ruler to scrub • Ctrl/Shift selects • drag empty space for marquee • Alt disables snapping",
                EditorStyles.centeredGreyMiniLabel);
        }

        private static void DrawSelectionToolbar(SerializedObject owner, SerializedProperty clips,
            TimelineState state)
        {
            if (state.SelectedClips.Count == 0)
                return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(state.SelectedClips.Count == 1
                        ? "1 clip selected"
                        : $"{state.SelectedClips.Count} clips selected",
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(state.SelectedClips.Count < 2))
                {
                    if (GUILayout.Button("Align start", EditorStyles.toolbarButton, GUILayout.Width(68f)))
                        AlignSelectedStarts(owner, clips, state);
                }

                float nudge = SnapValues[Mathf.Clamp(state.SnapIndex, 0, SnapValues.Length - 1)];
                if (nudge <= 0f)
                    nudge = 0.05f;
                if (GUILayout.Button($"−{nudge:0.##}", EditorStyles.toolbarButton, GUILayout.Width(43f)))
                    NudgeSelection(owner, clips, state, -nudge);
                if (GUILayout.Button($"+{nudge:0.##}", EditorStyles.toolbarButton, GUILayout.Width(43f)))
                    NudgeSelection(owner, clips, state, nudge);

                if (GUILayout.Button("↑", EditorStyles.toolbarButton, GUILayout.Width(24f)))
                    MoveSelectionRows(owner, clips, state, -1);
                if (GUILayout.Button("↓", EditorStyles.toolbarButton, GUILayout.Width(24f)))
                    MoveSelectionRows(owner, clips, state, 1);
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(42f)))
                {
                    state.SelectedClips.Clear();
                    state.SelectionAnchor = -1;
                    GUI.changed = true;
                }
            }
        }

        private static void AlignSelectedStarts(SerializedObject owner, SerializedProperty clips,
            TimelineState state)
        {
            float earliest = float.PositiveInfinity;
            foreach (int index in state.SelectedClips)
            {
                SerializedProperty delay = clips.GetArrayElementAtIndex(index).FindPropertyRelative("Delay");
                if (delay != null)
                    earliest = Mathf.Min(earliest, Mathf.Max(0f, delay.floatValue));
            }

            if (float.IsPositiveInfinity(earliest))
                return;

            RecordTimelineUndo(owner, "Align tween clips");
            foreach (int index in state.SelectedClips)
            {
                SerializedProperty delay = clips.GetArrayElementAtIndex(index).FindPropertyRelative("Delay");
                if (delay != null)
                    delay.floatValue = earliest;
            }
            CommitTimelineChange(owner);
        }

        private static void NudgeSelection(SerializedObject owner, SerializedProperty clips,
            TimelineState state, float amount)
        {
            float minimum = float.PositiveInfinity;
            foreach (int index in state.SelectedClips)
            {
                SerializedProperty delay = clips.GetArrayElementAtIndex(index).FindPropertyRelative("Delay");
                if (delay != null)
                    minimum = Mathf.Min(minimum, delay.floatValue);
            }
            if (float.IsPositiveInfinity(minimum))
                return;

            amount = Mathf.Max(amount, -minimum);
            RecordTimelineUndo(owner, "Nudge tween clips");
            foreach (int index in state.SelectedClips)
            {
                SerializedProperty delay = clips.GetArrayElementAtIndex(index).FindPropertyRelative("Delay");
                if (delay != null)
                    delay.floatValue = Mathf.Max(0f, delay.floatValue + amount);
            }
            CommitTimelineChange(owner);
        }

        private static void MoveSelectionRows(SerializedObject owner, SerializedProperty clips,
            TimelineState state, int direction)
        {
            if (direction == 0 || state.SelectedClips.Count == 0)
                return;

            var selected = new HashSet<int>(state.SelectedClips);
            bool canMove = false;
            foreach (int index in selected)
            {
                int adjacent = index + direction;
                if (adjacent >= 0 && adjacent < clips.arraySize && !selected.Contains(adjacent))
                {
                    canMove = true;
                    break;
                }
            }
            if (!canMove)
                return;

            bool moved = false;
            RecordTimelineUndo(owner, "Reorder tween clips");
            if (direction < 0)
            {
                for (int i = 1; i < clips.arraySize; i++)
                {
                    if (!selected.Contains(i) || selected.Contains(i - 1))
                        continue;
                    clips.MoveArrayElement(i, i - 1);
                    selected.Remove(i);
                    selected.Add(i - 1);
                    moved = true;
                }
            }
            else
            {
                for (int i = clips.arraySize - 2; i >= 0; i--)
                {
                    if (!selected.Contains(i) || selected.Contains(i + 1))
                        continue;
                    clips.MoveArrayElement(i, i + 1);
                    selected.Remove(i);
                    selected.Add(i + 1);
                    moved = true;
                }
            }

            if (!moved)
                return;

            state.SelectedClips.Clear();
            foreach (int index in selected)
                state.SelectedClips.Add(index);
            state.SelectionAnchor = Mathf.Clamp(state.SelectionAnchor + direction, 0, clips.arraySize - 1);
            CommitTimelineChange(owner);
        }

        private static void RecordTimelineUndo(SerializedObject owner, string label)
        {
            Undo.RecordObjects(owner.targetObjects, label);
        }

        private static void CommitTimelineChange(SerializedObject owner)
        {
            owner.ApplyModifiedProperties();
            foreach (UnityEngine.Object target in owner.targetObjects)
            {
                EditorUtility.SetDirty(target);
                if (!EditorUtility.IsPersistent(target))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            GUI.changed = true;
        }

        private static void DrawHeader(Rect rect, float viewStart, float viewDuration,
            float totalDuration, float normalizedPlayhead, Action<float> onScrub, TimelineState state)
        {
            Rect labelRect = new Rect(rect.x, rect.y, Mathf.Min(LabelWidth, rect.width * 0.4f), rect.height);
            Rect timeRect = new Rect(labelRect.xMax, rect.y, Mathf.Max(1f, rect.xMax - labelRect.xMax), rect.height);
            EditorGUI.DrawRect(labelRect, new Color(0.14f, 0.15f, 0.18f, 1f));
            EditorGUI.DrawRect(timeRect, new Color(0.105f, 0.115f, 0.14f, 1f));
            GUI.Label(new Rect(labelRect.x + 6f, labelRect.y + 2f, labelRect.width - 8f, labelRect.height),
                "Clips", EditorStyles.miniBoldLabel);

            float step = NiceStep(viewDuration / 5f);
            float firstTick = Mathf.Ceil(viewStart / step) * step;
            float viewEnd = viewStart + viewDuration;
            for (float time = firstTick; time <= viewEnd + step * 0.25f; time += step)
            {
                float x = TimeToX(time, timeRect, viewStart, viewDuration);
                Handles.color = new Color(1f, 1f, 1f, 0.18f);
                Handles.DrawLine(new Vector3(x, timeRect.yMax - 6f), new Vector3(x, timeRect.yMax));
                GUI.Label(new Rect(x + 2f, timeRect.y + 2f, 48f, 16f), $"{time:0.##}", EditorStyles.miniLabel);
            }

            DrawPlayhead(timeRect, normalizedPlayhead, totalDuration, viewStart, viewDuration);
            HandleViewportNavigation(timeRect, state, totalDuration, viewStart, viewDuration);
            HandlePlayhead(timeRect, normalizedPlayhead, onScrub, state, viewStart,
                viewDuration, totalDuration);
        }

        private static void DrawRow(SerializedObject owner, SerializedProperty clips, int index,
            Rect row, float viewStart, float viewDuration, float totalDuration,
            float normalizedPlayhead, TimelineState state, List<ClipGeometry> geometries)
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
            bool selected = state.SelectedClips.Contains(index);
            if (selected)
            {
                EditorGUI.DrawRect(labelRect, new Color(0.24f, 0.39f, 0.64f, 0.5f));
                EditorGUI.DrawRect(new Rect(timeRect.x, row.y, timeRect.width, row.height),
                    new Color(0.18f, 0.32f, 0.56f, 0.13f));
            }

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

            DrawGrid(timeRect, viewStart, viewDuration);
            HandleViewportNavigation(timeRect, state, totalDuration, viewStart, viewDuration);
            float viewEnd = viewStart + viewDuration;
            float blockX = TimeToX(start, timeRect, viewStart, viewDuration);
            float blockEndX = TimeToX(start + length, timeRect, viewStart, viewDuration);
            bool primaryVisible = start <= viewEnd && (isMarker ? start >= viewStart : start + length >= viewStart);
            Rect block = primaryVisible
                ? ClippedTimelineRect(blockX, blockEndX, row, timeRect)
                : Rect.zero;

            Color color = ClipColor(value.GetType());
            if (!enabled.boolValue)
                color = Color.Lerp(color, Color.gray, 0.65f);
            Rect repeatRegion = Rect.zero;
            if (repeated && !isMarker)
            {
                float repeatEnd = infinite ? viewEnd : value.EndTime;
                float repeatEndX = infinite
                    ? timeRect.xMax
                    : TimeToX(repeatEnd, timeRect, viewStart, viewDuration);
                if (start <= viewEnd && repeatEnd >= viewStart)
                {
                    repeatRegion = ClippedTimelineRect(blockX, repeatEndX, row, timeRect);
                    DrawRepeatRegion(repeatRegion, color, infinite);
                    EditorGUIUtility.AddCursorRect(repeatRegion, MouseCursor.MoveArrow);
                }
            }

            if (primaryVisible)
            {
                EditorGUI.DrawRect(block, color);
                if (isMarker)
                {
                    GUI.Label(block, "◆", EditorStyles.centeredGreyMiniLabel);
                    EditorGUIUtility.AddCursorRect(block, MouseCursor.MoveArrow);
                }
                else
                {
                    EditorGUI.DrawRect(new Rect(block.x, block.y, HandleWidth, block.height),
                        Color.Lerp(color, Color.white, 0.32f));
                    EditorGUI.DrawRect(new Rect(block.xMax - HandleWidth, block.y, HandleWidth, block.height),
                        Color.Lerp(color, Color.white, 0.32f));
                    GUI.Label(block, length <= 0.001f ? "0s" : repeated
                        ? $"{length:0.##}s {(infinite ? "∞" : "↻")}" : $"{length:0.##}s",
                        EditorStyles.centeredGreyMiniLabel);
                    EditorGUIUtility.AddCursorRect(new Rect(block.x, block.y, HandleWidth, block.height),
                        MouseCursor.ResizeHorizontal);
                    EditorGUIUtility.AddCursorRect(new Rect(block.xMax - HandleWidth, block.y, HandleWidth,
                        block.height), MouseCursor.ResizeHorizontal);
                    EditorGUIUtility.AddCursorRect(new Rect(block.x + HandleWidth, block.y,
                        Mathf.Max(0f, block.width - HandleWidth * 2f), block.height), MouseCursor.MoveArrow);
                }
            }

            Rect interactionRect = UnionVisible(block, repeatRegion);
            if (selected && interactionRect.width > 0f)
                DrawSelectionOutline(interactionRect);

            DrawPlayhead(timeRect, normalizedPlayhead, totalDuration, viewStart, viewDuration);
            if (interactionRect.width > 0f)
                geometries.Add(new ClipGeometry(index, interactionRect));
            HandleRow(owner, clips, clip, index, row, labelRect, block, interactionRect,
                viewStart, viewDuration, state, !isMarker && primaryVisible);
        }

        private static void HandleRow(SerializedObject owner, SerializedProperty clips,
            SerializedProperty clip, int index, Rect row, Rect labelRect, Rect block,
            Rect interactionRect, float viewStart, float viewDuration, TimelineState state,
            bool resizable)
        {
            Event current = Event.current;
            int control = GUIUtility.GetControlID((owner.targetObject.GetInstanceID() * 397) ^
                                                   clip.propertyPath.GetHashCode(), FocusType.Passive, row);

            if (current.type == EventType.MouseDown && current.button == 0 &&
                interactionRect.Contains(current.mousePosition))
            {
                UpdateSelectionForClick(state, index, clips.arraySize, current);
                if (!state.SelectedClips.Contains(index))
                {
                    current.Use();
                    return;
                }

                SerializedProperty delay = clip.FindPropertyRelative("Delay");
                SerializedProperty duration = clip.FindPropertyRelative("Duration");
                state.ActiveControl = control;
                state.DragClip = index;
                state.StartMouseX = current.mousePosition.x;
                state.StartDelay = delay.floatValue;
                state.StartDuration = duration?.floatValue ?? 0f;
                state.DragViewDuration = viewDuration;
                state.DragViewStart = viewStart;
                bool onPrimaryBlock = block.width > 0f && block.Contains(current.mousePosition);
                state.Mode = !resizable || !onPrimaryBlock
                    ? DragMode.Move
                    : current.mousePosition.x <= block.x + HandleWidth
                    ? DragMode.ResizeStart
                    : current.mousePosition.x >= block.xMax - HandleWidth
                        ? DragMode.ResizeEnd
                        : DragMode.Move;
                clip.isExpanded = true;

                CaptureDragSelection(clips, state, index);

                Undo.IncrementCurrentGroup();
                state.UndoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Edit tween timeline");
                Undo.RecordObjects(owner.targetObjects, "Edit tween timeline");
                GUIUtility.hotControl = control;
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0 &&
                labelRect.Contains(current.mousePosition))
            {
                UpdateSelectionForClick(state, index, clips.arraySize, current);
                clip.isExpanded = true;
                GUI.changed = true;
                current.Use();
                return;
            }

            if (GUIUtility.hotControl != control || state.ActiveControl != control)
                return;

            if (current.type == EventType.MouseDrag)
            {
                float delta = (current.mousePosition.x - state.StartMouseX) /
                              Mathf.Max(1f, row.width - Mathf.Min(LabelWidth, row.width * 0.4f)) *
                              state.DragViewDuration;
                ApplyDrag(owner, clips, clip, delta, current.alt, state);
                current.Use();
            }
            else if (current.type == EventType.MouseUp || current.type == EventType.Ignore)
            {
                FinishDrag(state, control);
                if (current.type == EventType.MouseUp)
                    current.Use();
            }
        }

        private static void UpdateSelectionForClick(TimelineState state, int index,
            int clipCount, Event current)
        {
            bool toggle = current.control || current.command;
            bool range = current.shift && state.SelectionAnchor >= 0;

            if (range)
            {
                if (!toggle)
                    state.SelectedClips.Clear();
                int from = Mathf.Clamp(Mathf.Min(state.SelectionAnchor, index), 0, clipCount - 1);
                int to = Mathf.Clamp(Mathf.Max(state.SelectionAnchor, index), 0, clipCount - 1);
                for (int i = from; i <= to; i++)
                    state.SelectedClips.Add(i);
            }
            else if (toggle)
            {
                if (!state.SelectedClips.Add(index))
                    state.SelectedClips.Remove(index);
                state.SelectionAnchor = index;
            }
            else
            {
                if (state.SelectedClips.Count != 1 || !state.SelectedClips.Contains(index))
                {
                    state.SelectedClips.Clear();
                    state.SelectedClips.Add(index);
                }
                state.SelectionAnchor = index;
            }

            GUI.changed = true;
        }

        private static void CaptureDragSelection(SerializedProperty clips, TimelineState state,
            int primaryIndex)
        {
            var indices = new List<int>();
            if (state.Mode == DragMode.Move)
            {
                foreach (int index in state.SelectedClips)
                {
                    if (index >= 0 && index < clips.arraySize)
                        indices.Add(index);
                }
            }
            else
            {
                indices.Add(primaryIndex);
            }

            indices.Sort();
            state.DragSelection = indices.ToArray();
            state.DragStartDelays = new float[state.DragSelection.Length];
            for (int i = 0; i < state.DragSelection.Length; i++)
            {
                SerializedProperty delay = clips.GetArrayElementAtIndex(state.DragSelection[i])
                    .FindPropertyRelative("Delay");
                state.DragStartDelays[i] = delay?.floatValue ?? 0f;
            }
        }

        private static void ApplyGroupMove(SerializedProperty clips, float delta, float snap,
            TimelineState state)
        {
            if (state.DragSelection.Length == 0)
                return;

            float minimumStart = float.PositiveInfinity;
            for (int i = 0; i < state.DragStartDelays.Length; i++)
                minimumStart = Mathf.Min(minimumStart, state.DragStartDelays[i]);
            delta = Mathf.Max(delta, -minimumStart);

            int primary = Array.IndexOf(state.DragSelection, state.DragClip);
            if (primary < 0)
                primary = 0;
            float primaryStart = state.DragStartDelays[primary];
            float snappedPrimary = Snap(Mathf.Max(0f, primaryStart + delta), snap);
            float effectiveDelta = Mathf.Max(snappedPrimary - primaryStart, -minimumStart);

            for (int i = 0; i < state.DragSelection.Length; i++)
            {
                int index = state.DragSelection[i];
                if (index < 0 || index >= clips.arraySize)
                    continue;
                SerializedProperty delay = clips.GetArrayElementAtIndex(index).FindPropertyRelative("Delay");
                if (delay != null)
                    delay.floatValue = Mathf.Max(0f, state.DragStartDelays[i] + effectiveDelta);
            }
        }

        private static void HandleMarquee(SerializedObject owner, SerializedProperty clips,
            Rect rows, IReadOnlyList<ClipGeometry> geometries, TimelineState state)
        {
            Event current = Event.current;
            int control = GUIUtility.GetControlID((owner.targetObject.GetInstanceID() * 613) ^
                                                   clips.propertyPath.GetHashCode(),
                FocusType.Passive, rows);

            if (current.type == EventType.MouseDown && current.button == 0 &&
                rows.Contains(current.mousePosition))
            {
                state.ActiveControl = control;
                state.Mode = DragMode.Marquee;
                state.MarqueeStart = current.mousePosition;
                state.MarqueeCurrent = current.mousePosition;
                state.MarqueeAdditive = current.control || current.command || current.shift;
                state.MarqueeInitialSelection.Clear();
                foreach (int index in state.SelectedClips)
                    state.MarqueeInitialSelection.Add(index);
                if (!state.MarqueeAdditive)
                    state.SelectedClips.Clear();
                GUIUtility.hotControl = control;
                current.Use();
            }

            if (GUIUtility.hotControl == control && state.ActiveControl == control &&
                state.Mode == DragMode.Marquee)
            {
                if (current.type == EventType.MouseDrag)
                {
                    state.MarqueeCurrent = current.mousePosition;
                    Rect selection = FromPoints(state.MarqueeStart, state.MarqueeCurrent);
                    state.SelectedClips.Clear();
                    if (state.MarqueeAdditive)
                    {
                        foreach (int index in state.MarqueeInitialSelection)
                            state.SelectedClips.Add(index);
                    }
                    for (int i = 0; i < geometries.Count; i++)
                    {
                        ClipGeometry geometry = geometries[i];
                        if (selection.Overlaps(geometry.Block, true))
                            state.SelectedClips.Add(geometry.Index);
                    }
                    GUI.changed = true;
                    current.Use();
                }
                else if (current.type == EventType.MouseUp || current.type == EventType.Ignore)
                {
                    if (state.SelectedClips.Count > 0)
                        state.SelectionAnchor = SmallestSelected(state);
                    else
                        state.SelectionAnchor = -1;
                    FinishDrag(state, control);
                    if (current.type == EventType.MouseUp)
                        current.Use();
                }
            }

            if (state.Mode == DragMode.Marquee && state.ActiveControl == control &&
                Event.current.type == EventType.Repaint)
            {
                Rect selection = FromPoints(state.MarqueeStart, state.MarqueeCurrent);
                EditorGUI.DrawRect(selection, new Color(0.25f, 0.52f, 0.9f, 0.18f));
                DrawSelectionOutline(selection);
            }
        }

        private static void ApplyDrag(SerializedObject owner, SerializedProperty clips,
            SerializedProperty clip, float delta, bool disableSnap, TimelineState state)
        {
            SerializedProperty delay = clip.FindPropertyRelative("Delay");
            SerializedProperty duration = clip.FindPropertyRelative("Duration");
            float snap = disableSnap ? 0f : SnapValues[Mathf.Clamp(state.SnapIndex, 0, SnapValues.Length - 1)];

            switch (state.Mode)
            {
                case DragMode.Move:
                    ApplyGroupMove(clips, delta, snap, state);
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
            state.DragSelection = Array.Empty<int>();
            state.DragStartDelays = Array.Empty<float>();
            state.Mode = DragMode.None;
            GUIUtility.hotControl = 0;
        }

        private static void HandlePlayhead(Rect timeRect, float normalizedPlayhead,
            Action<float> onScrub, TimelineState state, float viewStart, float viewDuration,
            float totalDuration)
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
                state.DragViewStart = viewStart;
                GUIUtility.hotControl = control;
                Scrub(current.mousePosition.x, timeRect, viewStart, viewDuration,
                    totalDuration, onScrub);
                current.Use();
            }
            else if (GUIUtility.hotControl == control && state.ActiveControl == control &&
                     current.type == EventType.MouseDrag)
            {
                Scrub(current.mousePosition.x, timeRect, state.DragViewStart,
                    state.DragViewDuration, totalDuration, onScrub);
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

        private static void Scrub(float mouseX, Rect timeRect, float viewStart,
            float viewDuration, float totalDuration, Action<float> onScrub)
        {
            float visible = Mathf.InverseLerp(timeRect.x, timeRect.xMax, mouseX);
            float time = viewStart + visible * viewDuration;
            onScrub?.Invoke(totalDuration <= 0f ? 0f : Mathf.Clamp01(time / totalDuration));
            GUI.changed = true;
        }

        private static void DrawGrid(Rect rect, float viewStart, float viewDuration)
        {
            float step = NiceStep(viewDuration / 5f);
            Handles.color = new Color(1f, 1f, 1f, 0.075f);
            float firstTick = Mathf.Ceil(viewStart / step) * step;
            float viewEnd = viewStart + viewDuration;
            for (float time = firstTick; time <= viewEnd + step * 0.25f; time += step)
            {
                float x = TimeToX(time, rect, viewStart, viewDuration);
                Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.yMax));
            }
        }

        private static void DrawPlayhead(Rect timeRect, float normalizedPlayhead,
            float totalDuration, float viewStart, float viewDuration)
        {
            if (normalizedPlayhead < 0f)
                return;

            float time = Mathf.Clamp01(normalizedPlayhead) * totalDuration;
            if (time < viewStart || time > viewStart + viewDuration)
                return;

            float x = TimeToX(time, timeRect, viewStart, viewDuration);
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

        private static void DrawSelectionOutline(Rect rect)
        {
            Color color = new Color(0.42f, 0.72f, 1f, 0.95f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private static Rect ClippedTimelineRect(float startX, float endX, Rect row, Rect timeRect)
        {
            float left = Mathf.Clamp(Mathf.Min(startX, endX), timeRect.x, timeRect.xMax);
            float right = Mathf.Clamp(Mathf.Max(startX, endX), timeRect.x, timeRect.xMax);
            if (right - left < MinimumBlockWidth)
            {
                left = Mathf.Clamp(left, timeRect.x, timeRect.xMax - MinimumBlockWidth);
                right = Mathf.Min(timeRect.xMax, left + MinimumBlockWidth);
            }
            return new Rect(left, row.y + 5f, Mathf.Max(0f, right - left), row.height - 10f);
        }

        private static Rect UnionVisible(Rect first, Rect second)
        {
            if (first.width <= 0f)
                return second;
            if (second.width <= 0f)
                return first;
            return Rect.MinMaxRect(Mathf.Min(first.xMin, second.xMin), Mathf.Min(first.yMin, second.yMin),
                Mathf.Max(first.xMax, second.xMax), Mathf.Max(first.yMax, second.yMax));
        }

        private static void HandleViewportNavigation(Rect timeRect, TimelineState state,
            float totalDuration, float viewStart, float viewDuration)
        {
            Event current = Event.current;
            if (current.type != EventType.ScrollWheel || !timeRect.Contains(current.mousePosition))
                return;

            if (current.control || current.command)
            {
                float anchor = Mathf.InverseLerp(timeRect.x, timeRect.xMax, current.mousePosition.x);
                float factor = Mathf.Pow(1.18f, -current.delta.y);
                SetZoom(state, state.Zoom * factor, totalDuration, anchor);
                current.Use();
            }
            else if (current.shift && state.Zoom > 1.001f)
            {
                state.ViewStart = ClampViewStart(viewStart + current.delta.y * viewDuration * 0.08f,
                    totalDuration, viewDuration);
                GUI.changed = true;
                current.Use();
            }
        }

        private static void SetZoom(TimelineState state, float zoom, float totalDuration,
            float anchor)
        {
            totalDuration = Mathf.Max(0.25f, totalDuration);
            float oldDuration = Mathf.Max(MinimumViewDuration, totalDuration / state.Zoom);
            float anchorTime = state.ViewStart + oldDuration * Mathf.Clamp01(anchor);
            state.Zoom = Mathf.Clamp(zoom, 1f, MaximumZoom);
            float nextDuration = Mathf.Max(MinimumViewDuration, totalDuration / state.Zoom);
            state.ViewStart = ClampViewStart(anchorTime - nextDuration * Mathf.Clamp01(anchor),
                totalDuration, nextDuration);
            GUI.changed = true;
        }

        private static float ClampViewStart(float start, float totalDuration, float viewDuration)
        {
            return Mathf.Clamp(start, 0f, Mathf.Max(0f, totalDuration - viewDuration));
        }

        private static Rect FromPoints(Vector2 first, Vector2 second)
        {
            return Rect.MinMaxRect(Mathf.Min(first.x, second.x), Mathf.Min(first.y, second.y),
                Mathf.Max(first.x, second.x), Mathf.Max(first.y, second.y));
        }

        private static int SmallestSelected(TimelineState state)
        {
            int result = int.MaxValue;
            foreach (int index in state.SelectedClips)
                result = Mathf.Min(result, index);
            return result == int.MaxValue ? -1 : result;
        }

        private static void NormalizeSelection(TimelineState state, int clipCount)
        {
            state.SelectedClips.RemoveWhere(index => index < 0 || index >= clipCount);
            if (state.SelectionAnchor < 0 || state.SelectionAnchor >= clipCount)
                state.SelectionAnchor = SmallestSelected(state);
        }

        private static float TimeToX(float time, Rect rect, float viewStart, float viewDuration)
        {
            return Mathf.LerpUnclamped(rect.x, rect.xMax,
                viewDuration <= 0f ? 0f : (time - viewStart) / viewDuration);
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
