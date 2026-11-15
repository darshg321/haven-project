using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenDuel;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SiteBundles
{
    const string MapPath = "Assets/Models/Maps/haven_full.glb";
    const string Dir = "Assets/Data/Sites";
    const string BundleDir = "Assets/StreamingAssets/sites";

    static readonly string[] NoCollide = { "Canopy", "Leaves", "Leaf", "Foliage", "Bush", "Vine", "Ivy", "Flower", "Banner", "Cloth", "Outline", "Decal", "Lantern" };
    static readonly string[] Cutout = { "Leaf", "Leaves", "Foliage", "Plant", "Tree", "Grass", "Vine", "Ivy", "Branch", "Decal", "Banner", "Flag", "Wires", "Cable" };
    static readonly string[] Overlay = { "Decal", "Grime", "Dirt", "Stain", "Leak", "Damage", "Destruct", "Overlay", "Wet", "Moss", "Dust", "Crack", "Puddle", "Soot", "Burn", "Paint", "Mural", "Graffiti", "Blend" };
    static readonly string[] Glow = { "RebelMetalCrates", "Glow", "Light", "Lamp", "Screen", "Emiss", "Neon", "Radianite", "Crystal" };

    static readonly Dictionary<Material, Material> materials = new();
    static readonly Dictionary<Texture, Texture2D> textures = new();

    [MenuItem("HavenDuel/Build Site Prefabs")]
    public static void BuildPrefabs()
    {
        materials.Clear();
        textures.Clear();
        AssetDatabase.DeleteAsset(Dir + "/Mat");
        AssetDatabase.DeleteAsset(Dir + "/Tex");
        Directory.CreateDirectory(Dir + "/Mat");
        Directory.CreateDirectory(Dir + "/Tex");
        var full = AssetDatabase.LoadAssetAtPath<GameObject>(MapPath);

        foreach (Site site in System.Enum.GetValues(typeof(Site)))
        {
            var area = Map.Area(site);
            var keep = Rect.MinMaxRect(area.xMin - 3f, area.yMin - 3f, area.xMax + 3f, area.yMax + 3f);
            var go = Object.Instantiate(full);
            go.name = "Site" + site;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var c = r.bounds.center;
                if (!keep.Contains(new Vector2(c.x, c.z)) || r.name.StartsWith("Floater"))
                    Object.DestroyImmediate(r.gameObject);
                else
                {
                    if (!Has(r.name, NoCollide)) r.gameObject.AddComponent<MeshCollider>();
                    r.sharedMaterials = r.sharedMaterials.Select(Convert).ToArray();
                }
            }
            foreach (var t in go.GetComponentsInChildren<Transform>(true).Reverse())
                if (t != go.transform && t.childCount == 0 && !t.GetComponent<Renderer>()) Object.DestroyImmediate(t.gameObject);

            var path = $"{Dir}/Site{site}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("HavenDuel/Build Site Bundles (WebGL)")]
    public static void BuildBundlesWebGL() => BuildBundles(BuildTarget.WebGL);

    public static void BuildBundles(BuildTarget target)
    {
        if (!File.Exists($"{Dir}/SiteA.prefab")) BuildPrefabs();
        Directory.CreateDirectory(BundleDir);
        var builds = System.Enum.GetNames(typeof(Site)).Select(s => new AssetBundleBuild
        {
            assetBundleName = "site_" + s.ToLower(),
            assetNames = new[] { $"{Dir}/Site{s}.prefab" },
        }).ToArray();
        BuildPipeline.BuildAssetBundles(BundleDir, builds, BuildAssetBundleOptions.ChunkBasedCompression, target);
        AssetDatabase.Refresh();
    }

    static Material Convert(Material src)
    {
        if (!src) return src;
        if (materials.TryGetValue(src, out var done)) return done;

        var baseTex = src.HasProperty("baseColorTexture") ? src.GetTexture("baseColorTexture") : null;
        var normal = src.HasProperty("normalTexture") ? src.GetTexture("normalTexture") : null;
        var emissive = src.HasProperty("emissiveTexture") ? src.GetTexture("emissiveTexture") : null;
        string names = src.name + " " + (baseTex ? baseTex.name : "");

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        var color = (src.HasProperty("baseColorFactor") ? src.GetColor("baseColorFactor") : Color.white) * Tint(baseTex ? baseTex.name : src.name);
        color.a = 1f;
        mat.SetColor("_BaseColor", color);
        if (baseTex) mat.SetTexture("_BaseMap", Compress(baseTex, false, false));
        if (normal)
        {
            mat.SetTexture("_BumpMap", Compress(normal, true, true));
            mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", Has(names, "Glass", "Metal") ? 0.45f : 0.12f);
        mat.renderQueue = 2000;

        bool hasAlpha = baseTex && AssetDatabase.GetAssetPath(mat.GetTexture("_BaseMap")).EndsWith(".png");
        if (Has(names, Cutout))
        {
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.4f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull", 0f);
            mat.renderQueue = 2450;
        }
        else if (hasAlpha && Has(names, Overlay))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
        }
        if (emissive && Has(names, Glow))
        {
            mat.SetTexture("_EmissionMap", Compress(emissive, false, false));
            mat.SetColor("_EmissionColor", Color.white * 1.6f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        AssetDatabase.CreateAsset(mat, AssetDatabase.GenerateUniqueAssetPath($"{Dir}/Mat/{FileName(src.name)}.mat"));
        return materials[src] = mat;
    }

    static Color Tint(string texture)
    {
        if (Has(texture, "Slate")) return new Color(1.7f, 1.65f, 1.58f);
        if (Has(texture, "Tile", "Stone", "Cobble", "Paver", "Concrete")) return new Color(1.2f, 1.16f, 1.1f);
        if (Has(texture, "Plaster", "Stucco")) return new Color(1f, 0.84f, 0.62f);
        if (Has(texture, "Brick")) return new Color(1.08f, 0.96f, 0.84f);
        return Color.white;
    }

    static Texture2D Compress(Texture src, bool linear, bool isNormal)
    {
        if (textures.TryGetValue(src, out var done)) return done;
        int w = Mathf.Min(src.width, 512), h = Mathf.Min(src.height, 512);
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, linear);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        bool alpha = tex.GetPixels32().Any(c => c.a < 250);
        var path = AssetDatabase.GenerateUniqueAssetPath($"{Dir}/Tex/{FileName(src.name)}.{(alpha ? "png" : "jpg")}");
        File.WriteAllBytes(path, alpha ? tex.EncodeToPNG() : tex.EncodeToJPG(85));
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !linear;
        importer.maxTextureSize = 512;
        importer.crunchedCompression = true;
        importer.compressionQuality = 50;
        importer.SaveAndReimport();
        return textures[src] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static bool Has(string s, params string[] words) =>
        words.Any(w => s.IndexOf(w, System.StringComparison.OrdinalIgnoreCase) >= 0);

    static string FileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
