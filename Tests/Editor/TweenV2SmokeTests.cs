using NUnit.Framework;
using UIMotionComposer.V2.Editor;
using UnityEngine;

namespace UIMotionComposer.V2.Tests
{
    /// <summary>
    /// EditMode wrapper around the checks in <see cref="TweenV2Validation"/>.
    ///
    /// The assertions live there rather than here so the Tools menu item and the test runner stay
    /// one source of truth: the menu entry is the quick authoring-time pass, these tests are the
    /// same checks reported case by case and runnable from batch mode. A failure surfaces as the
    /// InvalidOperationException that Require throws, whose message names the broken invariant.
    /// </summary>
    [TestFixture]
    public sealed class TweenV2SmokeTests
    {
        private GameObject _fixture;
        private RectTransform _rect;
        private CanvasGroup _canvasGroup;
        private TweenPlayer _player;
        private TweenAnimationAsset _asset;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<TweenAnimationAsset>();
            _fixture = TweenV2Validation.CreateFixture(out _rect, out _canvasGroup, out _player);
        }

        [TearDown]
        public void TearDown()
        {
            if (_fixture != null)
                Object.DestroyImmediate(_fixture);
            if (_asset != null)
                Object.DestroyImmediate(_asset);

            _fixture = null;
            _asset = null;
        }

        [Test]
        public void AnimationAsset_ResolvesItsOwnMonoScript()
        {
            TweenV2Validation.ValidateAssetScript(_asset);
        }

        [Test]
        public void ClipHierarchy_KeepsDurationAndTriggerFieldsApart()
        {
            TweenV2Validation.ValidateClipHierarchy(_asset);
        }

        [Test]
        public void Preview_SamplesMidpointAndRestoresOnStop()
        {
            TweenV2Validation.ValidatePreviewSampling(_player, _rect, _canvasGroup);
        }

        [Test]
        public void Preview_UsesSerializedInitialSnapshot()
        {
            TweenAnimation animation = TweenV2Validation.ValidatePreviewSampling(_player, _rect, _canvasGroup);
            TweenV2Validation.ValidateInitialSnapshot(_player, _rect, animation);
        }

        [Test]
        public void TargetSlot_ResolvesThroughPlayerBindings()
        {
            TweenAnimation animation = TweenV2Validation.ValidatePreviewSampling(_player, _rect, _canvasGroup);
            TweenV2Validation.ValidateTargetSlot(_player, animation, _canvasGroup);
        }

        [Test]
        public void LegacyStartValues_ImportIntoInitialSnapshot()
        {
            TweenAnimation animation = TweenV2Validation.ValidatePreviewSampling(_player, _rect, _canvasGroup);
            TweenV2Validation.ValidateInitialSnapshot(_player, _rect, animation);
            TweenV2Validation.ValidateLegacyInitialImport(_player, _rect);
        }

        [Test]
        public void LegacyAnimationData_ConvertsTimelineToDelayAndDuration()
        {
            TweenV2Validation.ValidateLegacyConversion();
        }

        [Test]
        public void AuthoringFingerprint_ObservesManagedReferenceEdits()
        {
            TweenV2Validation.ValidateAuthoringFingerprint(_player);
        }

        [Test]
        public void NestedPlayback_HonoursFireAndForgetWaitAndLinkLifetime()
        {
            TweenV2Validation.ValidateNestedPlaybackModes(_player, _canvasGroup);
        }

        [Test]
        public void ClipRepeat_HandlesRestartPingPongAndInfinite()
        {
            TweenV2Validation.ValidateClipRepeatSemantics();
        }

        [Test]
        public void ReversePlayback_ReusesCurrentEndpointAndCrossesDelayAtExactFrom()
        {
            TweenV2Validation.ValidateReversePlayback();
        }

        [Test]
        public void BindingConflicts_ReportOverlappingWritesToOneProperty()
        {
            TweenV2Validation.ValidateBindingConflictDiagnostics();
        }

        [Test]
        public void Clickable_StopsAnInfiniteHoverOnPointerExit()
        {
            TweenV2Validation.ValidateClickableStateMachine();
        }

        [Test]
        public void AnimationMode_RestoresPoseWithoutTouchingUndo()
        {
            TweenV2Validation.ValidateAnimationModeRestore(_rect);
        }
    }
}
