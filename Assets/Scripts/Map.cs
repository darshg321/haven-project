using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HavenDuel
{
    public enum Site { A, B, C }

    public class Map
    {
        public static Rect Area(Site s) => s switch
        {
            Site.A => Rect.MinMaxRect(-105f, -116f, -44f, -50f),
            Site.B => Rect.MinMaxRect(-38f, -118f, 12f, -48f),
            _ => Rect.MinMaxRect(-5f, -125f, 50f, -45f),
        };

        public float FallY => floor.min.y - 3f;
        public float CeilingY => floor.max.y + 12f;

        readonly Bounds floor;
        bool hasNavMesh;

        public Map(GameObject prefab, Site site)
        {
            var root = Object.Instantiate(prefab).transform;
            // Work on copies so play mode never rewrites the material assets.
            var toon = new Dictionary<Material, Material>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterial && r.sharedMaterial.renderQueue >= 3000) r.enabled = false;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    if (!toon.TryGetValue(mats[i], out var m)) toon[mats[i]] = m = Toon(mats[i]);
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
            if (probe) { probe.Release(); Object.Destroy(probe); Object.Destroy(probeRead); }
            Physics.SyncTransforms();

            var area = Area(site);
            bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var c = r.bounds.center;
                if (!r.name.StartsWith("Floor") || !area.Contains(new Vector2(c.x, c.z))) continue;
                if (!any) floor = r.bounds;
                else floor.Encapsulate(r.bounds);
                any = true;
            }
            floor.SetMinMax(new Vector3(area.xMin, floor.min.y, area.yMin), new Vector3(area.xMax, floor.max.y, area.yMax));
            BuildWalls();
        }

        // The cartoon palette. Each map surface takes one of these as its base tone.
        static readonly Color[] Palette = Hex("867f77", "696057", "d4b28c", "e3f1fc", "9bbedd", "674626", "70dba4", "b17b4b", "9a8c81", "655a5f", "b09880");
        static readonly Color Green = Hex("70dba4")[0];
        static readonly Color Sky = Hex("9bbedd")[0], SkyPale = Hex("e3f1fc")[0];

        static Color[] Hex(params string[] codes)
        {
            var colors = new Color[codes.Length];
            for (int i = 0; i < codes.Length; i++) ColorUtility.TryParseHtmlString("#" + codes[i], out colors[i]);
            return colors;
        }

        // Surfaces whose texture is the message (letters, logos, screens) only lean toward the palette.
        static readonly string[] KeepTexture = { "Graffiti", "Sign", "Flag", "Logo", "Map_", "Book", "Monitor", "Window", "Emissive", "Light", "Spice", "Plushie", "Rug" };
        static readonly string[] Plants = { "Foliage", "Tree", "Plant", "Grass", "Leaves", "Flower" };

        Material Toon(Material src)
        {
            var mat = new Material(src) { name = src.name + " (toon)" };
#if UNITY_EDITOR
            if (Shader.Find(mat.shader.name) is Shader s) mat.shader = s;
#endif
            if (!mat.HasProperty("_BaseColor")) return mat;

            var tex = mat.mainTexture;
            bool plant = Has(src.name, Plants);
            bool keep = Has(src.name, KeepTexture);

            // The palette is the base tone, not a flat fill. Each surface keeps its texture
            // (grain, bricks, planks, lighting), and the tint just moves the texture's
            // average color onto its palette color, nudged a bit per material for variety.
            Color avg = tex ? Average(tex) : Color.white;
            Color baseTint = mat.GetColor("_BaseColor");
            Color current = baseTint * avg;
            Color? fixedColor = Fixed(src.name, out bool glow);
            Color target = fixedColor ?? (plant ? Green : Nearest(current));
            if (fixedColor == null) target = Color.Lerp(target, current, 0.12f);
            uint hash = (uint)src.name.GetHashCode();
            float jitter = 1f + ((hash % 1000) / 1000f - 0.5f) * 0.16f;
            target *= jitter;

            if (glow && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", target * 0.45f);
            }
            if (keep && fixedColor == null)
            {
                // Letters, logos and screens: only lean toward the palette.
                target = Color.Lerp(current, target, 0.35f);
            }
            var tint = new Color(Ratio(target.r, avg.r), Ratio(target.g, avg.g), Ratio(target.b, avg.b), 1f);
            mat.SetColor("_BaseColor", tint);
            if (tex) tex.mipMapBias = keep ? 0.5f : 1.2f;

            mat.SetFloat("_Smoothness", 0f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            foreach (var map in new[] { "_BumpMap", "_DetailAlbedoMap", "_DetailNormalMap", "_OcclusionMap", "_ParallaxMap", "_MetallicGlossMap", "_SpecGlossMap" })
                if (mat.HasProperty(map)) mat.SetTexture(map, null);
            foreach (var k in new[] { "_NORMALMAP", "_DETAIL_MULX2", "_OCCLUSIONMAP", "_PARALLAXMAP", "_METALLICSPECGLOSSMAP" })
                mat.DisableKeyword(k);
            if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 0f);
            if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            return mat;
        }

        static float Ratio(float want, float have) => Mathf.Clamp(want / Mathf.Max(have, 0.04f), 0f, 3f);

        static readonly Color Dirt = Color.Lerp(Hex("674626")[0], Hex("b17b4b")[0], 0.25f);

        // Surfaces with a set color, matched by material name. Checked before the nearest-color guess.
        static readonly (string[] words, Color color, bool glow)[] Rules =
        {
            (new[] { "GlassShardGlow", "Glass_M0D" }, Green, true),          // crate panels: mint, softly lit
            (new[] { "RCrate", "FTBox" }, Hex("867f77")[0], false),         // crate frames: gray
            (new[] { "Tarp", "Cloth" }, Hex("9a8c81")[0], false),            // tarps: gray
            (new[] { "PackedDirt" }, Hex("9a8c81")[0], false),               // C site paving
            (new[] { "AshPowder", "Ash_M" }, Hex("867f77")[0], false),         // dust patches on the paving
            (new[] { "DirtGrassBlend", "DirtBlend", "Dirt_" }, Dirt, false),  // paths into the sites
            (new[] { "Tile_", "SquareTiles", "RoughStoneTiles", "LooseRoundStone", "Stone_M2", "Stairs", "Asphalt", "StoneBrickCover", "Ramp" }, Hex("867f77")[0], false),
            (new[] { "Paper" }, Hex("b09880")[0], false),                  // scattered papers: beige, never white
            (new[] { "bomb_marking" }, Hex("696057")[0], false),
            (new[] { "Trunk" }, Hex("674626")[0], false),
            (new[] { "Ashen" }, Hex("696057")[0], false),                    // bare gray trees
        };

        static Color? Fixed(string name, out bool glow)
        {
            foreach (var r in Rules)
                if (Has(name, r.words)) { glow = r.glow; return r.color; }
            glow = false;
            return null;
        }

        static bool Has(string name, string[] words)
        {
            foreach (var w in words) if (name.Contains(w)) return true;
            return false;
        }

        RenderTexture probe;
        Texture2D probeRead;

        // Average color of a texture, read back from a tiny GPU blit (works for non-readable textures and WebGL).
        Color Average(Texture tex)
        {
            if (!probe)
            {
                probe = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                probeRead = new Texture2D(8, 8, TextureFormat.RGBA32, false, false);
            }
            float bias = tex.mipMapBias;
            tex.mipMapBias = 6f;
            Graphics.Blit(tex, probe);
            tex.mipMapBias = bias;
            var prev = RenderTexture.active;
            RenderTexture.active = probe;
            probeRead.ReadPixels(new Rect(0, 0, 8, 8), 0, 0, false);
            RenderTexture.active = prev;
            Color sum = Color.clear;
            foreach (var p in probeRead.GetPixels()) sum += p;
            return sum / 64f;
        }

        // Closest palette entry. Saturation is exaggerated first so faint warm or blue
        // tints land on tan, brown or blue instead of all collapsing into the grays.
        static Color Nearest(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            var target = Color.HSVToRGB(h, Mathf.Clamp01(s * 1.8f), v);
            Color best = Palette[0];
            float bestD = float.MaxValue;
            foreach (var p in Palette)
            {
                if (p == Green || p == Sky || p == SkyPale) continue; // green is for plants and crates, the blues are sky only
                float dr = p.r - target.r, dg = p.g - target.g, db = p.b - target.b;
                float d = 2f * dr * dr + 4f * dg * dg + 3f * db * db;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        void BuildWalls()
        {
            var walls = new GameObject("Walls").transform;
            var c = floor.center;
            var s = floor.size;
            void Wall(float x, float z, float w, float d)
            {
                var box = new GameObject("Wall").AddComponent<BoxCollider>();
                box.transform.SetParent(walls);
                box.transform.position = new Vector3(x, c.y, z);
                box.size = new Vector3(w, 200f, d);
            }
            Wall(floor.min.x - 0.5f, c.z, 1f, s.z + 2f);
            Wall(floor.max.x + 0.5f, c.z, 1f, s.z + 2f);
            Wall(c.x, floor.min.z - 0.5f, s.x + 2f, 1f);
            Wall(c.x, floor.max.z + 0.5f, s.x + 2f, 1f);
        }

        public void BuildNavMesh()
        {
            var bounds = new Bounds(floor.center, floor.size + new Vector3(2f, 30f, 2f));
            var sources = new List<NavMeshBuildSource>();
            int layers = Physics.DefaultRaycastLayers & ~(1 << Fighter.Layer);
            NavMeshBuilder.CollectSources(bounds, layers, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.45f;
            settings.agentHeight = 1.9f;
            settings.agentClimb = 0.4f;
            settings.agentSlope = 45f;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            NavMesh.AddNavMeshData(data);
            hasNavMesh = true;
        }

        public Vector3 FindSpawn(float radius)
        {
            for (int i = 0; i < 400; i++)
            {
                var top = new Vector3(Random.Range(floor.min.x, floor.max.x), floor.max.y + 30f, Random.Range(floor.min.z, floor.max.z));
                if (!Physics.Raycast(top, Vector3.down, out var hit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.point.y > floor.max.y + 0.5f || hit.normal.y < 0.8f || !hit.collider.name.StartsWith("Floor")) continue;
                var feet = hit.point + Vector3.up * 0.05f;
                if (Blocked(feet, radius + 0.1f)) continue;
                if (hasNavMesh && !NavMesh.SamplePosition(hit.point, out _, 1f, NavMesh.AllAreas)) continue;
                return feet;
            }
            return floor.center + Vector3.up * floor.extents.y;
        }

        public (Vector3, Vector3) FindSpawnPair(float radius)
        {
            var spots = new List<Vector3>();
            for (int i = 0; i < 24; i++) spots.Add(FindSpawn(radius));

            var best = (spots[0], spots[1]);
            float bestScore = float.MaxValue;
            for (int i = 0; i < spots.Count; i++)
                for (int j = i + 1; j < spots.Count; j++)
                {
                    float d = Vector3.Distance(spots[i], spots[j]);
                    if (d < 1f) continue;
                    float score = Mathf.Abs(d - 24f);
                    if (hasNavMesh && !Connected(spots[i], spots[j])) score += 1000f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = (spots[i], spots[j]);
                    }
                }
            return best;
        }

        static bool Connected(Vector3 a, Vector3 b)
        {
            var path = new NavMeshPath();
            return NavMesh.SamplePosition(a, out var ha, 2f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(b, out var hb, 2f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete;
        }

        static bool Blocked(Vector3 feet, float radius, float height = 1.9f)
        {
            var bottom = feet + Vector3.up * (radius + 0.04f);
            var top = feet + Vector3.up * (height - radius);
            foreach (var c in Physics.OverlapCapsule(bottom, top, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (c is not CharacterController) return true;
            return Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.up, height + 0.3f);
        }
    }
}
