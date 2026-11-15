#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

// Unity Build Automation uses its own build entry point. This initializer ensures
// orientation and the startup scene are configured before that entry point reads settings.
[InitializeOnLoad]
public static class AdventureBuildBootstrap
{
    static AdventureBuildBootstrap()
    {
        ApplyAndroidLandscape();
        EnsureMainSceneRegistered();
    }

    public static void ApplyAndroidLandscape()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
    }

    public static void EnsureMainSceneRegistered()
    {
        const string path = "Assets/Scenes/Main.unity";
        if (!File.Exists(Path.Combine(Application.dataPath, "Scenes", "Main.unity")))
            AdventureBuild.CreateScene();

        var current = EditorBuildSettings.scenes;
        bool found = false;
        for (int i = 0; i < current.Length; i++)
        {
            if (current[i].path == path)
            {
                current[i].enabled = true;
                found = true;
            }
        }
        if (!found)
        {
            var next = new EditorBuildSettingsScene[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = new EditorBuildSettingsScene(path, true);
            current = next;
        }
        EditorBuildSettings.scenes = current;
    }
}

public class AdventureBuildPreprocess : IPreprocessBuildWithReport
{
    public int callbackOrder => -10000;
    public void OnPreprocessBuild(BuildReport report)
    {
        AdventureBuildBootstrap.ApplyAndroidLandscape();
        AdventureBuildBootstrap.EnsureMainSceneRegistered();
    }
}

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

    [MenuItem("Tools/Aventura/Forzar orientación horizontal")]
    public static void SetLandscapeOrientation()
    {
        AdventureBuildBootstrap.ApplyAndroidLandscape();
    }

    public static void BuildAndroid()
    {
        AdventureBuildBootstrap.ApplyAndroidLandscape();
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
        CreateScene();
        AdventureBuildBootstrap.EnsureMainSceneRegistered();

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
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Android build failed: " + report.summary.result);
        if (!File.Exists(ApkPath) || new FileInfo(ApkPath).Length == 0)
            throw new Exception("Unity terminó sin generar un APK válido en " + ApkPath);
        Debug.Log("APK generado: " + ApkPath + " (" + new FileInfo(ApkPath).Length + " bytes)");
    }
}
#endif
