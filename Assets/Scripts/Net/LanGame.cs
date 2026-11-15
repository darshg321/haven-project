using System.Collections.Generic;
using System.Linq;
using HavenDuel.Combat;
using HavenDuel.Pokemon;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace HavenDuel.Net
{
    /// Start screen, site loading and the 1v1 fight: you against the Rookie bot, or online
    /// through the relay (Tools/lan_server.py). Greninja and Charizard for now.
    public class LanGame : MonoBehaviour
    {
        public PokemonRoster roster;
        public CombatConfig combat;
        public int port = 7777;
        [Tooltip("Relay used when the page is https (itch.io): Tailscale Funnel to Tools/lan_server.py.")]
        public string publicRelay = "wss://flyingfish-pc.tail988c5f.ts.net:8443";
        public float sendRate = 15f;
        [Tooltip("Testing: a Rookie brain plays your Pokemon too (bot vs bot).")]
        public bool debugAutopilot;
        [Tooltip("Root of the level. Spawns are picked on its floor meshes.")]
        public Transform map;
        [Tooltip("Name prefix of the map's walkable floor meshes. Spawns and edge walls follow these.")]
        public string floorPrefix = "sols";

        public enum Site { A, B, C }
        public enum Opponent { Bot, Online }
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
                // same bundles in the Editor and the web build (the Editor's site prefabs need
                // haven_full.glb, which isn't in the repo)
                var url = $"{Application.streamingAssetsPath}/sites/site_{s.ToString().ToLower()}";
                if (!url.Contains("://")) url = "file://" + url;
                using var req = UnityEngine.Networking.UnityWebRequestAssetBundle.GetAssetBundle(url);
                var op = req.SendWebRequest();
                while (!op.isDone) { siteProgress[i] = req.downloadProgress * 0.95f; yield return null; }
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) { siteError[i] = req.error; continue; }
                var bundle = UnityEngine.Networking.DownloadHandlerAssetBundle.GetContent(req);
                var load = bundle.LoadAssetAsync<GameObject>($"Site{s}");
                yield return load;
                sitePrefabs[i] = load.asset as GameObject;
                siteProgress[i] = 1f;
            }
        }
        [Tooltip("Map pieces this far outside the chosen site are switched off.")]
        public float cutMargin = 6f;
        Site mySite = Site.C;
        Rect region;

        // ---------- network messages ----------
        [System.Serializable]
        class Msg
        {
            public string t; public int id; public int sp; public int m;
            public float x, y, z, yaw, pitch, vx, vy, vz; public bool g, f;
            public float hp;
            // hit
            public int tg; public float dmg;
            // round (host)
            public int rd, ph, rs, w; public float tl, pl;
            public int[] ids, wn, sids; public float[] spawn;
        }

        class Remote { public Fighter fighter; public PokemonAnimator anim; public Vector3 target, vel; public float yaw, pitch; public bool grounded, flying; public float lastSeen; }

        static readonly Species[] Choices = { Species.Greninja, Species.Charizard };

        LanSocket sock;
        int myId;
        Species mySpecies = Species.Greninja;
        Species botSpecies = Species.Charizard;
        Opponent opponent = Opponent.Bot;
        string host;
        bool joined;
        string status = "";

        Fighter me, bot;
        BotBrain botBrain, autopilot;
        Transform cam;
        float pitch, yaw, sendTimer, roundSendTimer;
        float spawnRadius = 0.4f;
        readonly Dictionary<int, Remote> remotes = new();
        RoundManager rounds;
        Hud hud;
        bool navReady;

        bool Offline => opponent == Opponent.Bot;
        bool IsHost => Offline || remotes.Keys.All(k => k > myId);

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
#if UNITY_EDITOR
                // the bundles carry WebGL shader code; the Editor draws them with its own copy of each shader
                var fixedShaders = new HashSet<Material>();
                foreach (var r in map.GetComponentsInChildren<Renderer>(true))
                    foreach (var mat in r.sharedMaterials)
                        if (mat && fixedShaders.Add(mat)) { var sh = Shader.Find(mat.shader.name); if (sh) mat.shader = sh; }
#endif
                floorPrefix = "Floor";
                region = Area(mySite);
                yield return null;
                Physics.SyncTransforms();
            }
            BuildBounds();
            if (Offline)
            {
                status = "Building bot paths...";
                yield return null;
                BuildNavMesh();
            }

            rounds = new RoundManager(combat)
            {
                Fighters = Participants,
                SpawnPair = SpawnPair,
                NewRound = OnNewRound,
            };

            myId = Offline ? 1 : 0;
            me = SpawnFighter(mySpecies, Offline ? 1 : 0, false);
            AttachCamera(me);
            if (Offline)
            {
                bot = SpawnFighter(botSpecies, 2, true);
                status = "Practice against the Rookie bot";
                if (debugAutopilot)
                {
                    autopilot = me.gameObject.AddComponent<BotBrain>();
                    autopilot.me = me; autopilot.target = bot;
                    status += " (autopilot)";
                }
            }
            else
            {
                sock = new LanSocket(host.Contains("://") ? host : $"ws://{host}:{port}");
                status = "Connecting...";
            }
            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.Bind(combat, me, Enemy, rounds);
            Fighter.Fired += OnFired;
            Fighter.LandedHit += OnLandedHit;
        }

        void OnDestroy()
        {
            Fighter.Fired -= OnFired;
            Fighter.LandedHit -= OnLandedHit;
            sock?.Dispose();
        }

        IReadOnlyList<Fighter> Participants()
        {
            var list = new List<Fighter>();
            if (me) list.Add(me);
            if (bot) list.Add(bot);
            foreach (var r in remotes.Values) if (r.fighter) list.Add(r.fighter);
            return list;
        }

        Fighter Enemy()
        {
            if (bot) return bot;
            return remotes.Values.Select(r => r.fighter).Where(f => f).OrderBy(f => f.id).FirstOrDefault();
        }

        Fighter SpawnFighter(Species s, int id, bool isBot)
        {
            var def = combat.Get(s);
            var a = roster.Spawn(s, Vector3.zero, Quaternion.identity, null, def.height);
            a.name = isBot ? $"Bot ({s})" : $"You ({s})";
            float radius = Radius(a.transform);
            spawnRadius = Mathf.Max(spawnRadius, radius);
            var body = a.gameObject.AddComponent<CharacterController>();
            body.height = def.height; body.radius = radius; body.center = Vector3.up * def.height * 0.5f;
            body.stepOffset = 0.35f; body.skinWidth = 0.04f; body.slopeLimit = 50f;
            var motor = a.gameObject.AddComponent<PokemonMotor>();
            motor.species = s; motor.ceilingY = ceilingY; motor.fallY = fallY;
            var f = a.gameObject.AddComponent<Fighter>();
            f.isBot = isBot;
            f.Init(id, true, def);
            var eyes = new GameObject("Aim").transform;
            eyes.SetParent(a.transform, false);
            eyes.localPosition = Vector3.up * def.height * 0.9f + Vector3.forward * radius * 0.5f;
            f.aim = eyes;
            var spawn = FindSpawn(radius);
            motor.Teleport(spawn, 0f);
            motor.Respawn = () => rounds != null && rounds.spawns.TryGetValue(f.id, out var sp) ? sp.pos : FindSpawn(radius);
            if (isBot)
            {
                botBrain = a.gameObject.AddComponent<BotBrain>();
                botBrain.me = f;
                botBrain.target = me;
            }
            return f;
        }

        void AttachCamera(Fighter f)
        {
            // first person: keep our own shadow, hide the mesh from our camera
            foreach (var r in f.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            var main = Camera.main;
            cam = main.transform;
            cam.SetParent(f.aim, false);
            cam.localPosition = Vector3.zero; cam.localRotation = Quaternion.identity;
            main.nearClipPlane = 0.05f;
            main.fieldOfView = f.Species == Species.Charizard ? 75f : 85f;
            f.aim = cam;  // shots come from the camera so they go where the crosshair is
            yaw = f.transform.eulerAngles.y;
        }

        // ---------- rounds ----------

        (Vector3, Vector3) SpawnPair()
        {
            // several open spots; keep the pair whose distance is closest to a duel range (~24 m)
            var c = new List<Vector3>();
            for (int i = 0; i < 24; i++) c.Add(FindSpawn(spawnRadius));
            (Vector3, Vector3) best = (c[0], c[1]); float score = float.MaxValue;
            for (int i = 0; i < c.Count; i++)
                for (int j = i + 1; j < c.Count; j++)
                {
                    float d = Vector3.Distance(c[i], c[j]);
                    if (d < 1f) continue;
                    float s = Mathf.Abs(d - 24f) + (navReady && !Connected(c[i], c[j]) ? 1000f : 0f);
                    if (s < score) { score = s; best = (c[i], c[j]); }
                }
            return best;
        }

        bool Connected(Vector3 a, Vector3 b)
        {
            var p = new NavMeshPath();
            return NavMesh.SamplePosition(a, out var ha, 2f, NavMesh.AllAreas) && NavMesh.SamplePosition(b, out var hb, 2f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, p) && p.status == NavMeshPathStatus.PathComplete;
        }

        /// Every machine respawns the fighters it owns.
        void OnNewRound(int n)
        {
            foreach (var f in new[] { me, bot })
            {
                if (!f) continue;
                f.Revive();
                if (rounds.spawns.TryGetValue(f.id, out var s))
                {
                    f.motor.Teleport(s.pos, s.yaw);
                    if (f == me) { yaw = s.yaw; pitch = 0f; }
                }
            }
            // remote players reset themselves; show them at full HP until their next update
            foreach (var r in remotes.Values) if (r.fighter) r.fighter.SetHp(r.fighter.MaxHp);
            foreach (var p in Projectile.All.ToArray()) if (p) Destroy(p.gameObject);
        }

        // ---------- frame ----------

        void Update()
        {
            if (!joined || !me) return;
            float dt = Time.deltaTime;
            Pump();
            if (IsHost) rounds.HostTick(dt);
            bool fightOn = rounds.phase == Phase.Fight;
            Fighter.CombatLive = fightOn;
            bool frozen = rounds.phase == Phase.Countdown;
            foreach (var f in new[] { me, bot }) if (f) f.motor.locked = frozen || !f.Alive;

            if (autopilot)
            {
                var auto = autopilot.Think(dt, out bool shoot);
                me.motor.Tick(auto, dt);
                if (shoot && fightOn) me.TryFire();
            }
            else MoveLocal(dt, fightOn);
            if (bot)
            {
                var input = botBrain.Think(dt, out bool fire);
                bot.motor.Tick(input, dt);
                if (fire && fightOn) bot.TryFire();
            }
            foreach (var r in remotes.Values)
            {
                if (!r.anim) continue;
                var t = r.anim.transform;
                t.position = Vector3.Lerp(t.position, r.target, 1f - Mathf.Exp(-12f * dt));
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.Euler(0f, r.yaw, 0f), 1f - Mathf.Exp(-12f * dt));
                r.anim.UpdateLocomotion(r.vel, r.grounded, r.flying);
            }
            Send(dt);
            hud.SetStatus(status);
        }

        void MoveLocal(float dt, bool canFire)
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            bool lockedNow = Cursor.lockState == CursorLockMode.Locked;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !lockedNow) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (mouse != null && lockedNow)
            {
                var d = mouse.delta.ReadValue() * 0.1f;
                yaw += d.x; pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
            }
            cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            var i = new MotorInput { yaw = yaw };
            if (kb != null && lockedNow)
            {
                Vector2 mv = Vector2.zero;
                if (kb.wKey.isPressed) mv.y += 1; if (kb.sKey.isPressed) mv.y -= 1;
                if (kb.dKey.isPressed) mv.x += 1; if (kb.aKey.isPressed) mv.x -= 1;
                i.move = mv; i.run = kb.leftShiftKey.isPressed;
                i.jump = kb.spaceKey.wasPressedThisFrame; i.jumpHeld = kb.spaceKey.isPressed;
                i.down = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
            }
            me.motor.Tick(i, dt);
            // while flying Charizard turns slowly, so the body lags behind the camera
            cam.parent.localRotation = Quaternion.Euler(0f, Mathf.DeltaAngle(me.transform.eulerAngles.y, yaw), 0f);

            if (canFire && lockedNow && mouse != null && mouse.leftButton.isPressed) me.TryFire();
        }

        // ---------- network ----------

        void OnFired(Fighter f, CombatConfig.MoveDef move, Vector3 origin, Vector3 dir)
        {
            if (f != me || sock == null || sock.Status != LanSocket.State.Open) return;
            sock.Send(JsonUtility.ToJson(new Msg { t = "fire", id = myId, m = (int)mySite, x = origin.x, y = origin.y, z = origin.z, vx = dir.x, vy = dir.y, vz = dir.z }));
        }

        void OnLandedHit(Fighter shooter, Fighter target, float dmg, Vector3 p)
        {
            // our shot hit a remote player: they own their HP, so tell them
            if (shooter != me || target.authority || sock == null || sock.Status != LanSocket.State.Open) return;
            sock.Send(JsonUtility.ToJson(new Msg { t = "hit", id = myId, m = (int)mySite, tg = target.id, dmg = dmg, x = p.x, y = p.y, z = p.z }));
        }

        void Send(float dt)
        {
            if (sock == null || sock.Status != LanSocket.State.Open || myId == 0) return;
            sendTimer -= dt;
            if (sendTimer <= 0f)
            {
                sendTimer = 1f / sendRate;
                var p = me.transform.position; var v = me.motor.Velocity;
                sock.Send(JsonUtility.ToJson(new Msg { t = "s", id = myId, m = (int)mySite, sp = (int)mySpecies, x = p.x, y = p.y, z = p.z, yaw = me.transform.eulerAngles.y, pitch = pitch, vx = v.x, vy = v.y, vz = v.z, g = me.motor.Grounded, f = me.motor.Flying, hp = me.hp, rd = rounds.round }));
            }
            roundSendTimer -= dt;
            if (IsHost && roundSendTimer <= 0f)
            {
                roundSendTimer = 0.2f;
                rounds.Export(out var ids, out var wn, out var sids, out var spawn);
                sock.Send(JsonUtility.ToJson(new Msg { t = "round", id = myId, m = (int)mySite, rd = rounds.round, ph = (int)rounds.phase, tl = rounds.timeLeft, pl = rounds.phaseLeft, w = rounds.winner, rs = (int)rounds.reason, ids = ids, wn = wn, sids = sids, spawn = spawn }));
            }
        }

        void Pump()
        {
            if (sock == null) return;
            status = sock.Status switch
            {
                LanSocket.State.Open => $"Online as player {myId} ({remotes.Count + 1} on Site {mySite}){(IsHost ? ", host" : "")}",
                LanSocket.State.Closed => $"Can't reach {host}. Is Tools/lan_server.py running?",
                _ => "Connecting...",
            };
            while (sock.TryReceive(out var raw))
            {
                Msg m;
                try { m = JsonUtility.FromJson<Msg>(raw); } catch { continue; }
                if (m == null) continue;
                if (m.t == "hello") { myId = m.id; me.id = m.id; continue; }
                if (m.t == "bye") { Drop(m.id); continue; }
                if (m.id == myId || m.id == 0) continue;
                if (m.m != (int)mySite) { Drop(m.id); continue; }
                switch (m.t)
                {
                    case "s":
                    {
                        var pos = new Vector3(m.x, m.y, m.z);
                        if (!remotes.TryGetValue(m.id, out var r) || !r.fighter || r.fighter.Species != (Species)m.sp)
                        {
                            Drop(m.id);
                            var def = combat.Get((Species)m.sp);
                            var a = roster.Spawn((Species)m.sp, pos, Quaternion.Euler(0f, m.yaw, 0f), null, def.height);
                            a.name = $"Player {m.id} ({(Species)m.sp})";
                            var f = a.gameObject.AddComponent<Fighter>();
                            f.Init(m.id, false, def);
                            r = new Remote { fighter = f, anim = a };
                            remotes[m.id] = r;
                        }
                        r.target = pos; r.yaw = m.yaw; r.pitch = m.pitch; r.vel = new Vector3(m.vx, m.vy, m.vz); r.grounded = m.g; r.flying = m.f; r.lastSeen = Time.time;
                        // HP from a round we've already left (lag) would KO them again at the next fight start
                        if (m.rd >= rounds.round) r.fighter.SetHp(m.hp);
                        break;
                    }
                    case "fire":
                        if (remotes.TryGetValue(m.id, out var shooter) && shooter.fighter)
                            shooter.fighter.Launch(shooter.fighter.def.basic, new Vector3(m.x, m.y, m.z), new Vector3(m.vx, m.vy, m.vz).normalized, false);
                        break;
                    case "hit":
                        if (m.tg == myId && Fighter.CombatLive)
                            me.TakeDamage(m.dmg, new Vector3(m.x, m.y, m.z), remotes.TryGetValue(m.id, out var from) ? from.fighter : null);
                        break;
                    case "round":
                        // follow the host (lowest id on this site)
                        if (!IsHost && remotes.Keys.Where(k => k != myId).DefaultIfEmpty(int.MaxValue).Min() == m.id)
                            rounds.Apply(m.rd, (Phase)m.ph, m.tl, m.pl, m.w, (EndReason)m.rs, m.ids, m.wn, m.sids, m.spawn);
                        break;
                }
            }
        }

        void Drop(int id)
        {
            if (!remotes.TryGetValue(id, out var gone)) return;
            if (gone.anim) Destroy(gone.anim.gameObject);
            remotes.Remove(id);
            rounds?.ForgetPair();
        }

        // ---------- map ----------

        float fallY = -50f;
        float ceilingY = 40f;

        /// Walkable NavMesh for the bot, built from the site's colliders when the round loads.
        void BuildNavMesh()
        {
            var fb = FloorBounds(out bool any);
            if (!any) return;
            var bounds = new Bounds(fb.center, fb.size + new Vector3(2f, 30f, 2f));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, Physics.DefaultRaycastLayers & ~(1 << Fighter.Layer), NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.45f; settings.agentHeight = 1.9f; settings.agentClimb = 0.4f; settings.agentSlope = 45f;
            float t0 = Time.realtimeSinceStartup;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data) { NavMesh.AddNavMeshData(data); navReady = true; }
            Debug.Log($"Bot NavMesh: {sources.Count} sources, {(Time.realtimeSinceStartup - t0) * 1000f:0} ms");
        }

        /// True if a player capsule standing at feet would touch any map geometry.
        bool Blocked(Vector3 feet, float radius, float height = 1.9f)
        {
            var hits = Physics.OverlapCapsule(feet + Vector3.up * (radius + 0.04f), feet + Vector3.up * (height - radius), radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var h in hits) if (!(h is CharacterController)) return true;
            // also make sure there is head room straight above (shells and beams that the overlap can miss)
            return Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.up, height + 0.3f);
        }

        Bounds FloorBounds(out bool any)
        {
            Bounds b = default; any = false;
            if (!map) return b;
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

        /// A random open spot on the map's floor meshes, with room for the capsule.
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
                if (!Physics.Raycast(p, Vector3.down, out var hit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.point.y > b.max.y + 0.5f) continue;
                if ((floorPrefix.Length > 0 && !hit.collider.name.StartsWith(floorPrefix)) || hit.normal.y < 0.8f) continue;
                if (Blocked(hit.point + Vector3.up * 0.05f, radius + 0.1f)) continue;
                if (navReady && !NavMesh.SamplePosition(hit.point, out _, 1f, NavMesh.AllAreas)) continue;
                return hit.point + Vector3.up * 0.05f;
            }
            return best;
        }

        // ---------- start screen ----------

        void OnGUI()
        {
            if (joined)
            {
                if (loadProgress >= 0f || !me) GUI.Label(new Rect(10, 10, 600, 22), loadProgress >= 0f ? $"{status} {Mathf.RoundToInt(loadProgress * 100f)}%" : status);
                return;
            }
            var box = new Rect(Screen.width / 2f - 160, Screen.height / 2f - 160, 320, 330);
            GUI.Box(box, "HavenDuel");
            float y = box.y + 28;
            GUI.Label(new Rect(box.x + 15, y, 290, 22), "Your Pokemon"); y += 22;
            for (int i = 0; i < Choices.Length; i++)
                if (GUI.Toggle(new Rect(box.x + 15 + i * 150, y, 140, 26), mySpecies == Choices[i], Choices[i].ToString(), "Button")) mySpecies = Choices[i];
            y += 36;
            GUI.Label(new Rect(box.x + 15, y, 290, 22), "Opponent"); y += 22;
            string[] opp = { "Bot: Greninja", "Bot: Charizard", "Online" };
            int cur = opponent == Opponent.Online ? 2 : botSpecies == Species.Greninja ? 0 : 1;
            for (int i = 0; i < 3; i++)
                if (GUI.Toggle(new Rect(box.x + 15 + i * 97, y, 93, 26), cur == i, opp[i], "Button"))
                {
                    opponent = i == 2 ? Opponent.Online : Opponent.Bot;
                    if (i < 2) botSpecies = i == 0 ? Species.Greninja : Species.Charizard;
                }
            y += 34;
            if (opponent == Opponent.Online)
            {
                GUI.Label(new Rect(box.x + 15, y, 70, 22), "Server");
                host = GUI.TextField(new Rect(box.x + 80, y, 225, 22), host);
            }
            y += 30;
            GUI.Label(new Rect(box.x + 15, y, 290, 22), "Map"); y += 22;
            string[] siteNames = { "Site A", "Site B + Mid", "Site C" };
            for (int i = 0; i < 3; i++)
                if (GUI.Toggle(new Rect(box.x + 15 + i * 97, y, 93, 26), (int)mySite == i, siteNames[i], "Button")) mySite = (Site)i;
            y += 40;
            if (GUI.Button(new Rect(box.x + 15, y, 290, 44), opponent == Opponent.Bot ? "Fight" : "Join")) Join();
            y += 50;
            if (siteBundles)
            {
                string dl = "Maps: ";
                for (int i = 0; i < 3; i++) dl += $"{(Site)i} " + (siteError[i] != null ? "failed  " : sitePrefabs[i] ? "ready  " : $"{Mathf.RoundToInt(siteProgress[i] * 100f)}%  ");
                GUI.Label(new Rect(box.x + 15, y, 300, 22), dl);
            }
        }
    }
}
