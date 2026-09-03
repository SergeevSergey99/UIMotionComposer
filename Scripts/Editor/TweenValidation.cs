using System;
using System.Reflection;
using UIMotionComposer.Tweening;
using UnityEditor;
using UnityEngine;

namespace UIMotionComposer.Editor
{
    /// <summary>Small dependency-free smoke suite that can also run from Unity -executeMethod.</summary>
    public static class TweenValidation
    {
        [MenuItem("Tools/UI Motion Composer/Run smoke tests")]
        public static void Run()
        {
            GameObject gameObject = null;
            TweenAnimationAsset animationAsset = null;
            try
            {
                animationAsset = ScriptableObject.CreateInstance<TweenAnimationAsset>();
                ValidateAssetScript(animationAsset);
                ValidateClipHierarchy(animationAsset);

                gameObject = CreateFixture(out RectTransform rect, out CanvasGroup canvasGroup,
                    out TweenPlayer player);

                TweenAnimation animation = ValidatePreviewSampling(player, rect, canvasGroup);
                ValidateTargetSlot(player, animation, canvasGroup);
                ValidateInitialSnapshot(player, rect, animation);
                ValidateAuthoringFingerprint(player);
                ValidateNestedPlaybackModes(player, canvasGroup);
                ValidateClipRepeatSemantics();
                ValidateReversePlayback();
                ValidateLayeredPlayback();
                ValidateLayeredColor();
                ValidateRunnerCallbackOrder();
                ValidateRunnerNestedWait();
                ValidateClickableStateMachine();
                ValidateAnimationModeRestore(rect);

                Debug.Log("[UI Motion Composer] Smoke tests passed.");
            }
            finally
            {
                if (gameObject != null)
                    UnityEngine.Object.DestroyImmediate(gameObject);
                if (animationAsset != null)
                    UnityEngine.Object.DestroyImmediate(animationAsset);
            }
        }

        /// <summary>
        /// Builds the object every fixture-based check runs against. Shared by the menu suite and by
        /// the EditMode tests, which build a fresh one per test case.
        /// </summary>
        internal static GameObject CreateFixture(out RectTransform rect, out CanvasGroup canvasGroup,
            out TweenPlayer player)
        {
            var gameObject = new GameObject("UI Motion Composer Validation",
                typeof(RectTransform), typeof(CanvasGroup));
            rect = gameObject.GetComponent<RectTransform>();
            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            player = gameObject.AddComponent<TweenPlayer>();

            rect.anchoredPosition = new Vector2(12f, 34f);
            canvasGroup.alpha = 0.8f;
            return gameObject;
        }

        internal static void ValidateAssetScript(TweenAnimationAsset animationAsset)
        {
            MonoScript assetScript = MonoScript.FromScriptableObject(animationAsset);
            Require(assetScript != null && assetScript.GetClass() == typeof(TweenAnimationAsset),
                "TweenAnimationAsset does not resolve to its own MonoScript file.");
        }

        /// <summary>Returns the animation it authored so later checks can keep editing it.</summary>
        internal static TweenAnimation ValidatePreviewSampling(TweenPlayer player, RectTransform rect,
            CanvasGroup canvasGroup)
        {
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

            return animation;
        }

        internal static void ValidateInitialSnapshot(TweenPlayer player, RectTransform rect,
            TweenAnimation animation)
        {
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
            TweenInitialPoseEntryInfo[] savedEntries = player.GetCapturedInitialPoseEntries();
            Require(savedEntries.Length == 1 && savedEntries[0].Target == rect &&
                    savedEntries[0].PropertyId == "RectTransform.AnchoredPosition" &&
                    savedEntries[0].CanRestore,
                "Initial Pose inspection API did not describe its saved property.");

            rect.anchoredPosition = new Vector2(250f, -80f);
            Require(player.Preview("Validation", 1f), "Initial endpoint preview did not start.");
            Require(Vector2.Distance(rect.anchoredPosition, new Vector2(12f, 34f)) < 0.001f,
                $"Initial endpoint did not use the authored snapshot: {rect.anchoredPosition}.");
            player.StopPreview();
            Require(Vector2.Distance(rect.anchoredPosition, new Vector2(250f, -80f)) < 0.001f,
                "Preview did not restore the pose that existed before previewing Initial.");

            Require(player.RestoreInitialValues() == 1,
                "Restore Initial Pose did not report its restored property.");
            Require(Vector2.Distance(rect.anchoredPosition, new Vector2(12f, 34f)) < 0.001f,
                "Restore Initial Pose did not apply the serialized authored value.");

            rect.anchoredPosition = new Vector2(-90f, 170f);
            Require(player.RestoreInitialValueAt(savedEntries[0].Index) &&
                    Vector2.Distance(rect.anchoredPosition, new Vector2(12f, 34f)) < 0.001f,
                "A single Initial Pose entry could not be restored.");

            // This is the same serialized path drawn by TweenInitialValueDrawer. Keeping the test
            // on SerializedProperty ensures native inspector edits are the source of truth rather
            // than a parallel editor-only representation.
            var serializedPlayer = new SerializedObject(player);
            SerializedProperty editedValue = serializedPlayer.FindProperty("initialPose")
                ?.FindPropertyRelative("values")
                ?.GetArrayElementAtIndex(0)
                ?.FindPropertyRelative("vector2Value");
            Require(editedValue != null, "Initial Pose is not exposed as a serialized wrapper.");
            var inspectorAuthoredValue = new Vector2(48f, -26f);
            editedValue.vector2Value = inspectorAuthoredValue;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

            rect.anchoredPosition = Vector2.zero;
            Require(player.RestoreInitialValueAt(0) &&
                    Vector2.Distance(rect.anchoredPosition, inspectorAuthoredValue) < 0.001f,
                "An Initial Pose value edited through SerializedProperty was not restored.");
        }

        /// <summary>
        /// A reverse launch reuses the concrete endpoints resolved by the preceding forward launch.
        /// This matters for Current, whose value has already become To by then. A delayed clip must
        /// also write exact From when a reverse tick crosses its start marker.
        /// </summary>
        internal static void ValidateReversePlayback()
        {
            GameObject gameObject = CreateFixture(out RectTransform rect, out _, out TweenPlayer player);
            try
            {
                Vector2 authoredStart = rect.anchoredPosition;
                Vector2 authoredEnd = new Vector2(100f, 0f);
                var animation = new TweenAnimation { Id = "Reverse" };
                animation.Clips.Add(new AnchorPositionTweenClip
                {
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.Custom,
                    ToValue = authoredEnd,
                    Ease = UIEase.Linear,
                    Delay = 0.3f,
                    Duration = 0.7f
                });
                player.AnimationDefinitions.Add(animation);

                object forward = CreatePlaybackForValidation(player, animation);
                TickPlaybackForValidation(forward, 1f);
                Require(Vector2.Distance(rect.anchoredPosition, authoredEnd) < 0.001f,
                    $"Forward setup did not reach To: {rect.anchoredPosition}.");

                object playback = CreatePlaybackForValidation(player, animation, reversed: true);
                Require(Vector2.Distance(rect.anchoredPosition, authoredEnd) < 0.001f,
                    $"Reversed play did not begin at the To value: {rect.anchoredPosition}.");

                TickPlaybackForValidation(playback, 0.35f);
                Vector2 midpoint = Vector2.Lerp(authoredStart, authoredEnd, 0.5f);
                Require(Vector2.Distance(rect.anchoredPosition, midpoint) < 0.001f,
                    $"Reversed midpoint is wrong: {rect.anchoredPosition}.");

                TickPlaybackForValidation(playback, 0.5f);
                Require(Vector2.Distance(rect.anchoredPosition, authoredStart) < 0.001f,
                    $"Reversed delayed clip did not apply exact From at its start: {rect.anchoredPosition}.");

                TickPlaybackForValidation(playback, 0.2f);
                Require(Vector2.Distance(rect.anchoredPosition, authoredStart) < 0.001f,
                    $"Reversed play did not end at the From value: {rect.anchoredPosition}.");
                Require(!ReadIsActive(playback),
                    "Reversed play did not finish when it reached zero.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        internal static void ValidateNestedPlaybackModes(TweenPlayer parentPlayer,
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

        internal static void ValidateLayeredPlayback()
        {
            var target = new GameObject("Layered Playback", typeof(RectTransform), typeof(TweenPlayer));
            var other = new GameObject("Other Player", typeof(TweenPlayer));
            try
            {
                TweenPlayer player = target.GetComponent<TweenPlayer>();
                TweenPlayer otherPlayer = other.GetComponent<TweenPlayer>();
                Transform transform = target.transform;
                TweenAnimation xy = MoveAnimation("XY", TweenVectorComponents.All2D, new Vector3(8, 16, 99), 4f);
                TweenAnimation z = MoveAnimation("Z", TweenVectorComponents.Z, new Vector3(99, 99, 24), 4f);
                ((MoveTweenClip)z.Clips[0]).Target = transform;
                ((DurationTweenClip)z.Clips[0]).RepeatMode = TweenLoopMode.Restart;
                ((DurationTweenClip)z.Clips[0]).RepeatCount = -1;
                z.Playback.AllowSelfOverride = false;
                TweenAnimation y = MoveAnimation("Y", TweenVectorComponents.Y, new Vector3(99, 100, 99), 1f);
                ((MoveTweenClip)y.Clips[0]).Target = transform;
                ((MoveTweenClip)y.Clips[0]).FromMode = TweenEndpointMode.Current;
                int markers = 0;
                var marker = new EventTweenClip { Delay = 2f };
                marker.Event.AddListener(() => markers++);
                xy.Clips.Add(marker);
                player.AnimationDefinitions.Add(xy);
                otherPlayer.AnimationDefinitions.Add(z);
                otherPlayer.AnimationDefinitions.Add(y);

                TweenHandle xyHandle = player.Play(xy.Id);
                TweenHandle zHandle = otherPlayer.Play(z.Id);
                Require(!otherPlayer.Play(z.Id).IsValid, "Same-animation launch guard was lost.");
                TickRunnerForValidation(0.5f);
                Require(Vector3.Distance(transform.localPosition, new Vector3(1, 2, 3)) < 0.001f,
                    "XY and Z did not compose across players.");

                TweenHandle yHandle = otherPlayer.Play(y.Id);
                TickRunnerForValidation(0.5f);
                Require(Vector3.Distance(transform.localPosition, new Vector3(2, 51, 6)) < 0.001f &&
                        xyHandle.IsActive && zHandle.IsActive,
                    "A newer Y must overwrite only Y, leaving the older XY and Z playbacks alive.");
                yHandle.Pause();
                TickRunnerForValidation(0.25f);
                Require(Mathf.Abs(transform.localPosition.y - 51f) < 0.001f,
                    "Pausing a layer must freeze its sample, not expose an earlier writer.");
                yHandle.Resume();
                TickRunnerForValidation(0.5f);
                Require(yHandle.WasCompleted && Mathf.Abs(transform.localPosition.y - 100f) < 0.001f,
                    "The finishing layer did not write its last sample.");
                TickRunnerForValidation(0.25f);
                Require(Vector3.Distance(transform.localPosition, new Vector3(4, 8, 12)) < 0.001f,
                    "The older Y did not become visible again after the newer animation finished.");
                TickRunnerForValidation(2f);
                Require(xyHandle.WasCompleted && markers == 1 && zHandle.IsActive,
                    "Layering changed another animation's events or infinite lifetime.");
                otherPlayer.StopAll();
                Require(zHandle.WasCancelled, "Explicit StopAll did not stop an infinite layer.");

                transform.localPosition = Vector3.zero;
                var preview = new TweenAnimation { Id = "Layered Preview" };
                preview.Clips.AddRange(xy.Clips);
                preview.Clips.AddRange(z.Clips);
                preview.Clips.AddRange(y.Clips);
                player.AnimationDefinitions.Add(preview);
                player.PreparePreview(preview.Id);
                player.SamplePreparedPreviewTime(2f);
                Require(Vector3.Distance(transform.localPosition, new Vector3(4, 100, 12)) < 0.001f,
                    "Preview did not compose clips in authored order.");
                player.SamplePreparedPreviewTime(0.5f);
                Require(Vector3.Distance(transform.localPosition, new Vector3(1, 50, 3)) < 0.001f,
                    "Backwards preview scrubbing changed component ownership.");
                player.StopPreview();
                Require(transform.localPosition == Vector3.zero && markers == 1,
                    "Layered preview changed the authored pose or fired a marker.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(other);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        internal static void ValidateLayeredColor()
        {
            var target = new GameObject("Layered Color", typeof(RectTransform),
                typeof(UnityEngine.UI.Image), typeof(TweenPlayer));
            try
            {
                TweenPlayer player = target.GetComponent<TweenPlayer>();
                var graphic = target.GetComponent<UnityEngine.UI.Image>();
                var color = new TweenAnimation { Id = "Color" };
                color.Clips.Add(new ColorTweenClip
                {
                    FromMode = TweenEndpointMode.Custom, FromValue = Color.black,
                    ToMode = TweenEndpointMode.Custom, ToValue = new Color(1f, 0.6f, 0.2f, 0f),
                    Duration = 2f, Ease = UIEase.Linear
                });
                var fade = new TweenAnimation { Id = "Fade" };
                fade.Clips.Add(new FadeTweenClip
                {
                    FromMode = TweenEndpointMode.Custom, FromValue = 1f,
                    ToMode = TweenEndpointMode.Custom, ToValue = 0.4f,
                    Duration = 1f, Ease = UIEase.Linear
                });
                player.AnimationDefinitions.Add(color);
                player.AnimationDefinitions.Add(fade);
                TweenHandle colorHandle = player.Play(color.Id);
                TweenHandle fadeHandle = player.Play(fade.Id);
                TickRunnerForValidation(0.5f);
                Require(Vector4.Distance(graphic.color, new Color(0.25f, 0.15f, 0.05f, 0.7f)) < 0.001f &&
                        colorHandle.IsActive,
                    $"Fade must replace alpha without stopping RGB. Actual: {graphic.color}; " +
                    $"Color active={colorHandle.IsActive}, t={colorHandle.NormalizedTime}; " +
                    $"Fade active={fadeHandle.IsActive}, t={fadeHandle.NormalizedTime}.");
                TickRunnerForValidation(0.5f);
                TickRunnerForValidation(0.5f);
                Require(Mathf.Abs(graphic.color.a - 0.25f) < 0.001f,
                    "The earlier Color alpha did not resume after Fade completed.");

                player.StopAll();
                player.Play(fade.Id);
                player.Play(color.Id);
                TickRunnerForValidation(0.5f);
                Require(Mathf.Abs(graphic.color.a - 0.75f) < 0.001f,
                    "Launch order, not clip type, must determine which alpha write wins.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        internal static void ValidateRunnerCallbackOrder()
        {
            var target = new GameObject("Callback Order", typeof(RectTransform), typeof(TweenPlayer));
            try
            {
                TweenPlayer player = target.GetComponent<TweenPlayer>();
                TweenAnimation first = MoveAnimation("Ends", TweenVectorComponents.X, Vector3.one * 4f, 0.25f);
                TweenAnimation survivor = MoveAnimation("Survives", TweenVectorComponents.X, Vector3.one * 20f, 2f);
                TweenAnimation child = MoveAnimation("Callback Child", TweenVectorComponents.X, Vector3.one * 100f, 1f);
                ((MoveTweenClip)child.Clips[0]).FromValue = Vector3.one * 50f;
                player.AnimationDefinitions.Add(first);
                player.AnimationDefinitions.Add(survivor);
                player.AnimationDefinitions.Add(child);
                TweenHandle childHandle = null;
                first.OnCompleted.AddListener(() => childHandle = player.Play(child.Id));
                player.Play(first.Id);
                TweenHandle survivorHandle = player.Play(survivor.Id);
                TickRunnerForValidation(0.25f);
                Require(childHandle != null && childHandle.NormalizedTime == 0f &&
                        Mathf.Abs(target.transform.localPosition.x - 50f) < 0.001f &&
                        Mathf.Abs(survivorHandle.NormalizedTime - 0.125f) < 0.001f,
                    "Callback launches must start last at time zero without skipping existing playbacks.");
                TickRunnerForValidation(0.25f);
                Require(Mathf.Abs(target.transform.localPosition.x - 62.5f) < 0.001f,
                    "Removing a completed playback changed the remaining layer order.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        internal static void ValidateRunnerNestedWait()
        {
            var target = new GameObject("Nested Runner", typeof(RectTransform), typeof(TweenPlayer));
            try
            {
                TweenPlayer player = target.GetComponent<TweenPlayer>();
                TweenAnimation parent = MoveAnimation("Parent", TweenVectorComponents.X, Vector3.one * 4f, 1f);
                TweenAnimation child = MoveAnimation("Child", TweenVectorComponents.Y, Vector3.one * 2f, 0.5f);
                parent.Clips.Add(new PlayTweenAnimationClip
                {
                    AnimationId = child.Id, Delay = 0.25f, Mode = TweenNestedPlaybackMode.Wait
                });
                player.AnimationDefinitions.Add(parent);
                player.AnimationDefinitions.Add(child);
                TweenHandle parentHandle = player.Play(parent.Id);
                TickRunnerForValidation(0.5f);
                Require(parentHandle.NormalizedTime == 0.25f && player.IsPlaying(child.Id) &&
                        target.transform.localPosition.y == 0f,
                    "A nested child must begin at its marker without consuming the parent's frame delta.");
                TickRunnerForValidation(0.5f);
                Require(parentHandle.NormalizedTime == 0.25f && !player.IsPlaying(child.Id),
                    "Wait did not hold the parent while the ordered runner advanced the child.");
                TickRunnerForValidation(0.25f);
                Require(parentHandle.NormalizedTime == 0.5f &&
                        Vector3.Distance(target.transform.localPosition, new Vector3(2, 2, 0)) < 0.001f,
                    "The parent did not resume after its nested child completed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static TweenAnimation MoveAnimation(string id, TweenVectorComponents components, Vector3 to,
            float duration)
        {
            return new TweenAnimation
            {
                Id = id,
                Clips = { new MoveTweenClip
                {
                    Components = components, FromMode = TweenEndpointMode.Custom, FromValue = Vector3.zero,
                    ToMode = TweenEndpointMode.Custom, ToValue = to, Duration = duration, Ease = UIEase.Linear
                } }
            };
        }

        internal static void ValidateClipRepeatSemantics()
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

        internal static void ValidateClickableStateMachine()
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
                player.AnimationDefinitions.Add(StateAnimation(TweenIds.Selected, new ScaleTweenClip
                {
                    Duration = 0.2f,
                    FromMode = TweenEndpointMode.Current,
                    ToMode = TweenEndpointMode.OffsetFromInitial,
                    ToOffset = Vector3.one * 0.05f
                }));
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
                clickable.SelectedAnimationId = TweenIds.Selected;

                clickable.OnPointerEnter(null);
                Require(clickable.State == TweenClickableState.Hovered && player.IsPlaying(TweenIds.Hover),
                    "Pointer enter did not start Hover.");

                clickable.OnSelect(null);
                Require(clickable.State == TweenClickableState.Selected &&
                        !player.IsPlaying(TweenIds.Hover) && player.IsPlaying(TweenIds.Selected),
                    "Selection did not take priority over Hover.");

                clickable.OnPointerExit(null);
                Require(clickable.State == TweenClickableState.Selected && player.IsPlaying(TweenIds.Selected),
                    "Pointer exit incorrectly cleared the higher-priority Selected state.");

                clickable.OnPointerDown(null);
                Require(clickable.State == TweenClickableState.Pressed && player.IsPlaying(TweenIds.Click),
                    "Pressed did not take priority over Selected.");

                clickable.OnPointerUp(null);
                Require(clickable.State == TweenClickableState.Selected && player.IsPlaying(TweenIds.Selected),
                    "Pointer up did not return to Selected.");

                clickable.OnDeselect(null);
                Require(clickable.State == TweenClickableState.Normal && player.IsPlaying(TweenIds.Unhover),
                    "Deselect did not return to Normal after the pointer had left.");

                clickable.OnPointerEnter(null);
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

        internal static object CreatePlaybackForValidation(TweenPlayer player, TweenAnimation animation,
            bool reversed = false)
        {
            Type type = typeof(TweenPlayer).Assembly.GetType("UIMotionComposer.TweenPlayback");
            MethodInfo create = type?.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);

            // Invoke does not fill in optional parameters, so every argument is passed explicitly.
            object playback = create?.Invoke(null, new object[] { player, animation, false, reversed });
            Require(playback != null, "Could not create a playback for nested animation validation.");
            type.GetMethod("Begin", BindingFlags.Public | BindingFlags.Instance)?.Invoke(playback, null);
            return playback;
        }

        private static void TickPlaybackForValidation(object playback, float delta)
        {
            playback.GetType().GetMethod("Tick", BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(playback, new object[] { delta, delta });
        }

        private static void TickRunnerForValidation(float delta)
        {
            Type type = typeof(TweenPlayer).Assembly.GetType("UIMotionComposer.TweenRuntimeRunner");
            object runner = type?.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            MethodInfo tick = type?.GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
            Require(runner != null && tick != null, "Could not tick the runtime runner for validation.");
            tick.Invoke(runner, new object[] { delta, delta });
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

        internal static void ValidateClipHierarchy(TweenAnimationAsset asset)
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

        internal static void ValidateTargetSlot(TweenPlayer player, TweenAnimation animation,
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

            TweenTargetOverride binding = player.TargetOverrideDefinitions[0];
            binding.Mode = TweenTargetBindingMode.Self;
            binding.Target = null;
            player.InvalidateTargetBindings();
            Require(player.Preview("Validation", 0.5f), "Self binding could not be sampled.");
            Require(Mathf.Abs(playerCanvasGroup.alpha - 0.5f) < 0.001f,
                "Self binding did not resolve the TweenPlayer GameObject.");
            player.StopPreview();

            var container = new GameObject("Container", typeof(RectTransform));
            container.transform.SetParent(player.transform, false);
            var pathChild = new GameObject("PathContent", typeof(RectTransform), typeof(CanvasGroup));
            pathChild.transform.SetParent(container.transform, false);
            CanvasGroup pathCanvasGroup = pathChild.GetComponent<CanvasGroup>();
            pathCanvasGroup.alpha = 0.3f;
            binding.Mode = TweenTargetBindingMode.ChildPath;
            binding.Query = "Container/PathContent";
            player.InvalidateTargetBindings();
            Require(player.Preview("Validation", 0.5f), "Child Path binding could not be sampled.");
            Require(Mathf.Abs(pathCanvasGroup.alpha - 0.5f) < 0.001f,
                "Child Path binding resolved the wrong descendant.");
            player.StopPreview();

            var namedChild = new GameObject("NamedContent", typeof(RectTransform), typeof(CanvasGroup));
            namedChild.transform.SetParent(player.transform, false);
            CanvasGroup namedCanvasGroup = namedChild.GetComponent<CanvasGroup>();
            namedCanvasGroup.alpha = 0.4f;
            binding.Mode = TweenTargetBindingMode.ChildName;
            binding.Query = "NamedContent";
            player.InvalidateTargetBindings();
            Require(player.Preview("Validation", 0.5f), "Child Name binding could not be sampled.");
            Require(Mathf.Abs(namedCanvasGroup.alpha - 0.5f) < 0.001f,
                "Child Name binding resolved the wrong descendant.");
            player.StopPreview();

            binding.Mode = TweenTargetBindingMode.Component;
            binding.Query = "NamedContent";
            binding.ComponentType = typeof(CanvasGroup).AssemblyQualifiedName;
            player.InvalidateTargetBindings();
            Require(player.ResolveTargetBinding("Content", animation) == namedCanvasGroup,
                "Component binding did not resolve the requested component type.");

            var localChild = new GameObject("LocalContent", typeof(RectTransform), typeof(CanvasGroup));
            localChild.transform.SetParent(player.transform, false);
            CanvasGroup localCanvasGroup = localChild.GetComponent<CanvasGroup>();
            localCanvasGroup.alpha = 0.25f;
            binding.Mode = TweenTargetBindingMode.Direct;
            binding.Target = child;
            binding.Query = string.Empty;
            binding.ComponentType = string.Empty;
            animation.TargetOverrides.Add(new TweenTargetOverride
            {
                Key = "Content",
                Mode = TweenTargetBindingMode.Direct,
                Target = localChild
            });
            player.InvalidateTargetBindings();
            Require(player.Preview("Validation", 0.5f), "Animation-local target override could not be sampled.");
            Require(Mathf.Abs(localCanvasGroup.alpha - 0.5f) < 0.001f &&
                    Mathf.Abs(childCanvasGroup.alpha - 0.2f) < 0.001f,
                "Animation-local target override did not take priority over the player binding.");
            player.StopPreview();
            animation.TargetOverrides.Clear();
            player.TargetOverrideDefinitions.Clear();
            player.InvalidateTargetBindings();
        }

        /// <summary>
        /// The preview refresh only fires when the authoring fingerprint changes, so this pins the
        /// managed-reference value, type and weighted-curve cases that the inspector must observe.
        /// </summary>
        internal static void ValidateAuthoringFingerprint(TweenPlayer player)
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

        internal static void ValidateAnimationModeRestore(RectTransform rect)
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
                throw new InvalidOperationException("UI Motion Composer validation: " + message);
        }
    }
}
