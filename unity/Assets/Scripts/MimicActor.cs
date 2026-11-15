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
            ApplyAndroidSafeMaterials();
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


    void ApplyAndroidSafeMaterials()
    {
        // glTFast PBR shaders can be stripped or unsupported in Android builds,
        // which renders the character bright magenta. Rebuild each imported material
        // with the included Unity Standard shader while keeping the GLB base-color maps.
        var anchor = Resources.Load<Material>("RuntimeShaders/StandardAnchor");
        Shader standard = anchor != null ? anchor.shader : Shader.Find("Standard");
        if (standard == null)
        {
            Debug.LogError("No Android-safe Standard shader is included for the Mimic.");
            return;
        }
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            var originals = renderer.sharedMaterials;
            var replacements = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                var source = originals[i];
                if (source == null) continue;
                var material = new Material(standard);
                material.name = source.name + " (Android Standard)";
                Color baseColor = Color.white;
                if (source.HasProperty("_BaseColorFactor")) baseColor = source.GetColor("_BaseColorFactor");
                else if (source.HasProperty("_BaseColor")) baseColor = source.GetColor("_BaseColor");
                else if (source.HasProperty("_Color")) baseColor = source.GetColor("_Color");
                material.color = baseColor;
                Texture baseTexture = FindTexture(source, "_BaseColorTexture", "_BaseMap", "_MainTex", "_BaseColorMap");
                if (baseTexture != null) material.SetTexture("_MainTex", baseTexture);
                Texture normal = FindTexture(source, "_NormalTexture", "_BumpMap", "_NormalMap");
                if (normal != null && material.HasProperty("_BumpMap"))
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }
                float metallic = source.HasProperty("_MetallicFactor") ? source.GetFloat("_MetallicFactor") :
                    (source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f);
                float roughness = source.HasProperty("_RoughnessFactor") ? source.GetFloat("_RoughnessFactor") : .62f;
                material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
                material.SetFloat("_Glossiness", Mathf.Clamp01(1f - roughness));
                replacements[i] = material;
            }
            renderer.sharedMaterials = replacements;
        }
    }

    static Texture FindTexture(Material material, params string[] properties)
    {
        foreach (var property in properties)
            if (material.HasProperty(property))
            {
                var texture = material.GetTexture(property);
                if (texture != null) return texture;
            }
        return null;
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
