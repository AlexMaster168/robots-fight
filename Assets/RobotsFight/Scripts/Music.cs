using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace RobotsFight
{
    public enum Wave { Sine, Saw, Square, Tri }
    public enum DrumKit { FourOnFloor, Breakbeat, HalfTime, Ambient }

    /// <summary>Параметры синтезируемого трека: темп, гармония (4 аккорда по 2 такта), ударные, тембр лида.</summary>
    public class MusicStyle
    {
        public string Id;
        public float Bpm = 120f;
        public int[] Roots = { 45, 41, 43, 40 };
        public bool[] Minor = { true, false, false, true };
        public DrumKit Drums;
        public Wave Lead = Wave.Square;
        public float LeadLp = 0.3f;
        public bool Arp16 = true, Pads;
        public int Seed;

        public static readonly MusicStyle Menu = new()
        {
            Id = "menu", Bpm = 92, Roots = new[] { 45, 41, 48, 43 }, Minor = new[] { true, false, false, false },
            Drums = DrumKit.Ambient, Lead = Wave.Tri, Pads = true, Arp16 = false, Seed = 3,
        };
    }

    /// <summary>
    /// Фоновая музыка. Свои треки из Resources/Music (id локации, battle или menu) имеют приоритет,
    /// иначе трек синтезируется в фоновом потоке — без фриза на старте боя. M — выкл/вкл.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer I;

        public static float Volume
        {
            get => PlayerPrefs.GetFloat("rf_music", 0.5f);
            set => PlayerPrefs.SetFloat("rf_music", Mathf.Clamp01(value));
        }

        public bool Muted { get; private set; }

        AudioSource src;
        AudioClip pending;
        MusicStyle wanted;
        readonly Dictionary<string, AudioClip> cache = new();
        readonly Dictionary<string, Task<float[]>> rendering = new();

        void Awake()
        {
            I = this;
            src = gameObject.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            src.ignoreListenerPause = true;
            Muted = PlayerPrefs.GetInt("rf_mute", 0) == 1;
        }

        public void Play(MusicStyle style)
        {
            wanted = style;
            Prewarm(style);
        }

        /// <summary>Готовит трек заранее (свой файл или фоновый синтез), не переключая музыку.</summary>
        public void Prewarm(MusicStyle style)
        {
            if (cache.ContainsKey(style.Id)) return;

            var custom = Resources.Load<AudioClip>($"Music/{style.Id}");
            if (!custom && style != MusicStyle.Menu) custom = Resources.Load<AudioClip>("Music/battle");
            if (custom)
            {
                cache[style.Id] = custom;
                return;
            }
            if (!rendering.ContainsKey(style.Id))
                rendering[style.Id] = Task.Run(() => MusicSynth.Render(style));
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            PlayerPrefs.SetInt("rf_mute", Muted ? 1 : 0);
        }

        void Update()
        {
            // Готовые фоновые рендеры → клипы (AudioClip создаётся только на главном потоке)
            if (rendering.Count > 0)
            {
                string done = null;
                foreach (var kv in rendering)
                    if (kv.Value.IsCompleted) { done = kv.Key; break; }
                if (done != null)
                {
                    var task = rendering[done];
                    rendering.Remove(done);
                    if (task.Status == TaskStatus.RanToCompletion)
                        cache[done] = MusicSynth.ToClip(done, task.Result);
                    else Debug.LogError($"Music render failed: {task.Exception}");
                }
            }

            if (wanted != null && cache.TryGetValue(wanted.Id, out var clip) && src.clip != clip && pending != clip)
            {
                if (!src.isPlaying)
                {
                    src.clip = clip;
                    src.Play();
                }
                else pending = clip; // сначала затухание текущего трека
            }

            float dt = Time.unscaledDeltaTime;
            float target = pending ? 0f : Muted ? 0f : Volume * 0.7f;
            src.volume = Mathf.MoveTowards(src.volume, target, dt * (pending ? 1.5f : 0.6f));
            if (pending && src.volume <= 0.001f)
            {
                src.clip = pending;
                pending = null;
                src.Play();
            }
        }
    }

    /// <summary>Мини-синтезатор: бочка, снейр, хэты, бас, арпеджио и пэды. Луп из 8 тактов. Потокобезопасный Render.</summary>
    public static class MusicSynth
    {
        public const int Rate = 44100;

        public static AudioClip Build(MusicStyle style) => ToClip(style.Id, Render(style));

        public static AudioClip ToClip(string id, float[] data)
        {
            var clip = AudioClip.Create($"rf_{id}", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static float[] Render(MusicStyle st)
        {
            float step = 60f / st.Bpm / 4f; // шестнадцатая
            const int bars = 8;
            int steps = bars * 16;
            int n = (int)System.Math.Round(steps * step * Rate);
            var mix = new float[n];
            var rng = new System.Random(st.Seed);
            int[] arp = { 0, 1, 2, 3, 2, 1, 2, 3 };
            bool calm = st.Drums == DrumKit.Ambient;

            for (int s = 0; s < steps; s++)
            {
                int bar = s / 16, k = s % 16, chord = bar / 2 % 4;
                int root = st.Roots[chord];
                int[] tri = { 0, st.Minor[chord] ? 3 : 4, 7, 12 };
                int at = (int)(s * step * Rate);

                Drums(mix, at, k, bar, st.Drums, rng);

                // Бас
                if (calm)
                {
                    if (k == 0 && bar % 2 == 0) Note(mix, at, step * 32, Midi(root), 0.22f, Wave.Saw, 0.05f, 0.4f, 0.05f);
                }
                else if (st.Drums == DrumKit.HalfTime)
                {
                    if (k % 4 == 0) Note(mix, at, step * 3.5f, Midi(root + (k == 12 ? 7 : 0)), 0.3f, Wave.Saw, 0.004f, 3f, 0.15f);
                }
                else if (k % 2 == 0) Note(mix, at, step * 1.8f, Midi(root + (k == 14 ? 12 : 0)), 0.3f, Wave.Saw, 0.004f, 6f, 0.2f);

                // Лид-арпеджио
                if (st.Arp16)
                    Note(mix, at, step * 0.9f, Midi(root + 24 + tri[arp[k % 8]]), calm ? 0.06f : 0.08f, st.Lead, 0.003f, 10f, st.LeadLp);
                else if (k % 2 == 0)
                    Note(mix, at, step * 3, Midi(root + 24 + tri[k / 2 % 4]), 0.07f, st.Lead, 0.005f, 3f, st.LeadLp);

                // Пэды / стабы
                if (st.Pads && k == 0 && bar % 2 == 0)
                    for (int i = 0; i < 3; i++)
                    {
                        Note(mix, at, step * 32, Midi(root + 12 + tri[i]), 0.045f, Wave.Saw, 0.8f, 0.25f, 0.08f);
                        Note(mix, at, step * 32, Midi(root + 12 + tri[i]) * 1.006f, 0.045f, Wave.Saw, 0.8f, 0.25f, 0.08f);
                    }
                else if (!st.Pads && k == 0)
                    for (int i = 0; i < 3; i++)
                        Note(mix, at, step * 14, Midi(root + 12 + tri[i]), 0.06f, Wave.Saw, 0.02f, 1.2f, 0.12f);
            }

            float peak = 0.001f;
            foreach (var v in mix) peak = System.Math.Max(peak, System.Math.Abs(v));
            float gain = 0.9f / peak;
            for (int i = 0; i < n; i++) mix[i] = (float)System.Math.Tanh(mix[i] * gain * 1.2f) * 0.85f;
            return mix;
        }

        static void Drums(float[] mix, int at, int k, int bar, DrumKit kit, System.Random rng)
        {
            switch (kit)
            {
                case DrumKit.FourOnFloor:
                    if (k % 4 == 0) Kick(mix, at, 0.9f);
                    if (k == 4 || k == 12) Snare(mix, at, 0.4f, rng);
                    if (k % 2 == 1) Hat(mix, at, 0.1f, rng);
                    if (bar == 7 && k >= 12) Snare(mix, at, 0.3f, rng); // сбивка перед повтором
                    break;
                case DrumKit.Breakbeat:
                    if (k == 0 || k == 6 || k == 10) Kick(mix, at, 0.95f);
                    if (k == 4 || k == 12) Snare(mix, at, 0.5f, rng);
                    if (k == 15 || k == 9) Snare(mix, at, 0.15f, rng); // гост-ноты
                    Hat(mix, at, k % 2 == 0 ? 0.12f : 0.06f, rng);
                    if (bar % 4 == 3 && k >= 8 && k % 2 == 0) Snare(mix, at, 0.35f, rng);
                    break;
                case DrumKit.HalfTime:
                    if (k == 0 || k == 10) Kick(mix, at, 0.85f);
                    if (k == 8) Snare(mix, at, 0.5f, rng);
                    if (k % 2 == 0) Hat(mix, at, 0.08f, rng);
                    break;
                default: // Ambient
                    if (k == 0) Kick(mix, at, 0.45f);
                    if (k == 8) Kick(mix, at, 0.3f);
                    if (k % 4 == 2) Hat(mix, at, 0.04f, rng);
                    break;
            }
        }

        static float Midi(int m) => 440f * (float)System.Math.Pow(2.0, (m - 69) / 12.0);

        static void Note(float[] buf, int start, float dur, float freq, float amp, Wave w, float attack, float decay, float lp)
        {
            int len = (int)(dur * Rate);
            float phase = 0, inc = freq / Rate, y = 0;
            float release = 0.01f * Rate;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                phase += inc;
                if (phase >= 1f) phase -= 1f;
                float v = w switch
                {
                    Wave.Sine => (float)System.Math.Sin(phase * 2.0 * System.Math.PI),
                    Wave.Saw => 2f * phase - 1f,
                    Wave.Square => phase < 0.5f ? 1f : -1f,
                    _ => 1f - 4f * System.Math.Abs(phase - 0.5f),
                };
                y += lp * (v - y);
                float env = System.Math.Min(1f, t / attack) * (float)System.Math.Exp(-t * decay) * System.Math.Min(1f, (len - i) / release);
                buf[(start + i) % buf.Length] += y * amp * env;
            }
        }

        static void Kick(float[] buf, int start, float amp)
        {
            int len = (int)(0.3f * Rate);
            double phase = 0;
            for (int i = 0; i < len; i++)
            {
                double t = (double)i / Rate;
                phase += (45.0 + 90.0 * System.Math.Exp(-t * 30.0)) / Rate;
                buf[(start + i) % buf.Length] += (float)(System.Math.Sin(phase * 2.0 * System.Math.PI) * System.Math.Exp(-t * 9.0) * amp);
            }
        }

        static void Snare(float[] buf, int start, float amp, System.Random rng)
        {
            int len = (int)(0.18f * Rate);
            for (int i = 0; i < len; i++)
            {
                double t = (double)i / Rate;
                double noise = rng.NextDouble() * 2.0 - 1.0;
                buf[(start + i) % buf.Length] += (float)((noise * System.Math.Exp(-t * 18.0) +
                    System.Math.Sin(t * 190.0 * 2.0 * System.Math.PI) * 0.5 * System.Math.Exp(-t * 25.0)) * amp);
            }
        }

        static void Hat(float[] buf, int start, float amp, System.Random rng)
        {
            int len = (int)(0.05f * Rate);
            double prev = 0;
            for (int i = 0; i < len; i++)
            {
                double t = (double)i / Rate;
                double noise = rng.NextDouble() * 2.0 - 1.0;
                buf[(start + i) % buf.Length] += (float)((noise - prev) * 0.5 * System.Math.Exp(-t * 70.0) * amp);
                prev = noise;
            }
        }
    }
}
