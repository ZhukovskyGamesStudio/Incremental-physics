using UnityEditor;
using UnityEngine;

namespace ChalkPhysics.EditorTools
{
    /// Command-line macOS build: a universal (Intel + Apple Silicon) Mono player, ad-hoc signed by Unity, unsigned otherwise.
    /// Unity.exe -batchmode -quit -projectPath <project> -buildTarget OSXUniversal -executeMethod ChalkPhysics.EditorTools.MacBuild.Build
    public static class MacBuild
    {
        public static void Build()
        {
            SetUniversal();
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Game.unity" },
                locationPathName = "Build/macOS/Incremental physics.app",
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[MacBuild] {report.summary.result}, {report.summary.totalSize / 1048576} MB, errors {report.summary.totalErrors}");
            // back to Windows, the platform the editor is normally opened on
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        // UnityEditor.OSXStandalone.UserBuildSettings lives in the macOS module's assembly, so it is reached by name
        static void SetUniversal()
        {
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType("UnityEditor.OSXStandalone.UserBuildSettings");
                var p = t?.GetProperty("architecture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (p == null) continue;
                p.SetValue(null, System.Enum.Parse(p.PropertyType, "x64ARM64"));
                Debug.Log("[MacBuild] architecture " + p.GetValue(null));
                return;
            }
            Debug.LogWarning("[MacBuild] could not set the architecture");
        }
    }
}
