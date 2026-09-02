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
    /// Thin Show/Hide wrapper around TweenPlayer. It owns panel activation and CanvasGroup input
    /// state while the player remains responsible only for motion.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup), typeof(TweenPlayer))]
    [AddComponentMenu("UI/UI Motion Composer/Tween UI Panel")]
    public sealed class TweenUIPanel : MonoBehaviour
    {
        [SerializeField] private TweenPlayer player;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Animations")]
        [SerializeField] private string showAnimation = TweenIds.Show;
        [SerializeField] private string hideAnimation = TweenIds.Hide;

        [Header("Behaviour")]
        [SerializeField] private bool hideOnAwake = true;
        [SerializeField] private bool deactivateWhenHidden = true;
        [SerializeField] private bool manageInteractability = true;
        [SerializeField] private bool interactableWhileShowing;

        [Header("Events")]
        [SerializeField] private UnityEvent onShowStarted = new UnityEvent();
        [SerializeField] private UnityEvent onShowCompleted = new UnityEvent();
        [SerializeField] private UnityEvent onShowCancelled = new UnityEvent();
        [SerializeField] private UnityEvent onHideStarted = new UnityEvent();
        [SerializeField] private UnityEvent onHideCompleted = new UnityEvent();
        [SerializeField] private UnityEvent onHideCancelled = new UnityEvent();

        private TweenHandle _transition = TweenHandle.Invalid;
        private int _transitionVersion;
        private bool _showRequestedBeforeAwake;
        private bool _initialized;

        /// <summary>Direction of a transition cut short by deactivation; Hidden means "none pending".</summary>
        private TweenPanelState _interruptedFrom = TweenPanelState.Hidden;

        public TweenPlayer Player => player;
        public CanvasGroup CanvasGroup => canvasGroup;
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

        public event Action ShowStarted;
        public event Action ShowCompleted;
        public event Action ShowCancelled;
        public event Action HideStarted;
        public event Action HideCompleted;
        public event Action HideCancelled;

        private void Awake()
        {
            ResolveReferences();
            _initialized = true;

            if (hideOnAwake && !_showRequestedBeforeAwake)
                ApplyHiddenState(deactivateWhenHidden);
            else
            {
                State = TweenPanelState.Visible;
                SetInteraction(true);
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public void Show()
        {
            Show(null);
        }

        public void Show(Action callback)
        {
            ResolveReferences();
            int version = BeginTransition();
            _showRequestedBeforeAwake = true;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            _showRequestedBeforeAwake = false;

            State = TweenPanelState.Showing;
            SetInteraction(interactableWhileShowing);
            onShowStarted?.Invoke();
            ShowStarted?.Invoke();

            _transition = player != null && !string.IsNullOrWhiteSpace(showAnimation)
                ? player.Play(showAnimation)
                : TweenHandle.Invalid;
            if (!_transition.IsValid)
            {
                CompleteShow(version, callback);
                return;
            }

            _transition
                .OnCompleted(() => CompleteShow(version, callback))
                .OnCancelled(() => CancelShow(version));
        }

        public void Hide()
        {
            Hide(null);
        }

        public void Hide(Action callback)
        {
            ResolveReferences();
            if (!gameObject.activeSelf)
            {
                // Nothing to animate, but callers still get the same started/completed pair they
                // would get from a real transition.
                int inactiveVersion = BeginTransition();
                State = TweenPanelState.Hiding;
                SetInteraction(false);
                onHideStarted?.Invoke();
                HideStarted?.Invoke();
                CompleteHide(inactiveVersion, callback);
                return;
            }

            int version = BeginTransition();
            State = TweenPanelState.Hiding;
            SetInteraction(false);
            onHideStarted?.Invoke();
            HideStarted?.Invoke();

            _transition = player != null && !string.IsNullOrWhiteSpace(hideAnimation)
                ? player.Play(hideAnimation)
                : TweenHandle.Invalid;
            if (!_transition.IsValid)
            {
                CompleteHide(version, callback);
                return;
            }

            _transition
                .OnCompleted(() => CompleteHide(version, callback))
                .OnCancelled(() => CancelHide(version));
        }

        public void InstantShow()
        {
            ResolveReferences();
            int version = BeginTransition();
            _showRequestedBeforeAwake = true;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            _showRequestedBeforeAwake = false;

            State = TweenPanelState.Showing;
            SetInteraction(false);
            onShowStarted?.Invoke();
            ShowStarted?.Invoke();

            TweenHandle handle = player != null && !string.IsNullOrWhiteSpace(showAnimation)
                ? player.Play(showAnimation)
                : TweenHandle.Invalid;
            if (handle.IsValid)
                handle.Complete();
            CompleteShow(version, null);
        }

        public void InstantHide()
        {
            ResolveReferences();
            int version = BeginTransition();
            State = TweenPanelState.Hiding;
            SetInteraction(false);
            onHideStarted?.Invoke();
            HideStarted?.Invoke();

            TweenHandle handle = player != null && !string.IsNullOrWhiteSpace(hideAnimation)
                ? player.Play(hideAnimation)
                : TweenHandle.Invalid;
            if (handle.IsValid)
                handle.Complete();
            CompleteHide(version, null);
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

        private int BeginTransition()
        {
            int version = ++_transitionVersion;
            if (_transition.IsActive)
                _transition.Stop();
            _transition = TweenHandle.Invalid;
            return version;
        }

        private void CompleteShow(int version, Action callback)
        {
            if (version != _transitionVersion)
                return;

            _transition = TweenHandle.Invalid;
            State = TweenPanelState.Visible;
            SetInteraction(true);
            callback?.Invoke();
            onShowCompleted?.Invoke();
            ShowCompleted?.Invoke();
        }

        private void CancelShow(int version)
        {
            if (version != _transitionVersion)
                return;

            _transition = TweenHandle.Invalid;
            State = TweenPanelState.Visible;
            SetInteraction(true);
            onShowCancelled?.Invoke();
            ShowCancelled?.Invoke();
        }

        private void CompleteHide(int version, Action callback)
        {
            if (version != _transitionVersion)
                return;

            _transition = TweenHandle.Invalid;
            State = TweenPanelState.Hidden;
            SetInteraction(false);
            callback?.Invoke();
            onHideCompleted?.Invoke();
            HideCompleted?.Invoke();

            if (deactivateWhenHidden && gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void CancelHide(int version)
        {
            if (version != _transitionVersion)
                return;

            _transition = TweenHandle.Invalid;
            State = TweenPanelState.Visible;
            SetInteraction(true);
            onHideCancelled?.Invoke();
            HideCancelled?.Invoke();
        }

        private void ApplyHiddenState(bool deactivate)
        {
            State = TweenPanelState.Hidden;
            SetInteraction(false);
            if (deactivate && gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void SetInteraction(bool value)
        {
            if (!manageInteractability || canvasGroup == null)
                return;

            canvasGroup.interactable = value;
            canvasGroup.blocksRaycasts = value;
        }

        private void ResolveReferences()
        {
            if (player == null)
                player = GetComponent<TweenPlayer>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnDisable()
        {
            // Completion-driven deactivation reaches this with an already terminal handle.
            if (!_initialized || !_transition.IsActive)
                return;

            ++_transitionVersion;
            _transition.Stop();
            _transition = TweenHandle.Invalid;
            _interruptedFrom = State;
            State = TweenPanelState.Hidden;
            SetInteraction(false);
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

            State = wasShowing ? TweenPanelState.Visible : TweenPanelState.Hidden;
            SetInteraction(wasShowing);
        }

        private void OnDestroy()
        {
            if (_transition.IsActive)
                _transition.Stop();
        }
    }
}
