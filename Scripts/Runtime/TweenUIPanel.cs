using System;
using UnityEngine;
using UnityEngine.Events;

namespace UIMotionComposer
{
    public enum TweenPanelState
    {
        Hidden,
        Showing,
        Visible,
        Hiding
    }

    /// <summary>
    /// Thin Show/Hide wrapper around TweenPlayer. It owns panel activation while the player remains
    /// responsible only for motion.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(TweenPlayer))]
    [AddComponentMenu("UI/UI Motion Composer/Tween UI Panel")]
    public sealed class TweenUIPanel : MonoBehaviour
    {
        [SerializeField] private TweenPlayer player;

        [Header("Animations")]
        [SerializeField] private string showAnimation = TweenIds.Show;
        [SerializeField] private string hideAnimation = TweenIds.Hide;

        [Header("Behaviour")]
        [SerializeField] private bool hideOnAwake = true;
        [SerializeField] private bool deactivateWhenHidden = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onShowStarted = new UnityEvent();
        [SerializeField] private UnityEvent onShowCompleted = new UnityEvent();
        [SerializeField] private UnityEvent onShowCancelled = new UnityEvent();
        [SerializeField] private UnityEvent onHideStarted = new UnityEvent();
        [SerializeField] private UnityEvent onHideCompleted = new UnityEvent();
        [SerializeField] private UnityEvent onHideCancelled = new UnityEvent();

        private TweenHandle _transition = TweenHandle.Invalid;
        private bool _showRequestedBeforeAwake;
        private bool _initialized;

        /// <summary>Direction of a transition cut short by deactivation; Hidden means "none pending".</summary>
        private TweenPanelState _interruptedFrom = TweenPanelState.Hidden;

        public TweenPlayer Player => player;
        public TweenPanelState State { get; private set; } = TweenPanelState.Visible;
        public bool IsVisible => State is TweenPanelState.Visible or TweenPanelState.Showing;
        public bool IsTransitioning => _transition.IsActive;

        public string ShowAnimationId
        {
            get => showAnimation;
            set => showAnimation = value;
        }

        public string HideAnimationId
        {
            get => hideAnimation;
            set => hideAnimation = value;
        }

        public bool HideOnAwake
        {
            get => hideOnAwake;
            set => hideOnAwake = value;
        }

        public bool DeactivateWhenHidden
        {
            get => deactivateWhenHidden;
            set => deactivateWhenHidden = value;
        }

        public event Action<TweenPanelState> StateChanged;
        public event Action ShowStarted;
        public event Action ShowCompleted;
        public event Action ShowCancelled;
        public event Action HideStarted;
        public event Action HideCompleted;
        public event Action HideCancelled;

        private void Awake()
        {
            _initialized = true;

            if (hideOnAwake && !_showRequestedBeforeAwake)
                ApplyHiddenState(deactivateWhenHidden);
            else
                SetState(TweenPanelState.Visible);
        }

        private void OnValidate()
        {
            if (player == null)
                player = GetComponent<TweenPlayer>();
        }

        public void Show()
        {
            Show(null);
        }

        public void Show(Action callback)
        {
            BeginShow(callback, false);
        }

        public void Hide()
        {
            Hide(null);
        }

        public void Hide(Action callback)
        {
            BeginHide(callback, false);
        }

        public void InstantShow()
        {
            BeginShow(null, true);
        }

        public void InstantHide()
        {
            BeginHide(null, true);
        }

        public void SetVisible(bool visible, bool instant = false)
        {
            if (visible)
            {
                if (instant) InstantShow();
                else Show();
            }
            else
            {
                if (instant) InstantHide();
                else Hide();
            }
        }

        // State and side effects come before the events in every step below, so a listener that
        // calls Show or Hide again starts from a settled panel and nothing after it overwrites the result.
        private void BeginShow(Action callback, bool instant)
        {
            StopTransition();
            _showRequestedBeforeAwake = true;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            _showRequestedBeforeAwake = false;

            TweenHandle handle = Play(showAnimation);
            if (instant)
            {
                handle.Complete();
                handle = TweenHandle.Invalid;
            }

            _transition = handle;
            SetState(TweenPanelState.Showing);
            if (handle.IsValid)
                handle.OnCompleted(() => CompleteShow(callback)).OnCancelled(() => CancelShow(handle));

            onShowStarted?.Invoke();
            ShowStarted?.Invoke();

            if (State == TweenPanelState.Showing && !_transition.IsValid)
                CompleteShow(callback);
        }

        private void BeginHide(Action callback, bool instant)
        {
            StopTransition();

            // An inactive panel has nothing to animate, but InstantHide still snaps it to the hidden pose.
            TweenHandle handle = instant || gameObject.activeSelf ? Play(hideAnimation) : TweenHandle.Invalid;
            if (instant)
            {
                handle.Complete();
                handle = TweenHandle.Invalid;
            }

            _transition = handle;
            SetState(TweenPanelState.Hiding);
            if (handle.IsValid)
                handle.OnCompleted(() => CompleteHide(callback)).OnCancelled(() => CancelHide(handle));

            onHideStarted?.Invoke();
            HideStarted?.Invoke();

            if (State == TweenPanelState.Hiding && !_transition.IsValid)
                CompleteHide(callback);
        }

        private TweenHandle Play(string animationId)
        {
            return player != null && !string.IsNullOrWhiteSpace(animationId)
                ? player.Play(animationId)
                : TweenHandle.Invalid;
        }

        // Clearing the field before Stop tells the cancelled callback that a newer transition owns the state.
        private void StopTransition()
        {
            TweenHandle previous = _transition;
            _transition = TweenHandle.Invalid;
            previous.Stop();
        }

        private void CompleteShow(Action callback)
        {
            _transition = TweenHandle.Invalid;
            SetState(TweenPanelState.Visible);
            callback?.Invoke();
            onShowCompleted?.Invoke();
            ShowCompleted?.Invoke();
        }

        private void CancelShow(TweenHandle handle)
        {
            if (_transition == handle)
            {
                _transition = TweenHandle.Invalid;
                SetState(TweenPanelState.Visible);
            }

            onShowCancelled?.Invoke();
            ShowCancelled?.Invoke();
        }

        private void CompleteHide(Action callback)
        {
            _transition = TweenHandle.Invalid;
            SetState(TweenPanelState.Hidden);
            if (deactivateWhenHidden && gameObject.activeSelf)
                gameObject.SetActive(false);

            callback?.Invoke();
            onHideCompleted?.Invoke();
            HideCompleted?.Invoke();
        }

        private void CancelHide(TweenHandle handle)
        {
            if (_transition == handle)
            {
                _transition = TweenHandle.Invalid;
                SetState(TweenPanelState.Visible);
            }

            onHideCancelled?.Invoke();
            HideCancelled?.Invoke();
        }

        private void ApplyHiddenState(bool deactivate)
        {
            SetState(TweenPanelState.Hidden);
            if (deactivate && gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void SetState(TweenPanelState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        private void OnDisable()
        {
            // Completion-driven deactivation reaches this with an already terminal handle.
            if (!_initialized || !_transition.IsActive)
                return;

            _interruptedFrom = State;
            StopTransition();
            SetState(TweenPanelState.Hidden);
        }

        /// <summary>
        /// Settles the state left behind when a transition was cut short by the object being
        /// deactivated. Without this a panel interrupted mid-Show comes back marked Hidden with
        /// input switched off — on screen but dead. The interrupted direction decides the outcome;
        /// neither case animates, so call <see cref="Show()"/> or <see cref="Hide()"/> for that.
        /// </summary>
        private void OnEnable()
        {
            if (!_initialized || _interruptedFrom == TweenPanelState.Hidden)
                return;

            bool wasShowing = _interruptedFrom == TweenPanelState.Showing;
            _interruptedFrom = TweenPanelState.Hidden;

            SetState(wasShowing ? TweenPanelState.Visible : TweenPanelState.Hidden);
        }

        private void OnDestroy()
        {
            StopTransition();
        }
    }
}
