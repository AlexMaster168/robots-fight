using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RobotsFight.EditorTools
{
    public static class RobotsFightTools
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string ResDir = "Assets/RobotsFight/Resources";

        /// <summary>Материалы-базы в Resources: гарантируют, что Standard (и вариант с эмиссией) попадёт в билд.</summary>
        [InitializeOnLoadMethod]
        public static void EnsureMaterials()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null) return;
            EditorApplication.delayCall += () =>
            {
                Directory.CreateDirectory(ResDir);
                MakeMat($"{ResDir}/RF_Base.mat", false);
                MakeMat($"{ResDir}/RF_Glow.mat", true);
            };
        }

        static void MakeMat(string path, bool glow)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) return;
            var m = new Material(Shader.Find("Standard"));
            m.SetFloat("_Glossiness", 0.35f);
            if (glow)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Robots Fight/Create Main Scene")]
        public static void CreateMainScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "Robots Fight";
            PlayerSettings.companyName = "Lekha";
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[RobotsFight] Main scene created");
        }

        [MenuItem("Robots Fight/Build Windows")]
        public static void BuildWindows() => Build("Builds/Windows/RobotsFight.exe");

        /// <summary>Для батч-режима: -executeMethod RobotsFight.EditorTools.RobotsFightTools.BuildAutotest</summary>
        public static void BuildAutotest()
        {
            if (!File.Exists(ScenePath)) CreateMainScene();
            Build("Builds/Autotest/RobotsFight.exe");
        }

        static void Build(string path)
        {
            if (!File.Exists(ScenePath)) CreateMainScene();
            Directory.CreateDirectory(ResDir);
            MakeMat($"{ResDir}/RF_Base.mat", false);
            MakeMat($"{ResDir}/RF_Glow.mat", true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception($"Build failed: {report.summary.result}, errors: {report.summary.totalErrors}");
            Debug.Log($"[RobotsFight] Build OK → {path}");
        }
    }
}
