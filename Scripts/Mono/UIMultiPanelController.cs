using System.Collections.Generic;
using UIMotionComposer.Inspector;
using UnityEngine;

namespace UIMotionComposer
{
    /// <summary>
    /// Panel that drives other panels alongside its own animation.
    /// </summary>
    public class UIMultiPanelController : UIPanelController
    {
        [SerializeField, BoxGroup("Multi Panel Settings")]
        [LabelText("Panels"), Tooltip("Panels shown and hidden together with this one.")]
        private List<BaseUIPanelController> panels = new List<BaseUIPanelController>();

        [SerializeField, BoxGroup("Multi Panel Settings")]
        [LabelText("Panels On End Show"), Tooltip("Panels shown once this panel's show animation finishes.")]
        private List<BaseUIPanelController> panelsOnEndShow = new List<BaseUIPanelController>();

        protected override void ShowStart()
        {
            base.ShowStart();

            foreach (var panel in panels)
            {
                panel?.Show();
            }
        }

        protected override void ShowEnd()
        {
            base.ShowEnd();
            foreach (var panel in panelsOnEndShow)
            {
                panel?.Show();
            }
        }

        protected override void HideStart()
        {
            base.HideStart();
            foreach (var panel in panels)
            {
                panel?.Hide();
            }
        }

        protected override void HideEnd()
        {
            base.HideEnd();
            foreach (var panel in panelsOnEndShow)
            {
                panel?.InstantHide();
            }
        }
    }
}
