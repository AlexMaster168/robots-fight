using System.Collections.Generic;
using UnityEngine;

namespace RobotsFight
{
    /// <summary>Пилот живёт весь матч; тело (Robot) создаётся при каждом выходе в бой.</summary>
    public class Pilot
    {
        public string Name;
        public int Team;
        public bool IsPlayer;
        public Color Color;
        public RobotDef Def, NextDef;
        public Robot Body;
        public int Kills, Deaths, Score;
        public float DamageDealt;
        public int Drops = -1; // сколько ещё выходов в бой; -1 = бесконечно
        public float RespawnAt = -1f;
        public float HpMult = 1f;
        public bool IsAlive => Body && Body.Alive;
    }

    public enum ModeType { Beacon, TeamDeathmatch, FreeForAll, Survival, Bomb }
    public enum Difficulty { Easy, Normal, Hard }

    public struct FeedEntry
    {
        public string Text;
        public Color Color;
        public float Time;
    }

    public class Match : MonoBehaviour
    {
        public static Match I;

        public Arena Arena { get; private set; }
        public GameMode Mode { get; private set; }
        public ModeType ModeType { get; private set; }
        public readonly List<Pilot> Pilots = new();
        public Pilot Player { get; private set; }
        public RobotDef PlayerDefChoice { get; private set; }
        public Difficulty Difficulty { get; private set; }
        public bool Autopilot { get; private set; }

        public float Elapsed { get; private set; }
        public float TimeLeft { get; private set; }
        public bool Over { get; private set; }
        public string Result { get; private set; }
        public string ResultDetails { get; private set; }
        public bool PlayerWon { get; private set; }

        public readonly List<FeedEntry> Feed = new();
        public float HitMarkerTime = -99f, PlayerHurtTime = -99f;
        public string Banner;
        public Color BannerColor = Color.white;
        public float BannerUntil;
        public Pilot LastKillerOfPlayer;

        List<string> names;
        const float LavaDps = 220f;

        float BotSkill => Difficulty switch { Difficulty.Easy => 0.25f, Difficulty.Hard => 0.85f, _ => 0.55f };

        public MapDef Map => Arena.Map;

        public static Match Begin(ModeType type, RobotDef playerDef, Difficulty diff, bool autopilot, MapDef map)
        {
            var go = new GameObject("Match");
            var m = go.AddComponent<Match>();
            I = m;
            m.ModeType = type;
            m.Difficulty = diff;
            m.Autopilot = autopilot;
            m.PlayerDefChoice = playerDef;
            m.names = Names.Shuffled();
            m.Arena = Arena.Build(go.transform, map);
            go.AddComponent<Projectiles>();
            m.Mode = GameMode.Create(type);
            m.Mode.M = m;
            m.Mode.Setup();
            m.TimeLeft = m.Mode.Duration;
            return m;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        // ---------------------------------------------------------------- Пилоты
        public Pilot AddPilot(int team, bool isPlayer, RobotDef def = null)
        {
            var p = new Pilot
            {
                Name = isPlayer ? "Лёха" : NextName(),
                Team = team,
                IsPlayer = isPlayer,
                Def = def ?? (isPlayer ? PlayerDefChoice : Catalog.RandomRobot()),
                Color = Palette.Team(team),
            };
            if (isPlayer) Player = p;
            Pilots.Add(p);
            return p;
        }

        string NextName()
        {
            if (names.Count == 0) names = Names.Shuffled();
            var n = names[names.Count - 1];
            names.RemoveAt(names.Count - 1);
            return n;
        }

        public Robot SpawnPilot(Pilot p)
        {
            if (p.NextDef != null) p.Def = p.NextDef;
            p.NextDef = null;
            if (p.Drops > 0) p.Drops--;
            var pos = Mode.SpawnPoint(p, out var rot);
            var r = Robot.Create(p, p.Def, pos, rot, transform);
            r.Brain = p.IsPlayer && !Autopilot ? new PlayerBrain() : new BotBrain { Skill = BotSkill };
            r.Brain.R = r;
            p.Body = r;
            p.RespawnAt = -1f;
            if (p.IsPlayer && PlayerCamera.I) PlayerCamera.I.SnapTo(r);
            return r;
        }

        public void DespawnPilot(Pilot p)
        {
            if (p.Body) Destroy(p.Body.gameObject);
            p.Body = null;
        }

        public bool AreEnemies(Pilot a, Pilot b) => a != null && b != null && a != b && a.Team != b.Team;

        public int AliveCount(int team)
        {
            int n = 0;
            foreach (var p in Pilots)
                if (p.Team == team && p.IsAlive) n++;
            return n;
        }

        // ---------------------------------------------------------------- Урон и смерть
        public void Damage(Robot target, float dmg, Pilot attacker, WeaponDef weapon, Vector3 point)
        {
            if (!target || !target.Alive) return;
            if (attacker != null && !AreEnemies(attacker, target.Pilot)) return; // френдли-фаера нет
            var victim = target.Pilot;
            float dealt = target.TakeDamage(dmg, attacker, weapon, point);
            if (dealt <= 0) return;
            if (attacker != null)
            {
                attacker.DamageDealt += dealt;
                attacker.Score += Mathf.RoundToInt(dealt * 0.05f);
                if (attacker == Player) HitMarkerTime = Time.time;
            }
            if (victim == Player) PlayerHurtTime = Time.time;
        }

        public void OnRobotKilled(Robot victim, Pilot killer, WeaponDef weapon)
        {
            var vp = victim.Pilot;
            vp.Deaths++;
            Pickup.Spawn(victim.transform.position, Random.value < 0.5f ? PickupType.Medkit : PickupType.Shield);
            vp.Body = null;
            bool realKill = killer != null && killer != vp;
            if (realKill)
            {
                killer.Kills++;
                killer.Score += 100;
            }
            if (vp == Player) LastKillerOfPlayer = killer;

            string w = weapon != null ? $" [{weapon.Name}] " : " → ";
            AddFeed(realKill ? $"{killer.Name}{w}{vp.Name}" : $"{vp.Name} уничтожен",
                realKill ? killer.Color : vp.Color);

            Mode.OnKilled(vp, killer);
            if (!Over && Mode.CanRespawn(vp)) vp.RespawnAt = Elapsed + Mode.RespawnDelay;
            else vp.RespawnAt = -1f;
            if (!vp.IsPlayer && Mode.BotsSwitchRobots) vp.NextDef = Catalog.RandomRobot();
        }

        public void AddFeed(string text, Color color)
        {
            Feed.Add(new FeedEntry { Text = text, Color = color, Time = Time.time });
            if (Feed.Count > 6) Feed.RemoveAt(0);
            if (Application.isBatchMode) Debug.Log($"[Feed] {text}");
        }

        public void ShowBanner(string text, Color color, float seconds = 3f)
        {
            Banner = text;
            BannerColor = color;
            BannerUntil = Time.time + seconds;
            if (Application.isBatchMode) Debug.Log($"[Banner] {text}");
        }

        public void End(string result, string details, bool playerWon)
        {
            if (Over) return;
            Over = true;
            Result = result;
            ResultDetails = details;
            PlayerWon = playerWon;
            foreach (var p in Pilots) p.RespawnAt = -1f;
            Debug.Log($"[Match] {Mode.Title}: {result} — {details}");
        }

        // ---------------------------------------------------------------- Цикл
        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = Feed.Count - 1; i >= 0; i--)
                if (Time.time - Feed[i].Time > 7f) Feed.RemoveAt(i);
            if (Over) return;

            Elapsed += dt;
            if (Mode.Duration > 0)
            {
                TimeLeft -= dt;
                if (TimeLeft <= 0)
                {
                    TimeLeft = 0;
                    Mode.OnTimeUp();
                }
            }
            Mode.Tick(dt);

            // Лава жжёт тех, кто стоит в ней (над лавой на ранце — можно)
            if (Arena.Lava.Count > 0)
                for (int i = 0; i < Pilots.Count; i++)
                {
                    var b = Pilots[i].Body;
                    if (b && b.Alive && b.transform.position.y < 0.8f && Arena.InLava(b.transform.position))
                        Damage(b, LavaDps * dt, null, Catalog.Lava, b.transform.position);
                }

            for (int i = 0; i < Pilots.Count && !Over; i++)
            {
                var p = Pilots[i];
                if (!p.IsAlive && p.RespawnAt >= 0 && Elapsed >= p.RespawnAt) SpawnPilot(p);
            }
        }
    }
}
