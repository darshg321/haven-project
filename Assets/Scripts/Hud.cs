using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HavenDuel
{
    public class Hud : MonoBehaviour
    {
        static readonly Color You = new(0.33f, 0.93f, 0.78f);
        static readonly Color Enemy = new(1f, 0.33f, 0.36f);
        static readonly Color PanelColor = new(0.04f, 0.05f, 0.06f, 0.55f);
        static readonly Color Gold = new(1f, 0.85f, 0.3f);

        class Bar
        {
            public GameObject root;
            public Image fill, chip, portrait;
            public TextMeshProUGUI name, value;
            public float shown = 1f;
        }

        CombatConfig cfg;
        Fighter me;
        System.Func<Fighter> getEnemy;
        Rounds rounds;
        Camera cam;

        RectTransform root;
        TextMeshProUGUI timer, roundLabel, scoreMe, scoreThem, banner, bannerSub, status, bigHp;
        Bar myBar, theirBar;
        Image crosshair, hitmarker, flash, stamina;
        GameObject clickToPlay;

        float sinceHit = 99f, sinceDamaged = 99f, sinceBanner;
        bool hitWasKo;
        Phase lastPhase;
        int lastRound = -1, lastCount = -1;

        class Slot
        {
            public Image icon, cooldown, ring;
            public Image[] pips;
        }

        static readonly string[] Keys = { "C", "Q", "E", "X" };
        Slot[] slots;

        class Number { public TextMeshProUGUI text; public Vector3 world; public float age; public Color color; }
        readonly List<Number> numbers = new();

        public void Bind(CombatConfig cfg, Fighter me, System.Func<Fighter> getEnemy, Rounds rounds)
        {
            this.cfg = cfg;
            this.me = me;
            this.getEnemy = getEnemy;
            this.rounds = rounds;
            cam = Camera.main;
            Build();
            myBar.portrait.sprite = me.def.portrait;
            myBar.name.text = me.DisplayName.ToUpper();
            Fighter.LandedHit += OnLandedHit;
            Fighter.Damaged += OnDamaged;
        }

        void OnDestroy()
        {
            Fighter.LandedHit -= OnLandedHit;
            Fighter.Damaged -= OnDamaged;
        }

        public void SetStatus(string s) => status.text = s;

        void OnLandedHit(Fighter shooter, Fighter target, float dmg, Vector3 point)
        {
            if (shooter != me) return;
            hitWasKo = target.hp <= dmg;
            sinceHit = 0f;
            Sfx.Ui(cfg.hitmarkerSound, 0.5f);
            var text = NewText(root, Mathf.RoundToInt(dmg).ToString(), 46, TextAlignmentOptions.Center);
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 200);
            numbers.Add(new Number { text = text, world = point + Random.insideUnitSphere * 0.25f, color = hitWasKo ? Gold : Color.white });
        }

        void OnDamaged(Fighter f, float dmg, Vector3 point)
        {
            if (f == me) sinceDamaged = 0f;
        }

        void Build()
        {
            var canvas = new GameObject("HUD").AddComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)canvas.transform;

            flash = NewImage(root, null, Color.clear);
            Stretch(flash.rectTransform);

            var top = NewImage(root, null, Color.clear).rectTransform;
            Place(top, new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(1400, 110));
            var timerBack = NewImage(top, cfg.panel, PanelColor).rectTransform;
            Place(timerBack, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(170, 92));
            timer = NewText(timerBack, "2:00", 70, TextAlignmentOptions.Center);
            Stretch(timer.rectTransform, 10);
            roundLabel = NewText(timerBack, "", 24, TextAlignmentOptions.Center);
            roundLabel.color = new Color(1f, 1f, 1f, 0.7f);
            Place(roundLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 2), new Vector2(170, 28));
            scoreMe = NewText(top, "0", 64, TextAlignmentOptions.Center);
            scoreMe.color = You;
            Place(scoreMe.rectTransform, new Vector2(0.5f, 1f), new Vector2(-125, -2), new Vector2(70, 90));
            scoreThem = NewText(top, "0", 64, TextAlignmentOptions.Center);
            scoreThem.color = Enemy;
            Place(scoreThem.rectTransform, new Vector2(0.5f, 1f), new Vector2(125, -2), new Vector2(70, 90));
            myBar = MakeBar(top, true);
            theirBar = MakeBar(top, false);

            crosshair = NewImage(root, cfg.crosshair, new Color(1f, 1f, 1f, 0.95f));
            Place(crosshair.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            hitmarker = NewImage(root, cfg.hitmarker, Color.clear);
            Place(hitmarker.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54));
            var staminaBack = NewImage(root, null, new Color(0f, 0f, 0f, 0.5f));
            Place(staminaBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 168), new Vector2(360, 8), new Vector2(0.5f, 0f));
            stamina = Filled(staminaBack.rectTransform, You, Image.FillMethod.Horizontal, 0);
            var wings = NewText(staminaBack.rectTransform, "WINGS", 18, TextAlignmentOptions.Left);
            wings.color = new Color(1f, 1f, 1f, 0.6f);
            Place(wings.rectTransform, new Vector2(0f, 1f), new Vector2(0, 14), new Vector2(120, 20), new Vector2(0f, 0f));

            banner = NewText(root, "", 150, TextAlignmentOptions.Center);
            banner.outlineWidth = 0.12f;
            banner.outlineColor = new Color32(0, 0, 0, 160);
            Place(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1600, 170));
            bannerSub = NewText(root, "", 44, TextAlignmentOptions.Center);
            Place(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 75), new Vector2(1400, 60));

            bigHp = NewText(root, "100", 84, TextAlignmentOptions.Left);
            Place(bigHp.rectTransform, Vector2.zero, new Vector2(48, 20), new Vector2(260, 100));
            var hint = NewText(root, ControlsHint(), 22, TextAlignmentOptions.Right);
            hint.color = new Color(1f, 1f, 1f, 0.6f);
            Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(-24, 20), new Vector2(900, 34));
            BuildAbilities();
            status = NewText(root, "", 24, TextAlignmentOptions.TopLeft);
            status.color = new Color(1f, 1f, 1f, 0.6f);
            Place(status.rectTransform, new Vector2(0f, 1f), new Vector2(20, -14), new Vector2(560, 60));

            var overlay = NewImage(root, null, new Color(0f, 0f, 0f, 0.45f));
            Stretch(overlay.rectTransform);
            Stretch(NewText(overlay.rectTransform, "CLICK TO PLAY", 72, TextAlignmentOptions.Center).rectTransform);
            clickToPlay = overlay.gameObject;
        }

        const float SlotSize = 112f, SlotGap = 128f, IconSize = 84f;

        // Drop a sprite into Assets/Resources/HudOverrides named after a move
        // (e.g. "Water Shuriken") to replace its icon without touching the roster.
        static Sprite IconFor(CombatConfig.MoveDef move) =>
            Resources.Load<Sprite>("HudOverrides/" + move.name) ?? move.icon;

        void BuildAbilities()
        {
            var moves = me.def.abilities;
            slots = new Slot[moves.Length];
            for (int i = 0; i < moves.Length; i++)
            {
                var slot = slots[i] = new Slot();
                var back = NewImage(root, Resources.Load<Sprite>("HudOverrides/slot") ?? cfg.panel, new Color(0.06f, 0.07f, 0.09f, 0.72f)).rectTransform;
                Place(back, new Vector2(0.5f, 0f), new Vector2((i - (moves.Length - 1) / 2f) * SlotGap, 58), new Vector2(SlotSize, SlotSize), new Vector2(0.5f, 0f));
                var edge = NewImage(back, null, new Color(1f, 1f, 1f, 0.18f));
                Place(edge.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(SlotSize, 3), new Vector2(0.5f, 0f));
                if (i == me.UltSlot)
                    slot.ring = Filled(back, Gold, Image.FillMethod.Radial360, (int)Image.Origin360.Top);
                slot.icon = NewImage(back, IconFor(moves[i]), Color.white);
                slot.icon.preserveAspect = true;
                Place(slot.icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(IconSize, IconSize));
                slot.cooldown = Filled(back, new Color(0f, 0f, 0f, 0.62f), Image.FillMethod.Vertical, (int)Image.OriginVertical.Top);
                var key = NewText(back, Keys[i], 32, TextAlignmentOptions.Center);
                Readable(key);
                Place(key.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, -22), new Vector2(60, 40), new Vector2(0.5f, 0.5f));
                var name = NewText(back, moves[i].name.ToUpper(), 24, TextAlignmentOptions.Center);
                Readable(name);
                name.characterSpacing = 2f;
                name.textWrappingMode = TextWrappingModes.NoWrap;
                Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 26), new Vector2(SlotGap, 30));

                slot.pips = new Image[moves[i].charges > 1 ? moves[i].charges : 0];
                for (int c = 0; c < slot.pips.Length; c++)
                {
                    slot.pips[c] = NewImage(back, null, You);
                    Place(slot.pips[c].rectTransform, new Vector2(1f, 1f), new Vector2(-8 - c * 12f, -8), new Vector2(8, 8), new Vector2(1f, 1f));
                    slot.pips[c].rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                }
            }
        }

        void UpdateAbilities()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                bool isUlt = i == me.UltSlot;
                float wait = isUlt ? 1f - me.ult : me.Cooldown01(i);
                slot.cooldown.fillAmount = wait;
                slot.icon.color = wait > 0f || !me.Alive ? new Color(1f, 1f, 1f, 0.4f) : Color.white;
                if (isUlt)
                {
                    slot.ring.fillAmount = me.ult;
                    slot.ring.color = me.ult >= 1f ? Gold : new Color(Gold.r, Gold.g, Gold.b, 0.45f);
                }
                for (int c = 0; c < slot.pips.Length; c++)
                    slot.pips[c].color = c < me.charges[i] ? You : new Color(1f, 1f, 1f, 0.25f);
            }
        }

        string ControlsHint()
        {
            string attack = "<b>LMB</b> " + me.def.basic.name.ToUpper();
            if (me.Species == Species.Charizard)
                return attack + "    <b>SPACE SPACE</b> fly, hold to climb    <b>CTRL</b> dive    <b>SHIFT</b> run";
            return attack + "    <b>SPACE</b> jump    <b>SHIFT</b> sprint";
        }

        Bar MakeBar(RectTransform parent, bool left)
        {
            float side = left ? -1f : 1f;
            float near = left ? 1f : 0f;
            var bar = new Bar();
            var back = NewImage(parent, cfg.panel, PanelColor).rectTransform;
            Place(back, new Vector2(0.5f, 1f), new Vector2(side * 210, 0), new Vector2(470, 92), new Vector2(near, 1f));
            bar.root = back.gameObject;

            bar.portrait = NewImage(back, null, Color.white);
            bar.portrait.preserveAspect = true;
            Place(bar.portrait.rectTransform, new Vector2(near, 0.5f), new Vector2(side * 8, 0), new Vector2(84, 84));
            bar.name = NewText(back, "", 34, left ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
            Place(bar.name.rectTransform, new Vector2(near, 1f), new Vector2(side * 100, -6), new Vector2(250, 40));
            bar.value = NewText(back, "", 34, left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
            Place(bar.value.rectTransform, new Vector2(1f - near, 1f), new Vector2(side * -14, -6), new Vector2(120, 40));

            var track = NewImage(back, null, new Color(0f, 0f, 0f, 0.55f)).rectTransform;
            track.anchorMin = Vector2.zero;
            track.anchorMax = new Vector2(1f, 0f);
            track.pivot = new Vector2(0.5f, 0f);
            track.offsetMin = new Vector2(left ? 14 : 100, 14);
            track.offsetMax = new Vector2(left ? -100 : -14, 36);
            int origin = (int)(left ? Image.OriginHorizontal.Right : Image.OriginHorizontal.Left);
            bar.chip = Filled(track, new Color(1f, 1f, 1f, 0.85f), Image.FillMethod.Horizontal, origin);
            bar.fill = Filled(track, left ? You : Enemy, Image.FillMethod.Horizontal, origin);
            return bar;
        }

        // Solid white with a dark outline and drop shadow so it holds up over bright walls.
        static void Readable(TextMeshProUGUI t)
        {
            t.color = Color.white;
            t.fontStyle |= FontStyles.Bold;
            t.outlineWidth = 0.22f;
            t.outlineColor = new Color32(10, 12, 16, 230);
            var m = t.fontMaterial;
            if (m.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.7f));
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.8f);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.3f);
            }
        }

        Image NewImage(RectTransform parent, Sprite sprite, Color color)
        {
            var img = new GameObject("Image").AddComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        Image Filled(RectTransform parent, Color color, Image.FillMethod method, int origin)
        {
            var img = NewImage(parent, null, color);
            var white = Texture2D.whiteTexture;
            img.sprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), Vector2.one / 2);
            img.type = Image.Type.Filled;
            img.fillMethod = method;
            img.fillOrigin = origin;
            Stretch(img.rectTransform);
            return img;
        }

        TextMeshProUGUI NewText(RectTransform parent, string text, float size, TextAlignmentOptions align)
        {
            var t = new GameObject("Text").AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            t.font = cfg.font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot ?? anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        static void Stretch(RectTransform r, float bottom = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(0f, bottom);
            r.offsetMax = Vector2.zero;
        }

        void Update()
        {
            if (!me) return;
            float dt = Time.unscaledDeltaTime;
            var enemy = getEnemy();

            UpdateBar(myBar, me, dt);
            bigHp.text = Mathf.CeilToInt(me.hp).ToString();
            bigHp.color = Color.Lerp(Enemy, Color.white, me.Hp01 * 2f);
            theirBar.root.SetActive(enemy);
            if (enemy)
            {
                UpdateBar(theirBar, enemy, dt);
                theirBar.portrait.sprite = enemy.def.portrait;
                theirBar.name.text = enemy.DisplayName.ToUpper() + (enemy.isBot ? " <size=60%>BOT</size>" : "");
            }

            bool fighting = rounds.phase is Phase.Fight or Phase.RoundOver;
            int secs = Mathf.CeilToInt(fighting ? rounds.timeLeft : cfg.roundTime);
            timer.text = $"{secs / 60}:{secs % 60:00}";
            timer.color = rounds.phase == Phase.Fight && secs <= 10 ? Enemy : Color.white;
            roundLabel.text = rounds.round > 0 ? $"ROUND {rounds.round}" : "WARMUP";
            scoreMe.text = rounds.Wins(me.id).ToString();
            scoreThem.text = enemy ? rounds.Wins(enemy.id).ToString() : "0";

            UpdateBanner(enemy, dt);

            sinceHit += dt;
            sinceDamaged += dt;
            float hit = Mathf.Clamp01(1f - sinceHit / 0.28f);
            hitmarker.color = (hitWasKo ? Enemy : Color.white) * new Color(1f, 1f, 1f, hit);
            hitmarker.transform.localScale = Vector3.one * (1f + 0.35f * hit);
            flash.color = new Color(1f, 0.1f, 0.1f, 0.12f * Mathf.Clamp01(1f - sinceDamaged / 0.3f));
            crosshair.enabled = me.Alive;

            bool showStamina = me.Species == Species.Charizard && (me.motor.Flying || me.motor.Stamina < 0.999f);
            stamina.transform.parent.gameObject.SetActive(showStamina);
            stamina.fillAmount = me.motor.Stamina;
            stamina.color = Color.Lerp(Enemy, You, me.motor.Stamina * 3f);

            UpdateAbilities();
            clickToPlay.SetActive(Cursor.lockState != CursorLockMode.Locked);
            UpdateNumbers(dt);
        }

        void UpdateBar(Bar bar, Fighter f, float dt)
        {
            bar.fill.fillAmount = f.Hp01;
            bar.shown = f.Hp01 > bar.shown ? f.Hp01 : Mathf.MoveTowards(bar.shown, f.Hp01, dt * 0.6f);
            bar.chip.fillAmount = bar.shown;
            bar.value.text = Mathf.CeilToInt(f.hp).ToString();
        }

        void UpdateBanner(Fighter enemy, float dt)
        {
            sinceBanner += dt;
            bool won = rounds.winner == me.id;
            bool draw = rounds.winner == -1;
            if (rounds.phase != lastPhase || rounds.round != lastRound)
            {
                sinceBanner = 0f;
                lastPhase = rounds.phase;
                lastRound = rounds.round;
                lastCount = -1;
                if (rounds.phase == Phase.Countdown)
                {
                    Sfx.Ui(me.def.cry, 0.4f);
                    me.anim.PlayAction(PokemonAnim.Roar, 1.4f);
                }
                else if (rounds.phase == Phase.Fight)
                    Sfx.Ui(cfg.fightSound, 0.6f);
                else if (rounds.phase == Phase.RoundOver)
                {
                    if (rounds.reason == EndReason.KO) Sfx.Ui(cfg.koSound, 0.8f);
                    if (!draw) Sfx.Ui(won ? cfg.winSound : cfg.loseSound, 0.6f);
                    if (won) Sfx.Ui(me.def.cry, 0.6f);
                }
            }

            banner.text = "";
            banner.color = Color.white;
            bannerSub.text = "";
            float pop = 1f + 0.2f * Mathf.Clamp01(1f - sinceBanner / 0.2f);
            switch (rounds.phase)
            {
                case Phase.Waiting:
                    bannerSub.text = "WAITING FOR AN OPPONENT";
                    break;
                case Phase.Countdown:
                    int n = Mathf.CeilToInt(rounds.phaseLeft);
                    if (n != lastCount && n > 0) Sfx.Ui(cfg.countdownSound, 0.4f);
                    lastCount = n;
                    banner.text = n > 0 ? n.ToString() : "";
                    bannerSub.text = $"ROUND {rounds.round}";
                    float frac = rounds.phaseLeft % 1f;
                    pop = 1f + 0.3f * Mathf.Clamp01((frac - 0.75f) / 0.25f);
                    break;
                case Phase.Fight:
                    if (sinceBanner < 0.9f)
                    {
                        banner.text = "FIGHT!";
                        banner.color = Gold;
                    }
                    break;
                case Phase.RoundOver:
                    banner.text = draw ? "DRAW" : won ? "ROUND WON" : "ROUND LOST";
                    banner.color = draw ? Color.white : won ? You : Enemy;
                    string loser = won ? (enemy ? enemy.DisplayName : "Opponent") : me.DisplayName;
                    string how = rounds.reason == EndReason.KO ? $"{loser} fainted!" : "Time! Higher HP wins";
                    bannerSub.text = $"{how}    {rounds.Wins(me.id)} - {(enemy ? rounds.Wins(enemy.id) : 0)}";
                    break;
            }
            banner.transform.localScale = Vector3.one * pop;
        }

        void UpdateNumbers(float dt)
        {
            for (int i = numbers.Count - 1; i >= 0; i--)
            {
                var n = numbers[i];
                n.age += dt;
                if (n.age > 0.9f)
                {
                    Destroy(n.text.gameObject);
                    numbers.RemoveAt(i);
                    continue;
                }
                var screen = cam.WorldToScreenPoint(n.world + Vector3.up * n.age * 0.9f);
                n.text.enabled = screen.z > 0f;
                n.text.transform.position = screen;
                n.text.color = new Color(n.color.r, n.color.g, n.color.b, Mathf.Clamp01(1.8f - n.age / 0.9f * 1.4f));
                n.text.transform.localScale = Vector3.one * (1f + 0.5f * Mathf.Clamp01(1f - n.age / 0.12f));
            }
        }
    }
}
