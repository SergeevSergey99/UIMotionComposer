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

                ValidateTargetSlot(player, animation, canvasGroup);

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

                ValidateAuthoringFingerprint(player);
                ValidateAnimationModeRestore(rect);

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

        private static void ValidateTargetSlot(TweenPlayer player, TweenAnimation animation,
            CanvasGroup playerCanvasGroup)
        {
            var child = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
            child.transform.SetParent(player.transform, false);
            CanvasGroup childCanvasGroup = child.GetComponent<CanvasGroup>();
            childCanvasGroup.alpha = 0.2f;
            playerCanvasGroup.alpha = 0.8f;

            animation.Clips.Clear();
            animation.Clips.Add(new FadeTweenClip
            {
                TargetKey = "Content",
                FadeTarget = TweenFadeTarget.CanvasGroup,
                FromMode = TweenEndpointMode.Custom,
                FromValue = 0f,
                ToMode = TweenEndpointMode.Custom,
                ToValue = 1f,
                Ease = UIEase.Linear,
                Duration = 1f
            });

            player.TargetOverrideDefinitions.Clear();
            Require(player.PreparePreview("Validation").Length == 0,
                "An unbound target slot unexpectedly fell back to the TweenPlayer root.");
            player.StopPreview();

            player.TargetOverrideDefinitions.Add(new TweenTargetOverride
            {
                Key = "Content",
                Target = child
            });
            Require(player.PreparePreview("Validation").Length == 1,
                "A bound target slot did not resolve its player-local object.");
            Require(player.SamplePreparedPreview(0.5f), "A bound target slot could not be sampled.");
            Require(Mathf.Abs(childCanvasGroup.alpha - 0.5f) < 0.001f,
                "A bound target slot animated the wrong object.");
            Require(Mathf.Abs(playerCanvasGroup.alpha - 0.8f) < 0.001f,
                "A bound target slot changed the TweenPlayer root.");
            player.StopPreview();
            player.TargetOverrideDefinitions.Clear();
        }

        /// <summary>
        /// The preview refresh only fires when the authoring fingerprint changes, so this pins the
        /// managed-reference value, type and weighted-curve cases that the inspector must observe.
        /// </summary>
        private static void ValidateAuthoringFingerprint(TweenPlayer player)
        {
            player.AnimationDefinitions.Clear();
            var clip = new AnchorPositionTweenClip
            {
                ToMode = TweenEndpointMode.Custom,
                ToValue = new Vector2(10f, 0f),
                Duration = 1f
            };
            player.AnimationDefinitions.Add(new TweenAnimation
            {
                Id = "Fingerprint",
                Clips = { clip }
            });

            using (var source = new SerializedObject(player))
            {
                // Re-read every time: a SerializedProperty grabbed before Update can go stale once
                // the managed reference behind it is replaced.
                int Fingerprint()
                {
                    source.Update();
                    return TweenAuthoringFingerprint.Of(
                        source.FindProperty("animations").GetArrayElementAtIndex(0));
                }

                int beforeValueEdit = Fingerprint();
                clip.ToValue = new Vector2(400f, 0f);

                Require(Fingerprint() != beforeValueEdit,
                    "Authoring fingerprint did not change after editing a [SerializeReference] clip. " +
                    "Preview would keep sampling stale From/To values.");

                // A clip swapped for another type keeps its property path; only the type name moves.
                int beforeTypeSwap = Fingerprint();
                player.AnimationDefinitions[0].Clips[0] = new ScaleTweenClip
                {
                    ToMode = TweenEndpointMode.Custom,
                    ToValue = new Vector3(400f, 0f, 0f),
                    Duration = 1f
                };

                Require(Fingerprint() != beforeTypeSwap,
                    "Authoring fingerprint did not change after swapping the clip type.");

                var curveClip = (ScaleTweenClip)player.AnimationDefinitions[0].Clips[0];
                curveClip.UseCustomCurve = true;
                curveClip.CustomCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(1f, 1f));

                int beforeWeightedCurve = Fingerprint();
                Keyframe[] keys = curveClip.CustomCurve.keys;
                keys[0].weightedMode = WeightedMode.Out;
                keys[0].outWeight = 0.72f;
                curveClip.CustomCurve.keys = keys;
                Require(Fingerprint() != beforeWeightedCurve,
                    "Authoring fingerprint did not change after editing a curve weight.");

                int beforeWrapMode = Fingerprint();
                curveClip.CustomCurve.postWrapMode = WrapMode.PingPong;
                Require(Fingerprint() != beforeWrapMode,
                    "Authoring fingerprint did not change after editing a curve wrap mode.");
            }

            player.AnimationDefinitions.Clear();
            player.InvalidateAuthoringCache();
        }

        private static void ValidateAnimationModeRestore(RectTransform rect)
        {
            Vector2 original = new Vector2(31f, -47f);
            rect.anchoredPosition = original;
            int undoGroup = Undo.GetCurrentGroup();

            using (var previewMode = new TweenPreviewAnimationMode())
            {
                Require(previewMode.TryStart(), "Could not start the isolated preview Animation Mode.");
                previewMode.RegisterTargets(new UnityEngine.Object[] { rect });
                rect.anchoredPosition = new Vector2(900f, 600f);
                previewMode.Stop();
            }

            Require(Vector2.Distance(rect.anchoredPosition, original) < 0.001f,
                "Animation Mode did not restore the previewed RectTransform.");
            Require(Undo.GetCurrentGroup() == undoGroup,
                "Animation Mode preview unexpectedly added an Undo group.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("UI Motion Composer V2 validation: " + message);
        }
    }
}
