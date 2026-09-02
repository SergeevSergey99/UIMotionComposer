using UnityEngine;
using UnityEngine.EventSystems;

namespace UIMotionComposer
{
    /// <summary>
    /// Optional no-code bridge from UI/EventSystem events to animation IDs on a TweenPlayer.
    /// Leave an ID empty to ignore that event.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TweenPlayer))]
    [AddComponentMenu("UI/UI Motion Composer/UI Event Trigger")]
    public sealed class TweenUITrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler,
        ISubmitHandler, ICancelHandler
    {
        [SerializeField] private TweenPlayer player;
        [SerializeField] private CanvasGroup interactabilitySource;
        [SerializeField, Tooltip("Normally missing event mappings are ignored. Enable this only while diagnosing trigger setup.")]
        private bool logMissingAnimations;

        [Header("Pointer")]
        public string PointerEnter = TweenIds.Hover;
        public string PointerExit = TweenIds.Unhover;
        public string PointerDown = TweenIds.Click;
        public string PointerUp = TweenIds.Hover;

        [Header("Navigation")]
        public string Select;
        public string Deselect;
        public string Submit = TweenIds.Click;
        public string Cancel;

        public TweenPlayer Player => player;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CanInteract()) Play(PointerEnter);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Play(PointerExit);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (CanInteract()) Play(PointerDown);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (CanInteract()) Play(PointerUp);
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (CanInteract()) Play(Select);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            Play(Deselect);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (CanInteract()) Play(Submit);
        }

        public void OnCancel(BaseEventData eventData)
        {
            Play(Cancel);
        }

        public TweenHandle Play(string animationId)
        {
            if (player == null || string.IsNullOrWhiteSpace(animationId))
                return TweenHandle.Invalid;

            if (player.FindAnimation(animationId) == null)
            {
                if (logMissingAnimations)
                    Debug.LogWarning($"[UI Motion Composer] Trigger animation '{animationId}' was not found on {name}.", this);
                return TweenHandle.Invalid;
            }

            return player.Play(animationId);
        }

        private bool CanInteract()
        {
            return interactabilitySource == null || interactabilitySource.interactable;
        }

        private void ResolveReferences()
        {
            if (player == null)
                player = GetComponent<TweenPlayer>();
            if (interactabilitySource == null)
                interactabilitySource = GetComponent<CanvasGroup>();
        }
    }
}
