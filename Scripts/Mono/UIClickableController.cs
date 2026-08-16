using UIPanelSystem.Inspector;
using UnityEngine;

namespace UIPanelSystem
{
    /// <summary>
    /// Clickable driven either by shared preset assets or by animation data authored on the
    /// component. Assigning a preset hides the corresponding inline data and takes precedence.
    /// </summary>
    public class UIClickableController : BaseUIClickableController
    {
        [BoxGroup("Hover Settings")]
        [LabelText("Hover Animation Preset"), SerializeField]
        protected UIAnimationPresetSO hoverAnimationPresetSo;

        [BoxGroup("Click Settings")]
        [LabelText("Click Animation Preset"), SerializeField]
        protected UIAnimationPresetSO clickAnimationPresetSo;

        [BoxGroup("Disable Settings")]
        [LabelText("Disable Animation Preset"), SerializeField]
        protected UIAnimationPresetSO disableAnimationPresetSo;

        [BoxGroup("Return Animations")]
        [LabelText("Return From Hover Preset"), SerializeField]
        protected UIAnimationPresetSO returnFromHoverPresetSo;

        [BoxGroup("Return Animations")]
        [LabelText("Return From Click Preset"), SerializeField]
        protected UIAnimationPresetSO returnFromClickPresetSo;

        [BoxGroup("Return Animations")]
        [LabelText("Return From Disable Preset"), SerializeField]
        protected UIAnimationPresetSO returnFromDisablePresetSo;

        bool hoverPresetSetted => hoverAnimationPresetSo != null;
        bool clickPresetSetted => clickAnimationPresetSo != null;
        bool disablePresetSetted => disableAnimationPresetSo != null;
        bool returnFromHoverPresetSetted => returnFromHoverPresetSo != null;
        bool returnFromClickPresetSetted => returnFromClickPresetSo != null;
        bool returnFromDisablePresetSetted => returnFromDisablePresetSo != null;

        [HideIf(nameof(hoverPresetSetted)), BoxGroup("Hover Settings"), SerializeField]
        [HideLabel] protected AnimationData HoverAnimationData;

        [HideIf(nameof(clickPresetSetted)), BoxGroup("Click Settings"), SerializeField]
        [HideLabel] protected AnimationData ClickAnimationData;

        [HideIf(nameof(disablePresetSetted)), BoxGroup("Disable Settings"), SerializeField]
        [HideLabel] protected AnimationData DisableAnimationData;

        [HideIf(nameof(returnFromHoverPresetSetted)), BoxGroup("Return Animations"), SerializeField]
        [HideLabel] protected AnimationData ReturnFromHoverAnimationData;

        [HideIf(nameof(returnFromClickPresetSetted)), BoxGroup("Return Animations"), SerializeField]
        [HideLabel] protected AnimationData ReturnFromClickAnimationData;

        [HideIf(nameof(returnFromDisablePresetSetted)), BoxGroup("Return Animations"), SerializeField]
        [HideLabel] protected AnimationData ReturnFromDisableAnimationData;

        public override AnimationData CurrentHoverAnimationData =>
            hoverPresetSetted ? hoverAnimationPresetSo.AnimationData : HoverAnimationData;

        public override AnimationData CurrentClickAnimationData =>
            clickPresetSetted ? clickAnimationPresetSo.AnimationData : ClickAnimationData;

        public override AnimationData CurrentDisableAnimationData =>
            disablePresetSetted ? disableAnimationPresetSo.AnimationData : DisableAnimationData;

        public override AnimationData CurrentReturnFromHoverAnimationData =>
            returnFromHoverPresetSetted ? returnFromHoverPresetSo.AnimationData : ReturnFromHoverAnimationData;

        public override AnimationData CurrentReturnFromClickAnimationData =>
            returnFromClickPresetSetted ? returnFromClickPresetSo.AnimationData : ReturnFromClickAnimationData;

        public override AnimationData CurrentReturnFromDisableAnimationData =>
            returnFromDisablePresetSetted ? returnFromDisablePresetSo.AnimationData : ReturnFromDisableAnimationData;
    }
}
