using System.Collections.Generic;
using UnityEngine;

namespace HavenDuel.Combat
{
    /// A skillshot in flight: travel time, optional drop, no aim assist (GAME.md pillar 3).
    /// Real projectiles belong to an authority fighter and report hits; ghost ones (another
    /// player's shot, sent over the network) only show and stop on impact.
    public class Projectile : MonoBehaviour
    {
        public static readonly List<Projectile> All = new();

        public Fighter Owner { get; private set; }
        public Vector3 Velocity => vel;
        public CombatConfig.MoveDef Move => move;
        public bool Real => real;

        CombatConfig.MoveDef move;
        Vector3 vel;
        bool real, done;
        float age;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Init(Fighter owner, CombatConfig.MoveDef move, Vector3 velocity, bool real)
        {
            Owner = owner; this.move = move; vel = velocity; this.real = real;
        }

        void Update()
        {
            if (done) return;
            float dt = Time.deltaTime;
            age += dt;
            if (age > move.life) { Finish(transform.position, Vector3.up, null, false); return; }
            vel += Vector3.down * move.gravity * dt;
            var a = transform.position;
            var b = a + vel * dt;

            // nearest fighter along the step (not the thrower)
            Fighter hitF = null; float bestT = 2f; Vector3 hitP = b;
            foreach (var f in Fighter.All)
            {
                if (f == Owner || !f.Alive) continue;
                if (f.SegmentHit(a, b, move.radius, out var p, out var t) && t < bestT) { bestT = t; hitF = f; hitP = p; }
            }
            // map geometry
            float len = (b - a).magnitude;
            if (len > 1e-5f && Physics.SphereCast(a, move.radius, (b - a) / len, out var wall, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && wall.distance / len < bestT)
            {
                Finish(wall.point, wall.normal, null, false);
                return;
            }
            if (hitF)
            {
                Finish(hitP, -vel.normalized, hitF, true);
                return;
            }
            transform.position = b;
            if (vel.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(vel);
        }

        void Finish(Vector3 point, Vector3 normal, Fighter target, bool onFighter)
        {
            done = true;
            All.Remove(this);   // gone for gameplay (bot dodging) while the trail fades
            transform.position = point;
            if (move.hitFx) Destroy(Instantiate(move.hitFx, point, Quaternion.LookRotation(normal)), 2f);
            var clip = onFighter ? move.hitSound : move.wallSound;
            if (clip) Sfx.At(clip, point, onFighter ? 0.9f : 0.5f);
            if (onFighter && real && target) target.ReportHit(Owner, move.damage, point);
            // let trails fade out instead of popping
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            {
                if (ps.main.simulationSpace == ParticleSystemSimulationSpace.World) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                else { ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); }
            }
            Destroy(gameObject, 1.2f);
        }
    }
}
