using UnityEngine;

namespace HavenDuel
{
    public struct MotorInput
    {
        public Vector2 move;
        public float yaw;
        public bool run, jump, jumpHeld, down;
    }

    [RequireComponent(typeof(CharacterController))]
    public class Motor : MonoBehaviour
    {
        public Species species;
        public float ceilingY = 40f;
        public float fallY = -50f;
        public bool locked;
        public System.Func<Vector3> respawn;

        public CharacterController Body { get; private set; }
        public Vector3 Velocity { get; private set; }
        public bool Grounded => Body.isGrounded;
        public bool Flying { get; private set; }
        public float Stamina { get; private set; } = 1f;

        float vy, lastSpace;
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
            lastPos = pos;
            vy = 0f;
            Flying = false;
            Stamina = 1f;
            Velocity = Vector3.zero;
        }

        public void Tick(MotorInput input, float dt)
        {
            if (locked) input = new MotorInput { yaw = transform.eulerAngles.y };
            var mv = Vector2.ClampMagnitude(input.move, 1f);
            var move = species == Species.Charizard ? Charizard(input, mv, dt) : Greninja(input, mv, dt);

            var hit = Body.Move((move + Vector3.up * vy) * dt);
            if ((hit & CollisionFlags.Above) != 0 && vy > 0f) vy = 0f;
            if (transform.position.y < fallY) Teleport(respawn(), transform.eulerAngles.y);

            Velocity = (transform.position - lastPos) / dt;
            lastPos = transform.position;
            anim.UpdateLocomotion(Velocity, Body.isGrounded, Flying);
        }

        Vector3 Greninja(MotorInput input, Vector2 mv, float dt)
        {
            transform.rotation = Quaternion.Euler(0f, input.yaw, 0f);
            if (Body.isGrounded)
            {
                vy = input.jump ? 10f : -1f;
            }
            else
            {
                vy += Physics.gravity.y * 1.4f * dt;
            }
            return transform.rotation * new Vector3(mv.x, 0f, mv.y) * (input.run ? 8.5f : 4.5f);
        }

        Vector3 Charizard(MotorInput input, Vector2 mv, float dt)
        {
            bool grounded = Body.isGrounded;
            if (input.jump)
            {
                if (Time.time - lastSpace < 0.3f && Stamina > 0.1f) Flying = !Flying;
                lastSpace = Time.time;
            }
            if (Flying && (Stamina <= 0f || grounded && vy <= 0f && !input.jumpHeld)) Flying = false;
            transform.rotation = Quaternion.Euler(0f, input.yaw, 0f);

            if (!Flying)
            {
                Stamina = grounded ? Mathf.Min(1f, Stamina + dt / 2.5f) : Stamina;
                if (grounded) vy = input.jump ? 5f : -1f;
                else vy += Physics.gravity.y * dt;
                return transform.rotation * new Vector3(mv.x, 0f, mv.y) * (input.run ? 4f : 2f);
            }

            if (input.jumpHeld)
            {
                vy = Mathf.MoveTowards(vy, 5f, 20f * dt);
                Stamina -= dt / 3f;
            }
            else
            {
                vy = Mathf.Max(vy + Physics.gravity.y * 0.3f * dt, input.down ? -12f : -4f);
                Stamina -= dt / 12f;
            }
            if (input.down) vy = Mathf.MoveTowards(vy, -12f, 30f * dt);
            if (transform.position.y > ceilingY) vy = Mathf.Min(vy, 0f);
            Stamina = Mathf.Max(Stamina, 0f);
            return transform.rotation * new Vector3(mv.x, 0f, mv.y) * (input.run ? 9f : 6f);
        }
    }
}
