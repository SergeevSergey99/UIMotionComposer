#if UNITY_EDITOR && !ODIN_INSPECTOR
using UnityEditor;

namespace UIPanelSystem.Inspector.Editor
{
    /// <summary>
    /// Hooks the fallback inspector onto the package's own base types only.
    ///
    /// Deliberately not a project wide fallback editor: other assets ship their own inspector
    /// replacements, and claiming every MonoBehaviour would put this package in a fight it has no
    /// reason to join. Compiled out entirely when Odin is installed.
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(BaseUIPanelController), true)]
    internal sealed class BaseUIPanelControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => UIPanelInspectorGUI.DrawInspector(serializedObject);
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(BaseUIClickableController), true)]
    internal sealed class BaseUIClickableControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => UIPanelInspectorGUI.DrawInspector(serializedObject);
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(UIAnimationPresetSO), true)]
    internal sealed class UIAnimationPresetSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => UIPanelInspectorGUI.DrawInspector(serializedObject);
    }
}
#endif
