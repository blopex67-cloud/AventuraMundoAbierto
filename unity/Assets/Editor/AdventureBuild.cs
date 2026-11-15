#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public static class AdventureBuild
{
    [MenuItem("Tools/Aventura/Crear escena de juego")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("World Bootstrap");
        root.AddComponent<WorldBootstrap>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
        AssetDatabase.Refresh();
        Debug.Log("Escena principal creada.");
    }

    public static void BuildAndroid()
    {
        Directory.CreateDirectory("Assets/Scenes");
        CreateScene();
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "Build/AventuraMundoAbierto.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.Exception("Android build failed: " + report.summary.result);
    }
}
#endif
