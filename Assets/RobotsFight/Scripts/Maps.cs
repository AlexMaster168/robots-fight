using UnityEngine;

namespace RobotsFight
{
    public enum CoverStyle { Industrial, Desert, Snow, City, Volcano, Moon }

    /// <summary>
    /// Локация: внешний вид, освещение, генерация укрытий, гравитация и свой трек.
    /// Точки режимов (маяки, бомба, спавны) общие для всех карт — любой режим работает на любой локации.
    /// </summary>
    public class MapDef
    {
        public string Id, Name, Description;
        public CoverStyle Style;
        public int Seed;

        public Color GroundA, GroundB, Line, Wall, CoverLow, CoverHigh, Accent, Accent2;
        public Color Fog, Ambient, SunColor;
        public float FogStart = 80f, FogEnd = 420f, SunIntensity = 1.15f;
        public Vector3 SunEuler = new(50, -35, 0);
        public bool SolidSky;
        public Color SkyColor;

        public float Gravity = 1f;
        public MusicStyle Music;
    }

    public static class Maps
    {
        public static readonly MapDef[] All =
        {
            new()
            {
                Id = "industrial", Name = "Промзона", Style = CoverStyle.Industrial, Seed = 1337,
                Description = "Разрушенный индустриальный квартал: бетонные блоки и цеха.",
                GroundA = new Color(0.30f, 0.306f, 0.285f), GroundB = new Color(0.27f, 0.275f, 0.257f), Line = new Color(0.55f, 0.5f, 0.3f),
                Wall = new Color(0.22f, 0.23f, 0.26f), CoverLow = new Color(0.45f, 0.43f, 0.38f), CoverHigh = new Color(0.42f, 0.43f, 0.45f),
                Accent = new Color(1f, 0.75f, 0.35f), Accent2 = new Color(1f, 0.75f, 0.35f),
                Fog = new Color(0.55f, 0.6f, 0.68f), Ambient = new Color(0.45f, 0.47f, 0.52f), SunColor = new Color(1f, 0.96f, 0.88f),
                Music = new MusicStyle { Id = "industrial", Bpm = 132, Roots = new[] { 45, 41, 43, 40 }, Minor = new[] { true, false, false, true },
                    Drums = DrumKit.FourOnFloor, Lead = Wave.Square, Seed = 7 },
            },
            new()
            {
                Id = "desert", Name = "Барханы", Style = CoverStyle.Desert, Seed = 4242,
                Description = "Пустынный каньон: скальные столы, валуны, жаркое марево.",
                GroundA = new Color(0.78f, 0.65f, 0.45f), GroundB = new Color(0.74f, 0.61f, 0.42f), Line = new Color(0.6f, 0.45f, 0.3f),
                Wall = new Color(0.55f, 0.38f, 0.26f), CoverLow = new Color(0.68f, 0.48f, 0.32f), CoverHigh = new Color(0.6f, 0.4f, 0.27f),
                Accent = new Color(1f, 0.85f, 0.5f), Accent2 = new Color(0.9f, 0.5f, 0.2f),
                Fog = new Color(0.88f, 0.78f, 0.62f), Ambient = new Color(0.62f, 0.55f, 0.45f), SunColor = new Color(1f, 0.9f, 0.72f),
                SunIntensity = 1.35f, FogStart = 60f, FogEnd = 320f, SunEuler = new Vector3(62, 20, 0),
                Music = new MusicStyle { Id = "desert", Bpm = 112, Roots = new[] { 50, 51, 50, 48 }, Minor = new[] { true, false, true, false },
                    Drums = DrumKit.HalfTime, Lead = Wave.Saw, LeadLp = 0.15f, Pads = true, Arp16 = false, Seed = 11 },
            },
            new()
            {
                Id = "snow", Name = "Ледяная база", Style = CoverStyle.Snow, Seed = 777,
                Description = "Полярная станция в метель: ледяные глыбы, топливные баки, радарные вышки.",
                GroundA = new Color(0.86f, 0.89f, 0.93f), GroundB = new Color(0.82f, 0.86f, 0.91f), Line = new Color(0.9f, 0.3f, 0.25f),
                Wall = new Color(0.55f, 0.6f, 0.68f), CoverLow = new Color(0.7f, 0.85f, 0.95f), CoverHigh = new Color(0.6f, 0.64f, 0.7f),
                Accent = new Color(0.4f, 0.9f, 1f), Accent2 = new Color(0.95f, 0.25f, 0.2f),
                Fog = new Color(0.85f, 0.88f, 0.93f), Ambient = new Color(0.62f, 0.66f, 0.74f), SunColor = new Color(0.9f, 0.95f, 1f),
                SunIntensity = 0.95f, FogStart = 35f, FogEnd = 230f, SunEuler = new Vector3(30, 120, 0),
                Music = new MusicStyle { Id = "snow", Bpm = 100, Roots = new[] { 52, 48, 43, 47 }, Minor = new[] { true, false, false, false },
                    Drums = DrumKit.HalfTime, Lead = Wave.Tri, Pads = true, Seed = 23 },
            },
            new()
            {
                Id = "city", Name = "Неон-Сити", Style = CoverStyle.City, Seed = 2077,
                Description = "Ночной мегаполис: небоскрёбы, неоновые окна, узкие улицы.",
                GroundA = new Color(0.09f, 0.1f, 0.13f), GroundB = new Color(0.075f, 0.085f, 0.11f), Line = new Color(0.2f, 0.9f, 1f),
                Wall = new Color(0.08f, 0.08f, 0.12f), CoverLow = new Color(0.22f, 0.22f, 0.27f), CoverHigh = new Color(0.14f, 0.15f, 0.2f),
                Accent = new Color(0.2f, 0.9f, 1f), Accent2 = new Color(1f, 0.25f, 0.8f),
                Fog = new Color(0.07f, 0.06f, 0.14f), Ambient = new Color(0.22f, 0.22f, 0.34f), SunColor = new Color(0.55f, 0.6f, 1f),
                SunIntensity = 0.45f, FogStart = 60f, FogEnd = 300f, SunEuler = new Vector3(55, 200, 0),
                SolidSky = true, SkyColor = new Color(0.04f, 0.03f, 0.1f),
                Music = new MusicStyle { Id = "city", Bpm = 110, Roots = new[] { 42, 38, 45, 40 }, Minor = new[] { true, false, false, false },
                    Drums = DrumKit.FourOnFloor, Lead = Wave.Saw, LeadLp = 0.2f, Pads = true, Seed = 31 },
            },
            new()
            {
                Id = "volcano", Name = "Вулкан", Style = CoverStyle.Volcano, Seed = 666,
                Description = "Базальтовые скалы и лавовые озёра. Лава жжёт — не стой в ней!",
                GroundA = new Color(0.16f, 0.13f, 0.12f), GroundB = new Color(0.14f, 0.115f, 0.105f), Line = new Color(1f, 0.35f, 0.05f),
                Wall = new Color(0.12f, 0.09f, 0.08f), CoverLow = new Color(0.24f, 0.2f, 0.19f), CoverHigh = new Color(0.2f, 0.17f, 0.16f),
                Accent = new Color(1f, 0.45f, 0.08f), Accent2 = new Color(1f, 0.25f, 0.05f),
                Fog = new Color(0.32f, 0.12f, 0.07f), Ambient = new Color(0.42f, 0.27f, 0.22f), SunColor = new Color(1f, 0.55f, 0.35f),
                SunIntensity = 0.85f, FogStart = 50f, FogEnd = 280f, SunEuler = new Vector3(40, -80, 0),
                SolidSky = true, SkyColor = new Color(0.22f, 0.07f, 0.04f),
                Music = new MusicStyle { Id = "volcano", Bpm = 150, Roots = new[] { 48, 44, 46, 43 }, Minor = new[] { true, false, false, false },
                    Drums = DrumKit.Breakbeat, Lead = Wave.Square, LeadLp = 0.4f, Seed = 66 },
            },
            new()
            {
                Id = "moon", Name = "Луна", Style = CoverStyle.Moon, Seed = 1969,
                Description = "Лунная база: кратеры, купола и низкая гравитация — прыжки и полёты дальше.",
                GroundA = new Color(0.5f, 0.5f, 0.52f), GroundB = new Color(0.46f, 0.46f, 0.48f), Line = new Color(0.3f, 0.6f, 1f),
                Wall = new Color(0.3f, 0.3f, 0.33f), CoverLow = new Color(0.55f, 0.55f, 0.57f), CoverHigh = new Color(0.75f, 0.77f, 0.8f),
                Accent = new Color(0.4f, 0.7f, 1f), Accent2 = new Color(1f, 0.9f, 0.6f),
                Fog = new Color(0.02f, 0.02f, 0.04f), Ambient = new Color(0.3f, 0.3f, 0.36f), SunColor = Color.white,
                SunIntensity = 1.5f, FogStart = 400f, FogEnd = 2000f, SunEuler = new Vector3(25, 60, 0),
                SolidSky = true, SkyColor = new Color(0.01f, 0.01f, 0.03f), Gravity = 0.35f,
                Music = new MusicStyle { Id = "moon", Bpm = 84, Roots = new[] { 47, 43, 50, 45 }, Minor = new[] { true, false, false, false },
                    Drums = DrumKit.Ambient, Lead = Wave.Sine, Pads = true, Arp16 = false, Seed = 69 },
            },
        };

        public static MapDef Random() => All[UnityEngine.Random.Range(0, All.Length)];
    }
}
