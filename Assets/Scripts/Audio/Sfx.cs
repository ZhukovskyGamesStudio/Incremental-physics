using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics
{
    /// Procedurally synthesized sound effects: cozy, a little silly. No audio files needed.
    public class Sfx : MonoBehaviour
    {
        public static Sfx I;
        public static bool Muted;

        const int Rate = 44100;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> _last = new Dictionary<string, float>();
        readonly List<AudioSource> _pool = new List<AudioSource>();
        System.Random _rnd = new System.Random(5);

        public static void Play(string name, float vol = 1f, float pitch = 1f, float pitchVar = 0.06f, float minGap = 0.045f)
        {
            if (I != null) I.PlayInternal(name, vol, pitch, pitchVar, minGap);
        }

        void Awake()
        {
            I = this;
            for (int i = 0; i < 20; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0;
                _pool.Add(s);
            }
            Build();
        }

        void PlayInternal(string name, float vol, float pitch, float pitchVar, float minGap)
        {
            if (Muted || !_clips.TryGetValue(name, out var clip)) return;
            float now = Time.unscaledTime;
            if (_last.TryGetValue(name, out var t) && now - t < minGap) return;
            _last[name] = now;
            AudioSource src = null;
            foreach (var s in _pool) if (!s.isPlaying) { src = s; break; }
            if (src == null) return;
            src.clip = clip;
            src.volume = vol * 0.8f;
            src.pitch = pitch * (1f + UnityEngine.Random.Range(-pitchVar, pitchVar));
            src.Play();
        }

        // ---------------- synthesis ----------------

        float Noise() => (float)(_rnd.NextDouble() * 2 - 1);

        AudioClip Make(string name, float seconds, Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            // tiny fade-in/out against clicks
            int fade = Mathf.Min(200, n / 4);
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[i] *= k; data[n - 1 - i] *= k; }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            _clips[name] = clip;
            return clip;
        }

        static float Sin(float ph) => Mathf.Sin(ph * Mathf.PI * 2);
        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);

        void Build()
        {
            // chalk scratch: filtered noise with jittery amplitude
            float lp = 0, hp = 0;
            Make("chalk", 0.16f, t =>
            {
                float n = Noise();
                lp += (n - lp) * 0.35f;
                hp = n - lp;
                float amp = Env(t, 0.01f, 0.05f) * (0.6f + 0.4f * Sin(t * 90));
                return hp * amp * 0.35f;
            });

            // soft wooden tick
            Make("tick", 0.08f, t => (Sin(t * 1400) * 0.5f + Sin(t * 2300) * 0.25f) * Env(t, 0.001f, 0.012f) * 0.6f);

            // ball landing thud
            Make("thud", 0.22f, t => (Sin(t * (140 - 60 * t)) * 0.8f + Noise() * 0.25f * Env(t, 0.001f, 0.01f)) * Env(t, 0.002f, 0.05f) * 0.7f);

            // payout ding (two soft partials)
            Make("ding", 0.35f, t => (Sin(t * 1318) * 0.5f + Sin(t * 1976) * 0.3f + Sin(t * 2637) * 0.1f) * Env(t, 0.003f, 0.09f) * 0.35f);

            // spring boing: pitch wobbles
            Make("boing", 0.6f, t =>
            {
                float f = 190 + 120 * Mathf.Exp(-t * 5) * Mathf.Sin(t * 45);
                return Sin(PhaseInt("boing", t, f)) * Env(t, 0.005f, 0.2f) * 0.5f;
            });

            // bowling strike: clatter of clacks + rumble
            Make("strike", 0.7f, t =>
            {
                float clack = 0;
                for (int k = 0; k < 7; k++)
                {
                    float t0 = k * 0.045f + (k % 3) * 0.013f;
                    if (t > t0) clack += Sin((t - t0) * (900 + k * 170)) * Env(t - t0, 0.001f, 0.02f);
                }
                return (clack * 0.35f + Noise() * 0.15f * Env(t, 0.002f, 0.15f)) * 0.8f;
            });
            Make("knock", 0.12f, t => (Sin(t * 700) * 0.4f + Noise() * 0.2f) * Env(t, 0.001f, 0.025f) * 0.6f);

            // rubber duck squeak
            Make("squeak", 0.32f, t =>
            {
                float f = 1100 + 500 * Mathf.Sin(Mathf.Min(1, t / 0.3f) * Mathf.PI) + 60 * Mathf.Sin(t * 180);
                float ph = PhaseInt("squeak", t, f);
                float saw = (ph % 1f) * 2 - 1;
                return (saw * 0.35f + Sin(ph) * 0.4f) * Env(t, 0.02f, 0.12f) * 0.45f;
            });

            // splash
            float lp2 = 0;
            Make("splash", 0.45f, t => { lp2 += (Noise() - lp2) * 0.25f; return lp2 * Env(t, 0.005f, 0.12f) * 1.2f; });

            // whoosh
            float lp3 = 0;
            Make("whoosh", 0.5f, t => { float k = 0.03f + 0.3f * Mathf.Sin(t / 0.5f * Mathf.PI); lp3 += (Noise() - lp3) * k; return lp3 * Mathf.Sin(t / 0.5f * Mathf.PI) * 0.9f; });

            // correct: happy arpeggio C-E-G-C
            Make("correct", 0.7f, t =>
            {
                float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                float s = 0;
                for (int k = 0; k < 4; k++)
                {
                    float t0 = k * 0.07f;
                    if (t > t0) s += (Sin((t - t0) * notes[k]) + 0.3f * Sin((t - t0) * notes[k] * 2)) * Env(t - t0, 0.005f, 0.18f);
                }
                return s * 0.22f;
            });

            // placed letter: short chalk tap + tone
            Make("place", 0.18f, t => (Sin(t * 880) * 0.4f * Env(t, 0.002f, 0.05f) + Noise() * 0.15f * Env(t, 0.001f, 0.02f)) * 0.8f);

            // wrong: comedic "wah-wah"
            Make("wrong", 0.55f, t =>
            {
                float f = t < 0.25f ? 330 : 262;
                float lt = t < 0.25f ? t : t - 0.25f;
                float wob = 1 + 0.04f * Mathf.Sin(t * 40);
                float ph = PhaseInt("wrong", t, f * wob);
                float sq = Mathf.Sign(Sin(ph)) * 0.25f + Sin(ph) * 0.3f;
                return sq * Env(lt, 0.01f, 0.12f) * 0.35f;
            });

            // upgrade blip
            Make("up", 0.1f, t => Sin(PhaseInt("up", t, 600 + 6000 * t)) * Env(t, 0.002f, 0.04f) * 0.3f);

            // denied / not enough energy
            Make("nope", 0.14f, t => Sin(PhaseInt("nope", t, 220 - 400 * t)) * Env(t, 0.003f, 0.05f) * 0.35f);

            // electric zap
            Make("zap", 0.18f, t => (Mathf.Sign(Sin(t * 120 + Noise() * 0.3f)) * 0.3f + Noise() * 0.4f) * Env(t, 0.001f, 0.04f) * 0.3f);

            // steam hiss
            float lp4 = 0;
            Make("hiss", 0.5f, t => { float n = Noise(); lp4 += (n - lp4) * 0.6f; return (n - lp4) * Env(t, 0.05f, 0.18f) * 0.5f; });

            // pendulum tock
            Make("tock", 0.1f, t => (Sin(t * 520) * 0.5f + Sin(t * 1300) * 0.2f) * Env(t, 0.001f, 0.02f) * 0.5f);

            // bell for reveals / new module
            Make("bell", 1.4f, t => (Sin(t * 660) * 0.5f + Sin(t * 1650) * 0.25f + Sin(t * 2640) * 0.12f + Sin(t * 3960) * 0.06f) * Env(t, 0.003f, 0.35f) * 0.35f);

            // revolution: warm swelling chord
            Make("revolution", 3f, t =>
            {
                float[] f = { 130.8f, 196f, 261.6f, 329.6f, 392f };
                float s = 0;
                for (int k = 0; k < f.Length; k++) s += Sin(t * f[k] * (1 + 0.002f * k)) / (k + 1.5f);
                float env = Mathf.Min(1, t / 1.2f) * Mathf.Min(1, (3 - t) / 0.8f);
                return s * env * 0.35f;
            });

            // collapse drone: slowly rising, then silence
            Make("collapse", 7f, t =>
            {
                float f = 55 * Mathf.Pow(2, t / 2.2f);
                float ph = PhaseInt("collapse", t, f);
                float s = Sin(ph) * 0.5f + Sin(ph * 1.5f) * 0.25f + Sin(ph * 2.01f) * 0.2f;
                float env = Mathf.Min(1, t / 2f) * (t > 6.4f ? Mathf.Max(0, (7 - t) / 0.6f) : 1);
                return s * env * 0.3f;
            });

            // big bang / final pop
            Make("bang", 2.5f, t => (Noise() * 0.6f * Env(t, 0.002f, 0.4f) + Sin(t * (60 - 20 * t)) * 0.8f * Env(t, 0.002f, 0.6f)) * 0.5f);

            // click on a button
            Make("click", 0.06f, t => (Sin(t * 1800) * 0.5f + Noise() * 0.2f) * Env(t, 0.001f, 0.01f) * 0.5f);

            // balloon pop-in
            Make("balloon", 0.3f, t => Sin(PhaseInt("balloon", t, 300 + 900 * t)) * Env(t, 0.01f, 0.08f) * 0.3f);
        }

        // integrates frequency over time for sweeps (phase in cycles)
        readonly Dictionary<string, (float t, float ph)> _phase = new Dictionary<string, (float, float)>();
        float PhaseInt(string key, float t, float freq)
        {
            if (!_phase.TryGetValue(key, out var s) || t < s.t) s = (0, 0);
            float ph = s.ph + (t - s.t) * freq;
            _phase[key] = (t, ph);
            return ph;
        }
    }
}
