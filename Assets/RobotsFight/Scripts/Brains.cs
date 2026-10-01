using UnityEngine;

namespace RobotsFight
{
    public abstract class Brain
    {
        public Robot R;
        public abstract void Think(float dt);
    }

    public class PlayerBrain : Brain
    {
        public override void Think(float dt)
        {
            var cam = PlayerCamera.I;
            if (!cam || GameRoot.InputBlocked)
            {
                R.MoveInput = Vector3.zero;
                R.FireInput = false;
                R.Interact = false;
                R.SprintInput = false;
                R.FlyInput = false;
                return;
            }
            R.SprintInput = GameInput.Sprint;
            if (GameInput.Jump) R.Jump();
            R.FlyInput = GameInput.JumpHeld;
            if (GameInput.Howitzer) R.FireHowitzer();
            Vector2 m = GameInput.Move;
            R.MoveInput = Vector3.ClampMagnitude(cam.FlatForward * m.y + cam.FlatRight * m.x, 1f);
            R.HullYaw = cam.Yaw;
            R.AimPoint = cam.AimPoint;
            R.FireInput = GameInput.Fire;
            R.Interact = GameInput.Interact;
            if (GameInput.Ability) R.UseAbility();
            if (GameInput.Melee) R.Swing();
            if (GameInput.Grenade) R.ThrowGrenade();
            if (GameInput.Medkit) R.UseMedkit();
            if (GameInput.ShieldItem) R.UseShieldItem();
        }
    }

    /// <summary>
    /// ИИ без навмеша: цель режима (маяк/бомба/игрок) + выбор врага по видимости,
    /// стрейф в бою, обход препятствий сфер-кастами, детект застревания.
    /// </summary>
    public class BotBrain : Brain
    {
        public float Skill = 0.5f; // 0..1

        Robot target;
        bool targetVisible;
        float retargetIn, reaction, aimJitterIn, abilityCheckIn, strafeSwitchIn, wanderIn, grenadeCheckIn;
        float howitzerCheckIn, flyCheckIn, flyUntil, vaultTryAt;
        float strafeSign = 1f, avoidSide = 1f, stuckTime, unstuckUntil;
        Vector3 aimError, unstuckDir, wanderOffset;

        public override void Think(float dt)
        {
            var match = Match.I;
            R.Interact = true; // боты всегда "держат E" — режим сам решает, где это важно

            retargetIn -= dt;
            if (retargetIn <= 0)
            {
                retargetIn = 0.35f;
                PickTarget(match);
            }
            if (target && !target.Alive) target = null;

            strafeSwitchIn -= dt;
            if (strafeSwitchIn <= 0)
            {
                strafeSwitchIn = Random.Range(1.2f, 3f);
                strafeSign = Random.value < 0.5f ? -1f : 1f;
            }

            Vector3 pos = R.transform.position;
            Objective? obj = match.Mode.BotObjective(R);
            // Подбитый бот идёт за аптечкой из дропа, если она рядом (свои аптечки ботам запрещены)
            if (R.Hp < R.MaxHp * 0.5f)
            {
                var kit = NearestPickup(PickupType.Medkit, 35f);
                if (kit) obj = new Objective(kit.transform.position, 0.5f);
            }
            bool fighting = target && targetVisible;
            Vector3 desired;

            if (obj.HasValue && Vec.FlatDist(obj.Value.Pos, pos) > obj.Value.Radius)
            {
                desired = Vec.Flat(obj.Value.Pos - pos).normalized;
                if (fighting) desired += StrafeDir() * 0.35f;
            }
            else if (fighting)
            {
                Vector3 to = Vec.Flat(target.transform.position - pos);
                float d = to.magnitude;
                desired = StrafeDir();
                float pref = R.Def.PreferredRange;
                if (d > pref) desired += to.normalized;
                else if (d < pref * 0.4f) desired -= to.normalized * 0.7f;
                // на объекте не уходим далеко
                if (obj.HasValue && Vec.FlatDist(obj.Value.Pos, pos) > obj.Value.Radius * 0.7f)
                    desired += Vec.Flat(obj.Value.Pos - pos).normalized * 0.8f;
            }
            else if (obj.HasValue)
            {
                wanderIn -= dt;
                if (wanderIn <= 0)
                {
                    wanderIn = Random.Range(2f, 4f);
                    Vector2 c = Random.insideUnitCircle * obj.Value.Radius * 0.6f;
                    wanderOffset = new Vector3(c.x, 0, c.y);
                }
                Vector3 to = Vec.Flat(obj.Value.Pos + wanderOffset - pos);
                desired = to.magnitude > 1.5f ? to.normalized * 0.5f : Vector3.zero;
            }
            else
            {
                var hunt = target ? target : NearestEnemy(match);
                desired = hunt ? Vec.Flat(hunt.transform.position - pos).normalized : Vector3.zero;
            }

            desired = Avoid(desired);

            // Застревание
            if (Time.time < unstuckUntil) desired = unstuckDir;
            else if (desired.sqrMagnitude > 0.1f)
            {
                if (Vec.Flat(R.Velocity).magnitude < R.Def.Speed * 0.2f) stuckTime += dt;
                else stuckTime = 0;
                if (stuckTime > 1.2f)
                {
                    stuckTime = 0;
                    unstuckUntil = Time.time + 1f;
                    unstuckDir = Quaternion.Euler(0, Random.Range(100f, 260f), 0) * desired.normalized;
                    if (R.Def.Ability == AbilityType.JumpJet) R.UseAbility();
                    else R.Jump();
                }
            }

            R.MoveInput = Vector3.ClampMagnitude(desired, 1f);
            // Бегом — только к далёкой цели и не в перестрелке
            float objDist = obj.HasValue ? Vec.FlatDist(obj.Value.Pos, pos) : 0f;
            R.SprintInput = !fighting && (objDist > 35f || (!obj.HasValue && !target));

            // Далеко до цели и ранец заправлен — иногда перелетаем
            flyCheckIn -= dt;
            if (flyCheckIn <= 0)
            {
                flyCheckIn = 1.5f;
                if (!fighting && objDist > 70f && R.Fuel > 0.95f && Random.value < 0.25f && R.Jump())
                    flyUntil = Time.time + Random.Range(1.2f, 2.2f);
            }
            R.FlyInput = Time.time < flyUntil;

            UseHowitzer(dt, match);
            if (R.MoveInput.sqrMagnitude > 0.01f) R.HullYaw = Quaternion.LookRotation(R.MoveInput).eulerAngles.y;

            // Прицеливание
            if (fighting)
            {
                reaction -= dt;
                float dist = Vector3.Distance(R.Eye, target.Center);
                aimJitterIn -= dt;
                if (aimJitterIn <= 0)
                {
                    aimJitterIn = 0.4f;
                    aimError = Random.insideUnitSphere * ((1.25f - Skill) * 0.05f * dist);
                }
                float projSpeed = R.Weapons[0].Def.Speed;
                Vector3 lead = target.Velocity * (dist / projSpeed) * Mathf.Lerp(0.3f, 1f, Skill);
                R.AimPoint = target.Center + lead + aimError;
                R.FireInput = reaction <= 0 && dist < R.Def.MaxRange * 0.95f;

                // Вплотную — рубим мечом
                float flatD = Vec.FlatDist(target.transform.position, pos) - target.Cc.radius;
                if (reaction <= 0 && flatD < R.MeleeReach * 0.9f && R.MeleeReadyIn <= 0) R.Swing();

                // Средняя дистанция — иногда граната под ноги
                grenadeCheckIn -= dt;
                if (grenadeCheckIn <= 0)
                {
                    grenadeCheckIn = 0.6f;
                    if (R.Grenades > 0 && flatD > 12f && flatD < 50f && Random.value < 0.04f + 0.1f * Skill)
                    {
                        R.AimPoint = target.transform.position + target.Velocity * 0.9f;
                        R.ThrowGrenade();
                    }
                }
            }
            else
            {
                R.FireInput = false;
                Vector3 look = R.MoveInput.sqrMagnitude > 0.01f ? R.MoveInput : R.transform.forward;
                R.AimPoint = pos + look * 40f + Vector3.up * R.Height;
            }

            UseAbilities(dt, obj, fighting);
        }

        /// <summary>Гаубица — по дальним врагам (60–250 м), даже без прямой видимости. С упреждением.</summary>
        void UseHowitzer(float dt, Match match)
        {
            howitzerCheckIn -= dt;
            if (howitzerCheckIn > 0 || R.HowitzerReadyIn > 0) return;
            howitzerCheckIn = 1.5f;
            var e = target ? target : NearestEnemy(match);
            if (!e) return;
            float d = Vec.FlatDist(e.transform.position, R.transform.position);
            if (d < 60f || d > 250f || Random.value > 0.25f + 0.35f * Skill) return;
            float flight = Mathf.Clamp(d / 45f, 1.6f, 4.5f);
            R.AimPoint = e.transform.position + Vec.Flat(e.Velocity) * (flight * Mathf.Lerp(0.2f, 0.7f, Skill));
            R.FireHowitzer();
        }

        Vector3 StrafeDir()
        {
            if (!target) return Vector3.zero;
            Vector3 to = Vec.Flat(target.transform.position - R.transform.position).normalized;
            return Vector3.Cross(Vector3.up, to) * strafeSign;
        }

        void UseAbilities(float dt, Objective? obj, bool fighting)
        {

            abilityCheckIn -= dt;
            if (abilityCheckIn > 0 || R.AbilityReadyIn > 0) return;
            abilityCheckIn = 0.5f;
            float hp = R.Hp / R.MaxHp;
            bool underFire = Time.time - R.LastDamagedTime < 0.6f;
            switch (R.Def.Ability)
            {
                case AbilityType.Dash:
                    if ((fighting && (hp < 0.35f || Random.value < 0.1f)) ||
                        (obj.HasValue && Vec.FlatDist(obj.Value.Pos, R.transform.position) > 40f && Random.value < 0.15f))
                        R.UseAbility();
                    break;
                case AbilityType.Shield:
                    if (underFire && hp < 0.85f) R.UseAbility();
                    break;
                case AbilityType.Repair:
                    if (hp < 0.45f) R.UseAbility();
                    break;
                case AbilityType.JumpJet:
                    if (fighting && Random.value < 0.08f) R.UseAbility();
                    break;
            }
        }

        void PickTarget(Match match)
        {
            Robot best = null;
            bool bestVis = false;
            float bestScore = float.MaxValue;
            foreach (var p in match.Pilots)
            {
                var e = p.Body;
                if (!e || !e.Alive || !match.AreEnemies(R.Pilot, p)) continue;
                float d = Vector3.Distance(R.Center, e.Center);
                if (d > 220f) continue;
                bool vis = HasLos(e);
                float score = d * (vis ? 1f : 4f) - (e == target ? 15f : 0f) - (1f - e.Hp / e.MaxHp) * 25f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = e;
                    bestVis = vis;
                }
            }
            if (best != target) reaction = Mathf.Lerp(0.9f, 0.15f, Skill) * Random.Range(0.7f, 1.3f);
            target = best;
            targetVisible = best && bestVis;
        }

        Pickup NearestPickup(PickupType type, float maxDist)
        {
            Pickup best = null;
            float bestD = maxDist;
            foreach (var p in Pickup.All)
            {
                if (p.Type != type) continue;
                float d = Vec.FlatDist(p.transform.position, R.transform.position);
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                }
            }
            return best;
        }

        Robot NearestEnemy(Match match)
        {
            Robot best = null;
            float bestD = float.MaxValue;
            foreach (var p in match.Pilots)
            {
                var e = p.Body;
                if (!e || !e.Alive || !match.AreEnemies(R.Pilot, p)) continue;
                float d = Vec.FlatDist(e.transform.position, R.transform.position);
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        bool HasLos(Robot e)
        {
            Vector3 o = R.Eye;
            Vector3 d = e.Center - o;
            return Phys.Raycast(o, d.normalized, d.magnitude + 1f, R.Cc, out var hit) && hit.collider == e.Cc;
        }

        Vector3 Avoid(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.001f) return dir;
            float radius = R.Cc.radius * 0.8f;
            Vector3 origin = R.transform.position + Vector3.up * Mathf.Max(R.Height * 0.45f, radius + 0.35f);
            float look = 2.5f + R.Cc.radius * 2f;
            Vector3 d = dir.normalized;
            if (!Blocked(origin, radius, d, look)) return dir;

            // Низкое препятствие — пробуем перепрыгнуть, а не обходить
            if (Time.time >= vaultTryAt && R.CanVaultAhead(d))
            {
                vaultTryAt = Time.time + 1.5f;
                if (Random.value < 0.6f)
                {
                    R.MoveInput = d; // Jump берёт направление из MoveInput
                    R.Jump();
                    return dir;
                }
            }
            for (int a = 30; a <= 150; a += 30)
            {
                for (int k = 0; k < 2; k++)
                {
                    float side = k == 0 ? avoidSide : -avoidSide;
                    Vector3 alt = Quaternion.Euler(0, a * side, 0) * d;
                    if (!Blocked(origin, radius, alt, look))
                    {
                        avoidSide = side;
                        return alt * dir.magnitude;
                    }
                }
            }
            return -d;
        }

        bool Blocked(Vector3 origin, float radius, Vector3 dir, float dist)
        {
            // Лаву обходим как стену
            var arena = Match.I.Arena;
            if (arena.Lava.Count > 0 && (arena.InLava(origin + dir * 3f) || arena.InLava(origin + dir * (dist + 3f)))) return true;
            if (!Phys.SphereCast(origin, radius, dir, dist, R.Cc, out var hit)) return false;
            if (hit.distance <= 0f) return false; // стартовое пересечение (земля и т.п.)
            return !hit.collider.GetComponent<Robot>();
        }
    }
}
