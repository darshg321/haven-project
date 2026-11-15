using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HavenDuel
{
    public class Online : IDisposable
    {
        [Serializable]
        class Msg
        {
            public string type;
            public int id, site, species, round, target, move;
            public Vector3 pos, vel;
            public float yaw, hp, damage;
            public bool grounded, flying;
            public string rounds;
        }

        class Remote
        {
            public Fighter fighter;
            public Vector3 pos, vel;
            public float yaw;
            public bool grounded, flying;
        }

        public int MyId { get; private set; }
        public bool IsHost => remotes.Keys.All(id => id > MyId);
        public IEnumerable<Fighter> Fighters => remotes.Values.Select(r => r.fighter);

        readonly Game game;
        readonly Relay relay;
        readonly string host;
        readonly Dictionary<int, Remote> remotes = new();
        float sendTimer, roundsTimer;

        public Online(Game game, string host)
        {
            this.game = game;
            this.host = host;
            relay = new Relay(host);
            Fighter.Fired += OnFired;
            Fighter.LandedHit += OnLandedHit;
        }

        public void Dispose()
        {
            Fighter.Fired -= OnFired;
            Fighter.LandedHit -= OnLandedHit;
            relay.Dispose();
        }

        public string Status => relay.Status switch
        {
            Relay.State.Open => $"Online as player {MyId} ({remotes.Count + 1} on Site {game.site})" + (IsHost ? ", host" : ""),
            Relay.State.Closed => $"Can't reach {host}. Is Tools/relay_server.py running?",
            _ => "Connecting...",
        };

        void Send(Msg m)
        {
            if (relay.Status != Relay.State.Open || MyId == 0) return;
            m.id = MyId;
            m.site = (int)game.site;
            relay.Send(JsonUtility.ToJson(m));
        }

        void OnFired(Fighter f, int move, Vector3 origin, Vector3 dir)
        {
            if (f == game.me) Send(new Msg { type = "fire", move = move, pos = origin, vel = dir });
        }

        void OnLandedHit(Fighter shooter, Fighter target, float damage, Vector3 point)
        {
            if (shooter == game.me && !target.authority) Send(new Msg { type = "hit", target = target.id, damage = damage, pos = point });
        }

        public void Update(float dt, float sendRate)
        {
            Receive();

            foreach (var r in remotes.Values)
            {
                var t = r.fighter.transform;
                float k = 1f - Mathf.Exp(-12f * dt);
                t.position = Vector3.Lerp(t.position, r.pos, k);
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.Euler(0f, r.yaw, 0f), k);
                r.fighter.anim.UpdateLocomotion(r.vel, r.grounded, r.flying);
            }

            var me = game.me;
            sendTimer -= dt;
            if (sendTimer <= 0f)
            {
                sendTimer = 1f / sendRate;
                Send(new Msg
                {
                    type = "state", species = (int)me.Species, round = game.rounds.round, hp = me.hp,
                    pos = me.transform.position, vel = me.motor.Velocity, yaw = me.transform.eulerAngles.y,
                    grounded = me.motor.Grounded, flying = me.motor.Flying,
                });
            }
            roundsTimer -= dt;
            if (IsHost && roundsTimer <= 0f)
            {
                roundsTimer = 0.2f;
                Send(new Msg { type = "rounds", rounds = JsonUtility.ToJson(game.rounds) });
            }
        }

        void Receive()
        {
            while (relay.TryReceive(out var raw))
            {
                var m = JsonUtility.FromJson<Msg>(raw);
                if (m.type == "hello")
                {
                    MyId = game.me.id = m.id;
                    continue;
                }
                if (m.type == "bye" || m.site != (int)game.site)
                {
                    Drop(m.id);
                    continue;
                }
                if (m.id == MyId) continue;

                switch (m.type)
                {
                    case "state":
                        if (!remotes.TryGetValue(m.id, out var r) || r.fighter.Species != (Species)m.species)
                        {
                            Drop(m.id);
                            r = remotes[m.id] = new Remote { fighter = game.SpawnRemote((Species)m.species, m.id, m.pos, m.yaw) };
                        }
                        r.pos = m.pos;
                        r.vel = m.vel;
                        r.yaw = m.yaw;
                        r.grounded = m.grounded;
                        r.flying = m.flying;
                        if (m.round >= game.rounds.round) r.fighter.SetHp(m.hp);
                        break;
                    case "fire":
                        if (remotes.TryGetValue(m.id, out var shooter)) shooter.fighter.Launch(m.move, m.pos, m.vel, false);
                        break;
                    case "hit":
                        if (m.target == MyId && Fighter.CombatLive) game.me.TakeDamage(m.damage, m.pos);
                        break;
                    case "rounds":
                        if (!IsHost && m.id == remotes.Keys.Min()) game.rounds.Apply(m.rounds);
                        break;
                }
            }
        }

        void Drop(int id)
        {
            if (!remotes.Remove(id, out var gone)) return;
            UnityEngine.Object.Destroy(gone.fighter.gameObject);
            game.rounds.ForgetSpawns();
        }
    }
}
