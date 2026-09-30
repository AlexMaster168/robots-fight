using System.Collections.Generic;
using UnityEngine;

namespace RobotsFight
{
    /// <summary>
    /// Кэш материалов по цвету. Базовые материалы лежат в Resources (RF_Base / RF_Glow) — иначе шейдер Standard
    /// вырежется из билда и всё станет пурпурным. На SRP-проектах берём дефолтный материал примитива.
    /// </summary>
    public static class Mats
    {
        static Material baseMat, glowMat;
        static readonly Dictionary<(Color, bool), Material> cache = new();

        static void Init()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
            {
                baseMat = Resources.Load<Material>("RF_Base");
                glowMat = Resources.Load<Material>("RF_Glow");
            }
            if (!baseMat)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseMat = tmp.GetComponent<Renderer>().sharedMaterial;
                Object.DestroyImmediate(tmp);
            }
            if (!glowMat) glowMat = baseMat;
        }

        public static Material Get(Color c, bool glow = false)
        {
            if (cache.TryGetValue((c, glow), out var m) && m) return m;
            if (!baseMat) Init();
            m = new Material(glow ? glowMat : baseMat) { color = c };
            if (glow)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 1.6f);
            }
            cache[(c, glow)] = m;
            return m;
        }
    }

    public static class Shapes
    {
        public static Transform Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color,
            bool collider = false, bool glow = false)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mats.Get(color, glow);
            return t;
        }

        public static Transform Node(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }
    }

    /// <summary>Физ-запросы с игнором своего коллайдера (без кастомных слоёв).</summary>
    public static class Phys
    {
        static readonly RaycastHit[] buf = new RaycastHit[48];
        static readonly Collider[] overlap = new Collider[64];

        public static bool Raycast(Vector3 origin, Vector3 dir, float dist, Collider ignore, out RaycastHit hit)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, buf, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            return Nearest(n, ignore, out hit);
        }

        public static bool SphereCast(Vector3 origin, float radius, Vector3 dir, float dist, Collider ignore, out RaycastHit hit)
        {
            int n = Physics.SphereCastNonAlloc(origin, radius, dir, buf, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            return Nearest(n, ignore, out hit);
        }

        static bool Nearest(int n, Collider ignore, out RaycastHit hit)
        {
            hit = default;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (buf[i].collider == ignore) continue;
                if (buf[i].distance < best)
                {
                    best = buf[i].distance;
                    hit = buf[i];
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Урон по площади: полный в центре, 30% на краю.</summary>
        public static void Splash(Vector3 point, float radius, float damage, Pilot attacker, WeaponDef weapon, Robot exclude)
        {
            int n = Physics.OverlapSphereNonAlloc(point, radius + 1.5f, overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var r = overlap[i].GetComponent<Robot>();
                if (!r || r == exclude || !r.Alive) continue;
                float d = Mathf.Max(0, Vector3.Distance(point, r.Center) - r.Cc.radius);
                if (d > radius) continue;
                float k = 1f - Mathf.Clamp01(d / radius) * 0.7f;
                Match.I.Damage(r, damage * k, attacker, weapon, point);
            }
        }
    }

    public static class Palette
    {
        public static readonly Color Blue = new(0.2f, 0.52f, 1f);
        public static readonly Color Red = new(1f, 0.27f, 0.2f);
        public static readonly Color Neutral = new(0.75f, 0.75f, 0.75f);
        public static readonly Color Gold = new(1f, 0.8f, 0.2f);

        static readonly Color[] ffa =
        {
            new(0.2f, 0.52f, 1f), new(1f, 0.27f, 0.2f), new(1f, 0.6f, 0.1f), new(0.95f, 0.9f, 0.2f),
            new(0.3f, 0.9f, 0.35f), new(0.7f, 0.35f, 1f), new(1f, 0.45f, 0.75f), new(0.9f, 0.9f, 0.95f),
        };

        public static Color Team(int team) => team == 0 ? Blue : team == 1 ? Red : Neutral;
        public static Color Ffa(int index) => ffa[index % ffa.Length];
    }

    public static class Names
    {
        static readonly string[] pool =
        {
            "Болт", "Гайка", "Шестерня", "Ржавый", "Кувалда", "Поршень", "Карбюратор", "Турбина", "Шпиндель",
            "Втулка", "Реле", "Коленвал", "Сварщик", "Молот", "Ковш", "Дроид-17", "Кибер-Вован", "Титаныч",
            "Железный Феликс", "Трансформатор", "Домкрат", "Штангенциркуль", "Паяльник", "Радиатор",
        };

        public static List<string> Shuffled()
        {
            var list = new List<string>(pool);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }
    }

    public static class Vec
    {
        public static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);
        public static float FlatDist(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
    }
}
