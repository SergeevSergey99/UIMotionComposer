#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace UIPanelSystem.Editor
{
    /// <summary>
    /// Keeps UIPANEL_DOTWEEN in sync with whether DOTween is actually in the project.
    ///
    /// Odin publishes its own ODIN_INSPECTOR symbol, but DOTween ships no equivalent, so the
    /// package detects it here. The symbol is added when DG.Tweening.DOTween can be found and
    /// removed when it disappears, which is what lets the same UIPanel folder be dropped into a
    /// project with DOTween and one without and compile in both.
    /// </summary>
    [InitializeOnLoad]
    internal static class UIPanelDefineSymbols
    {
        private const string DoTweenSymbol = "UIPANEL_DOTWEEN";
        private const string DoTweenTypeName = "DG.Tweening.DOTween";

        static UIPanelDefineSymbols()
        {
            // Delayed: during a domain reload the assembly list is still settling, and changing
            // define symbols mid reload triggers a second compile for nothing.
            EditorApplication.delayCall += Sync;
        }

        [MenuItem("Tools/UI Panel/Refresh Plugin Detection")]
        private static void SyncFromMenu()
        {
            Sync();
            Debug.Log(IsDoTweenPresent()
                ? "[UIPanel] DOTween detected - animations run through DOTween."
                : "[UIPanel] DOTween not found - animations run through the built-in tween engine.");
        }

        private static void Sync()
        {
            bool shouldBeDefined = IsDoTweenPresent();
            NamedBuildTarget buildTarget = GetActiveBuildTarget();

            string current = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            var symbols = current
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(symbol => symbol.Trim())
                .Where(symbol => symbol.Length > 0)
                .ToList();

            bool isDefined = symbols.Contains(DoTweenSymbol);
            if (isDefined == shouldBeDefined)
                return;

            if (shouldBeDefined)
                symbols.Add(DoTweenSymbol);
            else
                symbols.Remove(DoTweenSymbol);

            PlayerSettings.SetScriptingDefineSymbols(buildTarget, string.Join(";", symbols));
        }

        private static NamedBuildTarget GetActiveBuildTarget()
        {
            BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
            return group == BuildTargetGroup.Unknown
                ? NamedBuildTarget.Standalone
                : NamedBuildTarget.FromBuildTargetGroup(group);
        }

        private static bool IsDoTweenPresent()
        {
            IEnumerable<System.Reflection.Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (System.Reflection.Assembly assembly in assemblies)
            {
                try
                {
                    if (assembly.GetType(DoTweenTypeName, false) != null)
                        return true;
                }
                catch (Exception)
                {
                    // Dynamic or unloadable assemblies cannot answer, and are never DOTween.
                }
            }

            return false;
        }
    }
}
#endif
