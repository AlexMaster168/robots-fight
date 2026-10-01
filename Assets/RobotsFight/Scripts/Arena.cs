using System.Collections.Generic;
using UnityEngine;

namespace RobotsFight
{
    /// <summary>
    /// Карта 200x200 по описанию MapDef. Укрытия генерятся с фиксированным сидом локации
    /// и зеркально (точечная симметрия), чтобы стороны были честными.
    /// Команда 0 спавнится на юге (z-), команда 1 — на севере (z+). Точки режимов общие для всех локаций.
    /// </summary>
    public class Arena
    {
        public const float Half = 100f, Ceiling = 45f;

        public Transform Root;
        public MapDef Map;
        public readonly List<Vector3> SouthSpawns = new();
        public readonly List<Vector3> NorthSpawns = new();
        public readonly List<Vector3> RingSpawns = new();
        public readonly List<(Vector3 p, float r)> Lava = new();

        public readonly Vector3[] BeaconPoints =
        {
            new(-55, 0, -40), new(55, 0, -40), new(0, 0, 0), new(-55, 0, 40), new(55, 0, 40),
        };
        public readonly Vector3[] BombSites = { new(-50, 0, 52), new(50, 0, 52) };
        public readonly Vector3 BombStart = new(0, 0, -78);

        readonly List<(Vector3 p, float r)> occupied = new();
        readonly List<(Vector3 p, float r)> clear = new();
        System.Random rng;

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        public static Arena Build(Transform parent, MapDef map)
        {
            var a = new Arena { Root = Shapes.Node("Arena", parent, Vector3.zero), Map = map, rng = new System.Random(map.Seed) };
            a.BuildGround();
            a.BuildWalls();
            a.BuildSpawns();
            a.BuildCover();
            a.ApplyEnvironment();
            return a;
        }

        public bool InLava(Vector3 p)
        {
            foreach (var (c, r) in Lava)
                if (Vec.FlatDist(p, c) < r) return true;
            return false;
        }

        // ---------------------------------------------------------------- Земля, стены, спавны
        void BuildGround()
        {
            var ground = Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(0, -0.5f, 0), new Vector3(Half * 2, 1, Half * 2), Color.gray, collider: true);
            ground.name = "Ground";
            ground.GetComponent<Renderer>().enabled = false;

            const int n = 10;
            float tile = Half * 2 / n;
            for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
            {
                var t = Shapes.Prim(PrimitiveType.Cube, Root,
                    new Vector3(-Half + tile * (x + 0.5f), -0.01f, -Half + tile * (z + 0.5f)),
                    new Vector3(tile, 0.02f, tile), (x + z) % 2 == 0 ? Map.GroundA : Map.GroundB);
                t.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            bool glowLines = Map.Style == CoverStyle.City || Map.Style == CoverStyle.Volcano;
            Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(0, 0.005f, 0), new Vector3(Half * 2, 0.02f, 0.4f), Map.Line, glow: glowLines);
            if (Map.Style == CoverStyle.City)
            {
                // Неоновая разметка улиц
                for (int i = -4; i <= 4; i++)
                {
                    if (i == 0) continue;
                    Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(i * 20, 0.005f, 0), new Vector3(0.25f, 0.02f, Half * 2), Map.Line * 0.6f, glow: true);
                    Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(0, 0.005f, i * 20), new Vector3(Half * 2, 0.02f, 0.25f), Map.Accent2 * 0.5f, glow: true);
                }
            }
            Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(0, 0.005f, -86), new Vector3(90, 0.02f, 18), Palette.Blue * 0.45f);
            Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(0, 0.005f, 86), new Vector3(90, 0.02f, 18), Palette.Red * 0.45f);
        }

        void BuildWalls()
        {
            float h = 12f;
            void Wall(Vector3 pos, Vector3 size)
            {
                Shapes.Prim(PrimitiveType.Cube, Root, pos, size, Map.Wall, collider: true);
                // Невидимое продолжение стены — чтобы не улететь за карту
                var inv = Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(pos.x, h + 30f, pos.z), new Vector3(size.x, 60f, size.z), Map.Wall, collider: true);
                inv.GetComponent<Renderer>().enabled = false;
                inv.name = "InvisibleWall";
            }
            Wall(new Vector3(0, h / 2, Half + 1), new Vector3(Half * 2 + 4, h, 2));
            Wall(new Vector3(0, h / 2, -Half - 1), new Vector3(Half * 2 + 4, h, 2));
            Wall(new Vector3(Half + 1, h / 2, 0), new Vector3(2, h, Half * 2 + 4));
            Wall(new Vector3(-Half - 1, h / 2, 0), new Vector3(2, h, Half * 2 + 4));
        }

        void BuildSpawns()
        {
            for (int i = 0; i < 10; i++)
            {
                float x = -36 + i * 8;
                SouthSpawns.Add(new Vector3(x, 0.1f, -86 + (i % 2) * 5));
                NorthSpawns.Add(new Vector3(-x, 0.1f, 86 - (i % 2) * 5));
            }
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2 / 12;
                RingSpawns.Add(new Vector3(Mathf.Cos(a) * 78, 0.1f, Mathf.Sin(a) * 78));
            }
        }

        // ---------------------------------------------------------------- Укрытия
        void BuildCover()
        {
            foreach (var b in BeaconPoints) clear.Add((b, 12f));
            foreach (var s in BombSites) clear.Add((s, 13f));
            foreach (var p in RingSpawns) clear.Add((p, 7f));

            // Бордюр у спавнов — чтобы не простреливали прямо с респа
            for (int i = -1; i <= 1; i += 2)
            {
                var p = new Vector3(28 * i, 0, -70);
                occupied.Add((p, 5f));
                occupied.Add((-p, 5f));
                Box(new Vector3(p.x, 1.75f, p.z), new Vector3(10, 3.5f, 2), 0, Map.CoverLow);
                Box(new Vector3(-p.x, 1.75f, -p.z), new Vector3(10, 3.5f, 2), 0, Map.CoverLow);
            }

            switch (Map.Style)
            {
                case CoverStyle.Desert:
                    Scatter(16, 4f, 8.5f, Mesa);
                    Scatter(18, 1.5f, 3.5f, Boulders);
                    break;
                case CoverStyle.Snow:
                    Scatter(14, 3f, 6.5f, IceBlock);
                    Scatter(8, 3.5f, 5f, FuelTank);
                    Scatter(5, 2.5f, 3f, RadarTower);
                    Scatter(10, 2.5f, 4f, SnowWall);
                    break;
                case CoverStyle.City:
                    Scatter(16, 5f, 8.5f, Skyscraper);
                    Scatter(16, 2.2f, 3.2f, Car);
                    break;
                case CoverStyle.Volcano:
                    Scatter(6, 5f, 9f, LavaPool);
                    Scatter(18, 3f, 7f, JaggedRocks);
                    break;
                case CoverStyle.Moon:
                    Scatter(10, 4f, 8f, Crater);
                    Scatter(6, 4f, 7f, Dome);
                    Scatter(14, 1.5f, 3f, Boulders);
                    break;
                default:
                    Scatter(26, 2.5f, 8f, Industrial);
                    break;
            }
        }

        /// <summary>Расставляет count пар объектов (объект + зеркальная копия). Билдер получает свой rng — копии идентичны.</summary>
        void Scatter(int count, float minR, float maxR, System.Action<Vector3, float, float, System.Random> build)
        {
            int placed = 0, tries = 0;
            while (placed < count && tries++ < 4000)
            {
                float r = R(minR, maxR);
                var p = new Vector3(R(-88, 88), 0, R(-66, -3));
                if (!Free(p, r) || !Free(-p, r)) continue;
                occupied.Add((p, r));
                occupied.Add((-p, r));
                float yaw = R(0, 360);
                int seed = rng.Next();
                build(p, yaw, r, new System.Random(seed));
                build(-p, yaw + 180f, r, new System.Random(seed));
                placed++;
            }
        }

        bool Free(Vector3 p, float r)
        {
            if (Mathf.Abs(p.x) + r > Half - 4 || Mathf.Abs(p.z) + r > 70) return false;
            if (Mathf.Abs(p.z) - r < 3) return false; // не пересекаться со своим зеркалом
            foreach (var (op, or) in occupied)
                if (Vec.FlatDist(op, p) < r + or + 5f) return false; // зазор 5 м для проходов
            foreach (var (cp, cr) in clear)
                if (Vec.FlatDist(cp, p) < r + cr) return false;
            return true;
        }

        Transform Box(Vector3 center, Vector3 size, float yaw, Color c, bool collider = true, bool glow = false)
        {
            var t = Shapes.Prim(PrimitiveType.Cube, Root, center, size, c, collider, glow);
            t.rotation = Quaternion.Euler(0, yaw, 0);
            t.name = "Cover";
            return t;
        }

        Transform Prim(PrimitiveType type, Vector3 center, Vector3 size, Quaternion rot, Color c, bool collider = true, bool glow = false)
        {
            var t = Shapes.Prim(type, Root, center, size, c, collider, glow);
            t.rotation = rot;
            t.name = "Cover";
            return t;
        }

        static Vector3 Rot(float yaw, Vector3 local) => Quaternion.Euler(0, yaw, 0) * local;

        // ---------------------------------------------------------------- Билдеры по стилям
        void Industrial(Vector3 p, float yaw, float r, System.Random rr)
        {
            yaw = Mathf.Round(yaw / 90f) * 90f; // городская сетка
            float w = R(rr, 0.8f, 1.4f) * r, d = R(rr, 0.8f, 1.4f) * r;
            bool tall = rr.NextDouble() < 0.35;
            float h = tall ? R(rr, 7, 16) : R(rr, 2.5f, 4.5f);
            Box(new Vector3(p.x, h / 2, p.z), new Vector3(w, h, d), yaw, tall ? Map.CoverHigh : Map.CoverLow);
            if (!tall) return;
            Box(new Vector3(p.x, h + 0.2f, p.z), new Vector3(w + 0.4f, 0.4f, d + 0.4f), yaw, Map.CoverHigh * 0.7f, false);
            Box(new Vector3(p.x, h * 0.7f, p.z), new Vector3(w + 0.05f, 0.3f, d + 0.05f), yaw, Map.Accent, false, true);
        }

        void Mesa(Vector3 p, float yaw, float r, System.Random rr)
        {
            float h = R(rr, 5, 13), dia = r * R(rr, 1.6f, 1.95f);
            Prim(PrimitiveType.Cylinder, new Vector3(p.x, h / 2, p.z), new Vector3(dia, h / 2, dia), Quaternion.identity, Map.CoverHigh);
            Prim(PrimitiveType.Cylinder, new Vector3(p.x, h + 0.3f, p.z), new Vector3(dia * 1.08f, 0.3f, dia * 1.08f), Quaternion.identity, Map.CoverLow, false);
            Prim(PrimitiveType.Cylinder, new Vector3(p.x, h * R(rr, 0.3f, 0.6f), p.z), new Vector3(dia * 1.02f, 0.35f, dia * 1.02f), Quaternion.identity, Map.Accent2 * 0.8f, false);
        }

        void Boulders(Vector3 p, float yaw, float r, System.Random rr)
        {
            int n = 1 + rr.Next(3);
            for (int i = 0; i < n; i++)
            {
                float s = r * R(rr, 0.7f, 1.2f);
                var off = Rot(yaw + i * 120f, new Vector3(r * 0.35f * i, 0, 0));
                var rot = Quaternion.Euler(R(rr, -25, 25), yaw + R(rr, 0, 90), R(rr, -25, 25));
                Prim(PrimitiveType.Cube, new Vector3(p.x + off.x, s * 0.35f, p.z + off.z), new Vector3(s, s * R(rr, 0.6f, 1f), s * R(rr, 0.7f, 1f)), rot,
                    i % 2 == 0 ? Map.CoverLow : Map.CoverHigh);
            }
        }

        void IceBlock(Vector3 p, float yaw, float r, System.Random rr)
        {
            float w = r * R(rr, 0.9f, 1.35f), d = r * R(rr, 0.9f, 1.35f), h = R(rr, 3f, 6.5f);
            Box(new Vector3(p.x, h / 2, p.z), new Vector3(w, h, d), yaw, Map.CoverLow);
            Box(new Vector3(p.x, h + 0.2f, p.z), new Vector3(w + 0.3f, 0.4f, d + 0.3f), yaw, Color.white, false);
        }

        void FuelTank(Vector3 p, float yaw, float r, System.Random rr)
        {
            float len = r * 1.7f, dia = R(rr, 3f, 4f);
            var rot = Quaternion.Euler(0, yaw, 90);
            Prim(PrimitiveType.Cylinder, new Vector3(p.x, dia / 2, p.z), new Vector3(dia, len / 2, dia), rot, new Color(0.92f, 0.92f, 0.95f));
            for (int i = -1; i <= 1; i += 2)
            {
                var o = Rot(yaw, new Vector3(i * len * 0.3f, 0, 0));
                Prim(PrimitiveType.Cylinder, new Vector3(p.x + o.x, dia / 2, p.z + o.z), new Vector3(dia * 1.03f, 0.35f, dia * 1.03f), rot, Map.Accent2, false);
            }
        }

        void RadarTower(Vector3 p, float yaw, float r, System.Random rr)
        {
            float h = R(rr, 12, 18);
            Prim(PrimitiveType.Cylinder, new Vector3(p.x, h / 2, p.z), new Vector3(0.7f, h / 2, 0.7f), Quaternion.identity, Map.CoverHigh);
            Box(new Vector3(p.x, 1.5f, p.z), new Vector3(3, 3, 3), yaw, Map.CoverHigh);
            Box(new Vector3(p.x, h, p.z), new Vector3(4f, 2.2f, 0.3f), yaw, Color.white, false);
            Prim(PrimitiveType.Sphere, new Vector3(p.x, h + 1.5f, p.z), Vector3.one * 0.6f, Quaternion.identity, Map.Accent2, false, true);
        }

        void SnowWall(Vector3 p, float yaw, float r, System.Random rr)
        {
            Box(new Vector3(p.x, 1.1f, p.z), new Vector3(r * 1.9f, 2.2f, 1.3f), yaw, Color.white);
        }

        void Skyscraper(Vector3 p, float yaw, float r, System.Random rr)
        {
            yaw = Mathf.Round(yaw / 90f) * 90f;
            float w = r * R(rr, 1.0f, 1.35f), d = r * R(rr, 1.0f, 1.35f), h = R(rr, 14, 34);
            Box(new Vector3(p.x, h / 2, p.z), new Vector3(w, h, d), yaw, Map.CoverHigh);
            var c = rr.NextDouble() < 0.5 ? Map.Accent : Map.Accent2;
            for (float y = 4f; y < h - 1f; y += R(rr, 3f, 5f))
                Box(new Vector3(p.x, y, p.z), new Vector3(w + 0.06f, 0.5f, d + 0.06f), yaw, c * R(rr, 0.6f, 1f), false, true);
            Box(new Vector3(p.x, h + 2f, p.z), new Vector3(0.3f, 4f, 0.3f), yaw, Map.CoverLow, false);
            Prim(PrimitiveType.Sphere, new Vector3(p.x, h + 4.2f, p.z), Vector3.one * 0.6f, Quaternion.identity, new Color(1f, 0.15f, 0.1f), false, true);
        }

        void Car(Vector3 p, float yaw, float r, System.Random rr)
        {
            Color[] paint = { new(0.6f, 0.1f, 0.15f), new(0.1f, 0.2f, 0.5f), new(0.85f, 0.75f, 0.2f), new(0.25f, 0.25f, 0.28f) };
            var c = paint[rr.Next(paint.Length)];
            float len = r * 1.6f;
            Box(new Vector3(p.x, 0.7f, p.z), new Vector3(2.2f, 1.2f, len), yaw, c);
            var cab = Box(new Vector3(p.x, 1.65f, p.z), new Vector3(2f, 0.8f, len * 0.5f), yaw, c * 0.8f);
            cab.position += Rot(yaw, new Vector3(0, 0, -len * 0.08f));
            for (int i = -1; i <= 1; i += 2)
            {
                var o = Rot(yaw, new Vector3(i * 0.7f, 0, len / 2));
                Box(new Vector3(p.x + o.x, 0.8f, p.z + o.z), new Vector3(0.4f, 0.2f, 0.05f), yaw, new Color(1f, 0.95f, 0.8f), false, true);
                var b = Rot(yaw, new Vector3(i * 0.7f, 0, -len / 2));
                Box(new Vector3(p.x + b.x, 0.8f, p.z + b.z), new Vector3(0.4f, 0.2f, 0.05f), yaw, new Color(1f, 0.1f, 0.1f), false, true);
            }
        }

        void LavaPool(Vector3 p, float yaw, float r, System.Random rr)
        {
            Prim(PrimitiveType.Cylinder, new Vector3(p.x, 0.02f, p.z), new Vector3(r * 2.2f, 0.02f, r * 2.2f), Quaternion.identity, Map.CoverLow * 0.6f, false);
            Prim(PrimitiveType.Cylinder, new Vector3(p.x, 0.04f, p.z), new Vector3(r * 2f, 0.02f, r * 2f), Quaternion.identity, Map.Accent, false, true);
            Prim(PrimitiveType.Cylinder, new Vector3(p.x, 0.05f, p.z), new Vector3(r * 1.1f, 0.02f, r * 1.1f), Quaternion.identity, new Color(1f, 0.8f, 0.3f), false, true);
            Lava.Add((p, r * 0.95f));
        }

        void JaggedRocks(Vector3 p, float yaw, float r, System.Random rr)
        {
            int n = 2 + rr.Next(3);
            for (int i = 0; i < n; i++)
            {
                float h = R(rr, 3f, 11f);
                var off = Rot(yaw + i * 95f, new Vector3(r * 0.3f * (i > 0 ? 1 : 0), 0, 0));
                var rot = Quaternion.Euler(R(rr, -18, 18), yaw + R(rr, 0, 90), R(rr, -18, 18));
                Prim(PrimitiveType.Cube, new Vector3(p.x + off.x, h * 0.42f, p.z + off.z), new Vector3(r * R(rr, 0.6f, 1f), h, r * R(rr, 0.5f, 0.9f)), rot,
                    i % 2 == 0 ? Map.CoverLow : Map.CoverHigh);
            }
            if (rr.NextDouble() < 0.5)
                Prim(PrimitiveType.Cube, new Vector3(p.x, 0.15f, p.z), new Vector3(r * 1.4f, 0.3f, 0.35f), Quaternion.Euler(0, yaw, 0), Map.Accent2, false, true);
        }

        void Crater(Vector3 p, float yaw, float r, System.Random rr)
        {
            Prim(PrimitiveType.Cylinder, new Vector3(p.x, 0.02f, p.z), new Vector3(r * 1.7f, 0.02f, r * 1.7f), Quaternion.identity, Map.GroundB * 0.6f, false);
            int n = 9;
            for (int i = 0; i < n; i++)
            {
                if (i == rr.Next(n)) continue; // проломы в кольце
                var o = Rot(yaw + i * 360f / n, new Vector3(r * 0.9f, 0, 0));
                float s = R(rr, 1.4f, 2.2f);
                Prim(PrimitiveType.Cube, new Vector3(p.x + o.x, s * 0.3f, p.z + o.z), new Vector3(s * 1.4f, s, s),
                    Quaternion.Euler(R(rr, -15, 15), yaw + i * 360f / n, R(rr, -15, 15)), Map.CoverLow);
            }
        }

        void Dome(Vector3 p, float yaw, float r, System.Random rr)
        {
            float dia = r * 1.8f;
            Prim(PrimitiveType.Cylinder, new Vector3(p.x, 0.75f, p.z), new Vector3(dia, 0.75f, dia), Quaternion.identity, Map.CoverHigh * 0.85f);
            Prim(PrimitiveType.Sphere, new Vector3(p.x, 1.5f, p.z), new Vector3(dia, dia * 0.7f, dia), Quaternion.identity, Map.CoverHigh);
            Prim(PrimitiveType.Cylinder, new Vector3(p.x, 1.6f, p.z), new Vector3(dia * 1.01f, 0.15f, dia * 1.01f), Quaternion.identity, Map.Accent, false, true);
        }

        // ---------------------------------------------------------------- Окружение
        public static void SetupEnvironment()
        {
            if (!Object.FindAnyObjectByType<Light>())
            {
                var l = new GameObject("Sun").AddComponent<Light>();
                l.type = LightType.Directional;
                l.shadows = LightShadows.Soft;
            }
            QualitySettings.shadowDistance = 160f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }

        void ApplyEnvironment()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = Map.FogStart;
            RenderSettings.fogEndDistance = Map.FogEnd;
            RenderSettings.fogColor = Map.Fog;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Map.Ambient;

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                l.color = Map.SunColor;
                l.intensity = Map.SunIntensity;
                l.shadows = LightShadows.Soft;
                l.transform.rotation = Quaternion.Euler(Map.SunEuler);
                break;
            }

            var cam = Camera.main;
            if (cam)
            {
                cam.clearFlags = Map.SolidSky ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
                cam.backgroundColor = Map.SkyColor;
            }
        }
    }
}
