using System.Collections.Generic;
using UnityEngine;

namespace RobotsFight
{
    public struct Objective
    {
        public Vector3 Pos;
        public float Radius;
        public Objective(Vector3 pos, float radius)
        {
            Pos = pos;
            Radius = radius;
        }
    }

    public struct MapMarker
    {
        public Vector3 Pos;
        public string Label;
        public Color Color;
        public MapMarker(Vector3 pos, string label, Color color)
        {
            Pos = pos;
            Label = label;
            Color = color;
        }
    }

    public abstract class GameMode
    {
        public Match M;
        protected Arena A => M.Arena;

        public abstract string Title { get; }
        public virtual float Duration => 0f;
        public virtual float RespawnDelay => 5f;
        public virtual bool BotsSwitchRobots => true;

        public abstract void Setup();
        public abstract Vector3 SpawnPoint(Pilot p, out Quaternion rot);
        public virtual void Tick(float dt) { }
        public virtual Objective? BotObjective(Robot r) => null;
        public virtual bool CanRespawn(Pilot p) => p.Drops != 0;
        public virtual void OnKilled(Pilot victim, Pilot killer) { }
        public virtual void OnTimeUp() { }
        public virtual void DrawHud(Hud h) { }
        public virtual void DrawWorld(Hud h) { }
        public virtual string Hint() => null;
        public virtual void MapMarkers(List<MapMarker> list) { }
        public virtual string DeadText(Pilot p) => p.RespawnAt >= 0
            ? $"Возвращение в бой через {Mathf.CeilToInt(p.RespawnAt - M.Elapsed)}"
            : "Роботы закончились — ты выбыл. Наблюдай за боем.";

        public static GameMode Create(ModeType t) => t switch
        {
            ModeType.Beacon => new BeaconMode(),
            ModeType.TeamDeathmatch => new TeamDeathmatchMode(),
            ModeType.FreeForAll => new FreeForAllMode(),
            ModeType.Survival => new SurvivalMode(),
            _ => new BombMode(),
        };

        public static string Describe(ModeType t) => t switch
        {
            ModeType.Beacon => "5 на 5. Захватывай маяки — они приносят очки. 1000 очков или пустой ангар врага = победа. У каждого 3 выхода в бой.",
            ModeType.TeamDeathmatch => "5 на 5. Первая команда, набравшая 25 уничтожений, побеждает.",
            ModeType.FreeForAll => "8 пилотов, каждый сам за себя. Кто первый наберёт 15 уничтожений.",
            ModeType.Survival => "Ты один против волн ботов. Каждая волна злее. 3 запасных робота.",
            _ => "5 на 5, раунды до 3 побед. Атака несёт бомбу на точку A или B, защита не даёт заложить или разминирует. Стороны меняются.",
        };

        public static string Name(ModeType t) => t switch
        {
            ModeType.Beacon => "Захват маяков",
            ModeType.TeamDeathmatch => "Командный бой",
            ModeType.FreeForAll => "Все против всех",
            ModeType.Survival => "Выживание",
            _ => "Бомба",
        };

        // ------------------------------------------------------------ Общие помощники
        protected void CreateTeams(int perTeam)
        {
            M.AddPilot(0, true);
            for (int i = 1; i < perTeam; i++) M.AddPilot(0, false);
            for (int i = 0; i < perTeam; i++) M.AddPilot(1, false);
        }

        protected void SpawnAll()
        {
            foreach (var p in M.Pilots) M.SpawnPilot(p);
        }

        protected bool Occupied(Vector3 pos, float r)
        {
            foreach (var p in M.Pilots)
                if (p.IsAlive && Vec.FlatDist(p.Body.transform.position, pos) < r) return true;
            return false;
        }

        protected Vector3 PickFree(List<Vector3> pts)
        {
            int start = Random.Range(0, pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[(start + i) % pts.Count];
                if (!Occupied(p, 6f)) return p;
            }
            var c = Random.insideUnitCircle * 4f;
            return pts[start] + new Vector3(c.x, 0, c.y);
        }

        /// <summary>side 0 — юг (смотрит на север), side 1 — север.</summary>
        protected Vector3 TeamSpawn(int side, out Quaternion rot)
        {
            rot = Quaternion.Euler(0, side == 0 ? 0 : 180, 0);
            return PickFree(side == 0 ? A.SouthSpawns : A.NorthSpawns);
        }

        protected static string TeamName(int t) => t == 0 ? "Синие" : "Красные";
        protected static string Fmt(float s)
        {
            int t = Mathf.Max(0, Mathf.CeilToInt(s));
            return $"{t / 60}:{t % 60:00}";
        }

        protected void EndTeams(int winner, string details)
        {
            if (winner < 0) M.End("НИЧЬЯ", details, false);
            else
            {
                bool won = winner == M.Player.Team;
                M.End(won ? "ПОБЕДА" : "ПОРАЖЕНИЕ", $"{TeamName(winner)} победили. {details}", won);
            }
        }
    }

    // ================================================================= Командный бой
    public class TeamDeathmatchMode : GameMode
    {
        const int Limit = 25;
        readonly int[] kills = new int[2];

        public override string Title => "Командный бой";
        public override float Duration => 360f;

        public override void Setup()
        {
            CreateTeams(5);
            SpawnAll();
            M.ShowBanner($"Командный бой: до {Limit} уничтожений", Color.white);
        }

        public override Vector3 SpawnPoint(Pilot p, out Quaternion rot) => TeamSpawn(p.Team, out rot);

        public override void OnKilled(Pilot victim, Pilot killer)
        {
            if (killer == null || killer.Team == victim.Team) return;
            kills[killer.Team]++;
            if (kills[killer.Team] >= Limit) EndTeams(killer.Team, $"Счёт {kills[0]}:{kills[1]}");
        }

        public override void OnTimeUp() =>
            EndTeams(kills[0] > kills[1] ? 0 : kills[1] > kills[0] ? 1 : -1, $"Время вышло. Счёт {kills[0]}:{kills[1]}");

        public override void DrawHud(Hud h) => h.TeamScore(kills[0], kills[1], Limit, $"до {Limit} уничтожений");
    }

    // ================================================================= Все против всех
    public class FreeForAllMode : GameMode
    {
        const int Limit = 15;

        public override string Title => "Все против всех";
        public override float Duration => 300f;
        public override float RespawnDelay => 4f;

        public override void Setup()
        {
            var me = M.AddPilot(0, true);
            me.Color = Palette.Ffa(0);
            for (int i = 1; i < 8; i++) M.AddPilot(i, false).Color = Palette.Ffa(i);
            SpawnAll();
            M.ShowBanner($"Все против всех: до {Limit} уничтожений", Color.white);
        }

        public override Vector3 SpawnPoint(Pilot p, out Quaternion rot)
        {
            // Точка, максимально далёкая от ближайшего живого робота
            Vector3 best = A.RingSpawns[0];
            float bestD = -1f;
            foreach (var pt in A.RingSpawns)
            {
                float minD = float.MaxValue;
                foreach (var o in M.Pilots)
                    if (o != p && o.IsAlive) minD = Mathf.Min(minD, Vec.FlatDist(o.Body.transform.position, pt));
                minD += Random.Range(0f, 10f);
                if (minD > bestD)
                {
                    bestD = minD;
                    best = pt;
                }
            }
            rot = Quaternion.LookRotation(Vec.Flat(-best));
            return best;
        }

        Pilot Leader()
        {
            Pilot best = null;
            foreach (var p in M.Pilots)
                if (best == null || p.Kills > best.Kills || (p.Kills == best.Kills && p.Score > best.Score)) best = p;
            return best;
        }

        int Place(Pilot who)
        {
            int place = 1;
            foreach (var p in M.Pilots)
                if (p != who && (p.Kills > who.Kills || (p.Kills == who.Kills && p.Score > who.Score))) place++;
            return place;
        }

        void Finish(Pilot w)
        {
            bool won = w == M.Player;
            M.End(won ? "ПОБЕДА" : "ПОРАЖЕНИЕ",
                $"Лучший пилот: {w.Name} ({w.Kills} уничтожений). Твоё место: #{Place(M.Player)} из {M.Pilots.Count}", won);
        }

        public override void OnKilled(Pilot victim, Pilot killer)
        {
            if (killer != null && killer != victim && killer.Kills >= Limit) Finish(killer);
        }

        public override void OnTimeUp() => Finish(Leader());

        public override void DrawHud(Hud h)
        {
            var l = Leader();
            h.CenterText(14, $"Лидер: {l.Name} — {l.Kills}", h.SMid, l.Color);
            h.CenterText(48, $"Ты: {M.Player.Kills} · место #{Place(M.Player)} из {M.Pilots.Count} · до {Limit}", h.SSmall, Color.white);
        }
    }

    // ================================================================= Захват маяков
    public class Beacon
    {
        public const float Radius = 8f;
        public Vector3 Pos;
        public string Letter;
        public float Control; // -1 = синие, +1 = красные
        public int Owner = -1;
        public bool Contested;
        public Renderer Flag, Disc;
    }

    public class BeaconMode : GameMode
    {
        const float WinScore = 1000f, PointsPerBeacon = 2.5f, CapRate = 1f / 6f;
        readonly List<Beacon> beacons = new();
        readonly float[] score = new float[2];
        readonly Dictionary<Pilot, (int idx, float until)> plan = new();

        public override string Title => "Захват маяков";
        public override float Duration => 480f;
        public override float RespawnDelay => 4f;

        public override void Setup()
        {
            CreateTeams(5);
            foreach (var p in M.Pilots) p.Drops = 3;
            const string letters = "ABCDE";
            for (int i = 0; i < A.BeaconPoints.Length; i++)
            {
                var b = new Beacon { Pos = A.BeaconPoints[i], Letter = letters[i].ToString() };
                var root = Shapes.Node($"Beacon_{b.Letter}", A.Root, b.Pos);
                b.Disc = Shapes.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.03f, 0), new Vector3(Beacon.Radius * 2, 0.03f, Beacon.Radius * 2), Palette.Neutral * 0.5f).GetComponent<Renderer>();
                Shapes.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 3f, 0), new Vector3(0.35f, 3f, 0.35f), new Color(0.3f, 0.3f, 0.33f), collider: true);
                b.Flag = Shapes.Prim(PrimitiveType.Cube, root, new Vector3(0, 6.6f, 0), Vector3.one * 1.3f, Palette.Neutral, glow: true).GetComponent<Renderer>();
                beacons.Add(b);
            }
            SpawnAll();
            M.ShowBanner("Захват маяков: держи точки, копи очки", Color.white);
        }

        public override Vector3 SpawnPoint(Pilot p, out Quaternion rot) => TeamSpawn(p.Team, out rot);

        public override void Tick(float dt)
        {
            foreach (var b in beacons)
            {
                int n0 = 0, n1 = 0;
                foreach (var p in M.Pilots)
                {
                    if (!p.IsAlive || Vec.FlatDist(p.Body.transform.position, b.Pos) > Beacon.Radius) continue;
                    if (p.Team == 0) n0++;
                    else n1++;
                }
                b.Contested = n0 > 0 && n1 > 0;
                if (!b.Contested)
                {
                    if (n0 > 0) b.Control = Mathf.MoveTowards(b.Control, -1f, CapRate * Mathf.Min(n0, 3) * dt);
                    else if (n1 > 0) b.Control = Mathf.MoveTowards(b.Control, 1f, CapRate * Mathf.Min(n1, 3) * dt);
                }

                int prev = b.Owner;
                if (b.Control <= -1f) b.Owner = 0;
                else if (b.Control >= 1f) b.Owner = 1;
                else if ((b.Owner == 0 && b.Control > 0) || (b.Owner == 1 && b.Control < 0)) b.Owner = -1;
                if (prev != b.Owner)
                {
                    if (b.Owner >= 0)
                    {
                        M.AddFeed($"{TeamName(b.Owner)} захватили маяк {b.Letter}", Palette.Team(b.Owner));
                        if (b.Owner == M.Player.Team) M.ShowBanner($"Маяк {b.Letter} наш!", Palette.Team(b.Owner), 2f);
                    }
                    else M.AddFeed($"Маяк {b.Letter} нейтрализован", Palette.Neutral);
                }

                b.Flag.sharedMaterial = Mats.Get(b.Owner >= 0 ? Palette.Team(b.Owner) : Palette.Neutral, true);
                b.Flag.transform.Rotate(0, 90f * dt, 0);
                float t = Mathf.Round((b.Control + 1f) * 5f) / 10f; // квантуем, чтобы не плодить материалы
                b.Disc.sharedMaterial = Mats.Get(Color.Lerp(Palette.Blue, Palette.Red, t) * 0.55f);

                if (b.Owner >= 0) score[b.Owner] += PointsPerBeacon * dt;
            }

            for (int t = 0; t < 2; t++)
                if (score[t] >= WinScore)
                {
                    EndTeams(t, $"Очки {(int)score[0]}:{(int)score[1]}");
                    return;
                }
            for (int t = 0; t < 2; t++)
                if (TeamOut(t))
                {
                    EndTeams(1 - t, "Ангар противника пуст");
                    return;
                }
        }

        bool TeamOut(int team)
        {
            foreach (var p in M.Pilots)
                if (p.Team == team && (p.IsAlive || p.Drops != 0)) return false;
            return true;
        }

        public override void OnTimeUp() =>
            EndTeams(score[0] > score[1] ? 0 : score[1] > score[0] ? 1 : -1, $"Время вышло. Очки {(int)score[0]}:{(int)score[1]}");

        public override Objective? BotObjective(Robot r)
        {
            var p = r.Pilot;
            bool replan = !plan.TryGetValue(p, out var pl) || Time.time > pl.until;
            if (!replan && pl.idx >= 0)
            {
                var cur = beacons[pl.idx];
                if (cur.Owner == p.Team && Mathf.Abs(cur.Control) >= 1f && !cur.Contested) replan = true;
            }
            if (replan)
            {
                pl = (ChooseBeacon(r), Time.time + Random.Range(6f, 12f));
                plan[p] = pl;
            }
            return pl.idx < 0 ? null : new Objective(beacons[pl.idx].Pos, Beacon.Radius * 0.55f);
        }

        int ChooseBeacon(Robot r)
        {
            int best = -1;
            float bestCost = float.MaxValue;
            for (int i = 0; i < beacons.Count; i++)
            {
                var b = beacons[i];
                float cost = Vec.FlatDist(r.transform.position, b.Pos) + Random.Range(0f, 50f);
                bool secured = b.Owner == r.Team && Mathf.Abs(b.Control) >= 1f;
                if (secured) cost += 400f;
                if (b.Contested) cost -= 25f;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = i;
                }
            }
            return best;
        }

        public override void DrawHud(Hud h)
        {
            h.TeamScore(score[0], score[1], WinScore, "");
            float w = 46f, gap = 8f;
            float x0 = h.W / 2 - (beacons.Count * (w + gap) - gap) / 2;
            for (int i = 0; i < beacons.Count; i++)
            {
                var b = beacons[i];
                var r = new Rect(x0 + i * (w + gap), 76, w, w);
                Color c = b.Owner >= 0 ? Palette.Team(b.Owner) : new Color(0.35f, 0.35f, 0.35f);
                if (b.Contested && Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.25f) c = Color.white;
                h.Box(r, new Color(0, 0, 0, 0.55f));
                h.Box(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 12), c * 0.85f);
                h.Label(new Rect(r.x, r.y + 1, r.width, r.height - 8), b.Letter, h.SMidCenter, Color.white);
                float f = Mathf.Abs(b.Control);
                h.Box(new Rect(r.x + 3, r.yMax - 7, (r.width - 6) * f, 4), b.Control < 0 ? Palette.Blue : Palette.Red);
            }
            h.CenterText(128, $"Выходов в бой осталось: {M.Player.Drops}", h.SSmall, new Color(1, 1, 1, 0.8f));
        }

        public override void DrawWorld(Hud h)
        {
            foreach (var b in beacons)
                h.Marker(b.Pos + Vector3.up * 8.5f, b.Letter, b.Owner >= 0 ? Palette.Team(b.Owner) : Palette.Neutral);
        }

        public override void MapMarkers(List<MapMarker> list)
        {
            foreach (var b in beacons)
                list.Add(new MapMarker(b.Pos, b.Letter, b.Owner >= 0 ? Palette.Team(b.Owner) : Palette.Neutral));
        }

        public override string Hint()
        {
            var me = M.Player;
            if (!me.IsAlive) return null;
            foreach (var b in beacons)
            {
                if (Vec.FlatDist(me.Body.transform.position, b.Pos) > Beacon.Radius) continue;
                if (b.Contested) return $"Маяк {b.Letter} оспаривается — выбей врагов!";
                if (b.Owner != me.Team || Mathf.Abs(b.Control) < 1f) return $"Захват маяка {b.Letter}...";
            }
            return null;
        }
    }

    // ================================================================= Выживание
    public class SurvivalMode : GameMode
    {
        const string BestKey = "rf_best_wave";
        int wave;
        bool fighting;
        float nextWaveAt;

        public override string Title => "Выживание";
        public override float RespawnDelay => 4f;
        public override bool BotsSwitchRobots => false;
        public override bool CanRespawn(Pilot p) => p.IsPlayer && p.Drops != 0;

        public override void Setup()
        {
            var me = M.AddPilot(0, true);
            me.Drops = 4; // 1 + 3 запасных
            M.SpawnPilot(me);
            nextWaveAt = M.Elapsed + 4f;
            M.ShowBanner("Выживание: держись сколько сможешь", Color.white);
        }

        public override Vector3 SpawnPoint(Pilot p, out Quaternion rot)
        {
            if (p.IsPlayer)
            {
                rot = Quaternion.identity;
                var c = Random.insideUnitCircle * 5f;
                return new Vector3(c.x, 0.1f, c.y);
            }
            // Враги — на кольце, подальше от игрока
            Vector3 me = M.Player.IsAlive ? M.Player.Body.transform.position : Vector3.zero;
            var pts = new List<Vector3>(A.RingSpawns);
            pts.Sort((a, b) => Vec.FlatDist(b, me).CompareTo(Vec.FlatDist(a, me)));
            pts.RemoveRange(6, pts.Count - 6);
            var pos = PickFree(pts);
            rot = Quaternion.LookRotation(Vec.Flat(me - pos));
            return pos;
        }

        public override void Tick(float dt)
        {
            if (fighting)
            {
                if (M.AliveCount(1) == 0)
                {
                    fighting = false;
                    nextWaveAt = M.Elapsed + 7f;
                    M.ShowBanner($"Волна {wave} отбита! Ремонт...", new Color(0.4f, 1f, 0.5f));
                    if (M.Player.IsAlive) M.Player.Body.RepairFull();
                }
            }
            else if (M.Elapsed >= nextWaveAt) StartWave();
        }

        void StartWave()
        {
            wave++;
            fighting = true;
            M.Pilots.RemoveAll(p => !p.IsPlayer && !p.IsAlive);
            int count = Mathf.Min(2 + wave, 12);
            for (int i = 0; i < count; i++)
            {
                var e = M.AddPilot(1, false, PickDef(i));
                e.HpMult = 1f + 0.08f * (wave - 1);
                e.Drops = 1;
                M.SpawnPilot(e);
            }
            M.ShowBanner($"Волна {wave}: врагов {count}", Palette.Red);
        }

        RobotDef PickDef(int i)
        {
            int maxIndex = wave <= 2 ? 0 : wave <= 4 ? 1 : wave <= 6 ? 2 : 3;
            // порядок открытия: Стриж → Сокол → Гризли → Титан
            int[] order = { 0, 3, 1, 2 };
            return Catalog.Robots[order[Random.Range(0, maxIndex + 1)]];
        }

        public override void OnKilled(Pilot victim, Pilot killer)
        {
            if (!victim.IsPlayer || victim.Drops != 0) return;
            int survived = Mathf.Max(0, wave - (fighting ? 1 : 0));
            int best = Mathf.Max(PlayerPrefs.GetInt(BestKey, 0), survived);
            PlayerPrefs.SetInt(BestKey, best);
            M.End("ИГРА ОКОНЧЕНА", $"Пережито волн: {survived}. Рекорд: {best}. Уничтожено: {victim.Kills}", false);
        }

        public override Objective? BotObjective(Robot r)
        {
            if (r.Team == 0 || !M.Player.IsAlive) return null;
            return new Objective(M.Player.Body.transform.position, r.Def.PreferredRange * 0.8f);
        }

        public override void DrawHud(Hud h)
        {
            h.CenterText(14, $"Волна {wave}", h.SMid, Color.white);
            string sub = fighting
                ? $"Врагов: {M.AliveCount(1)} · Запасных роботов: {M.Player.Drops}"
                : $"Следующая волна через {Mathf.CeilToInt(nextWaveAt - M.Elapsed)} · Запасных роботов: {M.Player.Drops}";
            h.CenterText(48, sub, h.SSmall, Color.white);
        }
    }

    // ================================================================= Бомба
    public class BombMode : GameMode
    {
        enum BombState { Dropped, Carried, Planted, Done }

        const int WinRounds = 3;
        const float RoundTime = 150f, PlantTime = 3f, DefuseTime = 5f, FuseTime = 35f, SiteRadius = 9f, BlastRadius = 26f;
        static readonly string[] SiteNames = { "A", "B" };

        readonly int[] wins = new int[2];
        int round, attackers, targetSite, plantedSite = -1;
        BombState state;
        Pilot carrier;
        Vector3 bombPos;
        float plantProgress, defuseProgress, fuse, roundLeft, nextRoundAt = -1f;
        Transform bombVis;

        int Defenders => 1 - attackers;

        public override string Title => "Бомба";
        public override bool CanRespawn(Pilot p) => false;

        public override void Setup()
        {
            CreateTeams(5);
            for (int i = 0; i < A.BombSites.Length; i++)
            {
                var root = Shapes.Node($"Site_{SiteNames[i]}", A.Root, A.BombSites[i]);
                Shapes.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.03f, 0), new Vector3(SiteRadius * 2, 0.03f, SiteRadius * 2), new Color(0.55f, 0.35f, 0.1f));
                for (int k = 0; k < 4; k++)
                {
                    var a = k * Mathf.PI / 2 + Mathf.PI / 4;
                    Shapes.Prim(PrimitiveType.Cube, root, new Vector3(Mathf.Cos(a) * SiteRadius, 1.5f, Mathf.Sin(a) * SiteRadius),
                        new Vector3(0.5f, 3f, 0.5f), new Color(1f, 0.6f, 0.1f), collider: true, glow: true);
                }
            }
            bombVis = Shapes.Prim(PrimitiveType.Cube, A.Root, A.BombStart, new Vector3(0.9f, 0.6f, 0.6f), new Color(1f, 0.15f, 0.1f), glow: true);
            bombVis.name = "Bomb";
            StartRound();
        }

        void StartRound()
        {
            round++;
            attackers = round % 2 == 1 ? 0 : 1;
            foreach (var p in M.Pilots)
            {
                M.DespawnPilot(p);
                p.RespawnAt = -1f;
                if (!p.IsPlayer) p.NextDef = Catalog.RandomRobot();
            }
            foreach (var p in M.Pilots) M.SpawnPilot(p);

            state = BombState.Dropped;
            bombPos = A.BombStart;
            carrier = null;
            plantProgress = defuseProgress = 0;
            plantedSite = -1;
            roundLeft = RoundTime;
            targetSite = Random.Range(0, 2);
            bool att = M.Player.Team == attackers;
            M.ShowBanner(att ? $"Раунд {round} — АТАКА: доставь бомбу на A или B" : $"Раунд {round} — ЗАЩИТА: не дай заложить бомбу",
                att ? Palette.Gold : new Color(0.5f, 0.85f, 1f), 4f);
        }

        public override Vector3 SpawnPoint(Pilot p, out Quaternion rot) => TeamSpawn(p.Team == attackers ? 0 : 1, out rot);

        int SiteAt(Vector3 pos)
        {
            for (int i = 0; i < A.BombSites.Length; i++)
                if (Vec.FlatDist(pos, A.BombSites[i]) <= SiteRadius) return i;
            return -1;
        }

        public override void Tick(float dt)
        {
            if (nextRoundAt >= 0)
            {
                if (M.Elapsed >= nextRoundAt)
                {
                    nextRoundAt = -1f;
                    StartRound();
                }
                UpdateBombVis();
                return;
            }

            if (state != BombState.Planted) roundLeft -= dt;
            switch (state)
            {
                case BombState.Dropped:
                    foreach (var p in M.Pilots)
                    {
                        if (p.Team != attackers || !p.IsAlive) continue;
                        if (Vec.FlatDist(p.Body.transform.position, bombPos) < 2.8f + p.Body.Cc.radius)
                        {
                            carrier = p;
                            state = BombState.Carried;
                            M.AddFeed($"{p.Name} подобрал бомбу", p.Color);
                            break;
                        }
                    }
                    break;

                case BombState.Carried:
                    if (!carrier.IsAlive)
                    {
                        DropBomb();
                        break;
                    }
                    bombPos = carrier.Body.transform.position;
                    int site = SiteAt(bombPos);
                    if (site >= 0 && carrier.Body.Interact)
                    {
                        plantProgress += dt;
                        if (plantProgress >= PlantTime) Plant(site);
                    }
                    else plantProgress = 0;
                    break;

                case BombState.Planted:
                    fuse -= dt;
                    bool defusing = false;
                    foreach (var p in M.Pilots)
                        if (p.Team == Defenders && p.IsAlive && p.Body.Interact &&
                            Vec.FlatDist(p.Body.transform.position, bombPos) < 3.5f + p.Body.Cc.radius)
                            defusing = true;
                    if (defusing)
                    {
                        defuseProgress += dt;
                        if (defuseProgress >= DefuseTime)
                        {
                            RoundOver(Defenders, "бомба обезврежена");
                            return;
                        }
                    }
                    else defuseProgress = 0;
                    if (fuse <= 0)
                    {
                        Detonate();
                        return;
                    }
                    break;
            }

            int att = M.AliveCount(attackers), def = M.AliveCount(Defenders);
            if (def == 0) RoundOver(attackers, "защита уничтожена");
            else if (att == 0 && state != BombState.Planted) RoundOver(Defenders, "атака уничтожена");
            else if (state != BombState.Planted && roundLeft <= 0) RoundOver(Defenders, "время вышло");
            UpdateBombVis();
        }

        void DropBomb()
        {
            if (carrier != null) M.AddFeed($"{carrier.Name} потерял бомбу", carrier.Color);
            state = BombState.Dropped;
            carrier = null;
            plantProgress = 0;
            bombPos.y = 0;
        }

        void Plant(int site)
        {
            state = BombState.Planted;
            plantedSite = site;
            bombPos.y = 0;
            fuse = FuseTime;
            defuseProgress = 0;
            M.AddFeed($"{carrier.Name} заложил бомбу на {SiteNames[site]}", carrier.Color);
            carrier = null;
            M.ShowBanner($"Бомба заложена на точке {SiteNames[site]}!", Palette.Red, 3f);
        }

        void Detonate()
        {
            state = BombState.Done;
            Fx.Explosion(bombPos, 16f, new Color(1f, 0.5f, 0.1f));
            Fx.Explosion(bombPos + Vector3.up * 4f, 10f, Color.white);
            foreach (var p in M.Pilots)
                if (p.IsAlive && Vec.FlatDist(p.Body.transform.position, bombPos) < BlastRadius)
                    M.Damage(p.Body, 99999f, null, null, bombPos);
            RoundOver(attackers, "бомба взорвалась");
        }

        void RoundOver(int winner, string reason)
        {
            if (nextRoundAt >= 0 || M.Over) return;
            state = BombState.Done;
            wins[winner]++;
            M.AddFeed($"Раунд {round}: {TeamName(winner)} — {reason}", Palette.Team(winner));
            M.ShowBanner($"Раунд за {TeamName(winner)}: {reason}", Palette.Team(winner), 4.5f);
            if (wins[winner] >= WinRounds) EndTeams(winner, $"Раунды {wins[0]}:{wins[1]}");
            else nextRoundAt = M.Elapsed + 5f;
        }

        public override void OnKilled(Pilot victim, Pilot killer)
        {
            if (state == BombState.Carried && victim == carrier) DropBomb();
        }

        void UpdateBombVis()
        {
            if (!bombVis) return;
            bool show = state != BombState.Done || nextRoundAt >= 0;
            bombVis.gameObject.SetActive(show);
            if (state == BombState.Carried && carrier != null && carrier.IsAlive) bombVis.position = carrier.Body.Top;
            else bombVis.position = bombPos + Vector3.up * 0.35f;
            float pulse = state == BombState.Planted ? 1f + 0.25f * Mathf.Sin(Time.time * (6f + (FuseTime - fuse) * 0.5f)) : 1f;
            bombVis.localScale = new Vector3(0.9f, 0.6f, 0.6f) * pulse;
        }

        public override Objective? BotObjective(Robot r)
        {
            if (nextRoundAt >= 0 || state == BombState.Done) return null;
            var p = r.Pilot;
            var site = A.BombSites[targetSite];
            if (p.Team == attackers)
            {
                return state switch
                {
                    BombState.Dropped => new Objective(bombPos, 0.5f),
                    BombState.Carried => p == carrier ? new Objective(site, 3f) : new Objective(site, 14f),
                    _ => new Objective(bombPos, 8f),
                };
            }
            if (state == BombState.Planted) return new Objective(bombPos, 1.5f);
            if (state == BombState.Carried && carrier != null && carrier.IsAlive)
            {
                // Носитель близко к точке — все на неё
                var cpos = carrier.Body.transform.position;
                for (int i = 0; i < A.BombSites.Length; i++)
                    if (Vec.FlatDist(cpos, A.BombSites[i]) < 45f) return new Objective(A.BombSites[i], 6f);
            }
            return new Objective(A.BombSites[M.Pilots.IndexOf(p) % 2], 7f);
        }

        public override string DeadText(Pilot p) => M.Over ? "" : "Ты уничтожен. Жди следующего раунда.";

        public override string Hint()
        {
            var me = M.Player;
            if (!me.IsAlive || nextRoundAt >= 0) return null;
            bool att = me.Team == attackers;
            switch (state)
            {
                case BombState.Dropped:
                    return att ? "Подбери бомбу (подойди к ней)" : "Защищай точки A и B";
                case BombState.Carried:
                    if (carrier != me) return att ? "Прикрывай носителя бомбы" : "Уничтожь носителя бомбы!";
                    return SiteAt(bombPos) >= 0 ? "Держи [E] — закладка бомбы" : "У тебя бомба! Неси на точку A или B";
                case BombState.Planted:
                    if (att) return "Защищай бомбу до взрыва";
                    return Vec.FlatDist(me.Body.transform.position, bombPos) < 3.5f + me.Body.Cc.radius
                        ? "Держи [E] — разминирование"
                        : $"Бомба на точке {SiteNames[plantedSite]}! Беги разминировать";
            }
            return null;
        }

        public override void DrawHud(Hud h)
        {
            h.TeamScore(wins[0], wins[1], WinRounds, "");
            bool att = M.Player.Team == attackers;
            h.CenterText(78, $"Раунд {round} · {(att ? "АТАКА" : "ЗАЩИТА")}", h.SSmall, att ? Palette.Gold : new Color(0.5f, 0.85f, 1f));
            if (state == BombState.Planted)
                h.CenterText(102, $"ВЗРЫВ ЧЕРЕЗ {Mathf.CeilToInt(fuse)}", h.SMid, Mathf.Repeat(Time.time, 1f) < 0.5f ? Palette.Red : Color.white);
            else if (nextRoundAt < 0)
                h.CenterText(102, Fmt(roundLeft), h.SMid, Color.white);

            float prog = state == BombState.Planted ? defuseProgress / DefuseTime : plantProgress / PlantTime;
            if (prog > 0)
            {
                var r = new Rect(h.W / 2 - 160, 700, 320, 16);
                h.Bar(r, prog, state == BombState.Planted ? new Color(0.4f, 0.8f, 1f) : Palette.Gold);
                h.CenterText(672, state == BombState.Planted ? "Разминирование" : "Закладка", h.SSmall, Color.white);
            }
        }

        public override void DrawWorld(Hud h)
        {
            for (int i = 0; i < A.BombSites.Length; i++)
                h.Marker(A.BombSites[i] + Vector3.up * 6f, SiteNames[i], new Color(1f, 0.6f, 0.1f));
            if (state != BombState.Done && !(state == BombState.Carried && carrier == M.Player))
                h.Marker(bombVis.position + Vector3.up * 2.5f, "БОМБА", Palette.Red);
        }

        public override void MapMarkers(List<MapMarker> list)
        {
            for (int i = 0; i < A.BombSites.Length; i++)
                list.Add(new MapMarker(A.BombSites[i], SiteNames[i], new Color(1f, 0.6f, 0.1f)));
            if (state != BombState.Done) list.Add(new MapMarker(bombVis.position, "Б", Palette.Red));
        }
    }
}
