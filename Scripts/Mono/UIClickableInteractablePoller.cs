using UIPanelSystem.Inspector;
using UnityEngine;

namespace UIPanelSystem
{
    /// <summary>
    /// Watches CanvasGroup.interactable every frame and forwards changes to the clickable controller.
    ///
    /// Only needed when something outside the controller writes canvasGroup.interactable directly and
    /// cannot be changed to call <see cref="BaseUIClickableController.SetInteractable"/>. It is a
    /// separate component precisely so the per-frame cost lands on the handful of buttons that need
    /// it, instead of on every button in the project.
    /// </summary>
    [RequireComponent(typeof(BaseUIClickableController))]
    [AddComponentMenu("UI/UI Panel/Clickable Interactable Poller")]
    public sealed class UIClickableInteractablePoller : MonoBehaviour
    {
        [SerializeField, LabelText("Controller")]
        private BaseUIClickableController _controller;

        private void Awake()
        {
            if (_controller == null)
                _controller = GetComponent<BaseUIClickableController>();
        }

        private void Update()
        {
            _controller.RefreshInteractableState();
        }

        private void OnValidate()
        {
            if (_controller == null)
                _controller = GetComponent<BaseUIClickableController>();
        }
    }
}
