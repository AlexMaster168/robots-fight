using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RobotsFight
{
    /// <summary>Весь интерфейс на IMGUI — ноль ассетов, ноль настройки сцены. Виртуальная высота 1080.</summary>
    public class Hud
    {
        public float W { get; private set; }
        public const float H = 1080f;
        float scale;

        public GUIStyle SBig, SMid, SMidCenter, SSmall, SSmallLeft, SSmallRight, STiny, SButton, SMarker;
        readonly List<MapMarker> markers = new();

        public void Begin()
        {
            scale = Screen.height / H;
            W = Screen.width / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            if (SBig != null) return;

            SBig = Style(64, TextAnchor.MiddleCenter, FontStyle.Bold);
            SMid = Style(30, TextAnchor.MiddleCenter, FontStyle.Bold);
            SMidCenter = Style(26, TextAnchor.MiddleCenter, FontStyle.Bold);
            SSmall = Style(20, TextAnchor.MiddleCenter);
            SSmallLeft = Style(20, TextAnchor.MiddleLeft);
            SSmallRight = Style(20, TextAnchor.MiddleRight);
            STiny = Style(15, TextAnchor.MiddleCenter);
            SMarker = Style(18, TextAnchor.MiddleCenter, FontStyle.Bold);
            SButton = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = true };
        }

        static GUIStyle Style(int size, TextAnchor anchor, FontStyle fs = FontStyle.Normal) =>
            new(GUI.skin.label) { fontSize = size, alignment = anchor, fontStyle = fs, wordWrap = true, richText = true };

        // ---------------------------------------------------------------- Примитивы рисования
        public void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public void Label(Rect r, string text, GUIStyle st, Color c)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, c.a * 0.8f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, st);
            GUI.color = c;
            GUI.Label(r, text, st);
            GUI.color = old;
        }

        public void CenterText(float y, string text, GUIStyle st, Color c) =>
            Label(new Rect(0, y, W, st.fontSize * 1.6f), text, st, c);

        public void Bar(Rect r, float v, Color fill, Color? bg = null)
        {
            Box(r, bg ?? new Color(0, 0, 0, 0.55f));
            Box(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(v), r.height - 4), fill);
        }

        public bool ToGui(Vector3 world, out Vector2 p)
        {
            p = default;
            var cam = PlayerCamera.I ? PlayerCamera.I.Cam : Camera.main;
            if (!cam) return false;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0.5f) return false;
            p = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
            return true;
        }

        /// <summary>Маркер цели в мире; прижимается к краю экрана, если цель за его пределами.</summary>
        public void Marker(Vector3 world, string text, Color c)
        {
            var cam = PlayerCamera.I ? PlayerCamera.I.Cam : null;
            if (!cam) return;
            var sp = cam.WorldToScreenPoint(world);
            Vector2 p = new(sp.x / scale, (Screen.height - sp.y) / scale);
            if (sp.z < 0) p = new Vector2(W - p.x, H); // цель за спиной — прижимаем к низу
            p.x = Mathf.Clamp(p.x, 30, W - 30);
            p.y = Mathf.Clamp(p.y, 150, H - 60);
            var r = new Rect(p.x - 22, p.y - 16, 44, 32);
            if (text.Length > 2) r = new Rect(p.x - 40, p.y - 14, 80, 28);
            Box(r, new Color(0, 0, 0, 0.5f));
            Box(new Rect(r.x, r.yMax - 3, r.width, 3), c);
            Label(r, text, SMarker, c);
            float dist = Vector3.Distance(cam.transform.position, world);
            Label(new Rect(r.x - 10, r.yMax, r.width + 20, 18), $"{dist:0} м", STiny, new Color(1, 1, 1, 0.7f));
        }

        public void TeamScore(float a, float b, float max, string sub)
        {
            float cx = W / 2;
            Box(new Rect(cx - 260, 10, 520, 56), new Color(0, 0, 0, 0.5f));
            Bar(new Rect(cx - 250, 18, 190, 16), a / max, Palette.Blue);
            Bar(new Rect(cx + 60, 18, 190, 16), b / max, Palette.Red);
            Label(new Rect(cx - 250, 34, 190, 30), $"Синие  {Mathf.FloorToInt(a)}", SSmall, Palette.Blue);
            Label(new Rect(cx + 60, 34, 190, 30), $"{Mathf.FloorToInt(b)}  Красные", SSmall, Palette.Red);
            var m = Match.I;
            string timer = m.Mode.Duration > 0 ? FormatTime(m.TimeLeft) : "";
            Label(new Rect(cx - 60, 14, 120, 44), timer, SMid, Color.white);
            if (!string.IsNullOrEmpty(sub)) Label(new Rect(cx - 150, 66, 300, 22), sub, STiny, new Color(1, 1, 1, 0.75f));
        }

        public static string FormatTime(float s)
        {
            int t = Mathf.Max(0, Mathf.CeilToInt(s));
            return $"{t / 60}:{t % 60:00}";
        }

        // ---------------------------------------------------------------- Главный HUD
        public void Draw(Match m)
        {
            var me = m.Player;
            var body = me.Body;
            bool alive = me.IsAlive;

            DrawNameplates(m);
            m.Mode.DrawWorld(this);

            // Верх: режим/счёт
            m.Mode.DrawHud(this);
            if (m.Mode.Duration > 0 && !(m.Mode is TeamDeathmatchMode || m.Mode is BeaconMode))
                Label(new Rect(W / 2 - 80, 84, 160, 34), FormatTime(m.TimeLeft), SMid, Color.white);

            DrawRadar(m);
            DrawFeed(m);

            // Урон по нам — красная рамка
            float hurt = Mathf.Clamp01(1f - (Time.time - m.PlayerHurtTime) / 0.5f);
            if (hurt > 0 && alive)
            {
                var c = new Color(1f, 0.1f, 0.05f, 0.35f * hurt);
                Box(new Rect(0, 0, W, 24), c);
                Box(new Rect(0, H - 24, W, 24), c);
                Box(new Rect(0, 0, 24, H), c);
                Box(new Rect(W - 24, 0, 24, H), c);
            }

            if (alive)
            {
                DrawCrosshair(m);
                DrawStatus(body);
                DrawWeapons(body);
                DrawInventory(body);
            }

            if (m.Banner != null && Time.time < m.BannerUntil)
            {
                float a = Mathf.Clamp01((m.BannerUntil - Time.time) / 0.5f);
                var c = m.BannerColor;
                c.a = a;
                Box(new Rect(0, 230, W, 64), new Color(0, 0, 0, 0.45f * a));
                Label(new Rect(0, 230, W, 64), m.Banner, SMid, c);
            }

            string hint = m.Over ? null : m.Mode.Hint();
            if (!string.IsNullOrEmpty(hint)) CenterText(760, hint, SMidCenter, Palette.Gold);
        }

        void DrawCrosshair(Match m)
        {
            float cx = W / 2, cy = H / 2;
            bool hit = Time.time - m.HitMarkerTime < 0.15f;
            float gap = hit ? 10 : 6, len = 10;
            var c = hit ? new Color(1f, 0.3f, 0.2f) : new Color(1, 1, 1, 0.9f);
            Box(new Rect(cx - 1, cy - 1, 2, 2), c);
            Box(new Rect(cx - gap - len, cy - 1, len, 2), c);
            Box(new Rect(cx + gap, cy - 1, len, 2), c);
            Box(new Rect(cx - 1, cy - gap - len, 2, len), c);
            Box(new Rect(cx - 1, cy + gap, 2, len), c);

            // Куда реально смотрят стволы (торс поворачивается не мгновенно)
            var body = m.Player.Body;
            if (body && body.Weapons.Count > 0 && PlayerCamera.I)
            {
                var mz = body.Weapons[0].Muzzle;
                float d = Vector3.Distance(mz.position, PlayerCamera.I.AimPoint);
                if (ToGui(mz.position + mz.forward * d, out var p) && Vector2.Distance(p, new Vector2(cx, cy)) > 12)
                {
                    Box(new Rect(p.x - 4, p.y - 4, 8, 2), new Color(1, 1, 1, 0.5f));
                    Box(new Rect(p.x - 4, p.y + 2, 8, 2), new Color(1, 1, 1, 0.5f));
                }
            }
        }

        void DrawStatus(Robot r)
        {
            var box = new Rect(20, H - 150, 420, 130);
            Box(box, new Color(0, 0, 0, 0.5f));
            Label(new Rect(box.x + 14, box.y + 6, 400, 30), $"<b>{r.Def.Name}</b>  <size=16>{r.Def.Role}</size>", SSmallLeft, Color.white);
            float hp = r.Hp / r.MaxHp;
            var hc = hp > 0.5f ? new Color(0.3f, 0.9f, 0.4f) : hp > 0.25f ? Palette.Gold : Palette.Red;
            Bar(new Rect(box.x + 14, box.y + 40, 392, 26), hp, hc);
            Label(new Rect(box.x + 14, box.y + 40, 392, 26), $"{Mathf.CeilToInt(r.Hp)} / {Mathf.CeilToInt(r.MaxHp)}", STiny, Color.white);

            string ab = r.AbilityReadyIn <= 0 ? "ГОТОВО [Q]" : $"{r.AbilityReadyIn:0.0} с";
            if (r.ShieldActive) ab = "ЩИТ АКТИВЕН";
            if (r.Repairing) ab = "РЕМОНТ...";
            Label(new Rect(box.x + 14, box.y + 74, 392, 24), $"{r.Def.AbilityName}: {ab}", SSmallLeft,
                r.AbilityReadyIn <= 0 ? new Color(0.5f, 0.9f, 1f) : new Color(1, 1, 1, 0.7f));
            Bar(new Rect(box.x + 14, box.y + 102, 392, 12), r.AbilityCharge, new Color(0.4f, 0.8f, 1f));
            if (r.Invulnerable) Label(new Rect(box.x, box.y - 30, box.width, 26), "Защита при высадке", STiny, new Color(0.6f, 0.9f, 1f));
        }

        void DrawWeapons(Robot r)
        {
            int n = r.Weapons.Count;
            float rowH = 44;
            var box = new Rect(W - 380, H - 30 - n * rowH - 12, 360, n * rowH + 12);
            Box(box, new Color(0, 0, 0, 0.5f));
            for (int i = 0; i < n; i++)
            {
                var w = r.Weapons[i];
                float y = box.y + 8 + i * rowH;
                Label(new Rect(box.x + 12, y, 200, 24), w.Def.Name, SSmallLeft, w.Def.Color);
                string ammo = w.Reloading ? "перезарядка" : $"{w.Ammo} / {w.Def.Magazine}";
                Label(new Rect(box.x + 150, y, 196, 24), ammo, SSmallRight, w.Ammo == 0 || w.Reloading ? Palette.Red : Color.white);
                float v = w.Reloading ? w.ReloadProgress : (float)w.Ammo / w.Def.Magazine;
                Bar(new Rect(box.x + 12, y + 26, 336, 8), v, w.Reloading ? Palette.Gold : w.Def.Color);
            }
            Label(new Rect(box.x, box.y - 26, box.width, 22), "ЛКМ огонь · ПКМ зум · R перезарядка", STiny, new Color(1, 1, 1, 0.6f));
        }

        void DrawInventory(Robot r)
        {
            float w = 220, gap = 10;
            float x = W / 2 - (w * 3 + gap * 2) / 2, y = H - 84;
            Slot(new Rect(x, y, w, 64), "[F]  Меч", r.MeleeReadyIn <= 0 ? "готов" : $"{r.MeleeReadyIn:0.0} с",
                1f - r.MeleeReadyIn / Robot.MeleeCooldown, Catalog.Sword.Color, true);
            Slot(new Rect(x + w + gap, y, w, 64), "[G]  Гранаты", $"× {r.Grenades}",
                r.Grenades > 0 ? 1f - r.GrenadeReadyIn / Robot.GrenadeCooldown : 0f, Catalog.Grenade.Color, r.Grenades > 0);
            Slot(new Rect(x + (w + gap) * 2, y, w, 64), "[Shift]  Бег", r.Sprinting ? "бегу" : $"{r.Stamina * 100:0}%",
                r.Stamina, Palette.Gold, r.Stamina > 0.15f || r.Sprinting);
        }

        void Slot(Rect r, string title, string state, float charge, Color c, bool active)
        {
            Box(r, new Color(0, 0, 0, 0.5f));
            Box(new Rect(r.x, r.y, 4, r.height), active ? c : Color.gray);
            var tc = active ? Color.white : new Color(1, 1, 1, 0.4f);
            Label(new Rect(r.x + 14, r.y + 6, r.width - 24, 26), title, SSmallLeft, tc);
            Label(new Rect(r.x + 14, r.y + 6, r.width - 24, 26), state, SSmallRight, active ? c : Palette.Red);
            Bar(new Rect(r.x + 14, r.y + 40, r.width - 28, 10), charge, active ? c : Color.gray);
        }

        void DrawNameplates(Match m)
        {
            var cam = PlayerCamera.I ? PlayerCamera.I.Cam : null;
            if (!cam) return;
            foreach (var p in m.Pilots)
            {
                if (p.IsPlayer || !p.IsAlive) continue;
                var r = p.Body;
                float dist = Vector3.Distance(cam.transform.position, r.Center);
                if (dist > 200f || !ToGui(r.Top + Vector3.up * 0.8f, out var sp)) continue;
                bool enemy = m.AreEnemies(m.Player, p);
                if (enemy)
                {
                    // Врагов подписываем только если их видно
                    Vector3 dir = r.Center - cam.transform.position;
                    if (!Phys.Raycast(cam.transform.position, dir.normalized, dir.magnitude + 1f, m.Player.Body ? m.Player.Body.Cc : null, out var hit) ||
                        hit.collider != r.Cc) continue;
                }
                float w = 90;
                Label(new Rect(sp.x - 90, sp.y - 34, 180, 20), $"{p.Name} · {r.Def.Name}", STiny, p.Color);
                Bar(new Rect(sp.x - w / 2, sp.y - 12, w, 9), r.Hp / r.MaxHp, enemy ? Palette.Red : new Color(0.3f, 0.9f, 0.4f));
            }
        }

        void DrawRadar(Match m)
        {
            const float size = 220f, range = 130f;
            var rect = new Rect(20, 20, size, size);
            Box(rect, new Color(0, 0, 0, 0.5f));
            Vector2 c = rect.center;
            Box(new Rect(c.x - 0.5f, rect.y, 1, size), new Color(1, 1, 1, 0.1f));
            Box(new Rect(rect.x, c.y - 0.5f, size, 1), new Color(1, 1, 1, 0.1f));

            var cam = PlayerCamera.I;
            Vector3 center = m.Player.IsAlive ? m.Player.Body.transform.position : cam ? cam.transform.position : Vector3.zero;
            float yaw = (cam ? cam.transform.eulerAngles.y : 0) * Mathf.Deg2Rad;
            float k = size / 2 / range;

            Vector2 Map(Vector3 w)
            {
                Vector3 rel = w - center;
                float lx = rel.x * Mathf.Cos(yaw) - rel.z * Mathf.Sin(yaw);
                float lz = rel.x * Mathf.Sin(yaw) + rel.z * Mathf.Cos(yaw);
                var p = new Vector2(c.x + lx * k, c.y - lz * k);
                p.x = Mathf.Clamp(p.x, rect.x + 4, rect.xMax - 4);
                p.y = Mathf.Clamp(p.y, rect.y + 4, rect.yMax - 4);
                return p;
            }

            markers.Clear();
            m.Mode.MapMarkers(markers);
            foreach (var mk in markers)
            {
                var p = Map(mk.Pos);
                Box(new Rect(p.x - 9, p.y - 9, 18, 18), new Color(0, 0, 0, 0.6f));
                Label(new Rect(p.x - 12, p.y - 12, 24, 24), mk.Label, STiny, mk.Color);
            }

            foreach (var p in m.Pilots)
            {
                if (!p.IsAlive || p.IsPlayer) continue;
                bool enemy = m.AreEnemies(m.Player, p);
                // Врага видно на радаре, если он рядом или недавно стрелял
                if (enemy && Vec.FlatDist(p.Body.transform.position, center) > 45f && Time.time - p.Body.LastFireTime > 1.5f) continue;
                var q = Map(p.Body.transform.position);
                Box(new Rect(q.x - 4, q.y - 4, 8, 8), enemy ? Palette.Red : p.Color);
            }
            if (m.Player.IsAlive)
            {
                Box(new Rect(c.x - 5, c.y - 5, 10, 10), Color.white);
                Box(new Rect(c.x - 1, c.y - 14, 2, 9), Color.white);
            }
        }

        void DrawFeed(Match m)
        {
            float y = 20;
            foreach (var f in m.Feed)
            {
                float a = Mathf.Clamp01(7f - (Time.time - f.Time));
                var r = new Rect(W - 470, y, 450, 30);
                Box(r, new Color(0, 0, 0, 0.45f * a));
                var c = f.Color;
                c.a = a;
                Label(new Rect(r.x + 10, r.y, r.width - 20, r.height), f.Text, SSmallRight, c);
                y += 34;
            }
        }

        // ---------------------------------------------------------------- Таблица очков
        public void Scoreboard(Match m, float top)
        {
            var list = m.Pilots.OrderBy(p => m.Mode is FreeForAllMode ? 0 : p.Team)
                .ThenByDescending(p => p.Kills).ThenByDescending(p => p.Score).ToList();
            float rowH = 30, w = 760;
            float h = 50 + list.Count * rowH;
            var box = new Rect(W / 2 - w / 2, top, w, h);
            Box(box, new Color(0, 0, 0, 0.72f));
            float x = box.x + 20;
            Label(new Rect(x, top + 10, 280, 28), "Пилот", SSmallLeft, Color.gray);
            Label(new Rect(x + 280, top + 10, 180, 28), "Робот", SSmallLeft, Color.gray);
            Label(new Rect(x + 440, top + 10, 90, 28), "Уничт.", SSmall, Color.gray);
            Label(new Rect(x + 530, top + 10, 90, 28), "Смерти", SSmall, Color.gray);
            Label(new Rect(x + 620, top + 10, 100, 28), "Очки", SSmall, Color.gray);
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                float y = top + 44 + i * rowH;
                if (p.IsPlayer) Box(new Rect(box.x + 6, y, w - 12, rowH - 2), new Color(1, 1, 1, 0.1f));
                var c = p.IsAlive ? p.Color : p.Color * 0.6f;
                c.a = 1;
                Label(new Rect(x, y, 280, rowH), p.Name + (p.IsAlive ? "" : "  †"), SSmallLeft, c);
                Label(new Rect(x + 280, y, 180, rowH), p.Def.Name, SSmallLeft, Color.white);
                Label(new Rect(x + 440, y, 90, rowH), p.Kills.ToString(), SSmall, Color.white);
                Label(new Rect(x + 530, y, 90, rowH), p.Deaths.ToString(), SSmall, Color.white);
                Label(new Rect(x + 620, y, 100, rowH), p.Score.ToString(), SSmall, Color.white);
            }
        }
    }
}
