using HavenDuel.Pokemon;
using UnityEngine;
using UnityEngine.AI;

namespace HavenDuel.Combat
{
    /// Rookie bot (GAME.md section 9): aims at where you are now (no leading), dodges on
    /// reaction, walks a NavMesh path to find you, strafes once it can see you.
    public class BotBrain : MonoBehaviour
    {
        public Fighter me;
        public Fighter target;
        [Header("Rookie")]
        [Tooltip("Max aim turn speed, degrees/s.")] public float turnSpeed = 220f;
        [Tooltip("Wobble added to the aim, degrees.")] public float aimNoise = 2.5f;
        [Tooltip("Fires only when the aim is this close to the target, degrees.")] public float fireCone = 5f;
        [Tooltip("Extra pause after each throw, seconds (random up to this).")] public float fireHesitation = 0.45f;
        [Tooltip("Reaction time before a dodge, seconds.")] public Vector2 reaction = new(0.22f, 0.4f);
        public float preferredRange = 12f;

        float yaw, pitch, nextShot;
        float strafeDir = 1f, strafeTimer;
        NavMeshPath path;
        Vector3[] corners = new Vector3[0];
        int corner;
        float repath, stuckTime;
        float dodgeAt = -1f, dodgeUntil;
        Vector2 dodgeMove;
        bool dodgeJump;
        Projectile lastDodged;
        float seed;

        void Awake() => path = new NavMeshPath();

        void Start()
        {
            seed = Random.value * 100f;
            yaw = transform.eulerAngles.y;
        }

        public MotorInput Think(float dt, out bool fire)
        {
            fire = false;
            var input = new MotorInput { yaw = yaw };
            if (!me || !me.Alive || !target) return input;

            var eye = me.aim.position;
            var aimAt = target.Chest;
            var to = aimAt - eye;
            float dist = to.magnitude;
            bool sees = target.Alive && !Physics.Raycast(eye, to / dist, dist - 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            // --- movement ---
            Vector3 wantDir;   // world-space direction to move in
            bool run = false;
            if (!sees || dist > preferredRange * 1.8f)
            {
                wantDir = PathDir(target.transform.position, dt);
                run = true;
            }
            else
            {
                strafeTimer -= dt;
                if (strafeTimer <= 0f) { strafeDir = Random.value < 0.5f ? -1f : 1f; strafeTimer = Random.Range(0.8f, 2.2f); }
                var fwd = new Vector3(to.x, 0f, to.z).normalized;
                var side = Vector3.Cross(Vector3.up, fwd) * strafeDir;
                if (!Walkable(side)) { strafeDir = -strafeDir; side = -side; strafeTimer = Random.Range(0.8f, 2.2f); }
                float approach = dist > preferredRange + 3f ? 0.6f : dist < preferredRange - 5f ? -0.7f : 0f;
                wantDir = side + fwd * approach;
                if (approach < 0f && !Walkable(-fwd)) wantDir = side;
            }

            // dodge on reaction: an enemy shot will pass close in the next half second
            WatchShots();
            if (dodgeAt > 0f && Time.time >= dodgeAt) { dodgeAt = -1f; dodgeUntil = Time.time + 0.4f; }
            bool dodging = Time.time < dodgeUntil;

            // --- aim: look at the target when visible, else where we walk ---
            float wantYaw, wantPitch;
            if (sees)
            {
                float n = aimNoise * 2f;
                var flat = new Vector2(to.x, to.z).magnitude;
                wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + (Mathf.PerlinNoise(seed, Time.time * 0.7f) - 0.5f) * n;
                wantPitch = -Mathf.Atan2(to.y, flat) * Mathf.Rad2Deg + (Mathf.PerlinNoise(seed + 7f, Time.time * 0.7f) - 0.5f) * n;
            }
            else
            {
                wantYaw = wantDir.sqrMagnitude > 0.01f ? Mathf.Atan2(wantDir.x, wantDir.z) * Mathf.Rad2Deg : yaw;
                wantPitch = 0f;
            }
            yaw = Mathf.MoveTowardsAngle(yaw, wantYaw, turnSpeed * dt);
            pitch = Mathf.MoveTowards(pitch, wantPitch, turnSpeed * 0.6f * dt);
            me.aim.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // world direction -> local stick
            var local = Quaternion.Euler(0f, -yaw, 0f) * wantDir;
            var move = new Vector2(local.x, local.z);
            if (dodging) move = dodgeMove;
            input.move = Vector2.ClampMagnitude(move, 1f);
            input.yaw = yaw;
            input.run = run || dodging;
            input.jump = dodging && dodgeJump && Time.time > dodgeUntil - 0.38f;
            dodgeJump &= !input.jump;

            // stuck on something: hop and change strafe side
            if (me.motor && input.move.sqrMagnitude > 0.25f && new Vector2(me.motor.Velocity.x, me.motor.Velocity.z).magnitude < 0.4f)
            {
                stuckTime += dt;
                if (stuckTime > 0.5f) { input.jump = true; strafeDir = -strafeDir; repath = 0f; stuckTime = 0f; }
            }
            else stuckTime = 0f;

            // --- fire ---
            if (sees && Time.time >= nextShot && me.CanFire)
            {
                float err = Vector3.Angle(me.aim.forward, to);
                if (err < fireCone) { fire = true; nextShot = Time.time + me.def.basic.cooldown + Random.Range(0.05f, fireHesitation); }
            }
            return input;
        }

        bool Walkable(Vector3 dir)
        {
            var from = transform.position;
            if (!NavMesh.SamplePosition(from, out var h, 2f, NavMesh.AllAreas)) return true;
            return !NavMesh.Raycast(h.position, h.position + dir.normalized * 1.8f, out _, NavMesh.AllAreas);
        }

        Vector3 PathDir(Vector3 goal, float dt)
        {
            repath -= dt;
            if (repath <= 0f)
            {
                repath = 0.5f;
                if (NavMesh.SamplePosition(transform.position, out var a, 3f, NavMesh.AllAreas)
                    && NavMesh.SamplePosition(goal, out var b, 4f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)
                    && path.status != NavMeshPathStatus.PathInvalid)
                { corners = path.corners; corner = corners.Length > 1 ? 1 : 0; }
                else corners = new Vector3[0];
            }
            if (corners.Length == 0) { var d = goal - transform.position; d.y = 0f; return d.normalized; }
            while (corner < corners.Length - 1 && Flat(corners[corner] - transform.position).magnitude < 0.7f) corner++;
            return Flat(corners[corner] - transform.position).normalized;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void WatchShots()
        {
            if (dodgeAt > 0f || Time.time < dodgeUntil) return;
            var chest = me.Chest;
            foreach (var p in Projectile.All)
            {
                if (!p || p.Owner == me || p == lastDodged) continue;
                var v = p.Velocity; float sp = v.magnitude;
                if (sp < 1f) continue;
                var rel = chest - p.transform.position;
                float t = Vector3.Dot(rel, v) / (sp * sp);          // time to closest approach
                if (t < 0f || t > 0.7f) continue;
                var miss = rel - v * t;
                if (miss.magnitude > me.def.hitRadius + 0.7f) continue;
                lastDodged = p;
                dodgeAt = Time.time + Random.Range(reaction.x, reaction.y);
                // step sideways, away from the shot's line
                var side = Vector3.Cross(Vector3.up, v.normalized);
                if (Vector3.Dot(side, -miss) < 0f) side = -side;
                if (!Walkable(side)) side = -side;
                var local = Quaternion.Euler(0f, -yaw, 0f) * side;
                dodgeMove = new Vector2(local.x, local.z).normalized;
                dodgeJump = me.Species == Species.Greninja ? Random.value < 0.5f : Random.value < 0.2f;
                return;
            }
        }
    }
}
