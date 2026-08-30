using System;
using UIMotionComposer.Tweening;
using UnityEditor;
using UnityEngine;

namespace UIMotionComposer.V2.Editor
{
    /// <summary>Small dependency-free smoke suite that can also run from Unity -executeMethod.</summary>
    public static class TweenV2Validation
    {
        [MenuItem("Tools/UI Motion Composer V2/Run V2 smoke tests")]
        public static void Run()
        {
            GameObject gameObject = null;
            TweenAnimationAsset animationAsset = null;
            try
            {
                animationAsset = ScriptableObject.CreateInstance<TweenAnimationAsset>();
                MonoScript assetScript = MonoScript.FromScriptableObject(animationAsset);
                Require(assetScript != null && assetScript.GetClass() == typeof(TweenAnimationAsset),
                    "TweenAnimationAsset does not resolve to its own MonoScript file.");

                gameObject = new GameObject("UI Motion Composer V2 Validation", typeof(RectTransform), typeof(CanvasGroup));
                var rect = gameObject.GetComponent<RectTransform>();
                var canvasGroup = gameObject.GetComponent<CanvasGroup>();
                var player = gameObject.AddComponent<TweenPlayer>();

                rect.anchoredPosition = new Vector2(12f, 34f);
                canvasGroup.alpha = 0.8f;

                var animation = new TweenAnimation { Id = "Validation" };
                var moveClip = new AnchorPositionTweenClip
                {
                    FromMode = TweenEndpointMode.Custom,
                    FromValue = Vector2.zero,
                    ToMode = TweenEndpointMode.Custom,
                    ToValue = new Vector2(100f, 40f),
                    Ease = UIEase.Linear,
                    Duration = 1f
                };
                animation.Clips.Add(moveClip);
                animation.Clips.Add(new FadeTweenClip
                {
                    FadeTarget = TweenFadeTarget.CanvasGroup,
                    FromMode = TweenEndpointMode.Custom,
                    FromValue = 0f,
                    ToMode = TweenEndpointMode.Custom,
                    ToValue = 1f,
                    Ease = UIEase.Linear,
                    Duration = 1f
                });
                player.AnimationDefinitions.Add(animation);

                Require(Mathf.Approximately(player.GetDuration("Validation"), 1f), "Duration calculation failed.");
                Require(player.Preview("Validation", 0.5f), "Preview did not start.");
                Require(Vector2.Distance(rect.anchoredPosition, new Vector2(50f, 20f)) < 0.001f,
                    $"Unexpected midpoint position: {rect.anchoredPosition}.");
                Require(Mathf.Abs(canvasGroup.alpha - 0.5f) < 0.001f,
                    $"Unexpected midpoint alpha: {canvasGroup.alpha}.");

                moveClip.ToValue = new Vector2(200f, 80f);
                Require(player.PreparePreview("Validation").Length == 2,
                    "Preview refresh did not recapture affected targets.");
                Require(player.SamplePreparedPreview(0.5f), "Refreshed preview could not be sampled.");
                Require(Vector2.Distance(rect.anchoredPosition, new Vector2(100f, 40f)) < 0.001f,
                    $"Preview kept stale clip values after editing: {rect.anchoredPosition}.");

                player.StopPreview();
                Require(Vector2.Distance(rect.anchoredPosition, new Vector2(12f, 34f)) < 0.001f,
                    "Preview did not restore position.");
                Require(Mathf.Abs(canvasGroup.alpha - 0.8f) < 0.001f,
                    "Preview did not restore alpha.");

                animation.Clips.Clear();
                animation.Clips.Add(new AnchorPositionTweenClip
                {
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.Initial,
                    Ease = UIEase.Linear,
                    Duration = 1f
                });
                player.CaptureInitialValues();
                Require(player.HasCapturedInitialValues && player.CapturedInitialValueCount == 1,
                    "Serialized Initial snapshot was not captured.");

                rect.anchoredPosition = new Vector2(250f, -80f);
                Require(player.Preview("Validation", 1f), "Initial endpoint preview did not start.");
                Require(Vector2.Distance(rect.anchoredPosition, new Vector2(12f, 34f)) < 0.001f,
                    $"Initial endpoint did not use the authored snapshot: {rect.anchoredPosition}.");
                player.StopPreview();
                Require(Vector2.Distance(rect.anchoredPosition, new Vector2(250f, -80f)) < 0.001f,
                    "Preview did not restore the pose that existed before previewing Initial.");

                var legacyValues = new TempValues
                {
                    position = new Vector3(-75f, 28f, 3f),
                    localRotation = new Vector3(0f, 0f, 15f),
                    localScale = new Vector3(0.9f, 1.1f, 1f),
                    sizeDelta = new Vector2(320f, 180f),
                    pivot = new Vector2(0.25f, 0.75f),
                    alpha = 0.35f
                };
                player.ImportLegacyInitialValues(legacyValues);
                rect.anchoredPosition = Vector2.zero;
                player.Preview("Validation", 1f);
                Require(Vector2.Distance(rect.anchoredPosition, (Vector2)legacyValues.position) < 0.001f,
                    "Legacy authored pose was not imported into the V2 Initial snapshot.");
                player.StopPreview();

                var legacy = new AnimationData { Duration = 2f };
                legacy.Alpha.Mode = SimpleAnimationMode.Unified;
                legacy.Alpha.Unified.Timeline = new Vector2(0.25f, 0.75f);
                var migrated = LegacyTweenMigrator.Convert(legacy);
                Require(migrated.Count == 1, $"Expected one migrated clip, got {migrated.Count}.");
                Require(Mathf.Abs(migrated[0].Delay - 0.5f) < 0.001f, "Legacy delay migration failed.");
                Require(Mathf.Abs(migrated[0].Duration - 1f) < 0.001f, "Legacy duration migration failed.");

                Debug.Log("[UI Motion Composer] V2 smoke tests passed.");
            }
            finally
            {
                if (gameObject != null)
                    UnityEngine.Object.DestroyImmediate(gameObject);
                if (animationAsset != null)
                    UnityEngine.Object.DestroyImmediate(animationAsset);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("UI Motion Composer V2 validation: " + message);
        }
    }
}
