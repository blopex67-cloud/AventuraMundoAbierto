#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AdventureBuild
{
    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    private static string ScenePath => Path.Combine(ProjectRoot, "Assets", "Scenes", "Main.unity");
    private static string ApkPath => Path.Combine(ProjectRoot, "Build", "AventuraMundoAbierto.apk");

    [MenuItem("Tools/Aventura/Crear escena de juego")]
    public static void CreateScene()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("World Bootstrap");
        root.AddComponent<WorldBootstrap>();
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new Exception("No se pudo guardar la escena principal: " + ScenePath);
        AssetDatabase.Refresh();
        Debug.Log("Escena principal creada en " + ScenePath);
    }

    public static void BuildAndroid()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
        CreateScene();

        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("Android build failed: " + report.summary.result);
        if (!File.Exists(ApkPath) || new FileInfo(ApkPath).Length == 0)
            throw new Exception("Unity terminó sin generar un APK válido en " + ApkPath);
        Debug.Log("APK generado: " + ApkPath + " (" + new FileInfo(ApkPath).Length + " bytes)");
    }
}
#endif
