using System.Linq;
using HavenDuel.Net;
using HavenDuel.Pokemon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Builds the LAN multiplayer test scene and makes it the first scene in the build.
public static class LanSetup
{
    // leaves, canopies, cloth and decals: walk through them instead of getting stuck on top
    static readonly string[] NoCollide = { "Canopy", "Leaves", "Leaf", "Foliage", "Bush", "Vine", "Ivy", "Flower", "Banner", "Cloth", "Outline", "Decal", "Lantern" };
    const string ScenePath = "Assets/Scenes/LanTest.unity";

    [MenuItem("HavenDuel/Build LAN Test Scene")]
    public static void Build() => Build("Assets/Models/Maps/havenmap_sitec.glb", ScenePath, "sols");

    [MenuItem("HavenDuel/Build LAN Haven Scene (full map)")]
    public static void BuildHaven()
    {
        // No map in the scene: the chosen site is downloaded as its own bundle when you join.
        SiteBundles.BuildPrefabs();
        Build(null, "Assets/Scenes/LanHaven.unity", "Floor");
    }

    /// Valorant-ish look: warm low sun, blue sky fill, filmic tonemapping, a touch of bloom and saturation.
    static void Look(Light sun)
    {
        if (sun) { sun.color = new Color(1f, 0.93f, 0.82f); sun.intensity = 1.8f; sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f); sun.shadowStrength = 0.55f; }
        var sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_SunSize", 0.02f); sky.SetFloat("_AtmosphereThickness", 1.5f); sky.SetColor("_SkyTint", new Color(0.62f, 0.72f, 0.82f)); sky.SetFloat("_Exposure", 1.35f);
        AssetDatabase.CreateAsset(sky, AssetDatabase.GenerateUniqueAssetPath("Assets/Data/HavenSky.mat"));
        RenderSettings.skybox = sky;
        // Haven's own sky dome from the map file, unlit, around the playable area
        var domeAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Maps/haven_sky.glb");
        if (domeAsset)
        {
            var dome = (GameObject)PrefabUtility.InstantiatePrefab(domeAsset);
            dome.name = "SkyDome";
            dome.transform.position = new Vector3(-20f, 0f, -80f);
            dome.transform.localScale = Vector3.one * 0.05f;
            foreach (var r in dome.GetComponentsInChildren<Renderer>())
            {
                var src = r.sharedMaterial;
                var un = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                if (src && src.HasProperty("baseColorTexture")) un.SetTexture("_BaseMap", src.GetTexture("baseColorTexture"));
                un.SetFloat("_Cull", 0f);
                AssetDatabase.CreateAsset(un, AssetDatabase.GenerateUniqueAssetPath("Assets/Data/HavenSkyDome.mat"));
                r.sharedMaterial = un; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(1.05f, 1.08f, 1.15f);
        RenderSettings.ambientEquatorColor = new Color(0.92f, 0.88f, 0.8f);
        RenderSettings.ambientGroundColor = new Color(0.62f, 0.57f, 0.5f);
        RenderSettings.fog = false; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 35f; RenderSettings.fogEndDistance = 180f;
        RenderSettings.fogColor = new Color(0.8f, 0.84f, 0.88f);
        var cam = Camera.main;
        if (cam)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() ?? cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true; data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;
        }
        var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        var tone = profile.Add<UnityEngine.Rendering.Universal.Tonemapping>(true); tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);
        var col = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true); col.saturation.Override(18f); col.contrast.Override(10f); col.postExposure.Override(0.05f);
        var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true); bloom.intensity.Override(0.35f); bloom.threshold.Override(1.1f);
        var wb = profile.Add<UnityEngine.Rendering.Universal.WhiteBalance>(true); wb.temperature.Override(6f);
        var ppath = AssetDatabase.GenerateUniqueAssetPath("Assets/Data/HavenLook.asset");
        AssetDatabase.CreateAsset(profile, ppath);
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        AssetDatabase.SaveAssets();
        var vol = new GameObject("Look").AddComponent<UnityEngine.Rendering.Volume>();
        vol.isGlobal = true; vol.sharedProfile = profile;
    }

    public static void Build(string mapPath, string scenePath, string floorPrefix)
    {
        var roster = AssetDatabase.LoadAssetAtPath<PokemonRoster>("Assets/Data/PokemonRoster.asset");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Camera.main.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
        // Haven Site C at real scale (door 2.7 m); every mesh gets a collider so nobody walks through walls.
        var mapPrefab = (mapPath == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(mapPath));
        var map = mapPrefab ? (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab) : null;
        if (map) {
        map.name = "Map";
        foreach (var f in map.GetComponentsInChildren<MeshFilter>())
            if (f.sharedMesh && !f.GetComponent<Collider>() && !NoCollide.Any(k => f.name.Contains(k))) f.gameObject.AddComponent<MeshCollider>();
        }
        var sun = Object.FindFirstObjectByType<Light>();
        if (sun) { sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f); sun.shadows = LightShadows.Soft; }
        Look(sun);
        var game = new GameObject("LanGame").AddComponent<LanGame>();
        game.roster = roster;
        game.map = map ? map.transform : null;
        game.floorPrefix = floorPrefix;
        EditorSceneManager.SaveScene(scene, scenePath);

        var list = EditorBuildSettings.scenes.Where(s => s.path != scenePath).ToList();
        list.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log("Built " + scenePath);
    }
}
