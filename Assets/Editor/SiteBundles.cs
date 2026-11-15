using System.IO;
using System.Linq;
using HavenDuel.Net;
using UnityEditor;
using UnityEngine;

/// Cuts the full Haven map into one prefab per site and builds each as its own asset bundle,
/// so a player downloads only the site they pick.
public static class SiteBundles
{
    const string MapPath = "Assets/Models/Maps/haven_full.glb";
    const string PrefabDir = "Assets/Data/Sites";
    public const string BundleDir = "Assets/StreamingAssets/sites";
    static readonly string[] NoCollide = { "Canopy", "Leaves", "Leaf", "Foliage", "Bush", "Vine", "Ivy", "Flower", "Banner", "Cloth", "Outline", "Decal", "Lantern" };

    [MenuItem("HavenDuel/Build Site Prefabs")]
    public static void BuildPrefabs()
    {
        Directory.CreateDirectory(PrefabDir);
        mats.Clear(); texs.Clear();
        if (AssetDatabase.IsValidFolder(PrefabDir + "/Mat")) AssetDatabase.DeleteAsset(PrefabDir + "/Mat");
        if (AssetDatabase.IsValidFolder(PrefabDir + "/Tex")) AssetDatabase.DeleteAsset(PrefabDir + "/Tex");
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(MapPath);
        foreach (LanGame.Site site in System.Enum.GetValues(typeof(LanGame.Site)))
        {
            var area = LanGame.Area(site);
            var keep = Rect.MinMaxRect(area.xMin - 3f, area.yMin - 3f, area.xMax + 3f, area.yMax + 3f);
            var go = (GameObject)Object.Instantiate(src);
            go.name = "Site" + site;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!r) continue;
                var b = r.bounds;
                // keep a piece only if its centre is in the site (touching the edge is not enough)
                if (!keep.Contains(new Vector2(b.center.x, b.center.z)) || r.name.StartsWith("Floater")) { Object.DestroyImmediate(r.gameObject); continue; }
                if (r.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh && !NoCollide.Any(k => r.name.Contains(k)))
                    r.gameObject.AddComponent<MeshCollider>();
            }
            // drop empty branches left behind
            foreach (var t in go.GetComponentsInChildren<Transform>(true).Reverse())
                if (t && t != go.transform && t.childCount == 0 && !t.GetComponent<Renderer>()) Object.DestroyImmediate(t.gameObject);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = r.sharedMaterials.Select(CompressedMaterial).ToArray();
            var path = $"{PrefabDir}/Site{site}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            AssetImporter.GetAtPath(path).assetBundleName = "site_" + site.ToString().ToLower();
        }
        AssetDatabase.SaveAssets();
    }

    // glTF textures import uncompressed (RGBA32). Re-save each once as a jpg/png the importer can crunch.
    static readonly System.Collections.Generic.Dictionary<Material, Material> mats = new();
    static readonly System.Collections.Generic.Dictionary<Texture, Texture2D> texs = new();

    static Material CompressedMaterial(Material m)
    {
        if (!m) return m;
        if (mats.TryGetValue(m, out var done)) return done;
        Directory.CreateDirectory(PrefabDir + "/Mat");
        // Rebuild on URP Lit: the ripped glTF materials import as transparent (queue 3000), metallic and glowing.
        var copy = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        var baseTex = m.HasProperty("baseColorTexture") ? m.GetTexture("baseColorTexture") : null;
        var nrm = m.HasProperty("normalTexture") ? m.GetTexture("normalTexture") : null;
        var em = m.HasProperty("emissiveTexture") ? m.GetTexture("emissiveTexture") : null;
        string tn = m.name + " " + (baseTex ? baseTex.name : "");
        // Tint by what the texture is, not by the object name (a "Wall" object can carry wood or brick).
        string tx = baseTex ? baseTex.name : m.name;
        Color tint = Color.white;
        if (Has(tx, "Slate")) tint = new Color(1.7f, 1.65f, 1.58f);
        else if (Has(tx, "Tile", "Stone", "Cobble", "Paver", "Concrete")) tint = new Color(1.2f, 1.16f, 1.1f);
        else if (Has(tx, "Plaster", "Stucco")) tint = new Color(1f, 0.84f, 0.62f);
        else if (Has(tx, "Brick")) tint = new Color(1.08f, 0.96f, 0.84f);
        var bc = m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor") : Color.white;
        var col = bc * tint; col.a = 1f;
        copy.SetColor("_BaseColor", col);
        if (baseTex is Texture2D) copy.SetTexture("_BaseMap", CompressedTexture(baseTex, "baseColor"));
        if (nrm is Texture2D) { copy.SetTexture("_BumpMap", CompressedTexture(nrm, "normal")); copy.EnableKeyword("_NORMALMAP"); }
        copy.SetFloat("_Metallic", 0f);
        copy.SetFloat("_Smoothness", Has(tn, "Glass", "Metal") ? 0.45f : 0.12f);
        bool cutout = Has(tn, "Leaf", "Leaves", "Foliage", "Plant", "Tree", "Grass", "Vine", "Ivy", "Branch", "Decal", "Banner", "Flag", "Wires", "Cable");
        if (cutout)
        {
            copy.SetFloat("_AlphaClip", 1f); copy.SetFloat("_Cutoff", 0.4f); copy.EnableKeyword("_ALPHATEST_ON");
            copy.SetFloat("_Cull", 0f); copy.renderQueue = 2450;
        }
        else copy.renderQueue = 2000;
        // Grime, stains and damage are overlay meshes: blend them over the wall instead of drawing them solid.
        var bt = copy.GetTexture("_BaseMap");
        bool hasAlpha = bt && AssetDatabase.GetAssetPath(bt).EndsWith(".png");
        if (!cutout && hasAlpha && Has(tn, "Decal", "Grime", "Dirt", "Stain", "Leak", "Damage", "Destruct", "Overlay", "Wet", "Moss", "Dust", "Crack", "Puddle", "Soot", "Burn", "Paint", "Mural", "Graffiti", "Blend"))
        {
            copy.SetFloat("_Surface", 1f); copy.SetFloat("_Blend", 0f);
            copy.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            copy.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            copy.SetFloat("_ZWrite", 0f);
            copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            copy.SetOverrideTag("RenderType", "Transparent");
            copy.renderQueue = 3000;
            var c2 = copy.GetColor("_BaseColor"); c2.a = 1f; copy.SetColor("_BaseColor", c2);
        }
        bool glows = Has(tn, "RebelMetalCrates", "Glow", "Light", "Lamp", "Screen", "Emiss", "Neon", "Radianite", "Crystal");
        if (glows && em is Texture2D)
        {
            copy.SetTexture("_EmissionMap", CompressedTexture(em, "emissive"));
            copy.SetColor("_EmissionColor", Color.white * 1.6f);
            copy.EnableKeyword("_EMISSION");
            copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        var path = AssetDatabase.GenerateUniqueAssetPath($"{PrefabDir}/Mat/{San(m.name)}.mat");
        AssetDatabase.CreateAsset(copy, path);
        return mats[m] = copy;
    }

    static Texture2D CompressedTexture(Texture t, string prop)
    {
        if (texs.TryGetValue(t, out var done)) return done;
        Directory.CreateDirectory(PrefabDir + "/Tex");
        bool linear = !(prop.ToLower().Contains("base") || prop.ToLower().Contains("main") || prop.ToLower().Contains("emiss") || prop.ToLower().Contains("color"));
        bool normal = prop.ToLower().Contains("normal") || prop.ToLower().Contains("bump");
        int w = Mathf.Min(t.width, 512), h = Mathf.Min(t.height, 512);
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
        Graphics.Blit(t, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false, linear);
        tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0); tmp.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        bool alpha = false;
        foreach (var c in tmp.GetPixels32()) if (c.a < 250) { alpha = true; break; }
        var path = AssetDatabase.GenerateUniqueAssetPath($"{PrefabDir}/Tex/{San(t.name)}.{(alpha ? "png" : "jpg")}");
        File.WriteAllBytes(path, alpha ? tmp.EncodeToPNG() : tmp.EncodeToJPG(85));
        Object.DestroyImmediate(tmp);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        imp.sRGBTexture = !linear;
        imp.maxTextureSize = 512;
        imp.textureCompression = TextureImporterCompression.Compressed;
        imp.crunchedCompression = true; imp.compressionQuality = 50;
        imp.SaveAndReimport();
        return texs[t] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static bool Has(string s, params string[] keys) { foreach (var k in keys) if (s.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }

    static string San(string n) { foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_'); return string.IsNullOrEmpty(n) ? "tex" : n; }

    [MenuItem("HavenDuel/Build Site Bundles (WebGL)")]
    public static void BuildBundles() => BuildBundles(BuildTarget.WebGL);

    public static void BuildBundles(BuildTarget target)
    {
        if (!File.Exists($"{PrefabDir}/SiteA.prefab")) BuildPrefabs();
        Directory.CreateDirectory(BundleDir);
        var builds = System.Enum.GetValues(typeof(LanGame.Site)).Cast<LanGame.Site>().Select(s => new AssetBundleBuild
        {
            assetBundleName = "site_" + s.ToString().ToLower(),
            assetNames = new[] { $"{PrefabDir}/Site{s}.prefab" },
        }).ToArray();
        BuildPipeline.BuildAssetBundles(BundleDir, builds, BuildAssetBundleOptions.ChunkBasedCompression, target);
        AssetDatabase.Refresh();
    }
}
