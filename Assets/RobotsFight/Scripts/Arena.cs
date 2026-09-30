using System.Collections.Generic;
using UnityEngine;

namespace RobotsFight
{
    /// <summary>
    /// Карта 200x200: разрушенный индустриальный квартал. Укрытия генерятся с фиксированным сидом
    /// и зеркально (точечная симметрия), чтобы стороны были честными.
    /// Команда 0 спавнится на юге (z-), команда 1 — на севере (z+).
    /// </summary>
    public class Arena
    {
        public const float Half = 100f;

        public Transform Root;
        public readonly List<Vector3> SouthSpawns = new();
        public readonly List<Vector3> NorthSpawns = new();
        public readonly List<Vector3> RingSpawns = new();

        public readonly Vector3[] BeaconPoints =
        {
            new(-55, 0, -40), new(55, 0, -40), new(0, 0, 0), new(-55, 0, 40), new(55, 0, 40),
        };
        public readonly Vector3[] BombSites = { new(-50, 0, 52), new(50, 0, 52) };
        public readonly Vector3 BombStart = new(0, 0, -78);

        readonly List<Rect> occupied = new();
        readonly List<(Vector3 p, float r)> clear = new();

        public static Arena Build(Transform parent, int seed = 1337)
        {
            var a = new Arena { Root = Shapes.Node("Arena", parent, Vector3.zero) };
            a.BuildGround();
            a.BuildWalls();
            a.BuildSpawns();
            a.BuildCover(seed);
            return a;
        }

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
                float v = (x + z) % 2 == 0 ? 0.30f : 0.27f;
                var c = new Color(v, v * 1.02f, v * 0.95f);
                var t = Shapes.Prim(PrimitiveType.Cube, Root,
                    new Vector3(-Half + tile * (x + 0.5f), -0.01f, -Half + tile * (z + 0.5f)),
                    new Vector3(tile, 0.02f, tile), c);
                t.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Разметка: центральная линия и спавн-зоны
            Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(0, 0.005f, 0), new Vector3(Half * 2, 0.02f, 0.4f), new Color(0.55f, 0.5f, 0.3f));
            Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(0, 0.005f, -86), new Vector3(90, 0.02f, 18), Palette.Blue * 0.45f);
            Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(0, 0.005f, 86), new Vector3(90, 0.02f, 18), Palette.Red * 0.45f);
        }

        void BuildWalls()
        {
            var c = new Color(0.22f, 0.23f, 0.26f);
            float h = 12f;
            Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(0, h / 2, Half + 1), new Vector3(Half * 2 + 4, h, 2), c, collider: true);
            Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(0, h / 2, -Half - 1), new Vector3(Half * 2 + 4, h, 2), c, collider: true);
            Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(Half + 1, h / 2, 0), new Vector3(2, h, Half * 2 + 4), c, collider: true);
            Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(-Half - 1, h / 2, 0), new Vector3(2, h, Half * 2 + 4), c, collider: true);
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

        void BuildCover(int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            foreach (var b in BeaconPoints) clear.Add((b, 12f));
            foreach (var s in BombSites) clear.Add((s, 13f));
            foreach (var p in RingSpawns) clear.Add((p, 7f));

            // Бордюр у спавнов — пара укрытий, чтобы не простреливали прямо с респа
            for (int i = -1; i <= 1; i += 2)
            {
                AddBoxPair(new Vector3(28 * i, 0, -70), 10, 2, 3.5f, new Color(0.4f, 0.4f, 0.42f));
            }

            int placed = 0, tries = 0;
            while (placed < 26 && tries++ < 3000)
            {
                var p = new Vector3(R(-88, 88), 0, R(-66, -3));
                float w = R(3, 12), d = R(3, 12);
                bool tall = rng.NextDouble() < 0.35;
                float h = tall ? R(7, 16) : R(2.5f, 4.5f);
                if (!Free(p, w, d) || !Free(-p, w, d)) continue;
                float g = R(0.33f, 0.5f);
                var c = tall ? new Color(g * 0.95f, g * 0.97f, g) : new Color(g, g * 0.95f, g * 0.85f);
                AddBoxPair(p, w, d, h, c);
                placed++;
            }
        }

        bool Free(Vector3 p, float w, float d)
        {
            if (Mathf.Abs(p.x) + w / 2 > Half - 4 || Mathf.Abs(p.z) + d / 2 > 70) return false;
            if (Mathf.Abs(p.z) - d / 2 < 3) return false; // не пересекаться со своим зеркалом
            var r = new Rect(p.x - w / 2 - 5, p.z - d / 2 - 5, w + 10, d + 10); // зазор 5м для проходов
            foreach (var o in occupied)
                if (o.Overlaps(r)) return false;
            foreach (var (cp, cr) in clear)
            {
                float dx = Mathf.Max(Mathf.Abs(cp.x - p.x) - w / 2, 0);
                float dz = Mathf.Max(Mathf.Abs(cp.z - p.z) - d / 2, 0);
                if (dx * dx + dz * dz < cr * cr) return false;
            }
            return true;
        }

        void AddBoxPair(Vector3 p, float w, float d, float h, Color c)
        {
            AddBox(p, w, d, h, c);
            AddBox(-p, w, d, h, c);
        }

        void AddBox(Vector3 p, float w, float d, float h, Color c)
        {
            occupied.Add(new Rect(p.x - w / 2, p.z - d / 2, w, d));
            var b = Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(p.x, h / 2, p.z), new Vector3(w, h, d), c, collider: true);
            b.name = "Cover";
            if (h > 6f)
            {
                // крыша + светящиеся «окна» для атмосферы
                Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(p.x, h + 0.2f, p.z), new Vector3(w + 0.4f, 0.4f, d + 0.4f), c * 0.7f);
                Shapes.Prim(PrimitiveType.Cube, Root, new Vector3(p.x, h * 0.7f, p.z), new Vector3(w + 0.05f, 0.3f, d + 0.05f),
                    new Color(1f, 0.75f, 0.35f), glow: true);
            }
        }

        public static void SetupEnvironment()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance = 420f;
            RenderSettings.fogColor = new Color(0.55f, 0.6f, 0.68f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.52f);

            if (!Object.FindAnyObjectByType<Light>())
            {
                var l = new GameObject("Sun").AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.15f;
                l.shadows = LightShadows.Soft;
                l.transform.rotation = Quaternion.Euler(50, -35, 0);
            }
            QualitySettings.shadowDistance = 160f;
        }
    }
}
