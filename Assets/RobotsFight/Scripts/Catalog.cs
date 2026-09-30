using UnityEngine;

namespace RobotsFight
{
    public enum AbilityType { Dash, Shield, Repair, JumpJet }
    public enum Chassis { Biped, Spider, Tracks }
    public enum GunModel { Autocannon, Plasma, Rockets, Cannon, Rail, Grenade, Sword }

    public class WeaponDef
    {
        public string Name;
        public GunModel Model;
        public float Damage, FireRate, ReloadTime, Speed, Spread, Range, Splash;
        public float Gravity, Fuse; // для навесных снарядов (гранаты)
        public float Size = 0.25f;
        public int Magazine, Pellets = 1;
        public Color Color;
    }

    public class RobotDef
    {
        public string Name, Role, Description;
        public float Hp, Speed, Scale = 1f;
        public Chassis Chassis;
        public WeaponDef[] Weapons;
        public AbilityType Ability;
        public float AbilityCooldown;

        public string AbilityName => Ability switch
        {
            AbilityType.Dash => "Рывок",
            AbilityType.Shield => "Энергощит",
            AbilityType.Repair => "Ремкомплект",
            AbilityType.JumpJet => "Прыжковый ранец",
            _ => "?"
        };

        public float MaxRange
        {
            get
            {
                float r = 0;
                foreach (var w in Weapons) r = Mathf.Max(r, w.Range);
                return r;
            }
        }

        /// <summary>Дистанция, на которой боту комфортно воевать.</summary>
        public float PreferredRange
        {
            get
            {
                float r = float.MaxValue;
                foreach (var w in Weapons) r = Mathf.Min(r, w.Range);
                return Mathf.Clamp(r * 0.6f, 18f, 90f);
            }
        }
    }

    public static class Catalog
    {
        public static readonly WeaponDef Autocannon = new()
        {
            Name = "Автопушка", Model = GunModel.Autocannon, Damage = 28, FireRate = 8, Magazine = 40, ReloadTime = 3f,
            Speed = 130, Spread = 1.4f, Range = 160, Size = 0.2f, Color = new Color(1f, 0.85f, 0.3f),
        };

        public static readonly WeaponDef Plasma = new()
        {
            Name = "Плазмомёт", Model = GunModel.Plasma, Damage = 22, Pellets = 8, FireRate = 1.3f, Magazine = 6,
            ReloadTime = 3.5f, Speed = 90, Spread = 5f, Range = 60, Size = 0.3f, Color = new Color(0.3f, 0.95f, 1f),
        };

        public static readonly WeaponDef Rockets = new()
        {
            Name = "Ракеты", Model = GunModel.Rockets, Damage = 160, Splash = 4.5f, FireRate = 2.2f, Magazine = 6,
            ReloadTime = 5f, Speed = 55, Spread = 1.5f, Range = 220, Size = 0.35f, Color = new Color(1f, 0.5f, 0.15f),
        };

        public static readonly WeaponDef Cannon = new()
        {
            Name = "Тяжёлое орудие", Model = GunModel.Cannon, Damage = 230, Splash = 2.5f, FireRate = 0.9f, Magazine = 4,
            ReloadTime = 4f, Speed = 150, Spread = 0.4f, Range = 250, Size = 0.4f, Color = new Color(1f, 0.95f, 0.7f),
        };

        public static readonly WeaponDef Railgun = new()
        {
            Name = "Рельсотрон", Model = GunModel.Rail, Damage = 650, FireRate = 0.45f, Magazine = 3, ReloadTime = 5.5f,
            Speed = 450, Spread = 0f, Range = 450, Size = 0.25f, Color = new Color(0.75f, 0.4f, 1f),
        };

        public static readonly WeaponDef Grenade = new()
        {
            Name = "Граната", Model = GunModel.Grenade, Damage = 500, Splash = 7f, Gravity = 25f, Fuse = 3f,
            Range = 9999, Size = 0.55f, Color = new Color(0.45f, 1f, 0.3f),
        };

        public static readonly WeaponDef Sword = new()
        {
            Name = "Меч", Model = GunModel.Sword, Damage = 450, Color = new Color(0.4f, 0.95f, 1f),
        };

        public const int GrenadesPerDrop = 99;

        public static readonly RobotDef[] Robots =
        {
            new()
            {
                Name = "Стриж", Role = "Разведчик", Description = "Быстрый и хрупкий. Врывается, стреляет, уходит рывком.",
                Hp = 1400, Speed = 10f, Scale = 0.85f, Chassis = Chassis.Biped,
                Weapons = new[] { Autocannon, Autocannon }, Ability = AbilityType.Dash, AbilityCooldown = 6f,
            },
            new()
            {
                Name = "Гризли", Role = "Штурмовик", Description = "Паук ближнего боя: два плазмомёта, ракеты и щит.",
                Hp = 2600, Speed = 7f, Scale = 1.05f, Chassis = Chassis.Spider,
                Weapons = new[] { Plasma, Plasma, Rockets }, Ability = AbilityType.Shield, AbilityCooldown = 14f,
            },
            new()
            {
                Name = "Титан", Role = "Танк", Description = "Гусеничная крепость. Медленный, толстый, чинится на ходу.",
                Hp = 4200, Speed = 4.5f, Scale = 1.3f, Chassis = Chassis.Tracks,
                Weapons = new[] { Cannon, Cannon, Rockets }, Ability = AbilityType.Repair, AbilityCooldown = 18f,
            },
            new()
            {
                Name = "Сокол", Role = "Снайпер", Description = "Рельсотрон на дальней, автопушка вблизи, ранец для позиций.",
                Hp = 1700, Speed = 7.5f, Scale = 0.95f, Chassis = Chassis.Biped,
                Weapons = new[] { Railgun, Autocannon }, Ability = AbilityType.JumpJet, AbilityCooldown = 8f,
            },
        };

        public static RobotDef RandomRobot() => Robots[Random.Range(0, Robots.Length)];
    }
}
