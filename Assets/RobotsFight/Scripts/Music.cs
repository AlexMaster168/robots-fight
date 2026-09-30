using UnityEngine;

namespace RobotsFight
{
    /// <summary>
    /// Фоновая музыка. Если в Resources/Music лежат свои треки (menu / battle) — играет их,
    /// иначе синтезирует собственный луп прямо в коде. M — выкл/вкл.
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
        AudioClip menuClip, battleClip, pending;

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

        public void Play(bool battle)
        {
            AudioClip clip;
            if (battle)
            {
                if (!battleClip) battleClip = Load("battle", true);
                clip = battleClip;
            }
            else
            {
                if (!menuClip) menuClip = Load("menu", false);
                clip = menuClip;
            }
            if (src.clip == clip && src.isPlaying) return;
            if (!src.isPlaying)
            {
                src.clip = clip;
                src.Play();
            }
            else pending = clip; // сначала затухание текущего трека
        }

        static AudioClip Load(string name, bool battle)
        {
            var custom = Resources.Load<AudioClip>($"Music/{name}");
            return custom ? custom : MusicSynth.Build(battle);
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            PlayerPrefs.SetInt("rf_mute", Muted ? 1 : 0);
        }

        void Update()
        {
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

    /// <summary>Мини-синтезатор: бочка, снейр, хэты, бас, арпеджио и пэды. Луп из 8 тактов, ля-минор.</summary>
    public static class MusicSynth
    {
        const int Rate = 44100;
        enum Wave { Sine, Saw, Square, Tri }

        public static AudioClip Build(bool battle)
        {
            float bpm = battle ? 132f : 92f;
            float step = 60f / bpm / 4f; // шестнадцатая
            const int bars = 8;
            int steps = bars * 16;
            int n = Mathf.RoundToInt(steps * step * Rate);
            var mix = new float[n];
            var rng = new System.Random(battle ? 7 : 3);

            // Am F G Em (бой) / Am F C G (меню), по 2 такта на аккорд
            int[] roots = battle ? new[] { 45, 41, 43, 40 } : new[] { 45, 41, 48, 43 };
            bool[] minor = battle ? new[] { true, false, false, true } : new[] { true, false, false, false };
            int[] arp = { 0, 1, 2, 3, 2, 1, 2, 3 };

            for (int s = 0; s < steps; s++)
            {
                int bar = s / 16, st = s % 16, chord = bar / 2 % 4;
                int root = roots[chord];
                int[] tri = { 0, minor[chord] ? 3 : 4, 7, 12 };
                int at = (int)(s * step * Rate);

                if (battle)
                {
                    if (st % 4 == 0) Kick(mix, at, 0.9f);
                    if (st == 4 || st == 12) Snare(mix, at, 0.4f, rng);
                    if (st % 2 == 1) Hat(mix, at, 0.1f, rng);
                    if (st % 2 == 0) Note(mix, at, step * 1.8f, Midi(root + (st == 14 ? 12 : 0)), 0.3f, Wave.Saw, 0.004f, 6f, 0.2f);
                    Note(mix, at, step * 0.9f, Midi(root + 24 + tri[arp[st % 8]]), 0.08f, Wave.Square, 0.003f, 10f, 0.3f);
                    if (st == 0)
                        for (int k = 0; k < 3; k++)
                            Note(mix, at, step * 14, Midi(root + 12 + tri[k]), 0.06f, Wave.Saw, 0.02f, 1.2f, 0.12f);
                    if (bar == 7 && st >= 12) Snare(mix, at, 0.3f, rng); // сбивка перед повтором
                }
                else
                {
                    if (st == 0) Kick(mix, at, 0.45f);
                    if (st == 8) Kick(mix, at, 0.3f);
                    if (st % 4 == 2) Hat(mix, at, 0.04f, rng);
                    if (st == 0 && bar % 2 == 0)
                    {
                        Note(mix, at, step * 32, Midi(root), 0.22f, Wave.Saw, 0.05f, 0.4f, 0.05f);
                        for (int k = 0; k < 3; k++)
                        {
                            Note(mix, at, step * 32, Midi(root + 12 + tri[k]), 0.045f, Wave.Saw, 0.8f, 0.25f, 0.08f);
                            Note(mix, at, step * 32, Midi(root + 12 + tri[k]) * 1.006f, 0.045f, Wave.Saw, 0.8f, 0.25f, 0.08f);
                        }
                    }
                    if (st % 2 == 0)
                        Note(mix, at, step * 3, Midi(root + 24 + tri[st / 2 % 4]), 0.07f, Wave.Tri, 0.005f, 3f, 0.3f);
                }
            }

            float peak = 0.001f;
            foreach (var v in mix) peak = Mathf.Max(peak, Mathf.Abs(v));
            float gain = 0.9f / peak;
            for (int i = 0; i < n; i++) mix[i] = (float)System.Math.Tanh(mix[i] * gain * 1.2f) * 0.85f;

            var clip = AudioClip.Create(battle ? "rf_battle" : "rf_menu", n, 1, Rate, false);
            clip.SetData(mix, 0);
            return clip;
        }

        static float Midi(int m) => 440f * Mathf.Pow(2f, (m - 69) / 12f);

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
                    Wave.Sine => Mathf.Sin(phase * 2f * Mathf.PI),
                    Wave.Saw => 2f * phase - 1f,
                    Wave.Square => phase < 0.5f ? 1f : -1f,
                    _ => 1f - 4f * Mathf.Abs(phase - 0.5f),
                };
                y += lp * (v - y);
                float env = Mathf.Min(1f, t / attack) * Mathf.Exp(-t * decay) * Mathf.Min(1f, (len - i) / release);
                buf[(start + i) % buf.Length] += y * amp * env;
            }
        }

        static void Kick(float[] buf, int start, float amp)
        {
            int len = (int)(0.3f * Rate);
            float phase = 0;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                phase += (45f + 90f * Mathf.Exp(-t * 30f)) / Rate;
                buf[(start + i) % buf.Length] += Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-t * 9f) * amp;
            }
        }

        static void Snare(float[] buf, int start, float amp, System.Random rng)
        {
            int len = (int)(0.18f * Rate);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                buf[(start + i) % buf.Length] += (noise * Mathf.Exp(-t * 18f) + Mathf.Sin(t * 190f * 2f * Mathf.PI) * 0.5f * Mathf.Exp(-t * 25f)) * amp;
            }
        }

        static void Hat(float[] buf, int start, float amp, System.Random rng)
        {
            int len = (int)(0.05f * Rate);
            float prev = 0;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                buf[(start + i) % buf.Length] += (noise - prev) * 0.5f * Mathf.Exp(-t * 70f) * amp;
                prev = noise;
            }
        }
    }
}
