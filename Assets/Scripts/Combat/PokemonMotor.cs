using HavenDuel.Pokemon;
using UnityEngine;

namespace HavenDuel.Combat
{
    /// What a controller (keyboard and mouse, or the bot) asks the body to do this frame.
    public struct MotorInput
    {
        public Vector2 move;     // x strafe, y forward, magnitude <= 1
        public float yaw;        // facing, degrees
        public bool run, jump, jumpHeld, down;
    }

    /// Movement for one Pokemon on a CharacterController. Shared by the local player and the bot,
    /// so both move with the same profile (GAME.md section 4).
    [RequireComponent(typeof(CharacterController))]
    public class PokemonMotor : MonoBehaviour
    {
        public Species species;
        [Tooltip("Above this height Charizard can't climb any more (soft flight ceiling).")]
        public float ceilingY = 40f;
        [Tooltip("Falling below this height puts the body back at Respawn.")]
        public float fallY = -50f;
        [Tooltip("No steering or jumping, only gravity (countdowns, KO).")]
        public bool locked;

        public CharacterController Body { get; private set; }
        public Vector3 Velocity { get; private set; }
        public bool Grounded => Body.isGrounded;
        public bool Flying { get; private set; }
        /// Charizard's wing stamina, 0..1.
        public float Fatigue { get; private set; } = 1f;
        public System.Func<Vector3> Respawn;

        float vy, heading, flyVy;
        int airJumps;
        Vector3 lastPos;
        PokemonAnimator anim;

        void Awake()
        {
            Body = GetComponent<CharacterController>();
            anim = GetComponent<PokemonAnimator>();
            lastPos = transform.position;
        }

        public void Teleport(Vector3 pos, float yaw)
        {
            Body.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            Body.enabled = true;
            lastPos = pos; vy = 0f; flyVy = 0f; Flying = false; Fatigue = 1f; heading = yaw;
            Velocity = Vector3.zero;
        }

        public void Tick(MotorInput i, float dt)
        {
            if (locked) { i.move = Vector2.zero; i.jump = i.jumpHeld = i.down = i.run = false; i.yaw = transform.eulerAngles.y; }
            var mv = Vector2.ClampMagnitude(i.move, 1f);
            bool grounded = Body.isGrounded;
            Vector3 move;

            if (species == Species.Charizard)
            {
                // Glider-style flight: hold Space in the air to spread the wings and glide, tap Space
                // while gliding to flap upward, Ctrl to dive. Gliding and flapping use stamina; it refills on the ground.
                if (grounded) { Flying = false; Fatigue = Mathf.Min(1f, Fatigue + dt / 2.5f); }
                bool canFly = Fatigue > 0f;
                if (!grounded && i.jumpHeld && canFly) Flying = true;
                Flying = Flying && !grounded && i.jumpHeld && canFly;
                if (Flying)
                {
                    Fatigue -= dt / 14f;
                    if (i.jump && Fatigue > 0.12f) { flyVy = 6f; Fatigue -= 0.12f; }      // wing flap
                    heading = Mathf.MoveTowardsAngle(heading, i.yaw, 90f * dt);
                    float fwd = Mathf.Max(0f, mv.y) * (i.run ? 10f : 7f) + Mathf.Min(0f, mv.y) * 2f;
                    move = Quaternion.Euler(0f, heading, 0f) * new Vector3(mv.x * 2.5f, 0f, fwd);
                    float glideVy = i.down ? -9f : -1.6f;
                    flyVy = Mathf.MoveTowards(flyVy, glideVy, 9f * dt);
                    vy = flyVy;
                    if (transform.position.y > ceilingY && vy > 0f) vy = 0f;
                    if (Fatigue <= 0f) { Fatigue = 0f; Flying = false; }
                }
                else
                {
                    heading = i.yaw;
                    move = Quaternion.Euler(0f, i.yaw, 0f) * new Vector3(mv.x, 0f, mv.y) * (i.run ? 4f : 2f);
                    if (grounded) { vy = -1f; if (i.jump) vy = 5f; }
                    else vy += Physics.gravity.y * dt;
                    flyVy = Mathf.Max(vy, -2f);
                }
                transform.rotation = Quaternion.Euler(0f, Flying ? heading : i.yaw, 0f);
            }
            else
            {
                // Greninja: quick and agile. High jump and a second jump in the air.
                heading = i.yaw;
                transform.rotation = Quaternion.Euler(0f, i.yaw, 0f);
                float speed = i.run ? 8.5f : 4.5f;
                move = Quaternion.Euler(0f, i.yaw, 0f) * new Vector3(mv.x, 0f, mv.y) * speed;
                if (grounded) { airJumps = 1; vy = -1f; if (i.jump) vy = 7.5f; }
                else if (i.jump && airJumps > 0) { airJumps--; vy = 6.5f; }
                if (!grounded) vy += Physics.gravity.y * 1.4f * dt;
            }

            var flags = Body.Move((move + Vector3.up * vy) * dt);
            if ((flags & CollisionFlags.Above) != 0 && vy > 0f) { vy = 0f; flyVy = Mathf.Min(flyVy, 0f); }
            if (transform.position.y < fallY && Respawn != null) Teleport(Respawn(), transform.eulerAngles.y);

            Velocity = (transform.position - lastPos) / Mathf.Max(dt, 1e-4f);
            lastPos = transform.position;
            if (anim) anim.UpdateLocomotion(Velocity, Body.isGrounded, Flying);
        }
    }
}
