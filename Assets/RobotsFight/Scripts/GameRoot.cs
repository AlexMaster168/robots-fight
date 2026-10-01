using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RobotsFight
{
    /// <summary>
    /// Точка входа. Создаётся сама при старте любой сцены — достаточно нажать Play.
    /// Меню (с демо-боем ботов на фоне), пауза, экран смерти, итоги, автотест (-autotest).
    /// </summary>
    public class GameRoot : MonoBehaviour
    {
        enum State { Menu, Playing }

        static GameRoot I;
        public static bool InputBlocked => I == null || I.state != State.Playing || I.paused || !Match.I || Match.I.Over;

        readonly Hud hud = new();
        State state = State.Menu;
        bool paused;
        ModeType mode;
        int robotIndex;
        int mapIndex = -1; // -1 — случайная локация
        Difficulty difficulty = Difficulty.Normal;
        float demoRestartAt = -1f;

        bool autotest;
        int errors;
        readonly List<string> errorSamples = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameRoot>()) return;
            var go = new GameObject("RobotsFight");
            DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>();
        }

        void Awake()
        {
            I = this;
            var cam = Camera.main;
            if (!cam)
            {
                var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cgo.AddComponent<Camera>();
                cgo.AddComponent<AudioListener>();
            }
            var pc = cam.GetComponent<PlayerCamera>();
            if (!pc) pc = cam.gameObject.AddComponent<PlayerCamera>();
            pc.Sensitivity = PlayerPrefs.GetFloat("rf_sens", 2.5f);
            Arena.SetupEnvironment();

            mode = (ModeType)Mathf.Clamp(PlayerPrefs.GetInt("rf_mode", 0), 0, 4);
            robotIndex = Mathf.Clamp(PlayerPrefs.GetInt("rf_robot", 0), 0, Catalog.Robots.Length - 1);
            difficulty = (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt("rf_diff", 1), 0, 2);
            mapIndex = Mathf.Clamp(PlayerPrefs.GetInt("rf_map", -1), -1, Maps.All.Length - 1);

            var args = System.Environment.GetCommandLineArgs();
            autotest = args.Contains("-autotest");
            if (autotest)
            {
                Application.logMessageReceived += OnLog;
                StartCoroutine(Autotest());
            }
            else
            {
                var music = gameObject.AddComponent<MusicPlayer>();
                // Все треки синтезируются заранее в фоне — на старте боя музыка уже готова
                music.Prewarm(MusicStyle.Menu);
                foreach (var map in Maps.All) music.Prewarm(map.Music);
                StartDemo();
            }
            if (args.Contains("-shots")) StartCoroutine(Screenshots());
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        // ---------------------------------------------------------------- Управление матчами
        void StartDemo()
        {
            StopMatch();
            Match.Begin(ModeType.TeamDeathmatch, Catalog.RandomRobot(), Difficulty.Normal, true, Maps.Random());
            state = State.Menu;
            demoRestartAt = -1f;
            Time.timeScale = 1f;
            if (MusicPlayer.I) MusicPlayer.I.Play(MusicStyle.Menu);
        }

        void StartMatch()
        {
            StopMatch();
            PlayerPrefs.SetInt("rf_mode", (int)mode);
            PlayerPrefs.SetInt("rf_robot", robotIndex);
            PlayerPrefs.SetInt("rf_diff", (int)difficulty);
            PlayerPrefs.SetInt("rf_map", mapIndex);
            var map = mapIndex < 0 ? Maps.Random() : Maps.All[mapIndex];
            Match.Begin(mode, Catalog.Robots[robotIndex], difficulty, false, map);
            state = State.Playing;
            SetPaused(false);
            if (MusicPlayer.I) MusicPlayer.I.Play(map.Music);
        }

        static void StopMatch()
        {
            if (Match.I) Destroy(Match.I.gameObject);
            Match.I = null;
        }

        void ToMenu()
        {
            SetPaused(false);
            StartDemo();
        }

        void SetPaused(bool p)
        {
            paused = p;
            Time.timeScale = p ? 0f : 1f;
        }

        void Update()
        {
            if (autotest) return;
            var m = Match.I;
            if (GameInput.Mute && MusicPlayer.I) MusicPlayer.I.ToggleMute();

            if (state == State.Menu)
            {
                SetCursor(false);
                if (m && m.Over && demoRestartAt < 0) demoRestartAt = Time.unscaledTime + 4f;
                if (!m || (demoRestartAt > 0 && Time.unscaledTime > demoRestartAt)) StartDemo();
                if (GameInput.Confirm) StartMatch();
                return;
            }

            if (!m)
            {
                ToMenu();
                return;
            }
            if (GameInput.Pause && !m.Over) SetPaused(!paused);
            if (!m.Player.IsAlive && !m.Over)
                for (int n = 1; n <= Catalog.Robots.Length; n++)
                    if (GameInput.Number(n)) m.Player.NextDef = Catalog.Robots[n - 1];
            SetCursor(!paused && !m.Over && m.Player.IsAlive && !m.Autopilot);
        }

        static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // ---------------------------------------------------------------- GUI
        void OnGUI()
        {
            if (autotest || Screen.height <= 0) return;
            hud.Begin();
            if (state == State.Menu)
            {
                DrawMenu();
                return;
            }
            var m = Match.I;
            if (!m) return;
            hud.Draw(m);
            if (!m.Player.IsAlive && !m.Over) DrawDeath(m);
            if (m.Over) DrawEnd(m);
            else if (paused) DrawPause();
            else if (GameInput.Scoreboard) hud.Scoreboard(m, 170);
        }

        bool Button(Rect r, string text, bool selected = false, Color? tint = null, GUIStyle style = null)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = selected ? (tint ?? new Color(0.3f, 0.7f, 1f)) * 1.6f : new Color(0.55f, 0.55f, 0.6f);
            bool click = GUI.Button(r, text, style ?? hud.SButton);
            GUI.backgroundColor = old;
            return click;
        }

        void DrawMenu()
        {
            float W = hud.W, cx = W / 2;
            hud.Box(new Rect(0, 0, W, Hud.H), new Color(0.02f, 0.03f, 0.06f, 0.55f));
            hud.Label(new Rect(0, 30, W, 80), "БИТВА РОБОТОВ", hud.SBig, Palette.Gold);
            hud.Label(new Rect(0, 104, W, 30), "меха-бои в духе War Robots и Armored Squad", hud.SSmall, new Color(1, 1, 1, 0.75f));

            // Режимы
            var modes = (ModeType[])System.Enum.GetValues(typeof(ModeType));
            float bw = 230, gap = 12, x0 = cx - (modes.Length * (bw + gap) - gap) / 2;
            for (int i = 0; i < modes.Length; i++)
                if (Button(new Rect(x0 + i * (bw + gap), 160, bw, 58), GameMode.Name(modes[i]), mode == modes[i]))
                    mode = modes[i];
            string desc = GameMode.Describe(mode);
            if (mode == ModeType.Survival) desc += $"  Рекорд: {PlayerPrefs.GetInt("rf_best_wave", 0)} волн.";
            hud.Label(new Rect(cx - 600, 226, 1200, 56), desc, hud.SSmall, Color.white);

            // Роботы
            float cw = 290, ch = 290;
            x0 = cx - (Catalog.Robots.Length * (cw + gap) - gap) / 2;
            for (int i = 0; i < Catalog.Robots.Length; i++)
            {
                var d = Catalog.Robots[i];
                var r = new Rect(x0 + i * (cw + gap), 290, cw, ch);
                bool sel = robotIndex == i;
                hud.Box(r, sel ? new Color(0.15f, 0.35f, 0.6f, 0.85f) : new Color(0, 0, 0, 0.6f));
                if (sel) hud.Box(new Rect(r.x, r.y, r.width, 4), Palette.Gold);
                float y = r.y + 12;
                hud.Label(new Rect(r.x, y, cw, 36), $"{i + 1}. {d.Name}", hud.SMid, Color.white);
                hud.Label(new Rect(r.x, y + 36, cw, 24), d.Role, hud.SSmall, Palette.Gold);
                y += 70;
                Stat(r.x + 16, y, "Прочность", d.Hp / 4200f, $"{d.Hp:0}");
                Stat(r.x + 16, y + 34, "Скорость", d.Speed / 10f, $"{d.Speed:0.#}");
                var wnames = string.Join(", ", d.Weapons.GroupBy(w => w.Name).Select(g => g.Count() > 1 ? $"{g.Count()}× {g.Key}" : g.Key));
                hud.Label(new Rect(r.x + 16, y + 70, cw - 32, 50), wnames, hud.STiny, new Color(1f, 0.85f, 0.6f));
                hud.Label(new Rect(r.x + 16, y + 118, cw - 32, 24), $"Способность: {d.AbilityName}", hud.STiny, new Color(0.5f, 0.9f, 1f));
                hud.Label(new Rect(r.x + 16, y + 142, cw - 32, 62), d.Description, hud.STiny, new Color(1, 1, 1, 0.75f));
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) robotIndex = i;
            }

            // Локации
            float mw = 160, mg = 8;
            int mapCount = Maps.All.Length + 1;
            x0 = cx - (mapCount * (mw + mg) - mg) / 2;
            for (int i = -1; i < Maps.All.Length; i++)
            {
                string label = i < 0 ? "Случайная" : Maps.All[i].Name;
                if (Button(new Rect(x0 + (i + 1) * (mw + mg), 596, mw, 44), label, mapIndex == i, new Color(0.4f, 1f, 0.6f), hud.SButtonSmall))
                    mapIndex = i;
            }
            string mapDesc = mapIndex < 0 ? "Каждый бой — на случайной из 6 локаций." : Maps.All[mapIndex].Description;
            hud.Label(new Rect(cx - 600, 644, 1200, 30), mapDesc, hud.SSmall, new Color(0.7f, 1f, 0.8f));

            // Сложность
            string[] diffs = { "Лёгкие боты", "Нормальные", "Злые боты" };
            x0 = cx - (3 * (bw + gap) - gap) / 2;
            for (int i = 0; i < 3; i++)
                if (Button(new Rect(x0 + i * (bw + gap), 684, bw, 44), diffs[i], (int)difficulty == i, new Color(1f, 0.6f, 0.2f)))
                    difficulty = (Difficulty)i;

            if (Button(new Rect(cx - 220, 742, 440, 74), "В БОЙ!  [Enter]", true, new Color(1f, 0.7f, 0.1f)))
                StartMatch();

            hud.Label(new Rect(cx - 640, 830, 1280, 130),
                "WASD — движение · Shift — бег · Space — прыжок (через препятствие — само) · держать Space в воздухе — полёт\n" +
                "Мышь — обзор · ЛКМ — огонь · ПКМ — зум · Q — способность · F — меч · G — граната\n" +
                "H / колёсико — гаубица · E — заложить/разминировать бомбу · Tab — таблица · Esc — пауза · M — музыка\n" +
                "X — аптечка · C — щит · Пока мёртв — клавиши 1-4 выбирают следующего робота",
                hud.SSmall, new Color(1, 1, 1, 0.7f));
        }

        void Stat(float x, float y, string name, float v, string value)
        {
            hud.Label(new Rect(x, y, 120, 24), name, hud.SSmallLeft, new Color(1, 1, 1, 0.7f));
            hud.Bar(new Rect(x + 120, y + 6, 100, 12), v, new Color(0.4f, 0.8f, 1f));
            hud.Label(new Rect(x + 222, y, 50, 24), value, hud.SSmallRight, Color.white);
        }

        void DrawDeath(Match m)
        {
            float cx = hud.W / 2;
            var box = new Rect(cx - 440, 300, 880, 300);
            hud.Box(box, new Color(0, 0, 0, 0.65f));
            hud.Label(new Rect(box.x, box.y + 14, box.width, 50), "РОБОТ УНИЧТОЖЕН", hud.SMid, Palette.Red);
            var k = m.LastKillerOfPlayer;
            if (k != null && k != m.Player)
                hud.Label(new Rect(box.x, box.y + 62, box.width, 28), $"Тебя разобрал {k.Name} на «{k.Def.Name}»", hud.SSmall, k.Color);
            hud.Label(new Rect(box.x, box.y + 96, box.width, 30), m.Mode.DeadText(m.Player), hud.SSmall, Color.white);

            bool willReturn = m.Player.RespawnAt >= 0 || m.Mode is BombMode;
            if (!willReturn) return;
            hud.Label(new Rect(box.x, box.y + 140, box.width, 26), "Следующий робот (или клавиши 1-4):", hud.STiny, new Color(1, 1, 1, 0.7f));
            var next = m.Player.NextDef ?? m.Player.Def;
            float bw = 200, gap = 10, x0 = cx - (Catalog.Robots.Length * (bw + gap) - gap) / 2;
            for (int i = 0; i < Catalog.Robots.Length; i++)
            {
                var d = Catalog.Robots[i];
                if (Button(new Rect(x0 + i * (bw + gap), box.y + 172, bw, 70), $"{i + 1}. {d.Name}\n<size=15>{d.Role}</size>", next == d))
                    m.Player.NextDef = d;
            }
        }

        void DrawPause()
        {
            float cx = hud.W / 2;
            hud.Box(new Rect(0, 0, hud.W, Hud.H), new Color(0, 0, 0, 0.5f));
            var box = new Rect(cx - 250, 260, 500, 540);
            hud.Box(box, new Color(0.05f, 0.06f, 0.1f, 0.9f));
            hud.Label(new Rect(box.x, box.y + 20, box.width, 60), "ПАУЗА", hud.SBig, Color.white);
            if (Button(new Rect(cx - 180, box.y + 110, 360, 60), "Продолжить", true)) SetPaused(false);
            if (Button(new Rect(cx - 180, box.y + 185, 360, 60), "Заново")) StartMatch();
            if (Button(new Rect(cx - 180, box.y + 260, 360, 60), "В меню")) ToMenu();

            var pc = PlayerCamera.I;
            if (pc)
            {
                hud.Label(new Rect(box.x, box.y + 340, box.width, 30), $"Чувствительность мыши: {pc.Sensitivity:0.0}", hud.SSmall, Color.white);
                float s = GUI.HorizontalSlider(new Rect(cx - 180, box.y + 385, 360, 24), pc.Sensitivity, 0.5f, 8f);
                if (!Mathf.Approximately(s, pc.Sensitivity))
                {
                    pc.Sensitivity = s;
                    PlayerPrefs.SetFloat("rf_sens", s);
                }
            }

            var mp = MusicPlayer.I;
            if (mp)
            {
                hud.Label(new Rect(box.x, box.y + 420, box.width, 30),
                    mp.Muted ? "Музыка: выкл (M)" : $"Музыка: {MusicPlayer.Volume * 100:0}% (M — выкл)", hud.SSmall, Color.white);
                float v = GUI.HorizontalSlider(new Rect(cx - 180, box.y + 465, 360, 24), MusicPlayer.Volume, 0f, 1f);
                if (!Mathf.Approximately(v, MusicPlayer.Volume)) MusicPlayer.Volume = v;
            }
        }

        void DrawEnd(Match m)
        {
            float cx = hud.W / 2;
            hud.Box(new Rect(0, 0, hud.W, Hud.H), new Color(0, 0, 0, 0.45f));
            var c = m.PlayerWon ? new Color(0.4f, 1f, 0.5f) : m.Result == "НИЧЬЯ" ? Palette.Gold : Palette.Red;
            hud.Label(new Rect(0, 60, hud.W, 90), m.Result, hud.SBig, c);
            hud.Label(new Rect(cx - 600, 150, 1200, 40), m.ResultDetails, hud.SSmall, Color.white);
            hud.Scoreboard(m, 210);
            float y = Mathf.Min(Hud.H - 100, 210 + 60 + m.Pilots.Count * 30 + 20);
            if (Button(new Rect(cx - 330, y, 310, 64), "Ещё раз", true)) StartMatch();
            if (Button(new Rect(cx + 20, y, 310, 64), "В меню")) ToMenu();
        }

        // ---------------------------------------------------------------- Автотест: боты играют все режимы на ускорении
        static float ArgFloat(string name, float def)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        IEnumerator Autotest()
        {
            Application.runInBackground = true;
            float cap = ArgFloat("-autotestTime", 900f);
            float scale = ArgFloat("-autotestScale", 12f);
            var report = new StringBuilder();

            // Все треки синтезируем так же, как в игре — в фоновых потоках
            var t0 = Time.realtimeSinceStartup;
            var styles = new List<MusicStyle> { MusicStyle.Menu };
            styles.AddRange(Maps.All.Select(mp => mp.Music));
            var tasks = styles.Select(st => System.Threading.Tasks.Task.Run(() => MusicSynth.Render(st))).ToList();
            while (tasks.Any(t => !t.IsCompleted)) yield return null;
            var lens = new List<string>();
            for (int i = 0; i < styles.Count; i++)
            {
                if (tasks[i].IsFaulted) Debug.LogError($"Music {styles[i].Id} failed: {tasks[i].Exception}");
                else lens.Add($"{styles[i].Id}={MusicSynth.ToClip(styles[i].Id, tasks[i].Result).length:0.0}s");
            }
            report.AppendLine($"[AUTOTEST] Music: {string.Join(" ", lens)} synth={Time.realtimeSinceStartup - t0:0.00}s");

            // Каждая локация по разу, режимы по кругу — так покрыты все карты и все режимы
            var modes = (ModeType[])System.Enum.GetValues(typeof(ModeType));
            for (int i = 0; i < Maps.All.Length; i++)
            {
                var map = Maps.All[i];
                var mt = modes[i % modes.Length];
                StopMatch();
                yield return null;
                Robot.VaultCount = Robot.FlightCount = Robot.HowitzerCount = Robot.MedkitCount = Robot.ShieldItemCount = Pickup.PickedCount = 0;
                var m = Match.Begin(mt, Catalog.Robots[i % Catalog.Robots.Length], Difficulty.Normal, true, map);
                state = State.Playing;
                Time.timeScale = scale;
                int errBefore = errors;
                float realStart = Time.realtimeSinceStartup;
                while (m && !m.Over && m.Elapsed < cap) yield return null;
                Time.timeScale = 1f;

                string line = m
                    ? $"[AUTOTEST] {map.Id}/{mt}: finished={m.Over} result='{m.Result}' details='{m.ResultDetails}' gameTime={m.Elapsed:0}s " +
                      $"real={Time.realtimeSinceStartup - realStart:0.0}s kills={m.Pilots.Sum(p => p.Kills)} " +
                      $"vaults={Robot.VaultCount} flights={Robot.FlightCount} howitzer={Robot.HowitzerCount} medkits={Robot.MedkitCount} shields={Robot.ShieldItemCount} pickups={Pickup.PickedCount} errors={errors - errBefore}"
                    : $"[AUTOTEST] {map.Id}/{mt}: match object vanished!";
                Debug.Log(line);
                report.AppendLine(line);
            }

            StopMatch();
            yield return null;
            Debug.Log($"[AUTOTEST] DONE errors={errors}\n{report}");
            foreach (var s in errorSamples) Debug.Log($"[AUTOTEST] error sample: {s}");
            Application.Quit(errors > 0 ? 1 : 0);
        }

        /// <summary>
        /// -shots [-shotsDir путь]: снимки меню и всех режимов (игрок на автопилоте).
        /// По умолчанию — папка Screenshots рядом с exe.
        /// </summary>
        IEnumerator Screenshots()
        {
            var args = System.Environment.GetCommandLineArgs();
            int di = System.Array.IndexOf(args, "-shotsDir");
            string dir = di >= 0 && di + 1 < args.Length
                ? args[di + 1]
                : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", "Screenshots");
            System.IO.Directory.CreateDirectory(dir);

            yield return new WaitForSecondsRealtime(4f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "0_menu.png"));
            yield return null;
            yield return null;

            // Каждая локация в своём режиме (режимы по кругу)
            var modes = (ModeType[])System.Enum.GetValues(typeof(ModeType));
            for (int i = 0; i < Maps.All.Length; i++)
            {
                var map = Maps.All[i];
                var mt = modes[i % modes.Length];
                StopMatch();
                Match.Begin(mt, Catalog.Robots[(i + 1) % Catalog.Robots.Length], Difficulty.Normal, true, map);
                state = State.Playing;
                if (MusicPlayer.I) MusicPlayer.I.Play(map.Music);
                yield return new WaitForSecondsRealtime(mt == ModeType.Survival ? 14f : 20f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{i + 1}_{map.Id}.png"));
                yield return null;
                yield return null;
            }
            Application.Quit(0);
        }

        void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            errors++;
            if (errorSamples.Count < 5) errorSamples.Add($"{condition}\n{stack}");
        }
    }
}
