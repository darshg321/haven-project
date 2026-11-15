using System.Collections.Generic;
using UnityEngine;

namespace HavenDuel
{
    public class Projectile : MonoBehaviour
    {
        public static readonly List<Projectile> All = new();

        public Fighter Owner { get; private set; }
        public Vector3 Velocity { get; private set; }

        CombatConfig.MoveDef move;
        bool real, done;
        float age;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Init(Fighter owner, CombatConfig.MoveDef move, Vector3 velocity, bool real)
        {
            Owner = owner;
            this.move = move;
            Velocity = velocity;
            this.real = real;
        }

        void Update()
        {
            if (done) return;
            age += Time.deltaTime;
            if (age > move.life)
            {
                Finish(transform.position, Vector3.up, null);
                return;
            }

            Velocity += Vector3.down * move.gravity * Time.deltaTime;
            if (move.homing > 0f) Home();
            var a = transform.position;
            var step = Velocity * Time.deltaTime;

            Fighter target = null;
            float nearest = 2f;
            Vector3 hitPoint = a + step;
            foreach (var f in Fighter.All)
            {
                if (f == Owner || !f.Alive) continue;
                if (f.SegmentHit(a, a + step, move.radius, out var p, out var t) && t < nearest)
                {
                    nearest = t;
                    target = f;
                    hitPoint = p;
                }
            }

            float len = step.magnitude;
            if (len > 0f && Physics.SphereCast(a, move.radius, step / len, out var wall, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && wall.distance / len < nearest)
                Finish(wall.point, wall.normal, null);
            else if (target)
                Finish(hitPoint, -Velocity.normalized, target);
            else
            {
                transform.position = a + step;
                transform.rotation = Quaternion.LookRotation(Velocity);
            }
        }

        void Home()
        {
            Fighter nearest = null;
            foreach (var f in Fighter.All)
                if (f != Owner && f.Alive && (!nearest || (f.Chest - transform.position).sqrMagnitude < (nearest.Chest - transform.position).sqrMagnitude))
                    nearest = f;
            if (!nearest) return;
            var want = (nearest.Chest - transform.position).normalized * Velocity.magnitude;
            Velocity = Vector3.RotateTowards(Velocity, want, move.homing * Mathf.Deg2Rad * Time.deltaTime, 0f);
        }

        void Finish(Vector3 point, Vector3 normal, Fighter target)
        {
            done = true;
            All.Remove(this);
            transform.position = point;
            if (move.hitFx) Destroy(Instantiate(move.hitFx, point, Quaternion.LookRotation(normal)), 2f);
            if (target) Sfx.At(move.hitSound, point, 0.7f);
            else Sfx.At(move.wallSound, point, 0.3f);
            if (target && real) target.ReportHit(Owner, move.damage, point);

            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            {
                bool world = ps.main.simulationSpace == ParticleSystemSimulationSpace.World;
                ps.Stop(false, world ? ParticleSystemStopBehavior.StopEmitting : ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            Destroy(gameObject, 1.2f);
        }
    }
}
