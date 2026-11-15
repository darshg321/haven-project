using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HavenDuel
{
    public enum Phase { Waiting, Countdown, Fight, RoundOver }
    public enum EndReason { None, KO, Time }

    [Serializable]
    public class Rounds
    {
        [Serializable] public class Spawn { public int id; public Vector3 pos; public float yaw; }
        [Serializable] public class Score { public int id, wins; }

        public int round;
        public Phase phase;
        public float timeLeft, phaseLeft;
        public int winner;
        public EndReason reason;
        public List<Score> scores = new();
        public List<Spawn> spawns = new();

        [NonSerialized] public CombatConfig cfg;
        [NonSerialized] public Func<List<Fighter>> fighters;
        [NonSerialized] public Func<(Vector3, Vector3)> pickSpawns;
        [NonSerialized] public Action onNewRound;
        [NonSerialized] (Vector3, Vector3)? pair;

        public int Wins(int id) => scores.Find(s => s.id == id)?.wins ?? 0;
        public Spawn SpawnOf(int id) => spawns.Find(s => s.id == id);
        public void ForgetSpawns() => pair = null;

        public void HostTick(float dt)
        {
            var fs = fighters();
            if (fs.Count < 2 && phase != Phase.RoundOver)
            {
                phase = Phase.Waiting;
                return;
            }

            switch (phase)
            {
                case Phase.Waiting:
                    StartRound();
                    break;
                case Phase.Countdown:
                    phaseLeft -= dt;
                    if (phaseLeft <= 0f)
                    {
                        phase = Phase.Fight;
                        timeLeft = cfg.roundTime;
                    }
                    break;
                case Phase.Fight:
                    timeLeft -= dt;
                    var alive = fs.Where(f => f.Alive).ToList();
                    if (alive.Count <= 1)
                        End(alive.Count == 1 ? alive[0].id : -1, EndReason.KO);
                    else if (timeLeft <= 0f)
                    {
                        timeLeft = 0f;
                        var byHp = fs.OrderByDescending(f => f.Hp01).ToList();
                        End(byHp[0].Hp01 == byHp[1].Hp01 ? -1 : byHp[0].id, EndReason.Time);
                    }
                    break;
                case Phase.RoundOver:
                    phaseLeft -= dt;
                    if (phaseLeft <= 0f)
                    {
                        if (fs.Count >= 2) StartRound();
                        else phase = Phase.Waiting;
                    }
                    break;
            }
        }

        void End(int who, EndReason why)
        {
            winner = who;
            reason = why;
            phase = Phase.RoundOver;
            phaseLeft = cfg.roundOverTime;
            if (who <= 0) return;
            var score = scores.Find(s => s.id == who);
            if (score == null) scores.Add(new Score { id = who, wins = 1 });
            else score.wins++;
        }

        void StartRound()
        {
            round++;
            phase = Phase.Countdown;
            phaseLeft = cfg.countdown;
            timeLeft = cfg.roundTime;
            winner = 0;
            reason = EndReason.None;

            pair ??= pickSpawns();
            var (a, b) = pair.Value;
            if (round % 2 == 0) (a, b) = (b, a);
            spawns.Clear();
            var ids = fighters().Select(f => f.id).OrderBy(id => id).ToList();
            for (int i = 0; i < ids.Count; i++)
            {
                var pos = i == 0 ? a : i == 1 ? b : (a + b) / 2;
                var look = (i == 0 ? b : a) - pos;
                spawns.Add(new Spawn { id = ids[i], pos = pos, yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg });
            }
            onNewRound();
        }

        public void Apply(string json)
        {
            int before = round;
            JsonUtility.FromJsonOverwrite(json, this);
            if (round != before && round > 0) onNewRound();
        }
    }
}
