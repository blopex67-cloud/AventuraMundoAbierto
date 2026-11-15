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
        // Always replace GLTFast's runtime shader on Android: the old code skipped
        // recognized glTF shaders, and pink/magenta is Unity's missing-shader color.
        var anchor = Resources.Load<Material>("RuntimeShaders/StandardAnchor");
        Shader safeShader = anchor != null ? anchor.shader : null;
        if (safeShader == null) safeShader = Shader.Find("Standard");
        if (safeShader == null) safeShader = Shader.Find("Universal Render Pipeline/Lit");
        if (safeShader == null) safeShader = Shader.Find("Unlit/Texture");
        if (safeShader == null)
        {
            Debug.LogError("Mimic: no safe included shader is available; GLB material conversion was not possible.");
            return;
        }

        int slotCount = 0, texturesKept = 0, missingSlots = 0;
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            var originals = renderer.sharedMaterials;
            if (originals == null || originals.Length == 0) continue;
            var replacements = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                Material source = originals[i];
                var material = new Material(safeShader);
                material.name = (source != null ? source.name : renderer.name) + " (Android PBR)";
                Color color = ReadColor(source, Color.white, "_BaseColorFactor", "baseColorFactor", "_BaseColor", "_Color", "color");
                SetColor(material, color, "_Color", "_BaseColor");

                Texture baseColor = ReadTexture(source, "baseColorTexture", "_BaseColorTexture", "_BaseMap", "_MainTex", "_BaseColorMap");
                if (baseColor != null)
                {
                    SetTexture(material, baseColor, "_MainTex", "_BaseMap");
                    CopyTextureTransform(source, material, "baseColorTexture", "_MainTex", "_BaseMap", "_BaseColorTexture");
                    texturesKept++;
                }
                Texture normal = ReadTexture(source, "normalTexture", "_NormalTexture", "_BumpMap", "_NormalMap");
                if (normal != null && material.HasProperty("_BumpMap"))
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                    texturesKept++;
                }
                Texture metalRough = ReadTexture(source, "metallicRoughnessTexture", "_MetallicGlossMap", "_MetallicRoughnessMap");
                if (metalRough != null && material.HasProperty("_MetallicGlossMap"))
                {
                    material.SetTexture("_MetallicGlossMap", metalRough);
                    material.EnableKeyword("_METALLICGLOSSMAP");
                    texturesKept++;
                }
                Texture occlusion = ReadTexture(source, "occlusionTexture", "_OcclusionMap", "_OcclusionTexture");
                if (occlusion != null && material.HasProperty("_OcclusionMap"))
                { material.SetTexture("_OcclusionMap", occlusion); material.EnableKeyword("_OCCLUSIONMAP"); texturesKept++; }
                Texture emission = ReadTexture(source, "emissiveTexture", "_EmissionMap", "_EmissiveTexture");
                if (emission != null && material.HasProperty("_EmissionMap"))
                {
                    material.SetTexture("_EmissionMap", emission);
                    material.EnableKeyword("_EMISSION");
                    texturesKept++;
                }
                float metallic = ReadFloat(source, 0f, "metallicFactor", "_MetallicFactor", "_Metallic");
                float roughness = ReadFloat(source, .62f, "roughnessFactor", "_RoughnessFactor");
                SetFloat(material, Mathf.Clamp01(metallic), "_Metallic");
                SetFloat(material, Mathf.Clamp01(1f - roughness), "_Glossiness", "_Smoothness");
                Color emissionColor = ReadColor(source, Color.black, "emissiveFactor", "_EmissiveFactor", "_EmissionColor");
                if (emissionColor.maxColorComponent > .001f && material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emissionColor);
                    material.EnableKeyword("_EMISSION");
                }
                // A missing or unsupported source slot becomes a neutral, non-pink surface.
                if (source == null) missingSlots++;
                replacements[i] = material;
                slotCount++;
            }
            renderer.sharedMaterials = replacements;
        }
        Debug.Log("Mimic Android material conversion: " + slotCount + " slots; " + texturesKept + " texture maps retained; " + missingSlots + " empty slots repaired.");
    }

    static Texture ReadTexture(Material material, params string[] names)
    {
        if (material == null) return null;
        foreach (string name in names) if (material.HasProperty(name))
        {
            var texture = material.GetTexture(name);
            if (texture != null) return texture;
        }
        // GLTFast shader revisions differ in exposed property spelling.
        foreach (string property in material.GetTexturePropertyNames())
        {
            string lower = property.ToLowerInvariant();
            bool wantsBase = Array.Exists(names, n => n.ToLowerInvariant().Contains("basecolor")) || Array.Exists(names, n => n == "_MainTex");
            if (wantsBase && (lower.Contains("basecolor") || lower.Contains("basemap") || lower.Contains("maintex")))
            { var texture = material.GetTexture(property); if (texture != null) return texture; }
            bool wantsNormal = Array.Exists(names, n => n.ToLowerInvariant().Contains("normal") || n.ToLowerInvariant().Contains("bump"));
            if (wantsNormal && (lower.Contains("normal") || lower.Contains("bump")))
            { var texture = material.GetTexture(property); if (texture != null) return texture; }
            bool wantsEmission = Array.Exists(names, n => n.ToLowerInvariant().Contains("emission") || n.ToLowerInvariant().Contains("emissive"));
            if (wantsEmission && (lower.Contains("emission") || lower.Contains("emissive")))
            { var texture = material.GetTexture(property); if (texture != null) return texture; }
        }
        return null;
    }

    static Color ReadColor(Material material, Color fallback, params string[] names)
    {
        if (material == null) return fallback;
        foreach (string name in names) if (material.HasProperty(name)) return material.GetColor(name);
        return fallback;
    }

    static float ReadFloat(Material material, float fallback, params string[] names)
    {
        if (material == null) return fallback;
        foreach (string name in names) if (material.HasProperty(name)) return material.GetFloat(name);
        return fallback;
    }

    static void SetColor(Material material, Color color, params string[] names)
    { foreach (string name in names) if (material.HasProperty(name)) material.SetColor(name, color); }
    static void SetFloat(Material material, float value, params string[] names)
    { foreach (string name in names) if (material.HasProperty(name)) material.SetFloat(name, value); }
    static void SetTexture(Material material, Texture texture, params string[] names)
    { foreach (string name in names) if (material.HasProperty(name)) material.SetTexture(name, texture); }

    static void CopyTextureTransform(Material source, Material target, string sourceName, params string[] targetNames)
    {
        if (source == null || !source.HasProperty(sourceName)) return;
        Vector2 scale = source.GetTextureScale(sourceName), offset = source.GetTextureOffset(sourceName);
        foreach (string property in targetNames) if (target.HasProperty(property))
        { target.SetTextureScale(property, scale); target.SetTextureOffset(property, offset); }
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
