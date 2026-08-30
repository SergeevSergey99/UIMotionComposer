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
                animation.Clips.Add(new AnchorPositionTweenClip
                {
                    FromMode = TweenEndpointMode.Custom,
                    FromValue = Vector2.zero,
                    ToMode = TweenEndpointMode.Custom,
                    ToValue = new Vector2(100f, 40f),
                    Ease = UIEase.Linear,
                    Duration = 1f
                });
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

                player.StopPreview();
                Require(Vector2.Distance(rect.anchoredPosition, new Vector2(12f, 34f)) < 0.001f,
                    "Preview did not restore position.");
                Require(Mathf.Abs(canvasGroup.alpha - 0.8f) < 0.001f,
                    "Preview did not restore alpha.");

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
