using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UIMotionComposer
{
    public enum TweenClickableState
    {
        Normal,
        Hovered,
        Pressed,
        Disabled,
        Selected
    }

    /// <summary>
    /// Stateful EventSystem wrapper around TweenPlayer. Unlike TweenUITrigger it understands UI
    /// interaction state, so an infinite Hover animation is always stopped before Normal,
    /// Pressed or Disabled starts.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup), typeof(TweenPlayer))]
    [AddComponentMenu("UI/UI Motion Composer/Tween UI Clickable")]
    public sealed class TweenUIClickable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler,
        ISubmitHandler, ICancelHandler
    {
        [SerializeField] private TweenPlayer player;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Selectable selectable;

        [Header("State animations")]
        [SerializeField] private string normalAnimation = TweenIds.Unhover;
        [SerializeField] private string hoverAnimation = TweenIds.Hover;
        [SerializeField] private string pressedAnimation = TweenIds.Click;
        [SerializeField] private string selectedAnimation = TweenIds.Hover;
        [SerializeField] private string disabledAnimation = TweenIds.Disabled;
        [SerializeField] private string interactableAnimation = TweenIds.Interactable;

        [Header("Events")]
        [SerializeField] private UnityEvent onHoverStarted = new UnityEvent();
        [SerializeField] private UnityEvent onHoverEnded = new UnityEvent();
        [SerializeField] private UnityEvent onPressed = new UnityEvent();
        [SerializeField] private UnityEvent onReleased = new UnityEvent();
        [SerializeField] private UnityEvent onInteractableChanged = new UnityEvent();

        private TweenHandle _stateTween = TweenHandle.Invalid;
        private bool _pointerInside;
        private bool _pressed;
        private bool _selected;
        private bool _wasInteractable = true;

        public TweenPlayer Player => player;
        public CanvasGroup CanvasGroup => canvasGroup;
        public Selectable Selectable => selectable;
        public TweenClickableState State { get; private set; } = TweenClickableState.Normal;
        public bool IsInteractable => CanInteract();
        public bool IsPointerInside => _pointerInside;
        public bool IsPressed => _pressed;
        public bool IsSelected => _selected;

        public string NormalAnimationId { get => normalAnimation; set => normalAnimation = value; }
        public string HoverAnimationId { get => hoverAnimation; set => hoverAnimation = value; }
        public string PressedAnimationId { get => pressedAnimation; set => pressedAnimation = value; }
        public string SelectedAnimationId { get => selectedAnimation; set => selectedAnimation = value; }
        public string DisabledAnimationId { get => disabledAnimation; set => disabledAnimation = value; }
        public string InteractableAnimationId { get => interactableAnimation; set => interactableAnimation = value; }

        public event Action HoverStarted;
        public event Action HoverEnded;
        public event Action Pressed;
        public event Action Released;
        public event Action InteractableChanged;
        public event Action<TweenClickableState> StateChanged;

        private void Awake()
        {
            ResolveReferences();
            _wasInteractable = CanInteract();
            State = _wasInteractable ? TweenClickableState.Normal : TweenClickableState.Disabled;
        }

        private void OnEnable()
        {
            ResolveReferences();
            _pointerInside = false;
            _pressed = false;
            _selected = false;
            _wasInteractable = CanInteract();
            TweenClickableState state = _wasInteractable
                ? TweenClickableState.Normal
                : TweenClickableState.Disabled;
            TransitionTo(state, Application.isPlaying
                ? (state == TweenClickableState.Disabled ? disabledAnimation : normalAnimation)
                : null);
        }

        private void OnDisable()
        {
            StopStateTween();
            _pointerInside = false;
            _pressed = false;
            _selected = false;
            State = CanInteract() ? TweenClickableState.Normal : TweenClickableState.Disabled;
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            if (!CanInteract())
                return;

            onHoverStarted?.Invoke();
            HoverStarted?.Invoke();
            if (!_pressed && !_selected)
                TransitionTo(TweenClickableState.Hovered, hoverAnimation);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            if (!CanInteract() || _pressed)
                return;

            onHoverEnded?.Invoke();
            HoverEnded?.Invoke();
            TransitionTo(_selected ? TweenClickableState.Selected : TweenClickableState.Normal,
                _selected ? selectedAnimation : normalAnimation);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanInteract())
                return;

            _pressed = true;
            onPressed?.Invoke();
            Pressed?.Invoke();
            TransitionTo(TweenClickableState.Pressed, pressedAnimation);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pressed)
                return;

            _pressed = false;
            onReleased?.Invoke();
            Released?.Invoke();
            if (!CanInteract())
            {
                TransitionTo(TweenClickableState.Disabled, disabledAnimation);
                return;
            }

            ReturnToRestState();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            if (CanInteract() && !_pressed)
            {
                if (!_pointerInside)
                {
                    onHoverStarted?.Invoke();
                    HoverStarted?.Invoke();
                }
                TransitionTo(TweenClickableState.Selected, selectedAnimation);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            if (CanInteract() && !_pressed)
            {
                if (!_pointerInside)
                {
                    onHoverEnded?.Invoke();
                    HoverEnded?.Invoke();
                }
                TransitionTo(_pointerInside ? TweenClickableState.Hovered : TweenClickableState.Normal,
                    _pointerInside ? hoverAnimation : normalAnimation);
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!CanInteract())
                return;

            onPressed?.Invoke();
            Pressed?.Invoke();
            TransitionTo(TweenClickableState.Pressed, pressedAnimation);
            TweenHandle submittedTween = _stateTween;
            if (!submittedTween.IsValid)
            {
                onReleased?.Invoke();
                Released?.Invoke();
                ReturnToRestState();
                return;
            }
            submittedTween.OnCompleted(() =>
            {
                if (_stateTween == submittedTween && State == TweenClickableState.Pressed)
                {
                    onReleased?.Invoke();
                    Released?.Invoke();
                    ReturnToRestState();
                }
            });
        }

        public void OnCancel(BaseEventData eventData)
        {
            _pressed = false;
            _selected = false;
            if (CanInteract())
                TransitionTo(_pointerInside ? TweenClickableState.Hovered : TweenClickableState.Normal,
                    _pointerInside ? hoverAnimation : normalAnimation);
        }

        /// <summary>Updates both the CanvasGroup and an optional Selectable, then animates the state.</summary>
        public void SetInteractable(bool value)
        {
            ResolveReferences();
            if (canvasGroup != null)
                canvasGroup.interactable = value;
            if (selectable != null)
                selectable.interactable = value;
            RefreshInteractableState();
        }

        /// <summary>Call after another system changes CanvasGroup/Selectable interactability.</summary>
        public void RefreshInteractableState()
        {
            RefreshInteractableState(true);
        }

        public TweenHandle PlayCurrentState()
        {
            return TransitionTo(State, AnimationFor(State));
        }

        /// <summary>Forces a state animation. Intended for diagnostics and custom UI tooling.</summary>
        public TweenHandle PlayStateAnimation(TweenClickableState state)
        {
            return TransitionTo(state, AnimationFor(state));
        }

        private void RefreshInteractableState(bool animate)
        {
            bool current = CanInteract();
            if (current == _wasInteractable && (current || State == TweenClickableState.Disabled))
                return;

            _wasInteractable = current;
            _pressed = false;
            if (!current)
            {
                _pointerInside = false;
                TransitionTo(TweenClickableState.Disabled, animate ? disabledAnimation : null);
            }
            else
            {
                TweenClickableState rest = RestState();
                string animationId = animate
                    ? (rest == TweenClickableState.Selected ? selectedAnimation :
                        rest == TweenClickableState.Hovered ? hoverAnimation : interactableAnimation)
                    : null;
                TransitionTo(rest, animationId);
            }

            if (animate)
            {
                onInteractableChanged?.Invoke();
                InteractableChanged?.Invoke();
            }
        }

        private void ReturnToRestState()
        {
            TweenClickableState rest = RestState();
            TransitionTo(rest, AnimationFor(rest));
        }

        private TweenClickableState RestState()
        {
            if (_selected)
                return TweenClickableState.Selected;
            return _pointerInside ? TweenClickableState.Hovered : TweenClickableState.Normal;
        }

        private TweenHandle TransitionTo(TweenClickableState nextState, string animationId)
        {
            StopStateTween();
            State = nextState;
            StateChanged?.Invoke(nextState);

            if (player == null)
                ResolveReferences();

            if (player == null || string.IsNullOrWhiteSpace(animationId) ||
                player.FindAnimation(animationId) == null)
            {
                _stateTween = TweenHandle.Invalid;
                return _stateTween;
            }

            _stateTween = player.Play(animationId);
            return _stateTween;
        }

        private void StopStateTween()
        {
            if (_stateTween.IsActive)
                _stateTween.Stop();
            _stateTween = TweenHandle.Invalid;
        }

        private string AnimationFor(TweenClickableState state)
        {
            return state switch
            {
                TweenClickableState.Hovered => hoverAnimation,
                TweenClickableState.Pressed => pressedAnimation,
                TweenClickableState.Selected => selectedAnimation,
                TweenClickableState.Disabled => disabledAnimation,
                _ => normalAnimation
            };
        }

        private bool CanInteract()
        {
            return (canvasGroup == null || canvasGroup.interactable) &&
                   (selectable == null || selectable.interactable);
        }

        private void ResolveReferences()
        {
            if (player == null)
                player = GetComponent<TweenPlayer>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (selectable == null)
                selectable = GetComponent<Selectable>();
        }
    }
}
