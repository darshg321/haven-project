using System;
using System.Collections.Generic;
using HavenDuel.Pokemon;
using UnityEngine;

namespace HavenDuel.Combat
{
    /// One Pokemon in the fight: HP, hurtbox and its basic move.
    /// Authority = this machine owns its HP (the local player, or a bot). Remote players are
    /// proxies whose HP arrives over the network.
    public class Fighter : MonoBehaviour
    {
        public static readonly List<Fighter> All = new();
        public const int Layer = 2; // Ignore Raycast: map-only raycasts skip fighters, hurtboxes are tested by hand

        /// Hits only count while a round is being fought (not in the countdown or after a KO).
        public static bool CombatLive = true;

        public int id;
        public bool authority;
        public bool isBot;
        public CombatConfig.FighterDef def;
        public float hp;
        public PokemonAnimator anim;
        public PokemonMotor motor;
        /// Where shots come from and where they aim (camera for the player, eyes for the bot).
        public Transform aim;

        public float MaxHp => def.maxHp;
        public float Hp01 => Mathf.Clamp01(hp / def.maxHp);
        public bool Alive => hp > 0f;
        public Species Species => def.species;
        public float Height => def.height;
        public Vector3 Chest => transform.position + Vector3.up * def.height * 0.6f;
        public string DisplayName => def.displayName;

        float nextFire;
        bool fainted;

        /// (fighter, damage, hit point, attacker): raised when this fighter loses HP.
        public static event Action<Fighter, float, Vector3, Fighter> Damaged;
        /// (shooter, target, damage, point): raised on the shooter's machine when its shot connects.
        public static event Action<Fighter, Fighter, float, Vector3> LandedHit;
        /// Shot fired: (shooter, move, origin, direction). LanGame forwards authority shots to the network.
        public static event Action<Fighter, CombatConfig.MoveDef, Vector3, Vector3> Fired;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Init(int id, bool authority, CombatConfig.FighterDef def)
        {
            this.id = id; this.authority = authority; this.def = def;
            hp = def.maxHp;
            anim = GetComponent<PokemonAnimator>();
            motor = GetComponent<PokemonMotor>();
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
        }

        public bool CanFire => Alive && Time.time >= nextFire && (motor == null || !motor.locked);

        /// Throw the basic move from aim toward whatever the aim ray points at.
        public bool TryFire()
        {
            if (!CanFire || !aim) return false;
            var move = def.basic;
            nextFire = Time.time + move.cooldown;
            var origin = aim.position + aim.forward * 0.45f + aim.right * 0.18f - aim.up * 0.12f;
            var target = AimPoint(aim.position, aim.forward, 150f);
            var dir = (target - origin).normalized;
            if (Vector3.Dot(dir, aim.forward) < 0.5f) dir = aim.forward; // target almost on top of us
            Launch(move, origin, dir, true);
            Fired?.Invoke(this, move, origin, dir);
            return true;
        }

        /// Spawns the projectile. Real ones (from an authority fighter) deal damage; ghost ones only show.
        public Projectile Launch(CombatConfig.MoveDef move, Vector3 origin, Vector3 dir, bool real)
        {
            if (anim) anim.PlayAction(motor && motor.Flying ? PokemonAnim.FlyAttack : PokemonAnim.Attack, move.animSpeed);
            if (move.castSound) Sfx.At(move.castSound, origin, move.castVolume, authority && !isBot ? 0f : 1f);
            var go = move.projectile ? Instantiate(move.projectile, origin, Quaternion.LookRotation(dir)) : new GameObject(move.name);
            go.transform.position = origin;
            go.transform.localScale *= move.visualScale;
            var p = go.GetComponent<Projectile>() ?? go.AddComponent<Projectile>();
            p.Init(this, move, dir * move.speed, real);
            return p;
        }

        /// First thing along the ray: map geometry or another fighter's hurtbox.
        public Vector3 AimPoint(Vector3 from, Vector3 dir, float range)
        {
            float best = range;
            if (Physics.Raycast(from, dir, out var hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) best = hit.distance;
            foreach (var f in All)
            {
                if (f == this || !f.Alive) continue;
                if (f.SegmentHit(from, from + dir * best, 0f, out var p, out _)) best = Mathf.Min(best, Vector3.Distance(from, p));
            }
            return from + dir * best;
        }

        /// Swept sphere (a to b, radius r) against this fighter's capsule hurtbox.
        public bool SegmentHit(Vector3 a, Vector3 b, float r, out Vector3 point, out float t)
        {
            float rad = def.hitRadius;
            var p0 = transform.position + Vector3.up * rad;
            var p1 = transform.position + Vector3.up * Mathf.Max(rad, def.height - rad);
            ClosestPoints(a, b, p0, p1, out t, out var onSeg, out var onAxis);
            point = onSeg;
            float reach = rad + r;
            return (onSeg - onAxis).sqrMagnitude <= reach * reach;
        }

        static void ClosestPoints(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float s, out Vector3 c1, out Vector3 c2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r), t;
            if (a < 1e-8f && e < 1e-8f) { s = t = 0f; }
            else if (a < 1e-8f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e < 1e-8f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                    s = denom > 1e-8f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            c1 = p1 + d1 * s; c2 = p2 + d2 * t;
        }

        /// Called on the shooter's machine when its real projectile touches this fighter.
        public void ReportHit(Fighter shooter, float dmg, Vector3 point)
        {
            if (!CombatLive) return;
            LandedHit?.Invoke(shooter, this, dmg, point);
            if (authority) TakeDamage(dmg, point, shooter);
        }

        /// Lose HP. Only the machine that owns this fighter calls it.
        public void TakeDamage(float dmg, Vector3 point, Fighter from)
        {
            if (!Alive || dmg <= 0f) return;
            hp = Mathf.Max(0f, hp - dmg);
            Damaged?.Invoke(this, dmg, point, from);
            if (!Alive) Faint();
            else if (anim && !anim.IsPlayingAction) anim.PlayAction(motor && motor.Flying ? PokemonAnim.FlyHit : PokemonAnim.Hit, 1.5f);
        }

        /// Set from the network for remote fighters.
        public void SetHp(float value)
        {
            if (value < hp - 0.01f && anim && value > 0f && !anim.IsPlayingAction) anim.PlayAction(PokemonAnim.Hit, 1.5f);
            hp = value;
            if (!Alive && !fainted) Faint();
            if (Alive && fainted) Revive();
        }

        void Faint()
        {
            fainted = true;
            if (motor) motor.locked = true;
            if (anim) { anim.PlayAction(PokemonAnim.Faint); anim.HoldAfterAction(PokemonAnim.FaintLoop); }
        }

        /// Back to full HP for a new round.
        public void Revive()
        {
            hp = def.maxHp; fainted = false;
            if (anim) { anim.HoldAfterAction(null); anim.Play(PokemonAnim.BattleIdle, 0.1f); }
        }
    }
}
