using System.Collections.Generic;
using UnityEngine;

namespace RobotsFight
{
    public class Weapon
    {
        public readonly WeaponDef Def;
        public readonly Transform Muzzle;
        public int Ammo { get; private set; }
        public bool Reloading => reloadLeft > 0;
        public float ReloadProgress => Reloading ? 1f - reloadLeft / Def.ReloadTime : 1f;

        float cooldown, reloadLeft;

        public Weapon(WeaponDef def, Transform muzzle)
        {
            Def = def;
            Muzzle = muzzle;
            Ammo = def.Magazine;
        }

        public void Tick(float dt)
        {
            if (cooldown > 0) cooldown -= dt;
            if (reloadLeft > 0)
            {
                reloadLeft -= dt;
                if (reloadLeft <= 0)
                {
                    reloadLeft = 0;
                    Ammo = Def.Magazine;
                }
            }
        }

        public void Reload()
        {
            if (!Reloading && Ammo < Def.Magazine) reloadLeft = Def.ReloadTime;
        }

        public bool TryFire(Robot owner, Vector3 aimPoint)
        {
            if (Reloading || cooldown > 0) return false;
            if (Ammo <= 0)
            {
                Reload();
                return false;
            }
            cooldown += 1f / Def.FireRate;
            Ammo--;

            Vector3 origin = Muzzle.position;
            Vector3 dir = (aimPoint - origin).normalized;
            for (int i = 0; i < Def.Pellets; i++)
                Projectiles.Spawn(owner, origin, Spread(dir, Def.Spread), Def);
            Fx.Blob(origin, Def.Color, Def.Size * 3f, Def.Size * 0.5f, 0.07f);

            if (Ammo == 0) Reload();
            return true;
        }

        static Vector3 Spread(Vector3 dir, float degrees)
        {
            if (degrees <= 0) return dir;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(dir, right);
            Vector2 r = Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            return (dir + right * r.x + up * r.y).normalized;
        }
    }

    /// <summary>Все снаряды матча. Каждый кадр — свип-рейкаст на длину шага, так быстрые снаряды не пролетают сквозь цели.</summary>
    public class Projectiles : MonoBehaviour
    {
        static Projectiles I;

        class Shot
        {
            public Pilot Owner;
            public Collider OwnerCol;
            public WeaponDef Def;
            public Vector3 Pos, Vel;
            public float Traveled, Age;
            public Transform Vis;
        }

        readonly List<Shot> live = new();
        readonly Dictionary<WeaponDef, Stack<Transform>> pool = new();

        void Awake() => I = this;
        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public static void Spawn(Robot owner, Vector3 pos, Vector3 dir, WeaponDef def) => SpawnVel(owner, pos, dir * def.Speed, def);

        public static void SpawnVel(Robot owner, Vector3 pos, Vector3 vel, WeaponDef def)
        {
            if (!I) return;
            var s = new Shot
            {
                Owner = owner.Pilot, OwnerCol = owner.Cc, Def = def, Pos = pos, Vel = vel, Vis = I.TakeVis(def),
            };
            s.Vis.SetPositionAndRotation(pos, Quaternion.LookRotation(vel));
            I.live.Add(s);
        }

        Transform TakeVis(WeaponDef def)
        {
            if (!pool.TryGetValue(def, out var stack)) pool[def] = stack = new Stack<Transform>();
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (!t) continue;
                t.gameObject.SetActive(true);
                return t;
            }
            float len = def.Model switch
            {
                GunModel.Rail => 6f,
                GunModel.Autocannon => 2.5f,
                GunModel.Rockets => 3f,
                GunModel.Grenade => 1f,
                _ => 1.5f,
            };
            var v = Shapes.Prim(PrimitiveType.Sphere, transform, Vector3.zero, new Vector3(def.Size, def.Size, def.Size * len), def.Color, glow: true);
            v.name = "Shot";
            return v;
        }

        void Release(Shot s)
        {
            if (!s.Vis) return;
            s.Vis.gameObject.SetActive(false);
            pool[s.Def].Push(s.Vis);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var s = live[i];
                s.Age += dt;
                if (s.Def.Gravity > 0) s.Vel += Vector3.down * (s.Def.Gravity * dt);
                Vector3 step = s.Vel * dt;
                float len = step.magnitude;
                bool done = false;
                if (s.Def.Fuse > 0 && s.Age >= s.Def.Fuse)
                {
                    Impact(s, s.Pos, null);
                    done = true;
                }
                else if (len > 0f && Phys.Raycast(s.Pos, step / len, len, s.OwnerCol, out var hit))
                {
                    Impact(s, hit.point, hit.collider);
                    done = true;
                }
                else
                {
                    s.Pos += step;
                    s.Traveled += len;
                    if (s.Traveled > s.Def.Range)
                    {
                        if (s.Def.Splash > 0) Impact(s, s.Pos, null);
                        done = true;
                    }
                }

                if (done)
                {
                    Release(s);
                    live[i] = live[live.Count - 1];
                    live.RemoveAt(live.Count - 1);
                }
                else if (s.Vis) s.Vis.position = s.Pos;
            }
        }

        static void Impact(Shot s, Vector3 point, Collider col)
        {
            Robot direct = col ? col.GetComponent<Robot>() : null;
            if (direct) Match.I.Damage(direct, s.Def.Damage, s.Owner, s.Def, point);
            if (s.Def.Splash > 0)
            {
                Fx.Explosion(point, s.Def.Splash, s.Def.Color);
                Phys.Splash(point, s.Def.Splash, s.Def.Damage * 0.6f, s.Owner, s.Def, direct);
            }
            else Fx.Blob(point, s.Def.Color, s.Def.Size * 3f, 0.05f, 0.15f);
        }
    }

    /// <summary>Простые эффекты: светящиеся сферы, дым, обломки. Пул, чтобы не спамить аллокациями.</summary>
    public class Fx : MonoBehaviour
    {
        static readonly Stack<Fx> pool = new();
        Renderer rend;
        float life, age;
        Vector3 startScale, endScale, vel;
        bool pooled;

        static Transform Root => Match.I ? Match.I.transform : null;

        public static void Blob(Vector3 pos, Color c, float startSize, float endSize, float life, Vector3 vel = default, bool glow = true)
        {
            if (!Root) return;
            Fx f = null;
            while (pool.Count > 0 && !f) f = pool.Pop();
            if (!f)
            {
                var t = Shapes.Prim(PrimitiveType.Sphere, Root, Vector3.zero, Vector3.one, c, glow: glow);
                t.name = "Fx";
                f = t.gameObject.AddComponent<Fx>();
                f.rend = t.GetComponent<Renderer>();
                f.rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                f.pooled = true;
            }
            f.gameObject.SetActive(true);
            f.rend.sharedMaterial = Mats.Get(c, glow);
            f.transform.position = pos;
            f.startScale = Vector3.one * startSize;
            f.endScale = Vector3.one * endSize;
            f.transform.localScale = f.startScale;
            f.life = life;
            f.age = 0;
            f.vel = vel;
        }

        public static void Explosion(Vector3 pos, float radius, Color c)
        {
            Blob(pos, new Color(1f, 0.75f, 0.3f), radius * 0.4f, radius * 1.8f, 0.25f);
            Blob(pos, c, radius * 0.3f, radius * 1.2f, 0.35f);
            Blob(pos + Vector3.up * 0.5f, new Color(0.2f, 0.2f, 0.2f), radius * 0.35f, radius * 0.1f, 0.9f, Vector3.up * 3f, false);
        }

        public static void RobotExplosion(Vector3 center, float scale, Color c)
        {
            if (!Root) return;
            Explosion(center, 5f * scale, new Color(1f, 0.4f, 0.1f));
            for (int i = 0; i < 8; i++)
            {
                var t = Shapes.Prim(PrimitiveType.Cube, Root, center + Random.insideUnitSphere * scale,
                    Vector3.one * Random.Range(0.3f, 0.8f) * scale, i % 3 == 0 ? c : new Color(0.2f, 0.2f, 0.22f), collider: true);
                t.name = "Debris";
                var rb = t.gameObject.AddComponent<Rigidbody>();
                rb.mass = 50f;
                rb.linearVelocity = (Random.insideUnitSphere + Vector3.up * 1.2f) * 9f;
                rb.angularVelocity = Random.insideUnitSphere * 10f;
                var f = t.gameObject.AddComponent<Fx>();
                f.life = 5f;
                f.startScale = f.endScale = t.localScale;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age >= life)
            {
                if (pooled)
                {
                    gameObject.SetActive(false);
                    pool.Push(this);
                }
                else Destroy(gameObject);
                return;
            }
            float k = age / life;
            transform.localScale = Vector3.Lerp(startScale, endScale, k);
            if (vel != Vector3.zero) transform.position += vel * dt;
        }
    }
}
