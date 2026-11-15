using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HavenDuel.Pokemon
{
    /// Drives the legacy Animation component that glTFast puts on an imported .glb.
    /// Locomotion (idle/walk/run/jump/fall/fly) is picked from movement each frame;
    /// one-shot actions (attack, hit, roar...) play over it and then hand back.
    [DisallowMultipleComponent]
    public class PokemonAnimator : MonoBehaviour
    {
        [Tooltip("Ground speed (m/s) at which walk turns into run.")]
        public float runSpeed = 4f;
        [Tooltip("Below this speed the Pokemon counts as standing still.")]
        public float moveThreshold = 0.15f;
        public float crossFade = 0.15f;
        [Tooltip("Use the battle-stance idle instead of the relaxed one.")]
        public bool battleStance = true;

        Animation anim;
        readonly Dictionary<PokemonAnim, string> clipNames = new();
        string current;
        string oneShot;
        float oneShotEnd;
        PokemonAnim? hold;
        bool wasGrounded = true;

        public bool IsPlayingAction => oneShot != null;
        public Animation Animation => anim;

        void Awake()
        {
            anim = GetComponentInChildren<Animation>(true);
            if (!anim) { Debug.LogWarning($"{name}: no Animation component, is the glb imported with Legacy animation?", this); return; }
            anim.playAutomatically = false;
            foreach (AnimationState s in anim)
            {
                foreach (PokemonAnim a in System.Enum.GetValues(typeof(PokemonAnim)))
                    if (s.name == Snake(a)) clipNames[a] = s.name;
            }
            foreach (AnimationState s in anim) s.wrapMode = IsLooping(s.name) ? WrapMode.Loop : WrapMode.Once;
            Play(battleStance ? PokemonAnim.BattleIdle : PokemonAnim.Idle, 0f);
        }

        public bool Has(PokemonAnim a) => clipNames.ContainsKey(a);

        /// After the current one-shot, stay in this state instead of going back to locomotion
        /// (a fainted Pokemon stays down). Null hands control back to locomotion.
        public void HoldAfterAction(PokemonAnim? state) => hold = state;

        /// Call every frame from the controller (or bot) with the body's current state.
        public void UpdateLocomotion(Vector3 velocity, bool grounded, bool flying = false)
        {
            if (!anim) return;
            if (oneShot != null)
            {
                if (Time.time < oneShotEnd && anim.IsPlaying(oneShot)) return;
                oneShot = null;
            }
            if (hold != null) { Play(hold.Value); return; }

            float ground = new Vector2(velocity.x, velocity.z).magnitude;
            PokemonAnim want;
            if (flying && Has(PokemonAnim.FlyIdle))
                want = ground < moveThreshold ? (battleStance ? PokemonAnim.FlyBattleIdle : PokemonAnim.FlyIdle)
                     : ground < runSpeed ? PokemonAnim.FlyMove : PokemonAnim.FlyFast;
            else if (!grounded)
                want = velocity.y > 0.5f ? PokemonAnim.JumpLoop : PokemonAnim.FallLoop;
            else if (!wasGrounded && Has(PokemonAnim.Land))
            {
                wasGrounded = true;
                PlayAction(PokemonAnim.Land);
                return;
            }
            else
                want = ground < moveThreshold ? (battleStance ? PokemonAnim.BattleIdle : PokemonAnim.Idle)
                     : ground < runSpeed ? PokemonAnim.Walk : PokemonAnim.Run;

            wasGrounded = grounded;
            Play(want);
        }

        /// Loop or hold a state until something else is played.
        public void Play(PokemonAnim a, float fade = -1f)
        {
            var clip = Resolve(a);
            if (clip == null || clip == current) return;
            current = clip;
            if (fade <= 0f && fade != -1f) anim.Play(clip);
            else anim.CrossFade(clip, fade < 0f ? crossFade : fade);
        }

        /// Play a one-shot (attack, hit, roar...). Returns its length in seconds, 0 if the
        /// species has nothing to show for it.
        public float PlayAction(PokemonAnim a, float speed = 1f)
        {
            var clip = Resolve(a);
            if (clip == null || IsLooping(clip)) return 0f;
            var st = anim[clip];
            st.speed = speed;
            st.time = 0f;
            anim.CrossFade(clip, crossFade * 0.5f);
            current = oneShot = clip;
            float len = st.length / Mathf.Max(0.01f, speed);
            oneShotEnd = Time.time + len;
            return len;
        }

        /// Missing clips fall back to the nearest one the species has, so every species
        /// answers every state (Ditto only has idle, Mimikyu floats instead of jumping).
        string Resolve(PokemonAnim a)
        {
            for (int guard = 0; guard < 6; guard++)
            {
                if (clipNames.TryGetValue(a, out var n)) return n;
                PokemonAnim? next = a switch
                {
                    PokemonAnim.BattleIdle or PokemonAnim.FlyIdle or PokemonAnim.FlyBattleIdle => PokemonAnim.Idle,
                    PokemonAnim.Run or PokemonAnim.FlyFast => PokemonAnim.Walk,
                    PokemonAnim.FlyMove => PokemonAnim.Walk,
                    PokemonAnim.Walk => PokemonAnim.Idle,
                    PokemonAnim.JumpStart => PokemonAnim.JumpLoop,
                    PokemonAnim.JumpLoop or PokemonAnim.FallStart => PokemonAnim.FallLoop,
                    PokemonAnim.FallLoop => PokemonAnim.Idle,
                    PokemonAnim.Attack2 or PokemonAnim.FlyAttack => PokemonAnim.Attack,
                    PokemonAnim.FlySpecial or PokemonAnim.ChargeEnd => PokemonAnim.Special,
                    PokemonAnim.Special => PokemonAnim.Attack,
                    PokemonAnim.FlyHit or PokemonAnim.Stun => PokemonAnim.Hit,
                    PokemonAnim.FlyLand => PokemonAnim.Land,
                    PokemonAnim.FlyChargeStart => PokemonAnim.ChargeStart,
                    PokemonAnim.FlyChargeLoop => PokemonAnim.ChargeLoop,
                    PokemonAnim.FlyChargeEnd => PokemonAnim.ChargeEnd,
                    PokemonAnim.FaintLoop => PokemonAnim.Faint,
                    _ => null,
                };
                if (next == null) return null;
                a = next.Value;
            }
            return null;
        }

        static bool IsLooping(string clip) =>
            clip.Contains("idle") || clip == "walk" || clip == "run" || clip.EndsWith("_loop")
            || clip == "fly_move" || clip == "fly_fast" || clip == "stun";

        public static string Snake(PokemonAnim a)
        {
            var s = a.ToString();
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i])) sb.Append('_');
                sb.Append(char.ToLowerInvariant(s[i]));
            }
            return sb.ToString();
        }
    }
}
