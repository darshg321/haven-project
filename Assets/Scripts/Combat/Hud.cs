using System.Collections.Generic;
using HavenDuel.Pokemon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HavenDuel.Combat
{
    /// In-fight HUD: timer and score, both HP bars, crosshair and hitmarker, damage numbers,
    /// round banners, Charizard's wing stamina, and the click-to-play overlay.
    public class Hud : MonoBehaviour
    {
        static readonly Color You = new(0.33f, 0.93f, 0.78f);
        static readonly Color Enemy = new(1f, 0.33f, 0.36f);
        static readonly Color Panel = new(0.06f, 0.08f, 0.1f, 0.62f);

        CombatConfig cfg;
        Fighter me;
        System.Func<Fighter> enemyOf;
        RoundManager rounds;
        Camera cam;

        RectTransform root;
        TextMeshProUGUI timer, roundLabel, scoreMe, scoreThem, banner, bannerSub, status, hint, bigHp, clickToPlay;
        Bar myBar, theirBar;
        Image myPortrait, theirPortrait, crosshair, hitmarker, flash, staminaBack, staminaFill;
        TextMeshProUGUI myName, theirName;
        GameObject clickPanel;
        float hitmarkerT = 99f, flashT = 99f, bannerT;
        bool hitKo;
        Phase lastPhase = Phase.Waiting;
        int lastRound = -1, lastCount = -1;
        readonly List<(TextMeshProUGUI t, Vector3 world, float age, Color c)> numbers = new();
        readonly Stack<TextMeshProUGUI> numberPool = new();

        class Bar
        {
            public RectTransform root; public Image fill, chip; public TextMeshProUGUI value; public float shown = 1f;
        }

        public void Bind(CombatConfig cfg, Fighter me, System.Func<Fighter> enemyOf, RoundManager rounds)
        {
            this.cfg = cfg; this.me = me; this.enemyOf = enemyOf; this.rounds = rounds;
            cam = Camera.main;
            if (!root) Build();
            myPortrait.sprite = me.def.portrait; myName.text = me.DisplayName.ToUpperInvariant();
            hint.text = me.Species == Species.Charizard
                ? "<b>LMB</b> " + me.def.basic.name.ToUpperInvariant() + "    <b>SPACE</b> jump, hold in air to glide, tap to flap    <b>CTRL</b> dive    <b>SHIFT</b> run"
                : "<b>LMB</b> " + me.def.basic.name.ToUpperInvariant() + "    <b>SPACE</b> jump, again in air to double jump    <b>SHIFT</b> sprint";
            Fighter.LandedHit += OnLandedHit;
            Fighter.Damaged += OnDamaged;
        }

        void OnDestroy()
        {
            Fighter.LandedHit -= OnLandedHit;
            Fighter.Damaged -= OnDamaged;
        }

        public void SetStatus(string s) { if (status) status.text = s; }

        void OnLandedHit(Fighter shooter, Fighter target, float dmg, Vector3 point)
        {
            if (shooter != me) return;
            bool ko = target.hp - dmg <= 0f;   // raised before the damage lands
            hitmarkerT = 0f; hitKo = ko;
            Sfx.Ui(cfg.hitmarkerSound, 0.9f);
            SpawnNumber(point, dmg, ko ? new Color(1f, 0.85f, 0.3f) : Color.white);
        }

        void OnDamaged(Fighter f, float dmg, Vector3 point, Fighter from)
        {
            if (f == me) flashT = 0f;
        }

        // ---------- building ----------

        void Build()
        {
            var canvasGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)canvasGo.transform;

            flash = Img("DamageFlash", root, null, new Color(1f, 0.1f, 0.1f, 0f));
            Stretch(flash.rectTransform);

            // top centre: timer between the two HP panels
            var top = Rect("Top", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(1400, 110));
            var timerBack = Img("TimerBack", top, cfg.panel, Panel, true);
            Place(timerBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(170, 92));
            timer = Text("Timer", timerBack.rectTransform, "2:00", 70, TextAlignmentOptions.Center);
            Stretch(timer.rectTransform, new Vector2(0, 10), Vector2.zero);
            roundLabel = Text("Round", timerBack.rectTransform, "ROUND 1", 24, TextAlignmentOptions.Center);
            Place(roundLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 2), new Vector2(170, 28));
            roundLabel.color = new Color(1f, 1f, 1f, 0.7f);
            scoreMe = Text("ScoreMe", top, "0", 64, TextAlignmentOptions.Center); scoreMe.color = You;
            Place(scoreMe.rectTransform, new Vector2(0.5f, 1f), new Vector2(-125, -2), new Vector2(70, 90));
            scoreThem = Text("ScoreThem", top, "0", 64, TextAlignmentOptions.Center); scoreThem.color = Enemy;
            Place(scoreThem.rectTransform, new Vector2(0.5f, 1f), new Vector2(125, -2), new Vector2(70, 90));

            myBar = MakeBar(top, -1, You, out myPortrait, out myName);
            theirBar = MakeBar(top, 1, Enemy, out theirPortrait, out theirName);

            // centre: crosshair, hitmarker, wing stamina
            crosshair = Img("Crosshair", root, cfg.crosshair, new Color(1f, 1f, 1f, 0.95f));
            Place(crosshair.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46));
            hitmarker = Img("Hitmarker", root, cfg.hitmarker, new Color(1f, 1f, 1f, 0f));
            Place(hitmarker.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54));
            staminaBack = Img("WingStamina", root, null, new Color(0f, 0f, 0f, 0.45f));
            Place(staminaBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(60, 0), new Vector2(8, 80));
            staminaFill = Img("Fill", staminaBack.rectTransform, null, You);
            staminaFill.rectTransform.anchorMin = Vector2.zero; staminaFill.rectTransform.anchorMax = Vector2.one;
            staminaFill.rectTransform.offsetMin = staminaFill.rectTransform.offsetMax = Vector2.zero;
            staminaFill.type = Image.Type.Filled; staminaFill.fillMethod = Image.FillMethod.Vertical; staminaFill.fillOrigin = 0;
            staminaFill.sprite = WhiteSprite();

            // banner
            banner = Text("Banner", root, "", 150, TextAlignmentOptions.Center);
            Place(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1600, 170));
            banner.outlineWidth = 0.12f; banner.outlineColor = new Color32(0, 0, 0, 160);
            bannerSub = Text("BannerSub", root, "", 44, TextAlignmentOptions.Center);
            Place(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 75), new Vector2(1400, 60));

            // bottom: big HP number + controls
            bigHp = Text("Hp", root, "100", 84, TextAlignmentOptions.Left);
            bigHp.rectTransform.pivot = Vector2.zero;
            Place(bigHp.rectTransform, Vector2.zero, new Vector2(48, 20), new Vector2(260, 100));
            hint = Text("Hint", root, "", 26, TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(1500, 40));
            hint.color = new Color(1f, 1f, 1f, 0.75f);
            status = Text("Status", root, "", 24, TextAlignmentOptions.TopLeft);
            status.rectTransform.pivot = new Vector2(0f, 1f);
            Place(status.rectTransform, new Vector2(0f, 1f), new Vector2(20, -14), new Vector2(560, 60));
            status.color = new Color(1f, 1f, 1f, 0.6f);

            // click to play (pointer lock needs a click in the browser)
            var cp = Img("ClickToPlay", root, null, new Color(0f, 0f, 0f, 0.45f));
            Stretch(cp.rectTransform);
            clickPanel = cp.gameObject;
            clickToPlay = Text("Label", cp.rectTransform, "CLICK TO PLAY", 72, TextAlignmentOptions.Center);
            Stretch(clickToPlay.rectTransform);
        }

        Bar MakeBar(RectTransform parent, int side, Color color, out Image portrait, out TextMeshProUGUI nameText)
        {
            var b = new Bar();
            float x = side * 210f;
            var anchorPivot = new Vector2(side < 0 ? 1f : 0f, 1f);
            b.root = Rect(side < 0 ? "YouPanel" : "EnemyPanel", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), anchorPivot, new Vector2(x, 0), new Vector2(470, 92));
            var back = Img("Back", b.root, cfg.panel, Panel, true); Stretch(back.rectTransform);

            portrait = Img("Portrait", b.root, null, Color.white);
            portrait.preserveAspect = true;
            Place(portrait.rectTransform, new Vector2(side < 0 ? 1f : 0f, 0.5f), new Vector2(side * 8f, 0f), new Vector2(84, 84));

            nameText = Text("Name", b.root, "", 34, side < 0 ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
            nameText.rectTransform.anchorMin = nameText.rectTransform.anchorMax = new Vector2(side < 0 ? 1f : 0f, 1f);
            nameText.rectTransform.pivot = new Vector2(side < 0 ? 1f : 0f, 1f);
            nameText.rectTransform.anchoredPosition = new Vector2(side * 100f, -6f);
            nameText.rectTransform.sizeDelta = new Vector2(250, 40);

            b.value = Text("Value", b.root, "", 34, side < 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
            b.value.rectTransform.anchorMin = b.value.rectTransform.anchorMax = new Vector2(side < 0 ? 0f : 1f, 1f);
            b.value.rectTransform.pivot = new Vector2(side < 0 ? 0f : 1f, 1f);
            b.value.rectTransform.anchoredPosition = new Vector2(side * -14f, -6f);
            b.value.rectTransform.sizeDelta = new Vector2(120, 40);

            var track = Img("Track", b.root, null, new Color(0f, 0f, 0f, 0.55f));
            var tr = track.rectTransform;
            tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 0f); tr.pivot = new Vector2(0.5f, 0f);
            tr.offsetMin = new Vector2(side < 0 ? 14f : 100f, 14f); tr.offsetMax = new Vector2(side < 0 ? -100f : -14f, 36f);
            b.chip = Img("Chip", tr, WhiteSprite(), new Color(1f, 1f, 1f, 0.85f));
            b.fill = Img("Fill", tr, WhiteSprite(), color);
            foreach (var img in new[] { b.chip, b.fill })
            {
                Stretch(img.rectTransform);
                img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Horizontal;
                img.fillOrigin = side < 0 ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
            }
            return b;
        }

        static Sprite white;
        static Sprite WhiteSprite()
        {
            if (white) return white;
            var t = Texture2D.whiteTexture;
            return white = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
        }

        RectTransform Rect(string n, RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(n, typeof(RectTransform));
            var r = (RectTransform)go.transform; r.SetParent(parent, false);
            r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        static Image Img(string n, RectTransform parent, Sprite s, Color c, bool sliced = false)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>();
            i.sprite = s; i.color = c; i.raycastTarget = false;
            if (sliced && s && s.border != Vector4.zero) i.type = Image.Type.Sliced;
            return i;
        }

        TextMeshProUGUI Text(string n, RectTransform parent, string s, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (cfg.font) t.font = cfg.font;
            t.text = s; t.fontSize = size; t.alignment = align; t.color = Color.white; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            if (r.pivot == new Vector2(0.5f, 0.5f)) r.pivot = anchor;
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        static void Stretch(RectTransform r) => Stretch(r, Vector2.zero, Vector2.zero);
        static void Stretch(RectTransform r, Vector2 min, Vector2 max)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = min; r.offsetMax = max;
        }

        // ---------- per frame ----------

        void Update()
        {
            if (!root || !me) return;
            float dt = Time.unscaledDeltaTime;
            var enemy = enemyOf?.Invoke();

            // HP
            UpdateBar(myBar, me, dt);
            bigHp.text = Mathf.CeilToInt(me.hp).ToString();
            bigHp.color = me.Hp01 > 0.5f ? Color.white : Color.Lerp(Enemy, Color.white, me.Hp01 * 2f);
            theirBar.root.gameObject.SetActive(enemy);
            if (enemy)
            {
                UpdateBar(theirBar, enemy, dt);
                theirPortrait.sprite = enemy.def.portrait;
                theirName.text = enemy.DisplayName.ToUpperInvariant() + (enemy.isBot ? " <size=60%>BOT</size>" : "");
            }

            // timer and score
            var r = rounds;
            float t = r.phase == Phase.Fight || r.phase == Phase.RoundOver ? r.timeLeft : cfg.roundTime;
            int secs = Mathf.CeilToInt(Mathf.Max(0f, t));
            timer.text = $"{secs / 60}:{secs % 60:00}";
            timer.color = r.phase == Phase.Fight && t < 10f ? Enemy : Color.white;
            roundLabel.text = r.round > 0 ? $"ROUND {r.round}" : "WARMUP";
            scoreMe.text = r.Wins(me.id).ToString();
            scoreThem.text = enemy ? r.Wins(enemy.id).ToString() : "0";

            Banners(enemy, dt);

            // hitmarker, damage flash
            hitmarkerT += dt; flashT += dt;
            float hm = Mathf.Clamp01(1f - hitmarkerT / 0.28f);
            hitmarker.color = (hitKo ? Enemy : Color.white) * new Color(1, 1, 1, hm);
            hitmarker.rectTransform.localScale = Vector3.one * (1f + 0.35f * hm);
            flash.color = new Color(1f, 0.1f, 0.1f, 0.12f * Mathf.Clamp01(1f - flashT / 0.3f));
            crosshair.enabled = me.Alive;

            // wing stamina
            bool showStamina = me.motor && me.Species == Species.Charizard && me.motor.Fatigue < 0.999f;
            staminaBack.gameObject.SetActive(showStamina);
            if (showStamina) { staminaFill.fillAmount = me.motor.Fatigue; staminaFill.color = me.motor.Fatigue < 0.25f ? Enemy : You; }

            clickPanel.SetActive(Cursor.lockState != CursorLockMode.Locked);

            Numbers(dt);
        }

        void UpdateBar(Bar b, Fighter f, float dt)
        {
            float v = f.Hp01;
            b.fill.fillAmount = v;
            b.shown = v > b.shown ? v : Mathf.MoveTowards(b.shown, v, dt * 0.6f);
            b.chip.fillAmount = b.shown;
            b.value.text = Mathf.CeilToInt(f.hp).ToString();
        }

        void Banners(Fighter enemy, float dt)
        {
            var r = rounds;
            bannerT += dt;
            if (r.phase != lastPhase || r.round != lastRound)
            {
                bannerT = 0f;
                switch (r.phase)
                {
                    case Phase.Countdown:
                        if (me.def.cry) Sfx.Ui(me.def.cry, 0.5f);
                        if (me.anim) me.anim.PlayAction(PokemonAnim.Roar, 1.4f);
                        break;
                    case Phase.Fight: Sfx.Ui(cfg.fightSound, 0.9f); break;
                    case Phase.RoundOver:
                        bool won = r.winner == me.id;
                        Sfx.Ui(r.reason == EndReason.KO ? cfg.koSound : null, 0.8f);
                        Sfx.Ui(won ? cfg.winSound : r.winner == -1 ? null : cfg.loseSound, 0.9f);
                        if (won && me.def.cry) Sfx.Ui(me.def.cry, 0.6f);
                        break;
                }
                lastPhase = r.phase; lastRound = r.round; lastCount = -1;
            }

            string big = "", sub = "";
            Color c = Color.white;
            switch (r.phase)
            {
                case Phase.Waiting:
                    big = ""; sub = "WAITING FOR AN OPPONENT";
                    break;
                case Phase.Countdown:
                    int n = Mathf.CeilToInt(r.phaseLeft);
                    if (n != lastCount) { lastCount = n; if (n > 0) Sfx.Ui(cfg.countdownSound, 0.6f); }
                    big = n > 0 ? n.ToString() : ""; sub = $"ROUND {r.round}";
                    break;
                case Phase.Fight:
                    if (bannerT < 0.9f) { big = "FIGHT!"; c = new Color(1f, 0.85f, 0.3f); }
                    break;
                case Phase.RoundOver:
                    bool won = r.winner == me.id;
                    big = r.winner == -1 ? "DRAW" : won ? "ROUND WON" : "ROUND LOST";
                    c = r.winner == -1 ? Color.white : won ? You : Enemy;
                    string how = r.reason == EndReason.KO ? (won ? $"{(enemy ? enemy.DisplayName : "Opponent")} fainted!" : $"{me.DisplayName} fainted!") : "Time! Higher HP wins";
                    sub = $"{how}    {r.Wins(me.id)} - {(enemy ? r.Wins(enemy.id) : 0)}";
                    break;
            }
            banner.text = big; banner.color = c; bannerSub.text = sub;
            // each new countdown number (and every banner) pops in slightly larger
            float frac = r.phaseLeft - Mathf.Floor(r.phaseLeft);
            float pop = r.phase == Phase.Countdown ? 1f + 0.3f * Mathf.Clamp01((frac - 0.75f) / 0.25f) : 1f + 0.2f * Mathf.Clamp01(1f - bannerT / 0.2f);
            banner.rectTransform.localScale = Vector3.one * pop;
        }

        void SpawnNumber(Vector3 world, float dmg, Color c)
        {
            var t = numberPool.Count > 0 ? numberPool.Pop() : Text("Damage", root, "", 46, TextAlignmentOptions.Center);
            t.gameObject.SetActive(true);
            t.text = Mathf.RoundToInt(dmg).ToString();
            t.outlineWidth = 0.2f; t.outlineColor = new Color32(0, 0, 0, 200);
            t.rectTransform.sizeDelta = new Vector2(160, 60);
            numbers.Add((t, world + Random.insideUnitSphere * 0.25f, 0f, c));
        }

        void Numbers(float dt)
        {
            if (!cam) cam = Camera.main;
            for (int i = numbers.Count - 1; i >= 0; i--)
            {
                var (t, w, age, c) = numbers[i];
                age += dt;
                if (age > 0.9f || !cam) { t.gameObject.SetActive(false); numberPool.Push(t); numbers.RemoveAt(i); continue; }
                numbers[i] = (t, w, age, c);
                var sp = cam.WorldToScreenPoint(w + Vector3.up * age * 0.9f);
                if (sp.z < 0f) { t.enabled = false; continue; }
                t.enabled = true;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var local);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                t.rectTransform.anchoredPosition = local;
                t.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(1.4f - age / 0.9f * 1.4f + 0.4f));
                t.rectTransform.localScale = Vector3.one * (1f + 0.5f * Mathf.Clamp01(1f - age / 0.12f));
            }
        }
    }
}
