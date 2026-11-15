using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using GLTFast;

/// <summary>Loads the openly downloadable Mimic GLB and drives its legacy animation clips.</summary>
public sealed class MimicActor : MonoBehaviour
{
    public string modelFile = "the_mimic_dlc.glb";
    [Range(.2f, 1.5f)] public float modelScale = .72f;

    static GltfImport sharedImport;
    static Task<bool> sharedLoadTask;
    Animation[] legacyAnimations = Array.Empty<Animation>();
    bool ready;

    async void Start()
    {
        try
        {
            if (sharedLoadTask == null)
                sharedLoadTask = LoadSharedModel();
            if (!await sharedLoadTask || !this) return;

            int firstNewChild = transform.childCount;
            if (!await sharedImport.InstantiateMainSceneAsync(transform) || !this) return;
            for (int i = firstNewChild; i < transform.childCount; i++)
            {
                var modelRoot = transform.GetChild(i);
                modelRoot.localScale = Vector3.one * modelScale;
            }
            legacyAnimations = GetComponentsInChildren<Animation>(true);
            ready = legacyAnimations.Length > 0;
            if (ready) PlayClip("idle");
            else Debug.LogWarning("Mimic GLB loaded, but no legacy Animation component was found.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Could not load Mimic GLB: " + ex);
        }
    }

    static async Task<bool> LoadSharedModel()
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, "the_mimic_dlc.glb");
        using (var request = UnityWebRequest.Get(path))
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Mimic download/read failed: " + request.error + " | " + path);
                return false;
            }
            sharedImport = new GltfImport();
            bool loaded = await sharedImport.Load(request.downloadHandler.data, new Uri(path));
            if (!loaded)
            {
                Debug.LogError("glTFast could not import the Mimic GLB.");
                sharedImport = null;
            }
            return loaded;
        }
    }

    void PlayClip(string hint)
    {
        if (!ready) return;
        string search = hint.ToLowerInvariant();
        foreach (var animation in legacyAnimations)
        {
            foreach (AnimationState state in animation)
            {
                if (state.name.ToLowerInvariant().Contains(search))
                {
                    animation.Play(state.name);
                    return;
                }
            }
        }
        if (search == "idle" && legacyAnimations.Length > 0 && legacyAnimations[0].clip)
            legacyAnimations[0].Play();
    }

    public void PlayWalk() => PlayClip("walk_sk");
    public void PlayRun() => PlayClip("runa");
    public void PlayJumpscare() => PlayClip("jumpscarefront");
}
