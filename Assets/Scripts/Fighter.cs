using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HavenDuel
{
    public class Fighter : MonoBehaviour
    {
        public const int Basic = -1;
        public const int Layer = 2;
        public static readonly List<Fighter> All = new();
        public static bool CombatLive = true;

        public static event Action<Fighter, float, Vector3> Damaged;
        public static event Action<Fighter, Fighter, float, Vector3> LandedHit;
        public static event Action<Fighter, int, Vector3, Vector3> Fired;

        public int id;
        public bool authority, isBot;
        public CombatConfig.FighterDef def;
        public float hp, ult;
        public int[] charges;
        public PokemonAnimator anim;
        public Motor motor;
        public Transform aim;

        public float MaxHp => def.maxHp;
        public float Hp01 => Mathf.Clamp01(hp / def.maxHp);
        public bool Alive => hp > 0f;
        public Species Species => def.species;
        public string DisplayName => def.displayName;
        public Vector3 Chest => transform.position + Vector3.up * def.height * 0.6f;
        public bool CanFire => Alive && Time.time >= nextFire && (!motor || !motor.locked);
        public bool Recharging => Time.time < rechargeUntil;
        public int UltSlot => def.abilities.Length - 1;

        float nextFire, rechargeUntil;
        float[] rechargeAt;
        bool fainted;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Init(int id, bool authority, CombatConfig.FighterDef def)
        {
            this.id = id;
            this.authority = authority;
            this.def = def;
            anim = GetComponent<PokemonAnimator>();
            motor = GetComponent<Motor>();
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            Revive();
        }

        void Update()
        {
            for (int i = 0; i < UltSlot; i++)
            {
                if (charges[i] >= Move(i).charges || Time.time < rechargeAt[i]) continue;
                charges[i]++;
                rechargeAt[i] = Time.time + Move(i).cooldown;
            }
        }

        public CombatConfig.MoveDef Move(int slot) => slot == Basic ? def.basic : def.abilities[slot];

        public float Cooldown01(int slot) =>
            charges[slot] > 0 ? 0f : Mathf.Clamp01((rechargeAt[slot] - Time.time) / Move(slot).cooldown);

        public void TryFire()
        {
            if (!CanFire) return;
            nextFire = Time.time + def.basic.cooldown;
            Use(Basic);
        }

        public void TryAbility(int slot)
        {
            if (!Alive || motor.locked || slot >= def.abilities.Length) return;
            if (slot == UltSlot)
            {
                if (ult < 1f) return;
                ult = 0f;
            }
            else
            {
                if (charges[slot] == 0) return;
                if (charges[slot] == Move(slot).charges) rechargeAt[slot] = Time.time + Move(slot).cooldown;
                charges[slot]--;
            }
            Use(slot);
        }

        void Use(int slot)
        {
            if (Move(slot).stream > 0f)
            {
                StartCoroutine(Stream(slot));
                return;
            }
            rechargeUntil = Time.time + Move(slot).recharge;
            Fire(slot);
        }

        // A jet of water: keeps shooting small fast shots along wherever you aim for the stream's duration.
        IEnumerator Stream(int slot)
        {
            var move = Move(slot);
            float end = Time.time + move.stream;
            rechargeUntil = end + move.recharge;
            while (Time.time < end && Alive)
            {
                Fire(slot);
                yield return new WaitForSeconds(move.streamInterval);
            }
        }

        void Fire(int slot)
        {
            var origin = aim.position + aim.forward * 0.45f + aim.right * 0.18f - aim.up * 0.12f;
            var dir = (AimPoint(aim.position, aim.forward, 150f) - origin).normalized;
            if (Vector3.Dot(dir, aim.forward) < 0.5f) dir = aim.forward;
            Launch(slot, origin, dir, true);
            Fired?.Invoke(this, slot, origin, dir);
        }

        float streamCastAt = -99f;

        public void Launch(int slot, Vector3 origin, Vector3 dir, bool real)
        {
            var move = Move(slot);
            // A stream fires every few frames; only the start of it animates and plays the sound.
            bool start = move.stream <= 0f || Time.time - streamCastAt > move.stream;
            if (start)
            {
                if (move.stream > 0f) streamCastAt = Time.time;
                if (anim) anim.PlayAction(motor && motor.Flying ? PokemonAnim.FlyAttack : PokemonAnim.Attack, move.animSpeed);
                Sfx.At(move.castSound, origin, move.castVolume, authority && !isBot ? 0f : 1f);
            }
            if (move.clones > 0)
            {
                Decoy.Spawn(transform.GetChild(0), move.clones);
                return;
            }

            var up = Vector3.Cross(dir, Vector3.Cross(Vector3.up, dir)).normalized;
            for (int i = 0; i < move.count; i++)
            {
                var d = Quaternion.AngleAxis((i - (move.count - 1) / 2f) * move.spread, up) * dir;
                var go = Instantiate(move.projectile, origin, Quaternion.LookRotation(d));
                go.transform.localScale *= move.visualScale;
                go.GetComponent<Projectile>().Init(this, move, d * move.speed, real);
            }
        }

        public Vector3 AimPoint(Vector3 from, Vector3 dir, float range)
        {
            float dist = range;
            if (Physics.Raycast(from, dir, out var hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                dist = hit.distance;
            foreach (var f in All)
            {
                if (f != this && f.Alive && f.SegmentHit(from, from + dir * dist, 0f, out var p, out _))
                    dist = Mathf.Min(dist, Vector3.Distance(from, p));
            }
            return from + dir * dist;
        }

        public bool SegmentHit(Vector3 a, Vector3 b, float r, out Vector3 point, out float t)
        {
            float rad = def.hitRadius;
            var bottom = transform.position + Vector3.up * rad;
            var top = transform.position + Vector3.up * (def.height - rad);
            ClosestPoints(a, b, bottom, top, out t, out point, out var onAxis);
            return (point - onAxis).sqrMagnitude <= (rad + r) * (rad + r);
        }

        // Real-Time Collision Detection 5.1.9: closest points between two segments
        static void ClosestPoints(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float s, out Vector3 c1, out Vector3 c2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float c = Vector3.Dot(d1, r), b = Vector3.Dot(d1, d2);
            float t;
            if (a < 1e-8f)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float denom = a * e - b * b;
                s = denom > 1e-8f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                t = (b * s + f) / e;
                if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
        }

        public void ReportHit(Fighter shooter, float dmg, Vector3 point)
        {
            if (!CombatLive) return;
            shooter.ult = Mathf.Min(1f, shooter.ult + dmg / shooter.def.ultCost);
            LandedHit?.Invoke(shooter, this, dmg, point);
            if (authority) TakeDamage(dmg, point);
        }

        public void TakeDamage(float dmg, Vector3 point)
        {
            if (!Alive) return;
            hp = Mathf.Max(0f, hp - dmg);
            Damaged?.Invoke(this, dmg, point);
            if (!Alive) Faint();
            else if (!anim.IsPlayingAction) anim.PlayAction(motor && motor.Flying ? PokemonAnim.FlyHit : PokemonAnim.Hit, 1.5f);
        }

        public void SetHp(float value)
        {
            if (value < hp && value > 0f && !anim.IsPlayingAction) anim.PlayAction(PokemonAnim.Hit, 1.5f);
            hp = value;
            if (!Alive && !fainted) Faint();
            if (Alive && fainted) Revive();
        }

        void Faint()
        {
            fainted = true;
            if (motor) motor.locked = true;
            anim.PlayAction(PokemonAnim.Faint);
            anim.HoldAfterAction(PokemonAnim.FaintLoop);
        }

        public void Revive()
        {
            hp = def.maxHp;
            fainted = false;
            charges = Array.ConvertAll(def.abilities, m => m.charges);
            rechargeAt = new float[def.abilities.Length];
            anim.HoldAfterAction(null);
            anim.Play(PokemonAnim.BattleIdle, 0.1f);
        }
    }
}
