using System.Collections.Generic;
using HavenDuel.Pokemon;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HavenDuel.Net
{
    /// Very small LAN MVP: pick Greninja or Charizard, join the relay
    /// (Tools/lan_server.py), walk around in first person and see the others.
    public class LanGame : MonoBehaviour
    {
        public PokemonRoster roster;
        public int port = 7777;
        [Tooltip("Relay used when the page is https (itch.io): Tailscale Funnel to Tools/lan_server.py.")]
        public string publicRelay = "wss://flyingfish-pc.tail988c5f.ts.net:8443";
        public float sendRate = 15f;
        [Tooltip("Every player's Pokemon is scaled to this height (m), so they read as human sized on the map.")]
        public float playerHeight = 1.4f;
        [Tooltip("Root of the level. Spawns are picked on its floor meshes.")]
        public Transform map;
        [Tooltip("Name prefix of the map's walkable floor meshes. Spawns and edge walls follow these.")]
        public string floorPrefix = "sols";

        public enum Site { A, B, C }
        [Header("Maps")]
        [Tooltip("Low-poly Site C map.")] public Transform siteCMap;
        [Tooltip("Full Haven map; Sites A and B are fenced-off parts of it.")] public Transform havenMap;
        // x/z areas inside the full Haven map (Unity space)
        static readonly Rect AreaA = Rect.MinMaxRect(-105f, -116f, -44f, -50f);   // A site, A long, cellar (stops before defender spawn and B)
        static readonly Rect AreaB = Rect.MinMaxRect(-38f, -118f, 12f, -48f);     // B site, mid, up to the end of mid window
        static readonly Rect AreaC = Rect.MinMaxRect(-5f, -125f, 50f, -45f);      // C site, C long, garage
        public static Rect Area(Site s) => s == Site.A ? AreaA : s == Site.B ? AreaB : AreaC;
        [Tooltip("Load the site from its asset bundle (StreamingAssets/sites) instead of a map already in the scene.")]
        public bool siteBundles = true;
        float loadProgress = -1f;
        readonly GameObject[] sitePrefabs = new GameObject[3];
        readonly float[] siteProgress = new float[3];
        readonly string[] siteError = new string[3];

        /// Downloads all three sites while you sit on the start screen; each round then just places one.
        System.Collections.IEnumerator Preload()
        {
            for (int i = 0; i < 3; i++)
            {
                var s = (Site)i;
#if UNITY_EDITOR
                sitePrefabs[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Data/Sites/Site{s}.prefab");
                siteProgress[i] = 1f;
                yield return null;
#else
                var url = $"{Application.streamingAssetsPath}/sites/site_{s.ToString().ToLower()}";
                using var req = UnityEngine.Networking.UnityWebRequestAssetBundle.GetAssetBundle(url);
                var op = req.SendWebRequest();
                while (!op.isDone) { siteProgress[i] = req.downloadProgress * 0.95f; yield return null; }
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) { siteError[i] = req.error; continue; }
                var bundle = UnityEngine.Networking.DownloadHandlerAssetBundle.GetContent(req);
                var load = bundle.LoadAssetAsync<GameObject>($"Site{s}");
                yield return load;
                sitePrefabs[i] = load.asset as GameObject;
                siteProgress[i] = 1f;
#endif
            }
        }
        [Tooltip("Map pieces this far outside the chosen site are switched off.")]
        public float cutMargin = 6f;
        Site mySite = Site.C;
        Rect region;

        [System.Serializable]
        class Msg { public string t; public int id; public int sp; public int m; public float x, y, z, yaw, vx, vy, vz; public bool g, f; }

        class Remote { public PokemonAnimator anim; public Vector3 target, vel; public float yaw; public bool grounded, flying; }

        static readonly Species[] Choices = { Species.Greninja, Species.Charizard };

        LanSocket sock;
        int myId;
        Species mySpecies = Species.Greninja;
        string host;
        bool joined;
        string status = "";

        // local player
        CharacterController body;
        Transform cam;
        float pitch, yaw, vy, sendTimer;
        Vector3 lastPos;
        readonly Dictionary<int, Remote> remotes = new();

        void Start()
        {
            var page = LanSocket.PageHost;
            if (string.IsNullOrEmpty(page)) page = "localhost";
            // https pages (itch.io) can only open wss://, so they go through the public relay
            if (siteBundles) StartCoroutine(Preload());
            host = LanSocket.PageIsHttps ? publicRelay : $"ws://{page}:{port}";
        }

        void Join() => StartCoroutine(JoinRoutine());

        System.Collections.IEnumerator JoinRoutine()
        {
            joined = true;
            status = $"Loading Site {mySite}...";
            if (siteBundles)
            {
                int i = (int)mySite;
                while (!sitePrefabs[i] && siteError[i] == null) { loadProgress = siteProgress[i]; yield return null; }
                if (siteError[i] != null) { status = $"Couldn't load Site {mySite}: {siteError[i]}"; yield break; }
                var prefab = sitePrefabs[i];
                loadProgress = -1f;
                if (!prefab) { status = $"Site {mySite} is missing from the build"; yield break; }
                map = Instantiate(prefab).transform;
                map.name = "Site" + mySite;
                floorPrefix = "Floor";
                region = Area(mySite);
                yield return null;
                Physics.SyncTransforms();
            }
            sock = new LanSocket(host.Contains("://") ? host : $"ws://{host}:{port}");
            status = "Connecting...";
            BuildBounds();
            SpawnLocal();
        }

        void SpawnLocal()
        {
            var e = roster.Get(mySpecies);
            var a = roster.Spawn(mySpecies, Vector3.zero, Quaternion.identity, null, playerHeight);
            float radius = Radius(a.transform);
            var spawn = FindSpawn(radius);
            a.transform.position = spawn;
            // first person: keep our own shadow, hide the mesh from our camera
            foreach (var r in a.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            body = a.gameObject.AddComponent<CharacterController>();
            body.height = playerHeight; body.radius = radius; body.center = Vector3.up * playerHeight * 0.5f;
            body.stepOffset = 0.35f; body.skinWidth = 0.04f; body.slopeLimit = 50f;

            var main = Camera.main;
            cam = main.transform;
            cam.SetParent(a.transform, false);
            cam.localPosition = Vector3.up * playerHeight * 0.9f + Vector3.forward * body.radius * 0.5f;
            cam.localRotation = Quaternion.identity;
            main.nearClipPlane = 0.05f;
            main.fieldOfView = mySpecies == Species.Charizard ? 70f : 85f;
            lastPos = spawn;
        }

        void Update()
        {
            if (!joined || !body) return;
            Pump();
            MoveLocal();
            foreach (var r in remotes.Values)
            {
                var t = r.anim.transform;
                t.position = Vector3.Lerp(t.position, r.target, 1f - Mathf.Exp(-12f * Time.deltaTime));
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.Euler(0f, r.yaw, 0f), 1f - Mathf.Exp(-12f * Time.deltaTime));
                r.anim.UpdateLocomotion(r.vel, r.grounded, r.flying);
            }
        }

        // movement state
        bool flying, wasGrounded;
        int airJumps;
        float fatigue = 1f, heading, flyVy;

        void MoveLocal()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) Cursor.lockState = CursorLockMode.Locked;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = mouse.delta.ReadValue() * 0.1f;
                yaw += d.x; pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
            }
            cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            Vector2 mv = Vector2.zero;
            bool run = false, jump = false, jumpHeld = false, down = false;
            if (kb != null)
            {
                if (kb.wKey.isPressed) mv.y += 1; if (kb.sKey.isPressed) mv.y -= 1;
                if (kb.dKey.isPressed) mv.x += 1; if (kb.aKey.isPressed) mv.x -= 1;
                run = kb.leftShiftKey.isPressed; jump = kb.spaceKey.wasPressedThisFrame; jumpHeld = kb.spaceKey.isPressed;
                down = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
            }
            mv = Vector2.ClampMagnitude(mv, 1f);
            bool grounded = body.isGrounded;
            float dt = Time.deltaTime;
            Vector3 move;

            if (mySpecies == Species.Charizard)
            {
                // Glider-style flight: hold Space in the air to spread the wings and glide, tap Space
                // while gliding to flap upward, Ctrl to dive. Gliding and flapping use stamina; it refills on the ground.
                if (grounded) { flying = false; fatigue = Mathf.Min(1f, fatigue + dt / 2.5f); }
                bool canFly = fatigue > 0f;
                if (!grounded && jumpHeld && canFly) flying = true;
                if (!jumpHeld || !canFly || grounded) flying = flying && !grounded && jumpHeld && canFly;
                if (flying)
                {
                    fatigue -= dt / 14f;
                    if (jump && fatigue > 0.12f) { flyVy = 6f; fatigue -= 0.12f; }      // wing flap
                    heading = Mathf.MoveTowardsAngle(heading, yaw, 90f * dt);
                    float fwd = Mathf.Max(0f, mv.y) * (run ? 10f : 7f) + Mathf.Min(0f, mv.y) * 2f;
                    move = Quaternion.Euler(0f, heading, 0f) * new Vector3(mv.x * 2.5f, 0f, fwd);
                    float glideVy = down ? -9f : -1.6f;
                    flyVy = Mathf.MoveTowards(flyVy, glideVy, 9f * dt);
                    vy = flyVy;
                    if (body.transform.position.y > ceilingY && vy > 0f) vy = 0f;
                    if (fatigue <= 0f) { fatigue = 0f; flying = false; }
                }
                else
                {
                    heading = yaw;
                    move = Quaternion.Euler(0f, yaw, 0f) * new Vector3(mv.x, 0f, mv.y) * (run ? 4f : 2f);
                    if (grounded) { vy = -1f; if (jump) vy = 5f; }
                    else vy += Physics.gravity.y * dt;
                    flyVy = Mathf.Max(vy, -2f);
                }
                body.transform.rotation = Quaternion.Euler(0f, flying ? heading : yaw, 0f);
            }
            else
            {
                // Greninja: quick and agile. High jump, a second jump in the air, and a kick off walls.
                heading = yaw;
                body.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                float speed = run ? 8.5f : 4.5f;
                move = Quaternion.Euler(0f, yaw, 0f) * new Vector3(mv.x, 0f, mv.y) * speed;
                if (grounded) { airJumps = 1; vy = -1f; if (jump) vy = 7.5f; }
                else if (jump)
                {
                    if (airJumps > 0) { airJumps--; vy = 6.5f; }
                }
                if (!grounded) vy += Physics.gravity.y * 1.4f * dt;
            }

            var flags = body.Move((move + Vector3.up * vy) * dt);
            if ((flags & CollisionFlags.Above) != 0 && vy > 0f) vy = 0f;
            if (body.transform.position.y < fallY) { body.enabled = false; body.transform.position = FindSpawn(body.radius); body.enabled = true; vy = 0f; flying = false; }

            var vel = (body.transform.position - lastPos) / Mathf.Max(dt, 1e-4f);
            lastPos = body.transform.position;
            body.GetComponent<PokemonAnimator>().UpdateLocomotion(vel, body.isGrounded, flying);

            sendTimer -= dt;
            if (sock != null && sock.Status == LanSocket.State.Open && sendTimer <= 0f)
            {
                sendTimer = 1f / sendRate;
                var p = body.transform.position;
                sock.Send(JsonUtility.ToJson(new Msg { t = "s", id = myId, m = (int)mySite, sp = (int)mySpecies, x = p.x, y = p.y, z = p.z, yaw = body.transform.eulerAngles.y, vx = vel.x, vy = vel.y, vz = vel.z, g = body.isGrounded, f = flying }));
            }
        }

        float ceilingY = 40f;


        void Pump()
        {
            if (sock == null) return;
            status = sock.Status switch
            {
                LanSocket.State.Open => $"Online as player {myId} ({remotes.Count + 1} here)",
                LanSocket.State.Closed => $"Can't reach {host}. Is Tools/lan_server.py running?",
                _ => "Connecting...",
            };
            while (sock.TryReceive(out var raw))
            {
                Msg m;
                try { m = JsonUtility.FromJson<Msg>(raw); } catch { continue; }
                if (m == null) continue;
                switch (m.t)
                {
                    case "hello": myId = m.id; break;
                    case "bye":
                        if (remotes.TryGetValue(m.id, out var gone)) { Destroy(gone.anim.gameObject); remotes.Remove(m.id); }
                        break;
                    case "s":
                        if (m.id == myId || m.id == 0) break;
                        if (m.m != (int)mySite)
                        {
                            if (remotes.TryGetValue(m.id, out var other)) { Destroy(other.anim.gameObject); remotes.Remove(m.id); }
                            break;
                        }
                        var pos = new Vector3(m.x, m.y, m.z);
                        if (!remotes.TryGetValue(m.id, out var r) || (int)r.anim.GetComponent<PokemonModel>().species != m.sp)
                        {
                            if (r != null) Destroy(r.anim.gameObject);
                            var a = roster.Spawn((Species)m.sp, pos, Quaternion.Euler(0f, m.yaw, 0f), null, playerHeight);
                            a.name = $"Player {m.id} ({(Species)m.sp})";
                            r = new Remote { anim = a };
                            remotes[m.id] = r;
                        }
                        r.target = pos; r.yaw = m.yaw; r.vel = new Vector3(m.vx, m.vy, m.vz); r.grounded = m.g; r.flying = m.f;
                        break;
                }
            }
        }

        float fallY = -50f;
        Texture2D barTex;

        /// Switches off every map piece outside the chosen site, so only that site is drawn and collides.
        void CutToRegion()
        {
            var keep = Rect.MinMaxRect(region.xMin - cutMargin, region.yMin - cutMargin, region.xMax + cutMargin, region.yMax + cutMargin);
            int off = 0;
            foreach (var r in map.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                var r2 = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
                if (!keep.Overlaps(r2)) { r.gameObject.SetActive(false); off++; }
            }
            Debug.Log($"Site {mySite}: switched off {off} map pieces outside the site");
        }

        /// True if a player capsule standing at feet would touch any map geometry.
        bool Blocked(Vector3 feet, float radius)
        {
            var hits = Physics.OverlapCapsule(feet + Vector3.up * (radius + 0.04f), feet + Vector3.up * (playerHeight - radius), radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var h in hits) if (!(h is CharacterController)) return true;
            // also make sure there is head room straight above (shells and beams that the overlap can miss)
            return Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.up, playerHeight + 0.3f);
        }

        Bounds FloorBounds(out bool any)
        {
            Bounds b = default; any = false;
            foreach (var r in map.GetComponentsInChildren<Renderer>())
            {
                if (!r.name.StartsWith(floorPrefix)) continue;
                if (region.width > 0f && !region.Contains(new Vector2(r.bounds.center.x, r.bounds.center.z))) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (any && region.width > 0f)
                b.SetMinMax(new Vector3(region.xMin, b.min.y, region.yMin), new Vector3(region.xMax, b.max.y, region.yMax));
            return b;
        }

        /// Invisible walls around the playable area so nobody walks off the edge of the map.
        void BuildBounds()
        {
            if (!map) return;
            var b = FloorBounds(out bool any);
            if (!any) return;
            fallY = b.min.y - 3f;
            ceilingY = b.max.y + 12f;
            var root = new GameObject("Bounds").transform;
            float h = 200f, t = 1f, cy = b.center.y;
            void Wall(Vector3 c, Vector3 size) { var w = new GameObject("Wall").AddComponent<BoxCollider>(); w.transform.SetParent(root); w.transform.position = c; w.size = size; }
            Wall(new Vector3(b.min.x - t / 2, cy, b.center.z), new Vector3(t, h, b.size.z + 2 * t));
            Wall(new Vector3(b.max.x + t / 2, cy, b.center.z), new Vector3(t, h, b.size.z + 2 * t));
            Wall(new Vector3(b.center.x, cy, b.min.z - t / 2), new Vector3(b.size.x + 2 * t, h, t));
            Wall(new Vector3(b.center.x, cy, b.max.z + t / 2), new Vector3(b.size.x + 2 * t, h, t));
        }

        /// Half the model's footprint, so the capsule (and so the walls) keep the mesh out of the level.
        static float Radius(Transform t)
        {
            var b = PokemonModel.MeshBounds(t);
            if (b.size.sqrMagnitude < 1e-4f) return 0.35f;
            // Tight posed-mesh footprint; capped so doorways (1.6 m) stay passable.
            return Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.5f, 0.25f, 0.45f);
        }

        /// A random open spot on the map's floor meshes ("sols*"), with room for the capsule.
        Vector3 FindSpawn(float radius)
        {
            if (!map) return Vector3.up;
            var b = FloorBounds(out bool any);
            fallY = b.min.y - 3f;
            float rayTop = b.max.y + 30f;
            Vector3 best = b.center + Vector3.up * b.extents.y;
            for (int i = 0; i < 400; i++)
            {
                var p = new Vector3(Random.Range(b.min.x, b.max.x), rayTop, Random.Range(b.min.z, b.max.z));
                if (!Physics.Raycast(p, Vector3.down, out var hit, 200f)) continue;
                if (hit.point.y > b.max.y + 0.5f) continue;
                if ((floorPrefix.Length > 0 && !hit.collider.name.StartsWith(floorPrefix)) || hit.normal.y < 0.8f) continue;
                if (Blocked(hit.point + Vector3.up * 0.05f, radius + 0.1f)) continue;
                return hit.point + Vector3.up * 0.05f;
            }
            return best;
        }

        void OnGUI()
        {
            if (!joined)
            {
                var box = new Rect(Screen.width / 2f - 160, Screen.height / 2f - 120, 320, 240);
                GUI.Box(box, "HavenDuel");
                GUI.Label(new Rect(box.x + 15, box.y + 30, 70, 22), "Server");
                host = GUI.TextField(new Rect(box.x + 80, box.y + 30, 225, 22), host);
                for (int i = 0; i < Choices.Length; i++)
                {
                    var on = mySpecies == Choices[i];
                    if (GUI.Toggle(new Rect(box.x + 15 + i * 150, box.y + 65, 140, 26), on, Choices[i].ToString(), "Button")) mySpecies = Choices[i];
                }
                GUI.Label(new Rect(box.x + 15, box.y + 100, 290, 22), "Map");
                string[] siteNames = { "Site A", "Site B + Mid", "Site C" };
                for (int i = 0; i < 3; i++)
                    if (GUI.Toggle(new Rect(box.x + 15 + i * 97, box.y + 122, 93, 26), (int)mySite == i, siteNames[i], "Button")) mySite = (Site)i;
                if (GUI.Button(new Rect(box.x + 15, box.y + 170, 290, 44), "Join")) Join();
                if (siteBundles)
                {
                    string dl = "Maps: ";
                    for (int i = 0; i < 3; i++) dl += $"{(Site)i} " + (siteError[i] != null ? "failed  " : sitePrefabs[i] ? "ready  " : $"{Mathf.RoundToInt(siteProgress[i] * 100f)}%  ");
                    GUI.Label(new Rect(box.x + 15, box.y + 218, 300, 22), dl);
                }
                return;
            }
            GUI.Label(new Rect(10, 10, 600, 22), loadProgress >= 0f ? $"{status} {Mathf.RoundToInt(loadProgress * 100f)}%" : status);
            if (!body) return;
            GUI.Label(new Rect(10, 30, 900, 22), mySpecies == Species.Charizard ? "Mouse look, WASD move, Shift run, Space jump. In the air hold Space to glide, tap Space to flap, Ctrl to dive. Esc frees the mouse" : "Mouse look, WASD move, Shift sprint, Space jump (again in the air to double jump), Esc frees the mouse");
            if (mySpecies == Species.Charizard && fatigue < 0.999f)
            {
                if (!barTex) { barTex = new Texture2D(1, 1); barTex.SetPixel(0, 0, Color.white); barTex.Apply(); }
                float cx = Screen.width / 2f + 40f, cy = Screen.height / 2f - 30f, h = 60f;
                var old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.45f); GUI.DrawTexture(new Rect(cx, cy, 8f, h), barTex);
                GUI.color = fatigue < 0.25f ? new Color(0.95f, 0.3f, 0.25f) : new Color(0.45f, 0.95f, 0.45f);
                GUI.DrawTexture(new Rect(cx, cy + h * (1f - fatigue), 8f, h * fatigue), barTex);
                GUI.color = old;
            }
            GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 11, 20, 22), "+");
        }

        void OnDestroy() => sock?.Dispose();
    }
}
