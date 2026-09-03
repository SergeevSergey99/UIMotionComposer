using NUnit.Framework;
using UIMotionComposer.Editor;
using UnityEngine;

namespace UIMotionComposer.Tests
{
    /// <summary>
    /// EditMode wrapper around the checks in <see cref="TweenValidation"/>.
    ///
    /// The assertions live there rather than here so the Tools menu item and the test runner stay
    /// one source of truth: the menu entry is the quick authoring-time pass, these tests are the
    /// same checks reported case by case and runnable from batch mode. A failure surfaces as the
    /// InvalidOperationException that Require throws, whose message names the broken invariant.
    /// </summary>
    [TestFixture]
    public sealed class TweenSmokeTests
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
            _fixture = TweenValidation.CreateFixture(out _rect, out _canvasGroup, out _player);
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
            TweenValidation.ValidateAssetScript(_asset);
        }

        [Test]
        public void ClipHierarchy_KeepsDurationAndTriggerFieldsApart()
        {
            TweenValidation.ValidateClipHierarchy(_asset);
        }

        [Test]
        public void Preview_SamplesMidpointAndRestoresOnStop()
        {
            TweenValidation.ValidatePreviewSampling(_player, _rect, _canvasGroup);
        }

        [Test]
        public void Preview_UsesSerializedInitialSnapshot()
        {
            TweenAnimation animation = TweenValidation.ValidatePreviewSampling(_player, _rect, _canvasGroup);
            TweenValidation.ValidateInitialSnapshot(_player, _rect, animation);
        }

        [Test]
        public void TargetSlot_ResolvesThroughPlayerBindings()
        {
            TweenAnimation animation = TweenValidation.ValidatePreviewSampling(_player, _rect, _canvasGroup);
            TweenValidation.ValidateTargetSlot(_player, animation, _canvasGroup);
        }

        [Test]
        public void AuthoringFingerprint_ObservesManagedReferenceEdits()
        {
            TweenValidation.ValidateAuthoringFingerprint(_player);
        }

        [Test]
        public void NestedPlayback_HonoursFireAndForgetWaitAndLinkLifetime()
        {
            TweenValidation.ValidateNestedPlaybackModes(_player, _canvasGroup);
        }

        [Test]
        public void ClipRepeat_HandlesRestartPingPongAndInfinite()
        {
            TweenValidation.ValidateClipRepeatSemantics();
        }

        [Test]
        public void ReversePlayback_ReusesCurrentEndpointAndCrossesDelayAtExactFrom()
        {
            TweenValidation.ValidateReversePlayback();
        }

        [Test]
        public void Layering_XYAndZContinueWhileNewYOverwritesOnlyY()
        {
            TweenValidation.ValidateLayeredPlayback();
        }

        [Test]
        public void Layering_ColorAndFadeFollowLaunchOrder()
        {
            TweenValidation.ValidateLayeredColor();
        }

        [Test]
        public void Runner_CallbackLaunchStartsAfterExistingWritersAtTimeZero()
        {
            TweenValidation.ValidateRunnerCallbackOrder();
        }

        [Test]
        public void Runner_NestedWaitHoldsParentAndAdvancesChild()
        {
            TweenValidation.ValidateRunnerNestedWait();
        }

        [Test]
        public void Clickable_StopsAnInfiniteHoverOnPointerExit()
        {
            TweenValidation.ValidateClickableStateMachine();
        }

        [Test]
        public void AnimationMode_RestoresPoseWithoutTouchingUndo()
        {
            TweenValidation.ValidateAnimationModeRestore(_rect);
        }
    }
}
