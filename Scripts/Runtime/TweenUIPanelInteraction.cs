using UnityEngine;

namespace UIMotionComposer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TweenUIPanel), typeof(CanvasGroup))]
    [AddComponentMenu("UI/UI Motion Composer/Tween UI Panel Interaction")]
    public sealed class TweenUIPanelInteraction : MonoBehaviour
    {
        [SerializeField] private TweenUIPanel panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool interactableWhileShowing;

        // Subscribed for the whole lifetime, not OnEnable/OnDisable: the panel changes state while
        // its GameObject is inactive, and the CanvasGroup must already match when it comes back.
        private void Awake()
        {
            panel.StateChanged += Apply;
            Apply(panel.State);
        }

        private void OnDestroy()
        {
            if (panel != null)
                panel.StateChanged -= Apply;
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void Apply(TweenPanelState state)
        {
            bool value = state == TweenPanelState.Visible ||
                         state == TweenPanelState.Showing && interactableWhileShowing;
            canvasGroup.interactable = value;
            canvasGroup.blocksRaycasts = value;
        }

        private void ResolveReferences()
        {
            if (panel == null)
                panel = GetComponent<TweenUIPanel>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}
