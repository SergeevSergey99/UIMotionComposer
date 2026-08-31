using System;
using System.Reflection;
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
                ValidateClipHierarchy(animationAsset);

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
                Require(migrated[0] is DurationTweenClip migratedClip &&
                        Mathf.Abs(migratedClip.Duration - 1f) < 0.001f,
                    "Legacy duration migration failed.");

                ValidateAuthoringFingerprint(player);
                ValidateNestedPlaybackModes(player, canvasGroup);
                ValidateClipRepeatSemantics();
                ValidateBindingConflictDiagnostics();
                ValidateClickableStateMachine();
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

        private static void ValidateNestedPlaybackModes(TweenPlayer parentPlayer,
            CanvasGroup parentCanvasGroup)
        {
            var childObject = new GameObject("Nested Tween Validation", typeof(RectTransform),
                typeof(CanvasGroup), typeof(TweenPlayer));
            childObject.transform.SetParent(parentPlayer.transform, false);
            var childPlayer = childObject.GetComponent<TweenPlayer>();
            var childAnimation = new TweenAnimation { Id = "Child" };
            childAnimation.Clips.Add(new FadeTweenClip
            {
                FadeTarget = TweenFadeTarget.CanvasGroup,
                FromMode = TweenEndpointMode.Custom,
                FromValue = 0f,
                ToMode = TweenEndpointMode.Custom,
                ToValue = 1f,
                Ease = UIEase.Linear,
                Duration = 1f
            });
            childPlayer.AnimationDefinitions.Add(childAnimation);

            var nestedClip = new PlayTweenAnimationClip
            {
                Delay = 0.2f,
                Target = childPlayer,
                AnimationId = childAnimation.Id
            };
            var parentAnimation = new TweenAnimation { Id = "Parent" };
            parentAnimation.Clips.Add(nestedClip);
            parentAnimation.Clips.Add(new FadeTweenClip
            {
                Target = parentCanvasGroup,
                FadeTarget = TweenFadeTarget.CanvasGroup,
                FromMode = TweenEndpointMode.Custom,
                FromValue = 0f,
                ToMode = TweenEndpointMode.Custom,
                ToValue = 1f,
                Ease = UIEase.Linear,
                Duration = 1f
            });
            parentPlayer.AnimationDefinitions.Clear();
            parentPlayer.AnimationDefinitions.Add(parentAnimation);

            nestedClip.Mode = TweenNestedPlaybackMode.Wait;
            object waitPlayback = CreatePlaybackForValidation(parentPlayer, parentAnimation);
            TickPlaybackForValidation(waitPlayback, 0.5f);
            Require(Mathf.Abs(ReadNormalizedTime(waitPlayback) - 0.2f) < 0.001f,
                "Wait did not clamp the parent timeline to the nested marker.");
            Require(childPlayer.IsPlaying(childAnimation.Id),
                "Wait did not start the child animation.");
            childPlayer.Complete(childAnimation.Id);
            TickPlaybackForValidation(waitPlayback, 0.1f);
            Require(Mathf.Abs(ReadNormalizedTime(waitPlayback) - 0.3f) < 0.001f,
                "Wait did not resume the parent after the child completed.");
            StopPlaybackForValidation(waitPlayback, false);

            waitPlayback = CreatePlaybackForValidation(parentPlayer, parentAnimation);
            TickPlaybackForValidation(waitPlayback, 0.5f);
            StopPlaybackForValidation(waitPlayback, false);
            Require(!childPlayer.IsPlaying(childAnimation.Id),
                "Cancelling a waiting parent did not cancel its child.");

            nestedClip.Mode = TweenNestedPlaybackMode.FireAndForget;
            object fireAndForgetPlayback = CreatePlaybackForValidation(parentPlayer, parentAnimation);
            TickPlaybackForValidation(fireAndForgetPlayback, 0.5f);
            StopPlaybackForValidation(fireAndForgetPlayback, false);
            Require(childPlayer.IsPlaying(childAnimation.Id),
                "Fire And Forget child was incorrectly cancelled with its parent.");
            childPlayer.Stop(childAnimation.Id);

            nestedClip.Mode = TweenNestedPlaybackMode.LinkLifetime;
            object linkedPlayback = CreatePlaybackForValidation(parentPlayer, parentAnimation);
            TickPlaybackForValidation(linkedPlayback, 0.5f);
            StopPlaybackForValidation(linkedPlayback, false);
            Require(!childPlayer.IsPlaying(childAnimation.Id),
                "Link Lifetime did not cancel the child with its parent.");

            int childCompletions = 0;
            childAnimation.OnCompleted.AddListener(() => childCompletions++);
            linkedPlayback = CreatePlaybackForValidation(parentPlayer, parentAnimation);
            TickPlaybackForValidation(linkedPlayback, 0.5f);
            StopPlaybackForValidation(linkedPlayback, true);
            Require(childCompletions == 1 && !childPlayer.IsPlaying(childAnimation.Id),
                "Link Lifetime did not complete the child with its parent.");

            childPlayer.StopAll();
            parentPlayer.AnimationDefinitions.Clear();
        }

        private static void ValidateBindingConflictDiagnostics()
        {
            var target = new GameObject("Binding Conflict Validation", typeof(RectTransform),
                typeof(TweenPlayer));
            try
            {
                TweenPlayer player = target.GetComponent<TweenPlayer>();
                var first = new ScaleTweenClip
                {
                    Label = "First scale",
                    Delay = 0f,
                    Duration = 0.8f,
                    ToMode = TweenEndpointMode.Custom,
                    ToValue = Vector3.one * 1.1f
                };
                var second = new ScaleTweenClip
                {
                    Label = "Second scale",
                    Delay = 0.4f,
                    Duration = 0.8f,
                    ToMode = TweenEndpointMode.Custom,
                    ToValue = Vector3.one * 0.9f
                };
                player.AnimationDefinitions.Add(new TweenAnimation
                {
                    Id = "Conflict",
                    Clips = { first, second }
                });

                Require(player.GetBindingConflicts("Conflict").Length == 1,
                    "Overlapping clips on the same property were not reported.");
                second.Delay = 0.8f;
                Require(player.GetBindingConflicts("Conflict").Length == 0,
                    "Touching, non-overlapping clips were reported as a conflict.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void ValidateClipRepeatSemantics()
        {
            var target = new GameObject("Clip Repeat Validation", typeof(RectTransform), typeof(TweenPlayer));
            try
            {
                TweenPlayer player = target.GetComponent<TweenPlayer>();
                Transform transform = target.transform;
                var clip = new ScaleTweenClip
                {
                    FromMode = TweenEndpointMode.Custom,
                    FromValue = Vector3.one,
                    ToMode = TweenEndpointMode.Custom,
                    ToValue = Vector3.one * 2f,
                    Duration = 1f,
                    RepeatMode = TweenLoopMode.Restart,
                    RepeatCount = 3,
                    Ease = UIEase.Linear
                };
                var animation = new TweenAnimation { Id = "Repeat", Clips = { clip } };
                player.AnimationDefinitions.Add(animation);

                Require(Mathf.Approximately(player.GetDuration(animation.Id), 3f),
                    "Finite clip repeats were not included in animation duration.");
                player.PreparePreview(animation.Id);
                player.SamplePreparedPreviewTime(1.5f);
                Require(Vector3.Distance(transform.localScale, Vector3.one * 1.5f) < 0.001f,
                    "Restart repeat did not evaluate its second pass.");
                player.StopPreview();

                clip.RepeatDelay = 0.2f;
                Require(Mathf.Approximately(player.GetDuration(animation.Id), 3.4f),
                    "Repeat Delay was not included between finite clip passes.");
                player.PreparePreview(animation.Id);
                player.SamplePreparedPreviewTime(1.1f);
                Require(Vector3.Distance(transform.localScale, Vector3.one * 2f) < 0.001f,
                    "Clip did not hold its pass endpoint during Repeat Delay.");
                player.SamplePreparedPreviewTime(1.7f);
                Require(Vector3.Distance(transform.localScale, Vector3.one * 1.5f) < 0.001f,
                    "Clip did not resume after Repeat Delay.");
                player.StopPreview();

                clip.RepeatMode = TweenLoopMode.PingPong;
                clip.RepeatCount = 2;
                clip.RepeatDelay = 0f;
                Require(Mathf.Approximately(player.GetDuration(animation.Id), 2f),
                    "Ping-pong repeat duration is incorrect.");
                player.PreparePreview(animation.Id);
                player.SamplePreparedPreviewTime(1.5f);
                Require(Vector3.Distance(transform.localScale, Vector3.one * 1.5f) < 0.001f,
                    "Ping-pong repeat did not reverse its second pass.");
                player.SamplePreparedPreviewTime(2f);
                Require(Vector3.Distance(transform.localScale, Vector3.one) < 0.001f,
                    "Even ping-pong repeat did not finish at its From value.");
                player.StopPreview();

                clip.RepeatMode = TweenLoopMode.Restart;
                clip.RepeatCount = -1;
                Require(player.IsInfinite(animation.Id),
                    "Infinite clip repeat did not mark its animation as infinite.");
                Require(Mathf.Approximately(player.GetDuration(animation.Id), 1f),
                    "Infinite clip should expose one authored cycle as timeline duration.");

                object playback = CreatePlaybackForValidation(player, animation);
                TickPlaybackForValidation(playback, 2.5f);
                Require(ReadIsActive(playback), "Playback completed while an infinite clip was active.");
                Require(Vector3.Distance(transform.localScale, Vector3.one * 1.5f) < 0.001f,
                    "Infinite clip did not evaluate beyond its first authored cycle.");
                StopPlaybackForValidation(playback, false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void ValidateClickableStateMachine()
        {
            var target = new GameObject("Clickable Validation", typeof(RectTransform),
                typeof(CanvasGroup), typeof(TweenPlayer), typeof(TweenUIClickable));
            try
            {
                TweenPlayer player = target.GetComponent<TweenPlayer>();
                TweenUIClickable clickable = target.GetComponent<TweenUIClickable>();
                CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();

                player.AnimationDefinitions.Add(StateAnimation(TweenIds.Hover, new ScaleTweenClip
                {
                    Duration = 1f,
                    FromMode = TweenEndpointMode.Initial,
                    ToMode = TweenEndpointMode.OffsetFromInitial,
                    ToOffset = Vector3.one * 0.1f
                }, true));
                player.AnimationDefinitions.Add(StateAnimation(TweenIds.Unhover, new ScaleTweenClip
                {
                    Duration = 0.2f,
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.Initial
                }));
                player.AnimationDefinitions.Add(StateAnimation(TweenIds.Click, new PunchScaleTweenClip()));
                player.AnimationDefinitions.Add(StateAnimation(TweenIds.Disabled, new FadeTweenClip
                {
                    FadeTarget = TweenFadeTarget.CanvasGroup,
                    Duration = 0.2f,
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.Custom,
                    ToValue = 0.4f
                }));
                player.AnimationDefinitions.Add(StateAnimation(TweenIds.Interactable, new FadeTweenClip
                {
                    FadeTarget = TweenFadeTarget.CanvasGroup,
                    Duration = 0.2f,
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.Initial
                }));
                player.CaptureInitialValues();

                clickable.OnPointerEnter(null);
                Require(clickable.State == TweenClickableState.Hovered && player.IsPlaying(TweenIds.Hover),
                    "Pointer enter did not start Hover.");

                clickable.OnPointerExit(null);
                Require(clickable.State == TweenClickableState.Normal && !player.IsPlaying(TweenIds.Hover) &&
                        player.IsPlaying(TweenIds.Unhover),
                    "Pointer exit did not stop an infinite Hover and start Normal.");

                clickable.OnPointerDown(null);
                Require(clickable.State == TweenClickableState.Pressed && player.IsPlaying(TweenIds.Click),
                    "Pointer down did not enter Pressed.");

                clickable.SetInteractable(false);
                Require(!canvasGroup.interactable && clickable.State == TweenClickableState.Disabled &&
                        player.IsPlaying(TweenIds.Disabled),
                    "SetInteractable(false) did not enter Disabled.");

                clickable.SetInteractable(true);
                Require(canvasGroup.interactable && clickable.State == TweenClickableState.Normal &&
                        player.IsPlaying(TweenIds.Interactable),
                    "SetInteractable(true) did not restore Normal.");

                player.StopAll();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static TweenAnimation StateAnimation(string id, BaseTweenClip clip, bool infinite = false)
        {
            if (infinite && clip is DurationTweenClip durationClip)
            {
                durationClip.RepeatMode = TweenLoopMode.Restart;
                durationClip.RepeatCount = -1;
            }

            return new TweenAnimation
            {
                Id = id,
                Playback = new TweenPlaybackSettings
                {
                    LoopMode = TweenLoopMode.None,
                    LoopCount = 1
                },
                Clips = { clip }
            };
        }

        private static bool ReadIsActive(object playback)
        {
            return (bool)playback.GetType().GetProperty("IsActive",
                BindingFlags.Public | BindingFlags.Instance).GetValue(playback);
        }

        private static object CreatePlaybackForValidation(TweenPlayer player, TweenAnimation animation)
        {
            Type type = typeof(TweenPlayer).Assembly.GetType("UIMotionComposer.V2.TweenPlayback");
            MethodInfo create = type?.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
            object playback = create?.Invoke(null, new object[] { player, animation, false });
            Require(playback != null, "Could not create a playback for nested animation validation.");
            type.GetMethod("Begin", BindingFlags.Public | BindingFlags.Instance)?.Invoke(playback, null);
            return playback;
        }

        private static void TickPlaybackForValidation(object playback, float delta)
        {
            playback.GetType().GetMethod("Tick", BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(playback, new object[] { delta, delta });
        }

        private static void StopPlaybackForValidation(object playback, bool complete)
        {
            playback.GetType().GetMethod("Stop", BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(playback, new object[] { complete });
        }

        private static float ReadNormalizedTime(object playback)
        {
            return (float)playback.GetType().GetProperty("NormalizedTime",
                BindingFlags.Public | BindingFlags.Instance).GetValue(playback);
        }

        private static void ValidateClipHierarchy(TweenAnimationAsset asset)
        {
            asset.Clips.Clear();
            var eventClip = new EventTweenClip { Delay = 0.7f };
            asset.Clips.Add(eventClip);
            using (var source = new SerializedObject(asset))
            {
                SerializedProperty clip = source.FindProperty("Clips").GetArrayElementAtIndex(0);
                Require(clip.FindPropertyRelative("Delay") != null,
                    "Trigger clip lost its timeline marker.");
                Require(clip.FindPropertyRelative("FireOnReverse") != null,
                    "Trigger clip lost reverse playback configuration.");
                Require(clip.FindPropertyRelative("Duration") == null &&
                        clip.FindPropertyRelative("Ease") == null &&
                        clip.FindPropertyRelative("UseCustomCurve") == null &&
                        clip.FindPropertyRelative("ApplyFromBeforeDelay") == null,
                    "Trigger clip still serializes duration/easing fields it cannot use.");
                Require(clip.FindPropertyRelative("Target") == null &&
                        clip.FindPropertyRelative("TargetKey") == null,
                    "Targetless Event clip still serializes target fields it cannot use.");
            }
            Require(Mathf.Abs(eventClip.EndTime - eventClip.Delay) < 0.001f,
                "Trigger clip duration is not its marker time.");

            asset.Clips.Clear();
            asset.Clips.Add(new PlayTweenAnimationClip());
            using (var source = new SerializedObject(asset))
            {
                SerializedProperty clip = source.FindProperty("Clips").GetArrayElementAtIndex(0);
                Require(clip.FindPropertyRelative("Target") != null &&
                        clip.FindPropertyRelative("TargetKey") != null &&
                        clip.FindPropertyRelative("Mode") != null,
                    "Targeted trigger lost its target binding fields.");
                Require(clip.FindPropertyRelative("Duration") == null &&
                        clip.FindPropertyRelative("Ease") == null,
                    "Targeted trigger still serializes duration/easing fields it cannot use.");
            }

            asset.Clips.Clear();
            asset.Clips.Add(new ScaleTweenClip());
            using (var source = new SerializedObject(asset))
            {
                SerializedProperty clip = source.FindProperty("Clips").GetArrayElementAtIndex(0);
                Require(clip.FindPropertyRelative("Target") != null &&
                        clip.FindPropertyRelative("Duration") != null &&
                        clip.FindPropertyRelative("Ease") != null,
                    "Duration clip lost target or timing fields during hierarchy split.");
            }

            asset.Clips.Clear();
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
