using System.IO;
using HavenDuel.Combat;
using HavenDuel.Net;
using HavenDuel.Pokemon;
using TMPro;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Builds everything the fight needs from the downloaded assets in Assets/Art (see Assets/Art/SOURCES.md):
/// import settings, particle materials, projectile and impact prefabs, the HUD font, and
/// Assets/Data/CombatConfig.asset. Safe to run again; it overwrites what it made.
public static class CombatSetup
{
    const string Art = "Assets/Art";
    const string Out = "Assets/Art/Generated";   // materials/prefabs assembled from the downloaded textures
    const string ConfigPath = "Assets/Data/CombatConfig.asset";

    [MenuItem("HavenDuel/Build Combat Assets")]
    public static void Build()
    {
        Directory.CreateDirectory(Out);
        ImportSettings();
        AssetDatabase.Refresh();

        var cfg = AssetDatabase.LoadAssetAtPath<CombatConfig>(ConfigPath);
        if (!cfg) { cfg = ScriptableObject.CreateInstance<CombatConfig>(); AssetDatabase.CreateAsset(cfg, ConfigPath); }

        var shuriken = BuildWaterShuriken();
        var splash = BuildWaterSplash();
        var ember = BuildEmber();
        var burst = BuildEmberBurst();

        cfg.fighters = new[]
        {
            new CombatConfig.FighterDef
            {
                species = Species.Greninja, displayName = "Greninja", maxHp = 100f, height = 1.6f, hitRadius = 0.38f,
                portrait = Sprite("UI/portrait_greninja.png"), cry = Clip("Audio/cry_greninja.mp3"), color = new Color(0.3f, 0.55f, 0.95f),
                basic = new CombatConfig.MoveDef
                {
                    name = "Water Shuriken", damage = 7f, speed = 42f, gravity = 0f, radius = 0.16f, cooldown = 0.3f, life = 2.2f,
                    visualScale = 1f, animSpeed = 3.2f, projectile = shuriken, hitFx = splash,
                    castSound = Clip("Audio/shuriken_throw.ogg"), hitSound = Clip("Audio/shuriken_hit.ogg"), wallSound = Clip("Audio/shuriken_wall.ogg"), castVolume = 0.55f,
                },
            },
            new CombatConfig.FighterDef
            {
                species = Species.Charizard, displayName = "Charizard", maxHp = 150f, height = 2.0f, hitRadius = 0.55f,
                portrait = Sprite("UI/portrait_charizard.png"), cry = Clip("Audio/cry_charizard.mp3"), color = new Color(0.95f, 0.5f, 0.2f),
                basic = new CombatConfig.MoveDef
                {
                    name = "Ember", damage = 11f, speed = 28f, gravity = 2.5f, radius = 0.22f, cooldown = 0.5f, life = 2.5f,
                    visualScale = 1f, animSpeed = 3.5f, projectile = ember, hitFx = burst,
                    castSound = Clip("Audio/ember_cast.wav"), hitSound = Clip("Audio/ember_hit.ogg"), wallSound = Clip("Audio/ember_wall.ogg"), castVolume = 0.45f,
                },
            },
        };
        cfg.font = Font();
        cfg.crosshair = Sprite("UI/crosshair.png");
        cfg.hitmarker = Sprite("UI/hitmarker.png");
        cfg.panel = Sprite("UI/panel_flat.png");
        cfg.hitmarkerSound = Clip("Audio/hitmarker.ogg");
        cfg.countdownSound = Clip("Audio/countdown.ogg");
        cfg.fightSound = Clip("Audio/fight.ogg");
        cfg.koSound = Clip("Audio/ko.ogg");
        cfg.winSound = Clip("Audio/round_win.ogg");
        cfg.loseSound = Clip("Audio/round_lose.ogg");
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Debug.Log("Combat assets built: " + ConfigPath);
    }

    /// Point every LanGame in the open scene at the config and save the scene.
    [MenuItem("HavenDuel/Hook Combat Into Open Scene")]
    public static void HookScene()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<CombatConfig>(ConfigPath);
        foreach (var g in Object.FindObjectsByType<LanGame>(FindObjectsSortMode.None)) { g.combat = cfg; EditorUtility.SetDirty(g); }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    // ---------- import ----------

    static void ImportSettings()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Art + "/UI" }))
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.mipmapEnabled = false; ti.alphaIsTransparency = true;
            if (ti.assetPath.Contains("panel_")) ti.spriteBorder = new Vector4(12, 12, 12, 12);
            ti.SaveAndReimport();
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Art + "/VFX/Textures" }))
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            ti.textureType = TextureImporterType.Default; ti.alphaSource = TextureImporterAlphaSource.FromGrayScale; ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp; ti.maxTextureSize = 256; ti.mipmapEnabled = true;
            ti.SaveAndReimport();
        }
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Art + "/Audio" }))
        {
            var ai = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            ai.forceToMono = true;
            var s = ai.defaultSampleSettings; s.loadType = AudioClipLoadType.DecompressOnLoad; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.6f;
            ai.defaultSampleSettings = s;
            ai.SaveAndReimport();
        }
    }

    static Sprite Sprite(string rel) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/{rel}");
    static AudioClip Clip(string rel) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Art}/{rel}");
    static Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Art}/VFX/Textures/{n}.png");

    static TMP_FontAsset Font()
    {
        if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro")) TMP_PackageResourceImporter.ImportResources(true, false, false);
        var path = $"{Out}/Teko SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing) return existing;
        var ttf = AssetDatabase.LoadAssetAtPath<Font>($"{Art}/Fonts/Teko.ttf");
        var fa = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        fa.name = "Teko SDF";
        AssetDatabase.CreateAsset(fa, path);
        fa.atlasTexture.name = "Teko SDF Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
        fa.material.name = "Teko SDF Material"; AssetDatabase.AddObjectToAsset(fa.material, fa);
        fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~é");
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    // ---------- materials ----------

    /// intensity > 1 pushes additive layers past the bloom threshold so they glow on bright walls.
    static Material Mat(string tex, bool additive, float intensity = 1f)
    {
        var path = $"{Out}/M_{(additive ? "Add" : "Alpha")}_{tex}{(intensity != 1f ? "_x" + intensity.ToString("0.#") : "")}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", Tex(tex));
        m.SetColor("_BaseColor", new Color(intensity, intensity, intensity, 1f));
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_ColorMode", 0f);
        BaseShaderGUI.SetMaterialKeywords(m, null, ParticleGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- particle helpers ----------

    static ParticleSystem PS(Transform parent, string name, Material mat, bool world, bool loop = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = loop; main.playOnAwake = true; main.duration = loop ? 1f : 0.6f;
        main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startSpeed = 0f;
        var shape = ps.shape; shape.enabled = false;
        var em = ps.emission; em.rateOverTime = 0f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.maxParticleSize = 3f;
        return ps;
    }

    /// One long-lived particle that rides with the projectile.
    static ParticleSystem Single(Transform parent, string name, Material mat, float size, Color c, float spinDegPerSec)
    {
        var ps = PS(parent, name, mat, false);
        var main = ps.main;
        main.startLifetime = 100f; main.startSize = size; main.startColor = c; main.maxParticles = 1;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        if (spinDegPerSec != 0f)
        {
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = spinDegPerSec * Mathf.Deg2Rad;
        }
        return ps;
    }

    static void FadeOut(ParticleSystem ps, Color start, Color end)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                  new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(start.a * 0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    static void Shrink(ParticleSystem ps, float from = 1f, float to = 0f)
    {
        var s = ps.sizeOverLifetime; s.enabled = true;
        s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
    }

    static GameObject Save(GameObject go, string name)
    {
        var path = $"{Out}/{name}.prefab";
        var p = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return p;
    }

    // ---------- Greninja: Water Shuriken ----------

    static GameObject BuildWaterShuriken()
    {
        var root = new GameObject("WaterShuriken");
        root.AddComponent<Projectile>();
        var t = root.transform;
        Single(t, "Glow", Mat("light_01", true, 1.6f), 1.3f, new Color(0.1f, 0.5f, 1f, 0.6f), 0f);
        // solid blue body under the glow so the star reads against Haven's bright walls
        Single(t, "Body", Mat("star_07", false), 0.95f, new Color(0.08f, 0.42f, 0.95f, 0.9f), 1500f);
        Single(t, "Ring", Mat("twirl_01", true, 1.8f), 0.95f, new Color(0.3f, 0.75f, 1f, 0.9f), -1200f);
        Single(t, "Star", Mat("star_07", true, 2f), 0.8f, new Color(0.45f, 0.85f, 1f, 1f), 1500f);

        var trail = PS(t, "Trail", Mat("light_01", true, 1.6f), true);
        var m = trail.main; m.startLifetime = 0.28f; m.startSize = 0.55f; m.startColor = new Color(0.35f, 0.75f, 1f, 0.55f); m.maxParticles = 200;
        var em = trail.emission; em.rateOverDistance = 22f;
        Shrink(trail); FadeOut(trail, new Color(0.45f, 0.8f, 1f, 0.55f), new Color(0.1f, 0.4f, 1f, 0f));

        var drops = PS(t, "Drops", Mat("light_01", true), true);
        m = drops.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f); m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f); m.gravityModifier = 1f; m.startColor = new Color(0.8f, 0.95f, 1f, 0.9f); m.maxParticles = 200;
        em = drops.emission; em.rateOverDistance = 5f;
        var sh = drops.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
        return Save(root, "WaterShuriken");
    }

    static GameObject BuildWaterSplash()
    {
        var root = new GameObject("WaterSplash");
        var t = root.transform;
        var drops = PS(t, "Drops", Mat("light_01", true, 1.6f), true, false);
        var m = drops.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.65f); m.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 7f); m.gravityModifier = 1.6f; m.startColor = new Color(0.75f, 0.93f, 1f, 1f);
        m.stopAction = ParticleSystemStopAction.Destroy;
        var em = drops.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });
        var sh = drops.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 55f; sh.radius = 0.05f;
        FadeOut(drops, new Color(0.8f, 0.95f, 1f, 1f), new Color(0.3f, 0.6f, 1f, 0f));

        var ring = PS(t, "Ring", Mat("circle_05", true, 1.6f), true, false);
        m = ring.main; m.startLifetime = 0.3f; m.startSize = 1.6f; m.startColor = new Color(0.6f, 0.9f, 1f, 0.8f);
        em = ring.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        Shrink(ring, 0.15f, 1f); FadeOut(ring, new Color(0.6f, 0.9f, 1f, 0.8f), new Color(0.3f, 0.6f, 1f, 0f));

        var mist = PS(t, "Mist", Mat("smoke_01", false), true, false);
        m = mist.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f); m.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f); m.startColor = new Color(0.8f, 0.92f, 1f, 0.35f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        em = mist.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });
        sh = mist.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
        FadeOut(mist, new Color(0.85f, 0.95f, 1f, 0.35f), new Color(0.85f, 0.95f, 1f, 0f));
        return Save(root, "WaterSplash");
    }

    // ---------- Charizard: Ember ----------

    static GameObject BuildEmber()
    {
        var root = new GameObject("Ember");
        root.AddComponent<Projectile>();
        var t = root.transform;
        Single(t, "Glow", Mat("light_01", true, 2.2f), 1.4f, new Color(1f, 0.4f, 0.08f, 0.6f), 0f);
        // solid orange-red body so the fireball reads against bright walls, hot additive core on top
        Single(t, "Body", Mat("fire_01", false), 0.65f, new Color(0.95f, 0.32f, 0.05f, 0.95f), 300f);
        Single(t, "Core", Mat("fire_01", true, 2.5f), 0.5f, new Color(1f, 0.75f, 0.35f, 1f), 420f);

        var flames = PS(t, "Flames", Mat("fire_02", true, 2f), true);
        var m = flames.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.38f); m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); m.maxParticles = 300;
        var em = flames.emission; em.rateOverDistance = 26f;
        var sh = flames.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.08f;
        Shrink(flames, 1f, 0.15f);
        var col = flames.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.55f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.45f), new GradientColorKey(new Color(0.6f, 0.1f, 0.03f), 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sparks = PS(t, "Sparks", Mat("light_01", true), true);
        m = sparks.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f); m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f); m.gravityModifier = -0.25f; m.startColor = new Color(1f, 0.8f, 0.35f, 1f); m.maxParticles = 200;
        em = sparks.emission; em.rateOverTime = 35f;
        sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.12f;

        var smoke = PS(t, "Smoke", Mat("smoke_04", false), true);
        m = smoke.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f); m.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); m.startColor = new Color(0.22f, 0.2f, 0.19f, 0.35f); m.gravityModifier = -0.1f; m.maxParticles = 100;
        em = smoke.emission; em.rateOverDistance = 5f;
        Shrink(smoke, 0.6f, 1.6f); FadeOut(smoke, new Color(0.25f, 0.22f, 0.2f, 0.35f), new Color(0.3f, 0.3f, 0.3f, 0f));
        return Save(root, "Ember");
    }

    static GameObject BuildEmberBurst()
    {
        var root = new GameObject("EmberBurst");
        var t = root.transform;
        var flash = PS(t, "Flash", Mat("light_01", true, 2.5f), true, false);
        var m = flash.main; m.startLifetime = 0.14f; m.startSize = 2.6f; m.startColor = new Color(1f, 0.6f, 0.2f, 0.9f);
        m.stopAction = ParticleSystemStopAction.Destroy;
        var em = flash.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        FadeOut(flash, new Color(1f, 0.7f, 0.3f, 0.9f), new Color(1f, 0.3f, 0.1f, 0f));

        var fire = PS(t, "Fire", Mat("fire_02", true, 2f), true, false);
        m = fire.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f); m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new Color(1f, 0.6f, 0.2f, 1f);
        em = fire.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
        var sh = fire.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
        Shrink(fire, 1f, 0.3f); FadeOut(fire, new Color(1f, 0.8f, 0.4f, 1f), new Color(0.6f, 0.1f, 0.03f, 0f));

        var sparks = PS(t, "Sparks", Mat("light_01", true), true, false);
        m = sparks.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.65f); m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f); m.gravityModifier = 1f; m.startColor = new Color(1f, 0.85f, 0.4f, 1f);
        em = sparks.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });
        sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 70f; sh.radius = 0.05f;
        var r = sparks.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.04f; r.lengthScale = 1.5f;

        var smoke = PS(t, "Smoke", Mat("smoke_04", false), true, false);
        m = smoke.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f); m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new Color(0.2f, 0.18f, 0.17f, 0.45f); m.gravityModifier = -0.15f;
        em = smoke.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 5) });
        sh = smoke.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
        Shrink(smoke, 0.6f, 1.5f); FadeOut(smoke, new Color(0.22f, 0.2f, 0.18f, 0.45f), new Color(0.3f, 0.3f, 0.3f, 0f));
        return Save(root, "EmberBurst");
    }
}
