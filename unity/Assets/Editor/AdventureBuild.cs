#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
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
        RegisterMainScene();
        Debug.Log("Escena principal creada en " + ScenePath);
    }

    private static void RegisterMainScene()
    {
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Main.unity");
        if (scene == null) throw new Exception("No se pudo cargar Assets/Scenes/Main.unity");
        var scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
        EditorBuildSettings.scenes = scenes;
        if (!EditorBuildSettings.scenes[0].enabled)
            throw new Exception("La escena principal no quedó habilitada para la compilación");
    }

    [MenuItem("Tools/Aventura/Forzar orientación horizontal")]
    public static void SetLandscapeOrientation()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
    }

    public static void BuildAndroid()
    {
        SetLandscapeOrientation();
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
        CreateScene();
        RegisterMainScene();

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
