using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HavenDuel.Combat
{
    public enum Phase { Waiting, Countdown, Fight, RoundOver }
    public enum EndReason { None, KO, Time }

    /// Round loop (GAME.md section 2): countdown, 120 s fight, KO or higher HP% on time, short
    /// result screen, then the next round. One machine runs it (the host online, you against
    /// the bot); everyone else copies its state.
    public class RoundManager
    {
        public int round;
        public Phase phase = Phase.Waiting;
        public float timeLeft, phaseLeft;
        /// Fighter id of the round winner; 0 = none yet, -1 = draw.
        public int winner;
        public EndReason reason;
        public readonly Dictionary<int, int> wins = new();
        /// Where each fighter starts this round (id -> position, yaw).
        public readonly Dictionary<int, (Vector3 pos, float yaw)> spawns = new();

        readonly CombatConfig cfg;
        /// The fighters taking part. Supplied by the game.
        public Func<IReadOnlyList<Fighter>> Fighters;
        /// Two spawn points on opposite sides of the site.
        public Func<(Vector3 a, Vector3 b)> SpawnPair;
        /// Called on every machine when a new round starts (respawn your own fighters).
        public Action<int> NewRound;

        (Vector3 a, Vector3 b)? pair;

        public RoundManager(CombatConfig cfg) { this.cfg = cfg; }

        public int Wins(int id) => wins.TryGetValue(id, out var w) ? w : 0;

        /// Host only.
        public void HostTick(float dt)
        {
            var fs = Fighters();
            switch (phase)
            {
                case Phase.Waiting:
                    if (fs.Count >= 2) StartRound(round + 1, fs);
                    break;
                case Phase.Countdown:
                    phaseLeft -= dt;
                    if (fs.Count < 2) { phase = Phase.Waiting; break; }
                    if (phaseLeft <= 0f) { phase = Phase.Fight; timeLeft = cfg.roundTime; }
                    break;
                case Phase.Fight:
                    timeLeft -= dt;
                    if (fs.Count < 2) { phase = Phase.Waiting; break; }
                    var alive = fs.Where(f => f.Alive).ToList();
                    if (alive.Count <= 1) End(alive.Count == 1 ? alive[0].id : -1, EndReason.KO);
                    else if (timeLeft <= 0f)
                    {
                        timeLeft = 0f;
                        var best = fs.OrderByDescending(f => f.Hp01).ToList();
                        End(Mathf.Approximately(best[0].Hp01, best[1].Hp01) ? -1 : best[0].id, EndReason.Time);
                    }
                    break;
                case Phase.RoundOver:
                    phaseLeft -= dt;
                    if (phaseLeft <= 0f) { if (fs.Count >= 2) StartRound(round + 1, fs); else phase = Phase.Waiting; }
                    break;
            }
        }

        void End(int who, EndReason why)
        {
            winner = who; reason = why;
            phase = Phase.RoundOver; phaseLeft = cfg.roundOverTime;
            if (who > 0) wins[who] = Wins(who) + 1;
        }

        void StartRound(int n, IReadOnlyList<Fighter> fs)
        {
            round = n; phase = Phase.Countdown; phaseLeft = cfg.countdown; timeLeft = cfg.roundTime;
            winner = 0; reason = EndReason.None;
            pair ??= SpawnPair();
            // attackers and defenders swap sides every round
            var (a, b) = n % 2 == 1 ? pair.Value : (pair.Value.b, pair.Value.a);
            spawns.Clear();
            var order = fs.OrderBy(f => f.id).ToList();
            for (int i = 0; i < order.Count; i++)
            {
                var p = i == 0 ? a : i == 1 ? b : Vector3.Lerp(a, b, 0.5f);
                var other = i == 0 ? b : a;
                var d = other - p;
                spawns[order[i].id] = (p, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            }
            NewRound?.Invoke(n);
        }

        /// Copy the host's state (online, not host). Fires NewRound when the round number changes.
        public void Apply(int r, Phase ph, float tl, float pl, int w, EndReason why, int[] ids, int[] winCounts, int[] spawnIds, float[] spawnData)
        {
            bool fresh = r != round;
            round = r; phase = ph; timeLeft = tl; phaseLeft = pl; winner = w; reason = why;
            wins.Clear();
            if (ids != null && winCounts != null) for (int i = 0; i < Mathf.Min(ids.Length, winCounts.Length); i++) wins[ids[i]] = winCounts[i];
            if (spawnIds != null && spawnData != null)
            {
                spawns.Clear();
                for (int i = 0; i < spawnIds.Length && i * 4 + 3 < spawnData.Length; i++)
                    spawns[spawnIds[i]] = (new Vector3(spawnData[i * 4], spawnData[i * 4 + 1], spawnData[i * 4 + 2]), spawnData[i * 4 + 3]);
            }
            if (fresh && r > 0) NewRound?.Invoke(r);
        }

        /// Host side: spawn table flattened for the network.
        public void Export(out int[] ids, out int[] winCounts, out int[] spawnIds, out float[] spawnData)
        {
            ids = wins.Keys.ToArray(); winCounts = ids.Select(Wins).ToArray();
            spawnIds = spawns.Keys.ToArray();
            spawnData = new float[spawnIds.Length * 4];
            for (int i = 0; i < spawnIds.Length; i++)
            {
                var (p, y) = spawns[spawnIds[i]];
                spawnData[i * 4] = p.x; spawnData[i * 4 + 1] = p.y; spawnData[i * 4 + 2] = p.z; spawnData[i * 4 + 3] = y;
            }
        }

        /// Host handover: a new host continues from the state it last received.
        public void ForgetPair() => pair = null;
    }
}
