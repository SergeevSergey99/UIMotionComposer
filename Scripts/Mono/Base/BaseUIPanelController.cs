using System;
using System.Collections.Generic;
using UIMotionComposer.Inspector;
using UIMotionComposer.Tweening;
using UnityEngine;

namespace UIMotionComposer
{
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public abstract class BaseUIPanelController : MonoBehaviour
    {
        private const float FallbackDuration = 0.5f;

        [SerializeField] protected CanvasGroup _canvasGroup;

        [SerializeField, LabelText("Has Start Values")]
        [Tooltip("Whether the authored pose below has been captured yet. This is what the field being " +
                 "an unassigned reference used to say. Clear it to make the panel capture again on the " +
                 "next play, or use the Save Start Values button to capture right now.")]
        private bool hasStartValues;

        [SerializeField, ShowIf(nameof(hasStartValues)), LabelText("Start Values")]
        private TempValues startValues = new TempValues();

        [SerializeField] protected bool setStartValues = true;
        [SerializeField] protected bool disableOnStart = true;

        public event Action OnShowStarted;
        public event Action OnShowEnded;
        public event Action OnHideStarted;
        public event Action OnHideEnded;

        public RectTransform RectTransform => transform as RectTransform;
        public CanvasGroup CanvasGroup => _canvasGroup;

        /// <summary>The data driving the show animation, or null when the panel has none.</summary>
        public abstract AnimationData CurrentShowAnimationData { get; }

        /// <summary>The data driving the hide animation, or null when the panel has none.</summary>
        public abstract AnimationData CurrentHideAnimationData { get; }

        protected List<IAnimationHandler> _showHandlers;
        protected List<IAnimationHandler> _hideHandlers;

        protected IUISequence CurrentSequence { get; set; }
        public bool IsAnimated => CurrentSequence != null && CurrentSequence.IsActive() && CurrentSequence.IsPlaying();

        private bool _isInitialized;

        protected virtual void Awake()
        {
            StoreInitialValues();
        }

        private void InitializeHandlers()
        {
            _showHandlers = CurrentShowAnimationData?.GetHandlers() ?? new List<IAnimationHandler>();
            _hideHandlers = CurrentHideAnimationData?.GetHandlers() ?? new List<IAnimationHandler>();
        }

        private void StoreInitialValues()
        {
            if (_isInitialized) return;
            InitializeHandlers();

            if (!hasStartValues)
                SaveStartValues();

            if (setStartValues)
            {
                startValues.ApplyTo(RectTransform, _canvasGroup);
            }
            if (disableOnStart)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
                gameObject.SetActive(false);
            }
            _isInitialized = true;
        }

        [Button("Save Start Values")]
        private void SaveStartValues()
        {
            RectTransform.RebuildDrivenLayout();

            startValues ??= new TempValues();
            startValues.SetInitialState(RectTransform, _canvasGroup);
            hasStartValues = true;
        }

        [Button]
        public void Show()
        {
            if (Application.isPlaying) Show(null);
        }

        public virtual void Show(Action callback)
        {
            StoreInitialValues();

            var context = BeginAnimation(CurrentShowAnimationData);

            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            gameObject.SetActive(true);

            ShowStart();
            OnShowStarted?.Invoke();

            foreach (var handler in _showHandlers)
            {
                handler.AddToSequence(CurrentSequence, context);
            }

            CurrentSequence.SetUpdate(true);
            CurrentSequence.OnComplete(() =>
            {
                callback?.Invoke();
                ShowEnd();
                OnShowEnded?.Invoke();
            });

            CurrentSequence.Play();
        }

        protected virtual void ShowStart() {}
        protected virtual void ShowEnd() {}

        [Button]
        public void Hide()
        {
            if (Application.isPlaying) Hide(null);
        }

        public virtual void Hide(Action callback)
        {
            StoreInitialValues();

            var context = BeginAnimation(CurrentHideAnimationData);

            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            HideStart();
            OnHideStarted?.Invoke();

            foreach (var handler in _hideHandlers)
            {
                handler.AddToSequence(CurrentSequence, context);
            }

            CurrentSequence.SetUpdate(true);
            CurrentSequence.OnComplete(() =>
            {
                gameObject.SetActive(false);
                callback?.Invoke();
                HideEnd();
                OnHideEnded?.Invoke();
            });

            CurrentSequence.Play();
        }

        protected virtual void HideStart() {}
        protected virtual void HideEnd() {}

        /// <summary>
        /// Kills whatever is running and opens a fresh sequence. Whether the previous animation was
        /// still in flight decides if this one picks up from the current pose or from the configured
        /// initial values -- otherwise a Show interrupting a Hide snaps before it moves.
        /// </summary>
        private UIAnimationContext BeginAnimation(AnimationData data)
        {
            bool interrupted = IsAnimated;
            KillAllSequences();
            CurrentSequence = UITween.CreateSequence();

            bool startFromCurrent = interrupted && (data == null || !data.RestartFromInitialOnInterrupt);
            return new UIAnimationContext(
                startValues, RectTransform, _canvasGroup, data?.Duration ?? FallbackDuration, startFromCurrent);
        }

        public virtual void InstantHide()
        {
            KillAllSequences();

            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            HideStart();
            OnHideStarted?.Invoke();

            gameObject.SetActive(false);
            HideEnd();
            OnHideEnded?.Invoke();
        }

        public virtual void InstantShow()
        {
            StoreInitialValues();
            KillAllSequences();

            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            _canvasGroup.alpha = startValues.alpha;

            ShowStart();
            OnShowStarted?.Invoke();

            gameObject.SetActive(true);
            ShowEnd();
            OnShowEnded?.Invoke();
        }

        private void KillAllSequences() => CurrentSequence?.Kill();

        private void OnDisable()
        {
            KillAllSequences();
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
