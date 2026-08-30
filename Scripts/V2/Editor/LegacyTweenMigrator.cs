using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UIMotionComposer.Tweening;
using UnityEditor;
using UnityEngine;

namespace UIMotionComposer.V2.Editor
{
    public static class LegacyTweenMigrator
    {
        private const string MenuRoot = "Tools/UI Motion Composer V2/";

        [MenuItem(MenuRoot + "Migrate selected legacy preset assets")]
        private static void MigrateSelectedPresets()
        {
            UIAnimationPresetSO[] presets = Selection.objects.OfType<UIAnimationPresetSO>().ToArray();
            if (presets.Length == 0)
            {
                EditorUtility.DisplayDialog("UI Motion Composer V2",
                    "Select one or more legacy UIAnimationPreset assets first.", "OK");
                return;
            }

            var created = new List<TweenAnimationAsset>();
            foreach (UIAnimationPresetSO preset in presets)
            {
                string sourcePath = AssetDatabase.GetAssetPath(preset);
                string folder = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? "Assets";
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{preset.name}_V2.asset");
                var asset = ScriptableObject.CreateInstance<TweenAnimationAsset>();
                asset.Clips.AddRange(Convert(preset.AnimationData));
                AssetDatabase.CreateAsset(asset, path);
                created.Add(asset);
            }

            AssetDatabase.SaveAssets();
            Selection.objects = created.Cast<UnityEngine.Object>().ToArray();
            Debug.Log($"[UI Motion Composer] Migrated {created.Count} preset asset(s) to V2.");
        }

        [MenuItem(MenuRoot + "Migrate selected legacy components")]
        private static void MigrateSelectedComponents()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                EditorUtility.DisplayDialog("UI Motion Composer V2",
                    "Select one or more GameObjects with a legacy panel or clickable controller.", "OK");
                return;
            }

            int migrated = 0;
            foreach (GameObject gameObject in selected)
            {
                BaseUIPanelController panel = gameObject.GetComponent<BaseUIPanelController>();
                BaseUIClickableController clickable = gameObject.GetComponent<BaseUIClickableController>();
                if (panel == null && clickable == null)
                    continue;

                TweenPlayer player = gameObject.GetComponent<TweenPlayer>();
                if (player == null)
                    player = Undo.AddComponent<TweenPlayer>(gameObject);

                Undo.RecordObject(player, "Migrate UI Motion Composer component");
                if (panel != null)
                {
                    AddAnimation(player, TweenIds.Show, panel.CurrentShowAnimationData);
                    AddAnimation(player, TweenIds.Hide, panel.CurrentHideAnimationData);
                }

                if (clickable != null)
                {
                    AddAnimation(player, TweenIds.Hover, clickable.CurrentHoverAnimationData);
                    AddAnimation(player, TweenIds.Click, clickable.CurrentClickAnimationData);
                    AddAnimation(player, TweenIds.Disabled, clickable.CurrentDisableAnimationData);
                    AddAnimation(player, TweenIds.Unhover, clickable.CurrentReturnFromHoverAnimationData);
                    AddAnimation(player, "Return From Click", clickable.CurrentReturnFromClickAnimationData);
                    AddAnimation(player, TweenIds.Interactable, clickable.CurrentReturnFromDisableAnimationData);
                }

                TempValues legacyValues = panel != null && panel.HasStoredStartValues
                    ? panel.StoredStartValues
                    : clickable != null && clickable.HasStoredStartValues
                        ? clickable.StoredStartValues
                        : null;
                if (legacyValues != null)
                    player.ImportLegacyInitialValues(legacyValues);
                else
                    player.CaptureInitialValues();

                EditorUtility.SetDirty(player);
                migrated++;
            }

            Debug.Log($"[UI Motion Composer] Added/updated TweenPlayer V2 on {migrated} selected object(s). Legacy components were kept for safe comparison.");
        }

        [MenuItem(MenuRoot + "Migrate selected legacy components", true)]
        private static bool ValidateMigrateSelectedComponents()
        {
            return Selection.gameObjects.Any(gameObject =>
                gameObject.GetComponent<BaseUIPanelController>() != null ||
                gameObject.GetComponent<BaseUIClickableController>() != null);
        }

        public static List<BaseTweenClip> Convert(AnimationData data)
        {
            var result = new List<BaseTweenClip>();
            if (data == null)
                return result;

            float duration = Mathf.Max(0f, data.Duration);
            AddAlpha(result, data.Alpha, duration);
            AddVector3(result, data.Position, duration, () => new AnchorPosition3DTweenClip(), "Position");
            AddVector3(result, data.Rotation, duration, () => new RotateTweenClip(), "Rotation");
            AddVector3(result, data.Scale, duration, () => new ScaleTweenClip(), "Scale");
            AddVector2(result, data.Size, duration, () => new SizeDeltaTweenClip(), "Size");
            AddVector2(result, data.Pivot, duration, () => new PivotTweenClip(), "Pivot");
            return result;
        }

        private static void AddAnimation(TweenPlayer player, string id, AnimationData source)
        {
            if (source == null)
                return;

            TweenAnimation existing = player.FindAnimation(id);
            if (existing == null)
            {
                existing = new TweenAnimation { Id = id };
                player.AnimationDefinitions.Add(existing);
            }

            existing.Clips = Convert(source);
            existing.Asset = null;
            existing.Playback.UnscaledTime = true;
            existing.Playback.AllowSelfOverride = true;
        }

        private static void AddAlpha(ICollection<BaseTweenClip> output, AlphaAnimationHandler handler, float totalDuration)
        {
            if (handler == null || !handler.IsEnabled)
                return;

            var clip = new FadeTweenClip
            {
                Label = "Alpha",
                FromMode = Map(handler.InitialMode),
                FromValue = handler.InitialValue,
                FromOffset = handler.InitialOffset,
                ToMode = Map(handler.TargetMode),
                ToValue = handler.TargetValue,
                ToOffset = handler.TargetOffset,
                FadeTarget = TweenFadeTarget.CanvasGroup
            };
            ConfigureTiming(clip, handler.Unified, totalDuration);
            output.Add(clip);
        }

        private static void AddVector3<THandler>(ICollection<BaseTweenClip> output, THandler handler,
            float totalDuration, Func<Vector3TweenClip> factory, string label)
            where THandler : TransformAnimationHandler
        {
            if (handler == null || !handler.IsEnabled)
                return;

            if (handler.IsUnified)
            {
                Vector3TweenClip clip = factory();
                ConfigureVector3(clip, handler, TweenVectorComponents.All, label);
                ConfigureTiming(clip, handler.Unified, totalDuration);
                output.Add(clip);
                return;
            }

            AnimationProcessData[] axes = { handler.Separate.XAxis, handler.Separate.YAxis, handler.Separate.ZAxis };
            TweenVectorComponents[] masks = { TweenVectorComponents.X, TweenVectorComponents.Y, TweenVectorComponents.Z };
            string[] names = { "X", "Y", "Z" };
            for (int i = 0; i < axes.Length; i++)
            {
                Vector3TweenClip clip = factory();
                ConfigureVector3(clip, handler, masks[i], $"{label} {names[i]}");
                ConfigureTiming(clip, axes[i], totalDuration);
                output.Add(clip);
            }
        }

        private static void AddVector2<THandler>(ICollection<BaseTweenClip> output, THandler handler,
            float totalDuration, Func<Vector2TweenClip> factory, string label)
            where THandler : Transform2DAnimationHandler
        {
            if (handler == null || !handler.IsEnabled)
                return;

            if (handler.IsUnified)
            {
                Vector2TweenClip clip = factory();
                ConfigureVector2(clip, handler, TweenVectorComponents.All2D, label);
                ConfigureTiming(clip, handler.Unified, totalDuration);
                output.Add(clip);
                return;
            }

            AnimationProcessData[] axes = { handler.Separate2D.XAxis, handler.Separate2D.YAxis };
            TweenVectorComponents[] masks = { TweenVectorComponents.X, TweenVectorComponents.Y };
            string[] names = { "X", "Y" };
            for (int i = 0; i < axes.Length; i++)
            {
                Vector2TweenClip clip = factory();
                ConfigureVector2(clip, handler, masks[i], $"{label} {names[i]}");
                ConfigureTiming(clip, axes[i], totalDuration);
                output.Add(clip);
            }
        }

        private static void ConfigureVector3(Vector3TweenClip clip, TransformAnimationHandler handler,
            TweenVectorComponents components, string label)
        {
            clip.Label = label;
            clip.Components = components;
            clip.FromMode = Map(handler.InitialMode);
            clip.FromValue = handler.InitialValue;
            clip.FromOffset = handler.InitialOffset;
            clip.ToMode = Map(handler.TargetMode);
            clip.ToValue = handler.TargetValue;
            clip.ToOffset = handler.TargetOffset;
        }

        private static void ConfigureVector2(Vector2TweenClip clip, Transform2DAnimationHandler handler,
            TweenVectorComponents components, string label)
        {
            clip.Label = label;
            clip.Components = components;
            clip.FromMode = Map(handler.InitialMode);
            clip.FromValue = handler.InitialValue;
            clip.FromOffset = handler.InitialOffset;
            clip.ToMode = Map(handler.TargetMode);
            clip.ToValue = handler.TargetValue;
            clip.ToOffset = handler.TargetOffset;
        }

        private static void ConfigureTiming(BaseTweenClip clip, AnimationProcessData process, float totalDuration)
        {
            process ??= new AnimationProcessData();
            float start = Mathf.Clamp01(Mathf.Min(process.Timeline.x, process.Timeline.y));
            float end = Mathf.Clamp01(Mathf.Max(process.Timeline.x, process.Timeline.y));
            clip.Delay = start * totalDuration;
            clip.Duration = (end - start) * totalDuration;
            clip.Ease = process.Ease;
            clip.UseCustomCurve = process.CurveMode == CurveMode.Curve;
            clip.CustomCurve = process.Curve == null
                ? AnimationCurve.Linear(0f, 0f, 1f, 1f)
                : new AnimationCurve(process.Curve.keys);
        }

        private static TweenEndpointMode Map(InitialValueMode mode)
        {
            return mode switch
            {
                InitialValueMode.Current => TweenEndpointMode.Current,
                InitialValueMode.Custom => TweenEndpointMode.Custom,
                InitialValueMode.OffsetFromStored => TweenEndpointMode.OffsetFromInitial,
                _ => TweenEndpointMode.Current
            };
        }

        private static TweenEndpointMode Map(TargetValueMode mode)
        {
            return mode switch
            {
                TargetValueMode.StoredInitial => TweenEndpointMode.Initial,
                TargetValueMode.Custom => TweenEndpointMode.Custom,
                TargetValueMode.OffsetFromStored => TweenEndpointMode.OffsetFromInitial,
                _ => TweenEndpointMode.Initial
            };
        }
    }
}
