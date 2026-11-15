using System.Collections.Generic;
using UnityEngine;

namespace HavenDuel.Pokemon
{
    /// Test scene: the whole roster in a row. Number keys / buttons pick the state
    /// every model plays, so missing or broken clips are easy to spot.
    public class ModelShowcase : MonoBehaviour
    {
        public PokemonRoster roster;
        public float spacing = 1.8f;

        readonly List<PokemonAnimator> models = new();
        static readonly PokemonAnim[] Loops = {
            PokemonAnim.Idle, PokemonAnim.BattleIdle, PokemonAnim.Walk, PokemonAnim.Run,
            PokemonAnim.JumpLoop, PokemonAnim.FallLoop, PokemonAnim.ChargeLoop, PokemonAnim.FlyIdle, PokemonAnim.FlyMove,
        };
        static readonly PokemonAnim[] Actions = {
            PokemonAnim.Attack, PokemonAnim.Attack2, PokemonAnim.Special, PokemonAnim.Hit,
            PokemonAnim.Land, PokemonAnim.Roar, PokemonAnim.Faint, PokemonAnim.FlyAttack,
        };
        PokemonAnim loop = PokemonAnim.BattleIdle;
        string last = "battle_idle";

        void Start()
        {
            var all = (Species[])System.Enum.GetValues(typeof(Species));
            float x0 = -(all.Length - 1) * spacing * 0.5f;
            for (int i = 0; i < all.Length; i++)
            {
                var a = roster.Spawn(all[i], new Vector3(x0 + i * spacing, 0f, 0f), Quaternion.Euler(0f, 180f, 0f), transform);
                if (a) models.Add(a);
            }
        }

        void SetLoop(PokemonAnim a) { loop = a; last = PokemonAnimator.Snake(a); foreach (var m in models) m.Play(a); }
        void Action(PokemonAnim a) { last = PokemonAnimator.Snake(a); foreach (var m in models) m.PlayAction(a); }

        void Update()
        {
            foreach (var m in models) if (!m.IsPlayingAction) m.Play(loop);
        }

        void OnGUI()
        {
            const float h = 26f;
            GUI.Label(new Rect(10, 10, 300, h), $"Playing: {last}");
            Row(Screen.height - 2 * (h + 6) - 4, Loops, SetLoop);
            Row(Screen.height - (h + 6) - 4, Actions, Action);

            static void Row(float y, PokemonAnim[] items, System.Action<PokemonAnim> on)
            {
                float w = (Screen.width - 20f) / items.Length;
                for (int i = 0; i < items.Length; i++)
                    if (GUI.Button(new Rect(10 + i * w, y, w - 4, h), items[i].ToString())) on(items[i]);
            }
        }

        public void PlayAllForTest(string state)
        {
            if (System.Enum.TryParse<PokemonAnim>(state, out var a))
            {
                if (System.Array.IndexOf(Actions, a) >= 0) Action(a); else SetLoop(a);
            }
        }
    }
}
