using System.Collections.Generic;
using UIPanelSystem.Inspector;
using UnityEngine;

public class UIMultiPanelPresetsController : UIPanelPresetsController
{
    [SerializeField, BoxGroup("Multi Panel Settings")]
    [LabelText("Panels"), Tooltip("The preset for multi-panel animations.")]
    private List<BaseUIPanelController> panels;

    [SerializeField, BoxGroup("Multi Panel Settings")]
    [LabelText("Panels On End Show"), Tooltip("List of panels to show at the end of the show animation.")]
    private List<BaseUIPanelController> panelsOnEndShow;
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
