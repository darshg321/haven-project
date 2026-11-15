using UnityEngine;
using UnityEngine.AI;

namespace HavenDuel
{
    public class Bot : MonoBehaviour
    {
        public Fighter me, target;
        public float turnSpeed = 220f;
        public float aimNoise = 2.5f;
        public float fireCone = 5f;
        public float hesitation = 0.45f;
        public Vector2 reaction = new(0.22f, 0.4f);
        public float range = 12f;

        float yaw, pitch, nextShot, seed;
        float strafeDir = 1f, strafeTimer, stuckTime;
        NavMeshPath path;
        Vector3[] corners = new Vector3[0];
        int corner;
        float repath;
        float dodgeAt = -1f, dodgeUntil;
        Vector2 dodgeMove;
        bool dodgeJump;
        Projectile lastDodged;

        void Start()
        {
            path = new NavMeshPath();
            seed = Random.value * 100f;
            yaw = transform.eulerAngles.y;
        }

        public MotorInput Think(float dt, out bool fire)
        {
            fire = false;
            if (!me.Alive || !target) return new MotorInput { yaw = yaw };

            var eye = me.aim.position;
            var to = target.Chest - eye;
            float dist = to.magnitude;
            bool sees = target.Alive && !Physics.Raycast(eye, to / dist, dist - 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            Vector3 wantDir;
            bool run = false;
            if (!sees || dist > range * 1.8f)
            {
                wantDir = PathDir(target.transform.position, dt);
                run = true;
            }
            else
            {
                strafeTimer -= dt;
                if (strafeTimer <= 0f) NewStrafe(Random.value < 0.5f ? -1f : 1f);
                var forward = Flat(to).normalized;
                var side = Vector3.Cross(Vector3.up, forward) * strafeDir;
                if (!Walkable(side))
                {
                    NewStrafe(-strafeDir);
                    side = -side;
                }
                float approach = dist > range + 3f ? 0.6f : dist < range - 5f ? -0.7f : 0f;
                wantDir = side + forward * approach;
                if (approach < 0f && !Walkable(-forward)) wantDir = side;
            }

            WatchShots();
            if (dodgeAt > 0f && Time.time >= dodgeAt)
            {
                dodgeAt = -1f;
                dodgeUntil = Time.time + 0.4f;
            }
            bool dodging = Time.time < dodgeUntil;

            float wantYaw = yaw, wantPitch = 0f;
            if (sees)
            {
                float wobbleX = (Mathf.PerlinNoise(seed, Time.time * 0.7f) - 0.5f) * aimNoise * 2f;
                float wobbleY = (Mathf.PerlinNoise(seed + 7f, Time.time * 0.7f) - 0.5f) * aimNoise * 2f;
                wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + wobbleX;
                wantPitch = -Mathf.Atan2(to.y, Flat(to).magnitude) * Mathf.Rad2Deg + wobbleY;
            }
            else if (wantDir.sqrMagnitude > 0.01f)
                wantYaw = Mathf.Atan2(wantDir.x, wantDir.z) * Mathf.Rad2Deg;
            yaw = Mathf.MoveTowardsAngle(yaw, wantYaw, turnSpeed * dt);
            pitch = Mathf.MoveTowards(pitch, wantPitch, turnSpeed * 0.6f * dt);
            me.aim.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            var input = new MotorInput { yaw = yaw, run = run || dodging };
            input.move = dodging ? dodgeMove : Vector2.ClampMagnitude(ToStick(wantDir), 1f);
            input.jump = dodging && dodgeJump && Time.time > dodgeUntil - 0.38f;
            if (input.jump) dodgeJump = false;

            if (input.move.sqrMagnitude > 0.25f && Flat(me.motor.Velocity).magnitude < 0.4f)
            {
                stuckTime += dt;
                if (stuckTime > 0.5f)
                {
                    input.jump = true;
                    strafeDir = -strafeDir;
                    repath = 0f;
                    stuckTime = 0f;
                }
            }
            else stuckTime = 0f;

            if (sees && Time.time >= nextShot && me.CanFire && Vector3.Angle(me.aim.forward, to) < fireCone)
            {
                fire = true;
                nextShot = Time.time + me.def.basic.cooldown + Random.Range(0.05f, hesitation);
            }
            return input;
        }

        void NewStrafe(float dir)
        {
            strafeDir = dir;
            strafeTimer = Random.Range(0.8f, 2.2f);
        }

        Vector2 ToStick(Vector3 worldDir)
        {
            var local = Quaternion.Euler(0f, -yaw, 0f) * worldDir;
            return new Vector2(local.x, local.z);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        bool Walkable(Vector3 dir)
        {
            if (!NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas)) return true;
            return !NavMesh.Raycast(hit.position, hit.position + dir.normalized * 1.8f, out _, NavMesh.AllAreas);
        }

        Vector3 PathDir(Vector3 goal, float dt)
        {
            repath -= dt;
            if (repath <= 0f)
            {
                repath = 0.5f;
                corners = new Vector3[0];
                corner = 1;
                if (NavMesh.SamplePosition(transform.position, out var a, 3f, NavMesh.AllAreas)
                    && NavMesh.SamplePosition(goal, out var b, 4f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)
                    && path.status != NavMeshPathStatus.PathInvalid)
                    corners = path.corners;
            }
            if (corners.Length < 2) return Flat(goal - transform.position).normalized;
            while (corner < corners.Length - 1 && Flat(corners[corner] - transform.position).magnitude < 0.7f) corner++;
            return Flat(corners[corner] - transform.position).normalized;
        }

        void WatchShots()
        {
            if (dodgeAt > 0f || Time.time < dodgeUntil) return;
            foreach (var p in Projectile.All)
            {
                if (p.Owner == me || p == lastDodged) continue;
                var v = p.Velocity;
                var rel = me.Chest - p.transform.position;
                float t = Vector3.Dot(rel, v) / v.sqrMagnitude;
                if (t < 0f || t > 0.7f) continue;
                var miss = rel - v * t;
                if (miss.magnitude > me.def.hitRadius + 0.7f) continue;

                lastDodged = p;
                dodgeAt = Time.time + Random.Range(reaction.x, reaction.y);
                var side = Vector3.Cross(Vector3.up, v.normalized);
                if (Vector3.Dot(side, miss) > 0f) side = -side;
                if (!Walkable(side)) side = -side;
                dodgeMove = ToStick(side).normalized;
                dodgeJump = Random.value < (me.Species == Species.Greninja ? 0.5f : 0.2f);
                return;
            }
        }
    }
}
