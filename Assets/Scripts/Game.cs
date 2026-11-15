using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

namespace HavenDuel
{
    public class Game : MonoBehaviour
    {
        public PokemonRoster roster;
        public CombatConfig combat;
        public int port = 7777;
        public string publicRelay;
        public float sendRate = 15f;

        public Site site = Site.C;
        [System.NonSerialized] public Fighter me;
        [System.NonSerialized] public Rounds rounds;

        static readonly string[] SiteNames = { "A", "B + Mid", "C" };
        static readonly string[] Opponents = { "Bot Greninja", "Bot Charizard", "Online" };

        Species mySpecies = Species.Greninja;
        int opponent;
        string host;
        bool started;
        string status = "";

        readonly GameObject[] sitePrefabs = new GameObject[3];
        readonly float[] siteProgress = new float[3];
        readonly string[] siteError = new string[3];

        Map map;
        Fighter bot;
        Online online;
        Hud hud;
        Transform cam;
        float yaw, pitch;
        float spawnRadius = 0.4f;

        void Start()
        {
            // Clear blue day: the procedural sky only, no photo dome with a warm horizon.
            if (GameObject.Find("SkyDome") is GameObject dome) dome.SetActive(false);
            // Softer fill light so sunlit floors stay gray instead of washing out to white.
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.84f, 0.95f);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.intensity = Mathf.Min(l.intensity, 1.5f);
            // Everyone meets on the public Cloudflare relay (Tools/relay-worker), whether they play
            // from itch, the LAN page or the editor. Tools/relay_server.py is only for offline testing.
            host = string.IsNullOrEmpty(publicRelay) ? $"ws://{Relay.PageHost}:{port}" : publicRelay;
            StartCoroutine(Preload());
        }

        void OnDestroy() => online?.Dispose();

        IEnumerator Preload()
        {
            for (int i = 0; i < 3; i++)
            {
                var url = $"{Application.streamingAssetsPath}/sites/site_{((Site)i).ToString().ToLower()}";
                if (!url.Contains("://")) url = "file://" + url;
                using var req = UnityWebRequestAssetBundle.GetAssetBundle(url);
                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    siteProgress[i] = req.downloadProgress;
                    yield return null;
                }
                if (req.result != UnityWebRequest.Result.Success)
                {
                    siteError[i] = req.error;
                    continue;
                }
                var load = DownloadHandlerAssetBundle.GetContent(req).LoadAssetAsync<GameObject>($"Site{(Site)i}");
                yield return load;
                sitePrefabs[i] = (GameObject)load.asset;
            }
        }

        IEnumerator StartMatch()
        {
            started = true;
            int i = (int)site;
            while (!sitePrefabs[i] && siteError[i] == null)
            {
                status = $"Loading Site {site}... {Mathf.RoundToInt(siteProgress[i] * 100)}%";
                yield return null;
            }
            if (siteError[i] != null)
            {
                status = $"Couldn't load Site {site}: {siteError[i]}";
                yield break;
            }
            map = new Map(sitePrefabs[i], site);

            bool vsBot = opponent < 2;
            if (vsBot)
            {
                status = "Building bot paths...";
                yield return null;
                map.BuildNavMesh();
            }

            rounds = new Rounds
            {
                cfg = combat,
                fighters = Fighters,
                pickSpawns = () => map.FindSpawnPair(spawnRadius),
                onNewRound = OnNewRound,
            };

            me = SpawnFighter(mySpecies, vsBot ? 1 : 0, "You");
            AttachCamera();
            if (vsBot)
            {
                bot = SpawnFighter(opponent == 0 ? Species.Greninja : Species.Charizard, 2, "Bot");
                bot.isBot = true;
                var brain = bot.gameObject.AddComponent<Bot>();
                brain.me = bot;
                brain.target = me;
                status = "Practice against the Rookie bot";
            }
            else
                online = new Online(this, host.Contains("://") ? host : $"ws://{host}:{port}");

            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.Bind(combat, me, Enemy, rounds);
        }

        List<Fighter> Fighters()
        {
            var list = new List<Fighter> { me };
            if (bot) list.Add(bot);
            if (online != null) list.AddRange(online.Fighters);
            return list;
        }

        Fighter Enemy() => bot ? bot : online.Fighters.OrderBy(f => f.id).FirstOrDefault();

        Fighter SpawnFighter(Species species, int id, string label)
        {
            var def = combat.Get(species);
            var anim = roster.Spawn(species, Vector3.zero, Quaternion.identity, def.height);
            anim.name = $"{label} ({species})";

            var size = PokemonRoster.MeshBounds(anim.transform).size;
            float radius = Mathf.Clamp(Mathf.Max(size.x, size.z) / 2, 0.25f, 0.45f);
            spawnRadius = Mathf.Max(spawnRadius, radius);
            var body = anim.gameObject.AddComponent<CharacterController>();
            body.height = def.height;
            body.radius = radius;
            body.center = Vector3.up * def.height / 2;
            body.stepOffset = 0.35f;
            body.skinWidth = 0.04f;
            body.slopeLimit = 50f;

            var motor = anim.gameObject.AddComponent<Motor>();
            motor.species = species;
            motor.ceilingY = map.CeilingY;
            motor.fallY = map.FallY;

            var f = anim.gameObject.AddComponent<Fighter>();
            f.Init(id, true, def);
            f.aim = new GameObject("Aim").transform;
            f.aim.SetParent(f.transform, false);
            f.aim.localPosition = new Vector3(0f, def.height * 0.9f, radius / 2);

            motor.Teleport(map.FindSpawn(radius), 0f);
            motor.respawn = () => rounds.SpawnOf(f.id)?.pos ?? map.FindSpawn(radius);
            return f;
        }

        public Fighter SpawnRemote(Species species, int id, Vector3 pos, float yaw)
        {
            var def = combat.Get(species);
            var anim = roster.Spawn(species, pos, Quaternion.Euler(0f, yaw, 0f), def.height);
            anim.name = $"Player {id} ({species})";
            var f = anim.gameObject.AddComponent<Fighter>();
            f.Init(id, false, def);
            return f;
        }

        void AttachCamera()
        {
            foreach (var r in me.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            var camera = Camera.main;
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = me.Species == Species.Charizard ? 75f : 85f;
            cam = camera.transform;
            cam.SetParent(me.aim, false);
            cam.localPosition = Vector3.zero;
            cam.localRotation = Quaternion.identity;
            me.aim = cam;
            Hands.Attach(cam, me);
            yaw = me.transform.eulerAngles.y;
        }

        void OnNewRound()
        {
            foreach (var f in new[] { me, bot })
            {
                if (!f) continue;
                f.Revive();
                var spawn = rounds.SpawnOf(f.id);
                if (spawn == null) continue;
                f.motor.Teleport(spawn.pos, spawn.yaw);
                if (f == me)
                {
                    yaw = spawn.yaw;
                    pitch = 0f;
                }
            }
            if (online != null)
                foreach (var f in online.Fighters) f.SetHp(f.MaxHp);
            foreach (var p in Projectile.All.ToArray()) Destroy(p.gameObject);
        }

        void Update()
        {
            if (!me) return;
            float dt = Time.deltaTime;
            online?.Update(dt, sendRate);
            if (online == null || online.IsHost) rounds.HostTick(dt);

            bool fighting = rounds.phase == Phase.Fight;
            Fighter.CombatLive = fighting;
            foreach (var f in new[] { me, bot })
                if (f) f.motor.locked = rounds.phase == Phase.Countdown || !f.Alive || f.Recharging;

            HandleInput(dt, fighting);
            if (bot)
            {
                var input = bot.GetComponent<Bot>().Think(dt, out bool fire);
                bot.motor.Tick(input, dt);
                if (fire && fighting)
                {
                    bot.TryFire();
                    if (Random.value < 0.08f) bot.TryAbility(Random.Range(0, 4));
                }
            }
            hud.SetStatus(online?.Status ?? status);
        }

        void HandleInput(float dt, bool canFire)
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool mouseLocked = Cursor.lockState == CursorLockMode.Locked;
            if (mouse.leftButton.wasPressedThisFrame && !mouseLocked) Cursor.lockState = CursorLockMode.Locked;
            if (kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            Cursor.visible = !mouseLocked;

            var input = new MotorInput { yaw = yaw };
            if (mouseLocked)
            {
                var look = mouse.delta.ReadValue() * 0.1f;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);

                if (kb.wKey.isPressed) input.move.y += 1;
                if (kb.sKey.isPressed) input.move.y -= 1;
                if (kb.dKey.isPressed) input.move.x += 1;
                if (kb.aKey.isPressed) input.move.x -= 1;
                input.yaw = yaw;
                input.run = kb.leftShiftKey.isPressed;
                input.jump = kb.spaceKey.wasPressedThisFrame;
                input.jumpHeld = kb.spaceKey.isPressed;
                input.down = kb.leftCtrlKey.isPressed;
            }
            cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            me.motor.Tick(input, dt);

            if (!canFire || !mouseLocked) return;
            if (mouse.leftButton.isPressed) me.TryFire();
            if (kb.cKey.wasPressedThisFrame) me.TryAbility(0);
            if (kb.qKey.wasPressedThisFrame) me.TryAbility(1);
            if (kb.eKey.wasPressedThisFrame) me.TryAbility(2);
            if (kb.xKey.wasPressedThisFrame) me.TryAbility(3);
        }

        GUIStyle panel, label, title, button, picked, fight;
        Texture2D accent;

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void MakeStyles()
        {
            var teal = new Color(0.33f, 0.93f, 0.78f);
            var red = new Color(1f, 0.33f, 0.36f);
            panel = new GUIStyle { normal = { background = Solid(new Color(0.06f, 0.08f, 0.1f, 0.9f)) } };
            label = new GUIStyle { font = combat.menuFont, fontSize = 18, normal = { textColor = new Color(1f, 1f, 1f, 0.55f) } };
            title = new GUIStyle(label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            button = new GUIStyle(label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = Solid(new Color(1f, 1f, 1f, 0.07f)), textColor = Color.white },
                hover = { background = Solid(new Color(1f, 1f, 1f, 0.16f)), textColor = Color.white },
            };
            accent = Solid(teal);
            picked = new GUIStyle(button)
            {
                normal = { background = Solid(new Color(1f, 1f, 1f, 0.2f)), textColor = Color.white },
                hover = { background = Solid(new Color(1f, 1f, 1f, 0.24f)), textColor = Color.white },
            };
            fight = new GUIStyle(button)
            {
                fontSize = 32,
                normal = { background = Solid(red), textColor = Color.white },
                hover = { background = Solid(Color.Lerp(red, Color.white, 0.15f)), textColor = Color.white },
            };
        }

        int Choice(float x, float y, int value, string[] options)
        {
            float w = (340f - 8f * (options.Length - 1)) / options.Length;
            for (int i = 0; i < options.Length; i++)
                {
                var r = new Rect(x + i * (w + 8f), y, w, 36);
                if (GUI.Button(r, options[i].ToUpper(), i == value ? picked : button)) value = i;
                if (i == value) GUI.DrawTexture(new Rect(r.x, r.yMax - 3, r.width, 3), accent);
            }
            return value;
        }

        void OnGUI()
        {
            if (started)
            {
                if (!me) GUI.Label(new Rect(10, 10, 600, 22), status);
                return;
            }
            if (panel == null) MakeStyles();

            var box = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 230, 400, 460);
            GUI.Box(box, GUIContent.none, panel);
            float x = box.x + 30, y = box.y + 18;
            GUI.Label(new Rect(box.x, y, box.width, 56), "HAVEN DUEL", title);
            y += 70;

            GUI.Label(new Rect(x, y, 340, 22), "YOUR POKEMON", label);
            mySpecies = (Species)Choice(x, y + 24, (int)mySpecies, new[] { "Greninja", "Charizard" });
            y += 74;

            GUI.Label(new Rect(x, y, 340, 22), "OPPONENT", label);
            opponent = Choice(x, y + 24, opponent, Opponents);
            y += 74;
            if (opponent == 2)
            {
                GUI.Label(new Rect(x, y, 340, 22), "SERVER", label);
                host = GUI.TextField(new Rect(x, y + 24, 340, 30), host, button);
                y += 66;
            }

            GUI.Label(new Rect(x, y, 340, 22), "MAP", label);
            site = (Site)Choice(x, y + 24, (int)site, SiteNames);
            y += 80;

            if (GUI.Button(new Rect(x, y, 340, 54), opponent == 2 ? "JOIN" : "FIGHT", fight)) StartCoroutine(StartMatch());

            string maps = "";
            for (int i = 0; i < 3; i++)
            {
                string state = siteError[i] != null ? "failed" : sitePrefabs[i] ? "ready" : $"{Mathf.RoundToInt(siteProgress[i] * 100)}%";
                maps += $"SITE {(Site)i} {state}    ";
            }
            GUI.Label(new Rect(x, box.yMax - 30, 340, 22), maps, label);
        }
    }
}
