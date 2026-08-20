using UIMotionComposer.Inspector;
using UnityEngine;

namespace UIMotionComposer
{
    /// <summary>
    /// Panel driven either by shared preset assets or by animation data authored on the component.
    /// Assigning a preset hides the corresponding inline data and takes precedence over it.
    /// </summary>
    public class UIPanelController : BaseUIPanelController
    {
        [BoxGroup("SO Settings")]
        [LabelText("Show Animation Preset"), SerializeField]
        protected UIAnimationPresetSO showAnimationPresetSo;

        [BoxGroup("SO Settings")]
        [LabelText("Hide Animation Preset"), SerializeField]
        protected UIAnimationPresetSO hideAnimationPresetSo;

        bool showPresetSetted => showAnimationPresetSo != null;
        bool hidePresetSetted => hideAnimationPresetSo != null;

        [HideIf(nameof(showPresetSetted)), BoxGroup("Show Duration"), SerializeField]
        [HideLabel] protected AnimationData ShowAnimationData;

        [HideIf(nameof(hidePresetSetted)), BoxGroup("Hide Duration"), SerializeField]
        [HideLabel] protected AnimationData HideAnimationData;

        public override AnimationData CurrentShowAnimationData =>
            showPresetSetted ? showAnimationPresetSo.AnimationData : ShowAnimationData;

        public override AnimationData CurrentHideAnimationData =>
            hidePresetSetted ? hideAnimationPresetSo.AnimationData : HideAnimationData;
    }
}
