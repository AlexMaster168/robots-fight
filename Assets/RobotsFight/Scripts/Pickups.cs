using System.Collections.Generic;
using UnityEngine;

namespace RobotsFight
{
    public enum PickupType { Medkit, Shield }

    /// <summary>
    /// Дроп с уничтоженных роботов: аптечка или щит. Подбирается любым роботом, наступившим на него, эффект сразу.
    /// Для ботов это единственный способ лечиться и закрываться щитом.
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        public const float Lifetime = 30f, Radius = 2.2f;
        public static readonly List<Pickup> All = new();
        public static int PickedCount;

        public PickupType Type { get; private set; }
        public Color Color => Type == PickupType.Medkit ? new Color(0.3f, 1f, 0.4f) : new Color(0.4f, 0.8f, 1f);

        float expireAt;
        Transform item;
        Vector3 basePos;

        public static void Spawn(Vector3 pos, PickupType type)
        {
            if (!Match.I || Match.I.Arena.InLava(pos)) return;
            // Погиб в воздухе — дроп падает на землю (или на крышу под ним)
            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out var hit, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                pos = hit.point;
            var go = new GameObject($"Pickup_{type}");
            go.transform.SetParent(Match.I.transform, false);
            go.transform.position = new Vector3(pos.x, Mathf.Max(0f, pos.y), pos.z);
            var p = go.AddComponent<Pickup>();
            p.Type = type;
            p.expireAt = Time.time + Lifetime;
            p.basePos = go.transform.position;
            p.Build();
        }

        void Build()
        {
            var c = Color;
            item = Shapes.Node("Item", transform, new Vector3(0, 1.2f, 0));
            if (Type == PickupType.Medkit)
            {
                Shapes.Prim(PrimitiveType.Cube, item, Vector3.zero, new Vector3(1f, 1f, 1f), new Color(0.92f, 0.92f, 0.92f));
                for (int s = -1; s <= 1; s += 2)
                {
                    Shapes.Prim(PrimitiveType.Cube, item, new Vector3(0, 0, 0.5f * s), new Vector3(0.7f, 0.22f, 0.04f), c, glow: true);
                    Shapes.Prim(PrimitiveType.Cube, item, new Vector3(0, 0, 0.5f * s), new Vector3(0.22f, 0.7f, 0.04f), c, glow: true);
                }
            }
            else
            {
                var gem = Shapes.Prim(PrimitiveType.Cube, item, Vector3.zero, Vector3.one * 0.85f, c, glow: true);
                gem.localRotation = Quaternion.Euler(45, 0, 45);
                Shapes.Prim(PrimitiveType.Sphere, item, Vector3.zero, Vector3.one * 0.55f, Color.white, glow: true);
            }
            // Столб света — видно издалека
            var beam = Shapes.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 4f, 0), new Vector3(0.12f, 4f, 0.12f), c, glow: true);
            beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            float left = expireAt - Time.time;
            if (left <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            item.Rotate(0, 120f * Time.deltaTime, 0, Space.World);
            item.localPosition = new Vector3(0, 1.2f + 0.25f * Mathf.Sin(Time.time * 3f), 0);
            // Последние 5 секунд мигает
            bool visible = left > 5f || Mathf.Repeat(left, 0.4f) > 0.15f;
            if (item.gameObject.activeSelf != visible) item.gameObject.SetActive(visible);

            var m = Match.I;
            if (!m) return;
            foreach (var p in m.Pilots)
            {
                var r = p.Body;
                if (!r || !r.Alive) continue;
                Vector3 d = r.transform.position - basePos;
                if (Mathf.Abs(d.y) > 3f || Vec.Flat(d).magnitude > Radius + r.Cc.radius) continue;
                bool taken = Type == PickupType.Medkit ? r.ApplyMedkit() : r.ApplyShield();
                if (!taken) continue;
                PickedCount++;
                Fx.Blob(r.Center, Color, 3f * r.Def.Scale, 0.4f, 0.3f);
                if (p == m.Player) m.ShowBanner(Type == PickupType.Medkit ? "+ Аптечка" : "+ Щит", Color, 1.2f);
                Destroy(gameObject);
                return;
            }
        }
    }
}
