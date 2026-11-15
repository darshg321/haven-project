using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HavenDuel
{
    public enum PokemonAnim
    {
        Idle, BattleIdle, Walk, Run, JumpLoop, FallLoop, Land,
        Attack, Hit, Faint, FaintLoop, Roar,
        FlyIdle, FlyBattleIdle, FlyMove, FlyFast, FlyAttack, FlyHit,
    }

    public class PokemonAnimator : MonoBehaviour
    {
        public float runSpeed = 4f;

        static readonly Dictionary<PokemonAnim, PokemonAnim> Fallback = new()
        {
            [PokemonAnim.BattleIdle] = PokemonAnim.Idle,
            [PokemonAnim.FlyIdle] = PokemonAnim.Idle,
            [PokemonAnim.FlyBattleIdle] = PokemonAnim.Idle,
            [PokemonAnim.Run] = PokemonAnim.Walk,
            [PokemonAnim.FlyFast] = PokemonAnim.Walk,
            [PokemonAnim.FlyMove] = PokemonAnim.Walk,
            [PokemonAnim.Walk] = PokemonAnim.Idle,
            [PokemonAnim.JumpLoop] = PokemonAnim.FallLoop,
            [PokemonAnim.FallLoop] = PokemonAnim.Idle,
            [PokemonAnim.FlyAttack] = PokemonAnim.Attack,
            [PokemonAnim.FlyHit] = PokemonAnim.Hit,
            [PokemonAnim.FaintLoop] = PokemonAnim.Faint,
        };

        const float Fade = 0.15f;

        Animation anim;
        readonly HashSet<string> clips = new();
        string current, oneShot;
        float oneShotEnd;
        bool wasGrounded = true;
        PokemonAnim? hold;

        public bool IsPlayingAction => oneShot != null;

        void Awake()
        {
            anim = GetComponentInChildren<Animation>(true);
            anim.playAutomatically = false;
            foreach (AnimationState s in anim)
            {
                clips.Add(s.name);
                s.wrapMode = IsLooping(s.name) ? WrapMode.Loop : WrapMode.Once;
            }
            Play(PokemonAnim.BattleIdle, 0f);
        }

        public void UpdateLocomotion(Vector3 velocity, bool grounded, bool flying = false)
        {
            if (oneShot != null)
            {
                if (Time.time < oneShotEnd && anim.IsPlaying(oneShot)) return;
                oneShot = null;
            }
            if (hold != null)
            {
                Play(hold.Value);
                return;
            }

            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            bool moving = speed > 0.15f;
            if (flying && clips.Contains("fly_idle"))
                Play(!moving ? PokemonAnim.FlyBattleIdle : speed < runSpeed ? PokemonAnim.FlyMove : PokemonAnim.FlyFast);
            else if (!grounded)
                Play(velocity.y > 0.5f ? PokemonAnim.JumpLoop : PokemonAnim.FallLoop);
            else if (!wasGrounded && clips.Contains("land"))
                PlayAction(PokemonAnim.Land);
            else
                Play(!moving ? PokemonAnim.BattleIdle : speed < runSpeed ? PokemonAnim.Walk : PokemonAnim.Run);
            wasGrounded = grounded;
        }

        public void Play(PokemonAnim state, float fade = Fade)
        {
            var clip = Resolve(state);
            if (clip == null || clip == current) return;
            current = clip;
            if (fade > 0f) anim.CrossFade(clip, fade);
            else anim.Play(clip);
        }

        public float PlayAction(PokemonAnim state, float speed = 1f)
        {
            var clip = Resolve(state);
            if (clip == null || IsLooping(clip)) return 0f;
            var s = anim[clip];
            s.speed = speed;
            s.time = 0f;
            anim.CrossFade(clip, Fade / 2);
            current = oneShot = clip;
            float length = s.length / speed;
            oneShotEnd = Time.time + length;
            return length;
        }

        public void HoldAfterAction(PokemonAnim? state) => hold = state;

        string Resolve(PokemonAnim state)
        {
            while (true)
            {
                var name = Regex.Replace(state.ToString(), "(?<!^)([A-Z])", "_$1").ToLower();
                if (clips.Contains(name)) return name;
                if (!Fallback.TryGetValue(state, out state)) return null;
            }
        }

        static bool IsLooping(string clip) =>
            clip.Contains("idle") || clip.EndsWith("_loop") || clip is "walk" or "run" or "fly_move" or "fly_fast";
    }
}
