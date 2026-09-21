using System;
using UnityEngine;

namespace ChalkPhysics
{
    /// Background ambience, synthesized once at start: a low drone that slowly breathes, two soft chords that swell
    /// and fade over half a minute each, a distant air-like hush, and now and then one soft, slow note. Nothing sharp,
    /// nothing that asks for attention. Loops seamlessly and follows the sound toggle.
    public class Music : MonoBehaviour
    {
        const int Rate = 16000;
        const float Loop = 64f;
        AudioSource _src;
        float _fade;

        void Awake()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0;
            _src.loop = true;
            _src.volume = 0;
            _src.clip = Render();
            _src.Play();
        }

        /// While the studio splash plays its own sound, the ambience waits (and then fades in from silence).
        public static bool Hold;

        void Update()
        {
            _src.mute = Sfx.Muted;
            if (Hold) { _src.volume = 0; return; }
            _fade = Mathf.Min(1, _fade + Time.unscaledDeltaTime * 0.12f);   // an eight-second fade-in
            _src.volume = 0.17f * _fade;
        }

        static float Note(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        /// A slow sine that completes k whole cycles per loop, so the loop seam never shows.
        static float Cyc(float t, int k, float phase) => Mathf.Sin((t / Loop * k + phase) * Mathf.PI * 2);

        AudioClip Render()
        {
            int n = Mathf.CeilToInt(Loop * Rate);
            var data = new float[n];
            // the drone: A1, a detuned twin, its fifth and its octave, each breathing on its own slow cycle
            float[] df = { Note(33), Note(33) * 1.003f, Note(40), Note(45) };
            float[] da = { 0.30f, 0.22f, 0.12f, 0.10f };
            int[] dk = { 3, 5, 7, 4 };
            // two chords, an octave up, each swelling over its half of the loop: Am(add9), then Fmaj7
            int[][] chords = { new[] { 57, 64, 67, 71 }, new[] { 53, 60, 64, 69 } };
            var ph = new double[12];
            var rnd = new System.Random(7);
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = 0;
                for (int v = 0; v < df.Length; v++)
                {
                    ph[v] += df[v] / Rate;
                    float env = 0.7f + 0.3f * Cyc(t, dk[v], v * 0.27f);
                    s += Mathf.Sin((float)(ph[v] * Math.PI * 2)) * da[v] * env;
                }
                for (int c = 0; c < 2; c++)
                {
                    float center = (c + 0.5f) * Loop / 2;
                    float d = Mathf.Abs(Mathf.Repeat(t - center + Loop / 2, Loop) - Loop / 2);
                    float env = 0.5f + 0.5f * Mathf.Cos(Mathf.Clamp01(d / (Loop * 0.34f)) * Mathf.PI);
                    if (env <= 0.002f) continue;
                    for (int v = 0; v < 4; v++)
                    {
                        int idx = 4 + c * 4 + v;
                        ph[idx] += Note(chords[c][v]) / Rate;
                        s += Mathf.Sin((float)(ph[idx] * Math.PI * 2)) * env * 0.07f * (1 + 0.15f * Cyc(t, 2 + v, c * 0.5f));
                    }
                }
                // air: noise through two gentle low-pass stages, swelling twice per loop
                float nz = (float)(rnd.NextDouble() * 2 - 1);
                lp += (nz - lp) * 0.06f; lp2 += (lp - lp2) * 0.06f;
                s += lp2 * (0.5f + 0.5f * Cyc(t, 2, 0.4f)) * 0.9f;
                data[i] = s;
            }
            // a rare soft note: slow to rise, slow to fade, from the same five tones
            int[] scale = { 57, 60, 62, 64, 67 };
            var r2 = new System.Random(11);
            for (float t0 = 3f; t0 < Loop - 8f; t0 += 7f + (float)r2.NextDouble() * 8f)
                AddSoft(data, t0, Note(scale[r2.Next(scale.Length)]), 0.09f + 0.05f * (float)r2.NextDouble());
            // the loop seam: the last two seconds cross-fade into the first two
            int x = 2 * Rate;
            for (int i = 0; i < x; i++) { float w = i / (float)x; data[n - x + i] = data[n - x + i] * (1 - w) + data[i] * w; }
            for (int i = 0; i < n; i++) data[i] = (float)Math.Tanh(data[i] * 1.2) * 0.8f;
            var clip = AudioClip.Create("ambience", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void AddSoft(float[] data, float at, float f, float amp)
        {
            int n = data.Length, start = (int)(at * Rate), len = (int)(7f * Rate);
            for (int j = 0; j < len; j++)
            {
                float t = j / (float)Rate;
                float env = t < 2.5f ? 0.5f - 0.5f * Mathf.Cos(t / 2.5f * Mathf.PI) : Mathf.Exp(-(t - 2.5f) * 0.9f);
                float w = Mathf.Sin(t * f * Mathf.PI * 2) + 0.25f * Mathf.Sin(t * f * 2 * Mathf.PI * 2);
                data[(start + j) % n] += w * env * amp;
            }
        }
    }
}
