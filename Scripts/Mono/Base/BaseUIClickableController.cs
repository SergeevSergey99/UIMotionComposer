using System;
using System.Collections.Generic;
using UIMotionComposer.Inspector;
using UIMotionComposer.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UIMotionComposer
{
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public abstract class BaseUIClickableController : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float FallbackHoverDuration = 0.2f;
        private const float FallbackClickDuration = 0.1f;
        private const float FallbackDisableDuration = 0.3f;

        [SerializeField] protected CanvasGroup _canvasGroup;

        [SerializeField, LabelText("Has Start Values")]
        [Tooltip("Whether the authored pose below has been captured yet. This is what the field being " +
                 "an unassigned reference used to say. Clear it to make the clickable capture again on " +
                 "the next play, or use the Save Start Values button to capture right now.")]
        private bool hasStartValues;

        [SerializeField, ShowIf(nameof(hasStartValues)), LabelText("Start Values")]
        private TempValues startValues = new TempValues();

        public event Action OnHoverStarted;
        public event Action OnHoverEnded;
        public event Action OnClickStarted;
        public event Action OnClickEnded;
        public event Action OnInteractableChanged;

        public RectTransform RectTransform => transform as RectTransform;
        public CanvasGroup CanvasGroup => _canvasGroup;
        public bool HasStoredStartValues => hasStartValues;
        public TempValues StoredStartValues => startValues;

        public abstract AnimationData CurrentHoverAnimationData { get; }
        public abstract AnimationData CurrentClickAnimationData { get; }
        public abstract AnimationData CurrentDisableAnimationData { get; }
        public abstract AnimationData CurrentReturnFromHoverAnimationData { get; }
        public abstract AnimationData CurrentReturnFromClickAnimationData { get; }
        public abstract AnimationData CurrentReturnFromDisableAnimationData { get; }

        protected List<IAnimationHandler> _hoverHandlers;
        protected List<IAnimationHandler> _clickHandlers;
        protected List<IAnimationHandler> _disableHandlers;
        protected List<IAnimationHandler> _returnFromHoverHandlers;
        protected List<IAnimationHandler> _returnFromClickHandlers;
        protected List<IAnimationHandler> _returnFromDisableHandlers;

        protected bool _isHovered;
        protected bool _isClicked;
        protected bool _wasInteractable;

        protected IUISequence CurrentSequence { get; set; }
        public bool IsAnimated => CurrentSequence != null && CurrentSequence.IsActive() && CurrentSequence.IsPlaying();

        protected virtual void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            InitializeHandlers();
            StoreInitialValues();
            _wasInteractable = _canvasGroup.interactable;
        }

        private void InitializeHandlers()
        {
            _hoverHandlers = GetHandlers(CurrentHoverAnimationData);
            _clickHandlers = GetHandlers(CurrentClickAnimationData);
            _disableHandlers = GetHandlers(CurrentDisableAnimationData);
            _returnFromHoverHandlers = GetHandlers(CurrentReturnFromHoverAnimationData);
            _returnFromClickHandlers = GetHandlers(CurrentReturnFromClickAnimationData);
            _returnFromDisableHandlers = GetHandlers(CurrentReturnFromDisableAnimationData);
        }

        private static List<IAnimationHandler> GetHandlers(AnimationData data)
        {
            return data?.GetHandlers() ?? new List<IAnimationHandler>();
        }

        private void StoreInitialValues()
        {
            if (hasStartValues) return;
            SaveStartValues();
        }

        [Button("Save Start Values")]
        private void SaveStartValues()
        {
            RectTransform.RebuildDrivenLayout();

            startValues ??= new TempValues();
            startValues.SetInitialState(RectTransform, _canvasGroup);
            hasStartValues = true;
        }

        /// <summary>
        /// Sets interactability and plays the matching animation.
        ///
        /// Prefer this over writing canvasGroup.interactable directly: the controller has no way of
        /// noticing an external write without polling every frame, which a screenful of buttons
        /// cannot afford. If some code really must set the flag itself, follow it with
        /// <see cref="RefreshInteractableState"/> or add a UIClickableInteractablePoller.
        /// </summary>
        public void SetInteractable(bool value)
        {
            if (_canvasGroup.interactable == value)
                return;

            _canvasGroup.interactable = value;
            RefreshInteractableState();
        }

        /// <summary>Picks up an interactable change made elsewhere. No-op when nothing changed.</summary>
        public void RefreshInteractableState()
        {
            if (_canvasGroup.interactable == _wasInteractable)
                return;

            _wasInteractable = _canvasGroup.interactable;
            OnInteractableStateChanged();
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            if (!_canvasGroup.interactable) return;

            _isHovered = true;
            PlayAnimation(_hoverHandlers, CurrentHoverAnimationData, FallbackHoverDuration, OnHoverStarted);
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            if (!_canvasGroup.interactable) return;

            _isHovered = false;
            if (!_isClicked)
            {
                PlayAnimation(_returnFromHoverHandlers, CurrentReturnFromHoverAnimationData, FallbackHoverDuration, OnHoverEnded);
            }
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (!_canvasGroup.interactable) return;

            _isClicked = true;
            PlayAnimation(_clickHandlers, CurrentClickAnimationData, FallbackClickDuration, OnClickStarted);
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            if (!_canvasGroup.interactable) return;

            _isClicked = false;

            // Still hovered means returning to the hover pose, not all the way to idle.
            if (_isHovered)
            {
                PlayAnimation(_returnFromClickHandlers, CurrentReturnFromClickAnimationData, FallbackClickDuration, OnClickEnded);
                return;
            }

            PlayAnimation(_returnFromHoverHandlers, CurrentReturnFromHoverAnimationData, FallbackHoverDuration, OnClickEnded);
        }

        protected virtual void OnInteractableStateChanged()
        {
            OnInteractableChanged?.Invoke();

            if (!_canvasGroup.interactable)
            {
                _isHovered = false;
                _isClicked = false;
                PlayAnimation(_disableHandlers, CurrentDisableAnimationData, FallbackDisableDuration, null);
            }
            else
            {
                PlayAnimation(_returnFromDisableHandlers, CurrentReturnFromDisableAnimationData, FallbackDisableDuration, null);
            }
        }

        protected virtual void PlayAnimation(List<IAnimationHandler> handlers, AnimationData data, float fallbackDuration, Action callback)
        {
            if (handlers == null || handlers.Count == 0) return;
            StoreInitialValues();

            // Hovering on and off quickly is the normal case here, so continuing from the current
            // pose rather than snapping back to the configured start is what keeps it readable.
            bool interrupted = IsAnimated;
            CurrentSequence?.Kill();
            CurrentSequence = UITween.CreateSequence();

            bool startFromCurrent = interrupted && (data == null || !data.RestartFromInitialOnInterrupt);
            var context = new UIAnimationContext(
                startValues, RectTransform, _canvasGroup, data?.Duration ?? fallbackDuration, startFromCurrent);

            foreach (var handler in handlers)
            {
                handler?.AddToSequence(CurrentSequence, context);
            }

            CurrentSequence.SetUpdate(true);
            CurrentSequence.OnComplete(() => callback?.Invoke());
            CurrentSequence.Play();
        }

        protected void KillAllSequences() => CurrentSequence?.Kill();

        private void OnDisable()
        {
            KillAllSequences();

            if (hasStartValues)
                startValues.ApplyTo(RectTransform, _canvasGroup);
        }

        protected virtual void OnValidate()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }
        }
    }
}
