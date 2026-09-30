using System.Collections.Generic;
using UnityEngine;

namespace RobotsFight
{
    /// <summary>Тело робота: движение, торс/оружие, способность, здоровье. Решения принимает Brain.</summary>
    public class Robot : MonoBehaviour
    {
        public RobotDef Def { get; private set; }
        public Pilot Pilot { get; private set; }
        public int Team => Pilot.Team;
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public bool Alive { get; private set; } = true;
        public CharacterController Cc { get; private set; }
        public readonly List<Weapon> Weapons = new();
        public Brain Brain;

        // Входы, которые выставляет мозг
        [System.NonSerialized] public Vector3 MoveInput;
        [System.NonSerialized] public float HullYaw;
        [System.NonSerialized] public Vector3 AimPoint;
        [System.NonSerialized] public bool FireInput;
        [System.NonSerialized] public bool Interact;
        [System.NonSerialized] public bool SprintInput;

        // Бег и прыжок
        public const float SprintMultiplier = 1.6f, SprintDuration = 4f, StaminaRegenTime = 3f, JumpSpeed = 9f;
        public float Stamina { get; private set; } = 1f;
        public bool Sprinting { get; private set; }
        float staminaRegenAt;

        public Vector3 Velocity { get; private set; }
        public float Height => Cc.height;
        public Vector3 Center => transform.position + Vector3.up * (Cc.height * 0.5f);
        public Vector3 Top => transform.position + Vector3.up * (Cc.height + 0.4f);
        public Vector3 Eye => torso.position + Vector3.up * (0.6f * Def.Scale);
        public float LastDamagedTime { get; private set; } = -99f;
        public float LastFireTime { get; private set; } = -99f;
        public bool ShieldActive => Time.time < shieldUntil;
        public bool Repairing => Time.time < repairUntil;
        public bool Invulnerable => Time.time < invulnUntil;
        public float AbilityReadyIn => Mathf.Max(0, abilityReadyAt - Time.time);
        public float AbilityCharge => 1f - AbilityReadyIn / Def.AbilityCooldown;

        float TurnSpeed => 140f / Def.Scale;
        float TorsoTurnSpeed => 220f / Def.Scale;

        // Инвентарь: меч и гранаты
        public const float MeleeCooldown = 1.4f, GrenadeCooldown = 1f, SwingTime = 0.3f;
        public int Grenades { get; private set; } = Catalog.GrenadesPerDrop;
        public float MeleeReach => 4.2f * Def.Scale;
        public float MeleeReadyIn => Mathf.Max(0, meleeReadyAt - Time.time);
        public float GrenadeReadyIn => Mathf.Max(0, grenadeReadyAt - Time.time);
        float meleeReadyAt, grenadeReadyAt, swingStart = -99f;
        bool meleeHitDone = true;
        Transform swordPivot;
        static readonly Quaternion SwordSheathed = Quaternion.Euler(-115, 0, 25);

        Transform torso, shieldFx, repairFx, jetFx;
        readonly List<Transform> gunPivots = new();
        readonly List<Transform> legs = new();
        readonly List<float> legBaseYaw = new();
        float torsoYaw, velY, walkPhase, abilityReadyAt, shieldUntil, repairUntil, dashUntil, invulnUntil;
        Vector3 dashVel;
        Pilot lastAttacker;
        WeaponDef lastWeapon;

        public static Robot Create(Pilot pilot, RobotDef def, Vector3 pos, Quaternion rot, Transform parent)
        {
            var go = new GameObject($"Robot_{pilot.Name}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var r = go.AddComponent<Robot>();
            r.Init(pilot, def);
            return r;
        }

        void Init(Pilot pilot, RobotDef def)
        {
            Pilot = pilot;
            Def = def;
            MaxHp = Hp = def.Hp * pilot.HpMult;
            float hipY = BuildVisual();

            float s = def.Scale;
            Cc = gameObject.AddComponent<CharacterController>();
            Cc.height = hipY + 1.15f * s;
            Cc.radius = (def.Chassis == Chassis.Tracks ? 1.1f : 0.95f) * s;
            Cc.center = Vector3.up * (Cc.height * 0.5f);
            Cc.stepOffset = 0.5f;
            Cc.slopeLimit = 50f;
            Cc.skinWidth = 0.08f;

            torsoYaw = HullYaw = transform.eulerAngles.y;
            AimPoint = transform.position + transform.forward * 60f + Vector3.up * Cc.height;
            invulnUntil = Time.time + 2.5f;
            abilityReadyAt = Time.time + 2f;
        }

        // ---------------------------------------------------------------- Визуал из примитивов
        float BuildVisual()
        {
            float s = Def.Scale;
            var dark = new Color(0.16f, 0.17f, 0.19f);
            var metal = new Color(0.42f, 0.44f, 0.47f);
            var team = Pilot.Color;
            var body = Color.Lerp(team, metal, 0.35f);
            var root = transform;
            float hipY;

            switch (Def.Chassis)
            {
                case Chassis.Spider:
                    hipY = 1.1f * s;
                    for (int i = 0; i < 4; i++)
                    {
                        float yaw = 45f + 90f * i;
                        var pivot = Shapes.Node("Leg", root, new Vector3(0, hipY, 0));
                        pivot.localRotation = Quaternion.Euler(0, yaw, 0);
                        Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(0, 0.1f * s, 0.7f * s), new Vector3(0.3f, 0.3f, 1.3f) * s, metal);
                        Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(0, -0.45f * s, 1.35f * s), new Vector3(0.28f, 1.3f, 0.28f) * s, dark);
                        legs.Add(pivot);
                        legBaseYaw.Add(yaw);
                    }
                    Shapes.Prim(PrimitiveType.Cube, root, new Vector3(0, hipY, 0), new Vector3(1.2f, 0.45f, 1.2f) * s, dark);
                    break;

                case Chassis.Tracks:
                    hipY = 1.0f * s;
                    for (int i = -1; i <= 1; i += 2)
                    {
                        Shapes.Prim(PrimitiveType.Cube, root, new Vector3(0.78f * i * s, 0.4f * s, 0), new Vector3(0.55f, 0.8f, 2.3f) * s, dark);
                        for (int w = -1; w <= 1; w++)
                        {
                            var wheel = Shapes.Prim(PrimitiveType.Cylinder, root, new Vector3(0.8f * i * s, 0.4f * s, 0.75f * w * s),
                                new Vector3(0.6f, 0.3f, 0.6f) * s, metal);
                            wheel.localRotation = Quaternion.Euler(0, 0, 90);
                        }
                    }
                    Shapes.Prim(PrimitiveType.Cube, root, new Vector3(0, 0.75f * s, 0), new Vector3(1.1f, 0.5f, 1.9f) * s, metal);
                    break;

                default: // Biped
                    hipY = 1.3f * s;
                    for (int i = -1; i <= 1; i += 2)
                    {
                        var pivot = Shapes.Node("Leg", root, new Vector3(0.45f * i * s, hipY, 0));
                        Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(0, -0.35f * s, 0), new Vector3(0.38f, 0.75f, 0.45f) * s, metal);
                        Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(0, -0.95f * s, 0.05f * s), new Vector3(0.32f, 0.6f, 0.38f) * s, dark);
                        Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(0, -1.24f * s, 0.12f * s), new Vector3(0.45f, 0.12f, 0.75f) * s, dark);
                        legs.Add(pivot);
                        legBaseYaw.Add(0);
                    }
                    Shapes.Prim(PrimitiveType.Cube, root, new Vector3(0, hipY, 0), new Vector3(1.15f, 0.35f, 0.7f) * s, dark);
                    break;
            }

            torso = Shapes.Node("Torso", root, new Vector3(0, hipY + 0.2f * s, 0));
            Shapes.Prim(PrimitiveType.Cube, torso, new Vector3(0, 0.4f * s, -0.05f * s), new Vector3(1.3f, 0.85f, 1.05f) * s, body);
            Shapes.Prim(PrimitiveType.Cube, torso, new Vector3(0, 0.5f * s, 0.5f * s), new Vector3(0.7f, 0.3f, 0.15f) * s, new Color(0.2f, 0.95f, 1f), glow: true);
            Shapes.Prim(PrimitiveType.Cube, torso, new Vector3(0, 0.86f * s, -0.1f * s), new Vector3(1.0f, 0.1f, 0.8f) * s, team, glow: true);
            Shapes.Prim(PrimitiveType.Cube, torso, new Vector3(0, 0.35f * s, -0.7f * s), new Vector3(0.9f, 0.6f, 0.35f) * s, dark);

            for (int i = 0; i < Def.Weapons.Length; i++)
            {
                Vector3 mount = i switch
                {
                    0 => new Vector3(-0.95f, 0.45f, 0.05f),
                    1 => new Vector3(0.95f, 0.45f, 0.05f),
                    _ => new Vector3(i == 2 ? 0.45f : -0.45f, 1.1f, -0.2f),
                } * s;
                var pivot = Shapes.Node("Gun", torso, mount);
                var muzzle = BuildGun(pivot, Def.Weapons[i], s, dark, metal);
                gunPivots.Add(pivot);
                Weapons.Add(new Weapon(Def.Weapons[i], muzzle));
            }

            // Меч за спиной
            swordPivot = Shapes.Node("Sword", torso, new Vector3(0, 0.55f * s, -0.1f * s));
            Shapes.Prim(PrimitiveType.Cube, swordPivot, new Vector3(0, 0, 0.2f * s), new Vector3(0.14f, 0.14f, 0.45f) * s, dark);
            Shapes.Prim(PrimitiveType.Cube, swordPivot, new Vector3(0, 0, 0.45f * s), new Vector3(0.6f, 0.12f, 0.12f) * s, metal);
            Shapes.Prim(PrimitiveType.Cube, swordPivot, new Vector3(0, 0, 1.75f * s), new Vector3(0.08f, 0.3f, 2.5f) * s, Catalog.Sword.Color, glow: true);
            swordPivot.localRotation = SwordSheathed;

            // Эффекты способностей
            shieldFx = Shapes.Node("Shield", torso, new Vector3(0, 0.3f * s, 1.6f * s));
            var sc = new Color(0.3f, 0.8f, 1f);
            Shapes.Prim(PrimitiveType.Cube, shieldFx, Vector3.zero, new Vector3(2.4f, 2.4f, 0.08f) * s, sc, glow: true);
            Shapes.Prim(PrimitiveType.Cube, shieldFx, new Vector3(-1.35f * s, 0, -0.4f * s), new Vector3(0.08f, 2.4f, 0.9f) * s, sc, glow: true);
            Shapes.Prim(PrimitiveType.Cube, shieldFx, new Vector3(1.35f * s, 0, -0.4f * s), new Vector3(0.08f, 2.4f, 0.9f) * s, sc, glow: true);
            shieldFx.gameObject.SetActive(false);

            repairFx = Shapes.Prim(PrimitiveType.Sphere, root, new Vector3(0, hipY + 1.8f * s, 0), Vector3.one * 0.5f * s, new Color(0.3f, 1f, 0.4f), glow: true);
            repairFx.gameObject.SetActive(false);

            jetFx = Shapes.Node("Jets", root, new Vector3(0, hipY * 0.6f, -0.6f * s));
            for (int i = -1; i <= 1; i += 2)
                Shapes.Prim(PrimitiveType.Sphere, jetFx, new Vector3(0.35f * i * s, 0, 0), new Vector3(0.35f, 0.7f, 0.35f) * s, new Color(1f, 0.55f, 0.1f), glow: true);
            jetFx.gameObject.SetActive(false);

            return hipY;
        }

        static Transform BuildGun(Transform pivot, WeaponDef w, float s, Color dark, Color metal)
        {
            float len;
            switch (w.Model)
            {
                case GunModel.Plasma:
                    Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(0, 0, 0.2f * s), new Vector3(0.45f, 0.45f, 0.75f) * s, metal);
                    Barrel(pivot, 0.34f * s, 0.55f * s, 0.55f * s, dark);
                    Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(0, 0, 1.1f * s), new Vector3(0.28f, 0.28f, 0.05f) * s, w.Color, glow: true);
                    len = 1.15f;
                    break;
                case GunModel.Rockets:
                    Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(0, 0, 0.1f * s), new Vector3(0.65f, 0.5f, 0.85f) * s, dark);
                    for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(0.15f * x * s, 0.11f * y * s, 0.53f * s), new Vector3(0.16f, 0.14f, 0.02f) * s, w.Color, glow: true);
                    len = 0.6f;
                    break;
                case GunModel.Cannon:
                    Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(0, 0, 0.15f * s), new Vector3(0.55f, 0.55f, 0.95f) * s, metal);
                    Barrel(pivot, 0.26f * s, 1.7f * s, 0.55f * s, dark);
                    len = 2.25f;
                    break;
                case GunModel.Rail:
                    Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(0, 0, 0.1f * s), new Vector3(0.45f, 0.45f, 0.8f) * s, metal);
                    Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(-0.13f * s, 0, 1.3f * s), new Vector3(0.07f, 0.22f, 2.0f) * s, dark);
                    Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(0.13f * s, 0, 1.3f * s), new Vector3(0.07f, 0.22f, 2.0f) * s, dark);
                    Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(0, 0, 1.2f * s), new Vector3(0.06f, 0.06f, 1.8f) * s, w.Color, glow: true);
                    len = 2.3f;
                    break;
                default:
                    Shapes.Prim(PrimitiveType.Cube, pivot, new Vector3(0, 0, 0.1f * s), new Vector3(0.36f, 0.36f, 0.65f) * s, metal);
                    Barrel(pivot, 0.14f * s, 1.0f * s, 0.4f * s, dark);
                    len = 1.4f;
                    break;
            }
            return Shapes.Node("Muzzle", pivot, new Vector3(0, 0, len * s));
        }

        static void Barrel(Transform pivot, float diameter, float length, float start, Color c)
        {
            var b = Shapes.Prim(PrimitiveType.Cylinder, pivot, new Vector3(0, 0, start + length * 0.5f), new Vector3(diameter, length * 0.5f, diameter), c);
            b.localRotation = Quaternion.Euler(90, 0, 0);
        }

        // ---------------------------------------------------------------- Цикл
        void Update()
        {
            if (!Alive) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            var match = Match.I;
            if (match && !match.Over && Brain != null) Brain.Think(dt);
            else
            {
                MoveInput = Vector3.zero;
                FireInput = false;
            }

            // Корпус
            float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, HullYaw, TurnSpeed * dt);
            transform.rotation = Quaternion.Euler(0, yaw, 0);

            // Движение
            // Бег: тратит выносливость, восстанавливается через секунду после остановки бега
            bool moving = Vec.Flat(MoveInput).sqrMagnitude > 0.01f;
            if (!SprintInput || !moving || Stamina <= 0f) Sprinting = false;
            else if (Stamina > 0.15f) Sprinting = true; // стартовать бег можно только с запасом
            if (Sprinting)
            {
                Stamina = Mathf.Max(0f, Stamina - dt / SprintDuration);
                staminaRegenAt = Time.time + 1f;
            }
            else if (Time.time >= staminaRegenAt) Stamina = Mathf.Min(1f, Stamina + dt / StaminaRegenTime);

            Vector3 move = Vector3.ClampMagnitude(Vec.Flat(MoveInput), 1f) * (Def.Speed * (Sprinting ? SprintMultiplier : 1f));
            if (Time.time < dashUntil) move += dashVel;
            if (Cc.isGrounded && velY < 0) velY = -2f;
            velY -= 25f * dt;
            Vector3 before = transform.position;
            Cc.Move((move + Vector3.up * velY) * dt);
            Velocity = (transform.position - before) / dt;

            // Торс и стволы смотрят на точку прицела
            Vector3 toAim = Vec.Flat(AimPoint - torso.position);
            if (toAim.sqrMagnitude > 0.01f)
                torsoYaw = Mathf.MoveTowardsAngle(torsoYaw, Quaternion.LookRotation(toAim).eulerAngles.y, TorsoTurnSpeed * dt);
            torso.rotation = Quaternion.Euler(0, torsoYaw, 0);
            foreach (var gp in gunPivots)
            {
                Vector3 local = torso.InverseTransformDirection(AimPoint - gp.position);
                if (local.z < 0.3f) local.z = 0.3f;
                gp.localRotation = Quaternion.LookRotation(local);
            }

            // Стрельба (только если торс довернулся)
            foreach (var w in Weapons) w.Tick(dt);
            bool facing = toAim.sqrMagnitude < 0.01f || Vector3.Angle(torso.forward, toAim) < 25f;
            if (FireInput && facing)
            {
                float dist = Vector3.Distance(torso.position, AimPoint);
                foreach (var w in Weapons)
                {
                    if (!Pilot.IsPlayer && dist > w.Def.Range) continue; // боты не тратят патроны впустую
                    if (w.TryFire(this, AimPoint)) LastFireTime = Time.time;
                }
            }

            UpdateSwing();

            // Способности
            if (Repairing) Hp = Mathf.Min(MaxHp, Hp + MaxHp * 0.35f / 3f * dt);
            shieldFx.gameObject.SetActive(ShieldActive);
            repairFx.gameObject.SetActive(Repairing);
            if (Repairing) repairFx.localScale = Vector3.one * (0.4f + 0.15f * Mathf.Sin(Time.time * 12f)) * Def.Scale;
            jetFx.gameObject.SetActive(Def.Ability == AbilityType.JumpJet ? !Cc.isGrounded : Time.time < dashUntil);

            AnimateLegs(dt);

            if (transform.position.y < -30f) Die(lastAttacker, lastWeapon);
        }

        void AnimateLegs(float dt)
        {
            float speed = Vec.Flat(Velocity).magnitude;
            float dir = Vector3.Dot(Velocity, transform.forward) >= 0 ? 1f : -1f;
            walkPhase += speed * dt * 1.6f / Def.Scale * dir;
            float amp = Mathf.Clamp01(speed / 3f);
            for (int i = 0; i < legs.Count; i++)
            {
                float ph = walkPhase + (i % 2 == 0 ? 0 : Mathf.PI);
                if (Def.Chassis == Chassis.Spider)
                    legs[i].localRotation = Quaternion.Euler(Mathf.Max(0, Mathf.Sin(ph)) * -12f * amp, legBaseYaw[i] + Mathf.Cos(ph) * 12f * amp, 0);
                else
                    legs[i].localRotation = Quaternion.Euler(Mathf.Sin(ph) * 32f * amp, 0, 0);
            }
        }

        // ---------------------------------------------------------------- Действия
        public void ReloadAll()
        {
            foreach (var w in Weapons) w.Reload();
        }

        public void ReloadIfLow(float fraction)
        {
            foreach (var w in Weapons)
                if (w.Ammo < w.Def.Magazine * fraction) w.Reload();
        }

        /// <summary>Обычный прыжок — только с земли.</summary>
        public bool Jump()
        {
            if (!Alive || !Cc.isGrounded) return false;
            velY = JumpSpeed;
            return true;
        }

        public bool UseAbility()
        {
            if (!Alive || Time.time < abilityReadyAt) return false;
            abilityReadyAt = Time.time + Def.AbilityCooldown;
            Vector3 dir = Vec.Flat(MoveInput).sqrMagnitude > 0.01f ? Vec.Flat(MoveInput).normalized : transform.forward;
            switch (Def.Ability)
            {
                case AbilityType.Dash:
                    dashVel = dir * 24f;
                    dashUntil = Time.time + 0.35f;
                    Fx.Blob(transform.position + Vector3.up, new Color(1f, 0.6f, 0.2f), 1.5f, 0.2f, 0.3f);
                    break;
                case AbilityType.Shield:
                    shieldUntil = Time.time + 5f;
                    break;
                case AbilityType.Repair:
                    repairUntil = Time.time + 3f;
                    break;
                case AbilityType.JumpJet:
                    velY = 14f;
                    dashVel = dir * 7f;
                    dashUntil = Time.time + 1.3f;
                    Fx.Blob(transform.position, new Color(1f, 0.55f, 0.1f), 2.5f, 0.3f, 0.4f);
                    break;
            }
            return true;
        }

        /// <summary>Удар мечом: короткий выпад вперёд + дуга 150° перед торсом.</summary>
        public bool Swing()
        {
            if (!Alive || Time.time < meleeReadyAt) return false;
            meleeReadyAt = Time.time + MeleeCooldown;
            swingStart = Time.time;
            meleeHitDone = false;
            dashVel = Vec.Flat(torso.forward).normalized * 12f;
            dashUntil = Time.time + 0.18f;
            return true;
        }

        void UpdateSwing()
        {
            float k = (Time.time - swingStart) / SwingTime;
            if (k >= 1f)
            {
                swordPivot.localRotation = Quaternion.Slerp(swordPivot.localRotation, SwordSheathed, 1f - Mathf.Exp(-12f * Time.deltaTime));
                return;
            }
            swordPivot.localRotation = Quaternion.Euler(12f, Mathf.Lerp(-110f, 110f, Mathf.SmoothStep(0, 1, k)), 0);
            if (!meleeHitDone && k >= 0.35f)
            {
                meleeHitDone = true;
                MeleeHit();
            }
        }

        void MeleeHit()
        {
            var m = Match.I;
            if (!m) return;
            Vector3 fwd = Vec.Flat(torso.forward).normalized;
            foreach (var p in m.Pilots)
            {
                var e = p.Body;
                if (!e || !e.Alive || !m.AreEnemies(Pilot, p)) continue;
                Vector3 to = Vec.Flat(e.Center - torso.position);
                if (to.magnitude - e.Cc.radius > MeleeReach || Vector3.Angle(fwd, to) > 75f) continue;
                Vector3 hitPoint = e.Center - to.normalized * e.Cc.radius;
                m.Damage(e, Catalog.Sword.Damage, Pilot, Catalog.Sword, hitPoint);
                Fx.Blob(hitPoint, Catalog.Sword.Color, 1.6f, 0.2f, 0.2f);
            }
        }

        /// <summary>Граната по баллистике точно в точку прицела (не дальше 55 м).</summary>
        public bool ThrowGrenade()
        {
            if (!Alive || Grenades <= 0 || Time.time < grenadeReadyAt) return false;
            grenadeReadyAt = Time.time + GrenadeCooldown;
            Grenades--;
            var g = Catalog.Grenade;
            Vector3 origin = torso.position + Vector3.up * (1.2f * Def.Scale) + torso.forward * (0.8f * Def.Scale);
            Vector3 target = AimPoint;
            Vector3 flat = Vec.Flat(target - origin);
            if (flat.magnitude > 55f) target = new Vector3(origin.x, transform.position.y, origin.z) + flat.normalized * 55f;
            Vector3 delta = target - origin;
            float t = Mathf.Clamp(Vec.Flat(delta).magnitude / 24f, 0.45f, 2.2f);
            Vector3 vel = delta / t + Vector3.up * (0.5f * g.Gravity * t);
            Projectiles.SpawnVel(this, origin, vel, g);
            return true;
        }

        public void RepairFull() => Hp = MaxHp;

        /// <summary>Возвращает фактически нанесённый урон.</summary>
        public float TakeDamage(float dmg, Pilot attacker, WeaponDef weapon, Vector3 point)
        {
            if (!Alive || Invulnerable) return 0;
            if (ShieldActive) dmg *= 0.3f;
            dmg = Mathf.Min(dmg, Hp);
            Hp -= dmg;
            LastDamagedTime = Time.time;
            if (attacker != null)
            {
                lastAttacker = attacker;
                lastWeapon = weapon;
            }
            if (Hp <= 0.01f) Die(attacker ?? lastAttacker, weapon ?? lastWeapon);
            return dmg;
        }

        void Die(Pilot killer, WeaponDef weapon)
        {
            if (!Alive) return;
            Alive = false;
            Hp = 0;
            Fx.RobotExplosion(Center, Def.Scale, Pilot.Color);
            Match.I?.OnRobotKilled(this, killer, weapon);
            Destroy(gameObject);
        }
    }
}
