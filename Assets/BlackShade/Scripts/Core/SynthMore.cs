using System.Collections.Generic;
using UnityEngine;

namespace BlackShade
{
    // 본편용 임시 효과음과 졸업가 음악을 코드로 만든다.
    // 진짜 소리 파일이 생기면 AudioDirector 인스펙터 칸에 넣으면 그게 우선 쓰여요.
    public static class SynthMore
    {
        const int SR = 44100;

        static AudioClip Make(string name, float[] data, int sr = SR)
        {
            AudioClip c = AudioClip.Create(name, data.Length, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
        static float[] Buf(float s, int sr = SR) { return new float[Mathf.Max(1, Mathf.CeilToInt(s * sr))]; }
        static void Normalize(float[] d, float peak)
        {
            float m = 0.0001f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }
        static void Edges(float[] d, float f, int sr = SR)
        {
            int n = Mathf.Min(d.Length / 2, Mathf.CeilToInt(f * sr));
            for (int i = 0; i < n; i++) { float t = (float)i / n; d[i] *= t; d[d.Length - 1 - i] *= t; }
        }
        static float Noise(System.Random r) { return (float)r.NextDouble() * 2f - 1f; }

        // 문 여닫는 '끼익'
        public static AudioClip DoorCreak()
        {
            float[] d = Buf(0.9f);
            System.Random r = new System.Random(31);
            float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float f = 380f + 140f * Mathf.Sin(t * 5f) + 60f * Mathf.Sin(t * 23f);
                ph += f / SR;
                float stick = (Mathf.Sin(ph * 2 * Mathf.PI) > 0.6f ? 1f : -0.3f);
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.9f));
                d[i] = (stick * 0.5f + Noise(r) * 0.2f) * env;
            }
            Normalize(d, 0.35f); Edges(d, 0.01f);
            return Make("DoorCreak", d);
        }

        // 교실 미닫이문 '드르륵'
        public static AudioClip DoorSlide()
        {
            float[] d = Buf(0.6f);
            System.Random r = new System.Random(33);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float rattle = Mathf.Abs(Mathf.Sin(t * 2 * Mathf.PI * 38f));
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.6f));
                d[i] = Noise(r) * rattle * env * 0.8f + Mathf.Sin(2 * Mathf.PI * 90f * t) * env * 0.2f;
            }
            float lp = 0f;
            for (int i = 0; i < d.Length; i++) { lp += (d[i] - lp) * 0.25f; d[i] = lp; }
            Normalize(d, 0.4f); Edges(d, 0.01f);
            return Make("DoorSlide", d);
        }

        // 잠긴 문 '덜컥덜컥'
        public static AudioClip Rattle()
        {
            float[] d = Buf(0.55f);
            System.Random r = new System.Random(35);
            for (int k = 0; k < 3; k++)
            {
                int s = Mathf.RoundToInt((0.04f + k * 0.16f) * SR);
                for (int i = s; i < d.Length && i < s + SR * 0.12f; i++)
                {
                    float t = (float)(i - s) / SR;
                    d[i] += (Noise(r) * 0.6f + Mathf.Sin(2 * Mathf.PI * 160f * t)) * Mathf.Exp(-t * 40f);
                }
            }
            Normalize(d, 0.45f); Edges(d, 0.003f);
            return Make("Rattle", d);
        }

        // 열쇠/자물쇠 '철컥'
        public static AudioClip Unlock()
        {
            float[] d = Buf(0.4f);
            System.Random r = new System.Random(37);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float a = t < 0.02f ? Noise(r) * (1f - t / 0.02f) : 0f;
                float b = (t > 0.18f && t < 0.2f) ? Noise(r) * 1.2f : 0f;
                float ring = Mathf.Sin(2 * Mathf.PI * 2400f * t) * Mathf.Exp(-(t - 0.18f) * 30f) * (t > 0.18f ? 0.3f : 0f);
                d[i] = a + b + ring;
            }
            Normalize(d, 0.45f); Edges(d, 0.002f);
            return Make("Unlock", d);
        }

        // 철제 캐비닛 문 '덜컹'
        public static AudioClip Cabinet()
        {
            float[] d = Buf(0.5f);
            System.Random r = new System.Random(39);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float body = Mathf.Sin(2 * Mathf.PI * 210f * t) * Mathf.Exp(-t * 12f) + Mathf.Sin(2 * Mathf.PI * 517f * t) * Mathf.Exp(-t * 18f) * 0.6f;
                d[i] = body + Noise(r) * Mathf.Exp(-t * 60f) * 0.8f;
            }
            Normalize(d, 0.5f); Edges(d, 0.002f);
            return Make("Cabinet", d);
        }

        // 심장 박동 (두 번 '쿵쿵')
        public static AudioClip Heartbeat()
        {
            float[] d = Buf(0.8f);
            for (int k = 0; k < 2; k++)
            {
                int s = Mathf.RoundToInt(k * 0.22f * SR);
                float amp = k == 0 ? 1f : 0.7f;
                for (int i = s; i < d.Length; i++)
                {
                    float t = (float)(i - s) / SR;
                    d[i] += Mathf.Sin(2 * Mathf.PI * (52f - 12f * t) * t) * Mathf.Exp(-t * 16f) * amp;
                }
            }
            Normalize(d, 0.7f); Edges(d, 0.003f);
            return Make("Heartbeat", d);
        }

        // 괴물의 무거운 발소리
        public static AudioClip HeavyStep(int seed)
        {
            float[] d = Buf(0.35f);
            System.Random r = new System.Random(seed);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                lp += (Noise(r) - lp) * 0.08f;
                d[i] = Mathf.Sin(2 * Mathf.PI * (60f - 20f * t) * t) * Mathf.Exp(-t * 14f) + lp * Mathf.Exp(-t * 20f) * 1.5f;
            }
            Normalize(d, 0.7f); Edges(d, 0.002f);
            return Make("HeavyStep", d);
        }

        // 괴물의 낮은 웅얼거림 (여러 사람이 동시에 무언가를 부르는 듯한)
        public static AudioClip Murmur()
        {
            float len = 3.2f;
            float[] d = Buf(len);
            System.Random r = new System.Random(41);
            float[] f0 = { 92f, 97f, 110f, 123f };
            float[] ph = new float[f0.Length];
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float s = 0f;
                for (int v = 0; v < f0.Length; v++)
                {
                    float f = f0[v] * (1f + 0.04f * Mathf.Sin(t * (1.3f + v * 0.7f) + v));
                    ph[v] += f / SR;
                    float saw = (ph[v] % 1f) * 2f - 1f;
                    float syll = 0.5f + 0.5f * Mathf.Sin(t * (3.1f + v * 0.9f) + v * 2f);
                    s += saw * syll;
                }
                d[i] = s * 0.25f + Noise(r) * 0.05f;
            }
            float a = 0f, b = 0f;
            for (int i = 0; i < d.Length; i++) { a += (d[i] - a) * 0.08f; b += (a - b) * 0.08f; d[i] = b; }
            Normalize(d, 0.55f); Edges(d, 0.3f);
            return Make("Murmur", d);
        }

        // 공격받았을 때 '퍽' + 귀 울림
        public static AudioClip Hit()
        {
            float[] d = Buf(1.2f);
            System.Random r = new System.Random(43);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float punch = Mathf.Sin(2 * Mathf.PI * (90f - 40f * t) * t) * Mathf.Exp(-t * 10f) + Noise(r) * Mathf.Exp(-t * 30f);
                float ring = Mathf.Sin(2 * Mathf.PI * 3100f * t) * Mathf.Exp(-t * 2.2f) * 0.12f;
                d[i] = punch + ring;
            }
            Normalize(d, 0.8f); Edges(d, 0.002f);
            return Make("Hit", d);
        }

        // 빈 캔 '깡그랑'
        public static AudioClip CanClatter()
        {
            float[] d = Buf(0.9f);
            System.Random r = new System.Random(45);
            float[] hits = { 0f, 0.14f, 0.26f, 0.36f, 0.44f };
            for (int k = 0; k < hits.Length; k++)
            {
                int s = Mathf.RoundToInt(hits[k] * SR);
                float amp = 1f - k * 0.17f;
                for (int i = s; i < d.Length; i++)
                {
                    float t = (float)(i - s) / SR;
                    float tone = Mathf.Sin(2 * Mathf.PI * 1850f * t) + 0.7f * Mathf.Sin(2 * Mathf.PI * 2730f * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * 4120f * t);
                    d[i] += (tone * 0.5f + Noise(r) * 0.3f) * Mathf.Exp(-t * 28f) * amp;
                }
            }
            Normalize(d, 0.6f); Edges(d, 0.002f);
            return Make("Can", d);
        }

        // 자판기 '덜컹' + 캔 따는 '칙'
        public static AudioClip Vending()
        {
            float[] d = Buf(1.1f);
            System.Random r = new System.Random(47);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float drop = t > 0.2f ? Mathf.Sin(2 * Mathf.PI * 140f * (t - 0.2f)) * Mathf.Exp(-(t - 0.2f) * 14f) + Noise(r) * Mathf.Exp(-(t - 0.2f) * 40f) * 0.6f : 0f;
                float hiss = t > 0.75f ? Noise(r) * Mathf.Exp(-(t - 0.75f) * 9f) * 0.5f : 0f;
                d[i] = drop + hiss + Mathf.Sin(2 * Mathf.PI * 60f * t) * 0.04f;
            }
            Normalize(d, 0.55f); Edges(d, 0.003f);
            return Make("Vending", d);
        }

        // 꿀꺽꿀꺽
        public static AudioClip Gulp()
        {
            float[] d = Buf(0.9f);
            for (int k = 0; k < 3; k++)
            {
                int s = Mathf.RoundToInt(k * 0.28f * SR);
                for (int i = s; i < d.Length; i++)
                {
                    float t = (float)(i - s) / SR;
                    d[i] += Mathf.Sin(2 * Mathf.PI * (300f - 900f * t) * t) * Mathf.Exp(-t * 25f);
                }
            }
            Normalize(d, 0.35f); Edges(d, 0.003f);
            return Make("Gulp", d);
        }

        // 손전등 '딸깍'
        public static AudioClip Click()
        {
            float[] d = Buf(0.06f);
            System.Random r = new System.Random(49);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                d[i] = (Noise(r) * 0.5f + Mathf.Sin(2 * Mathf.PI * 3300f * t)) * Mathf.Exp(-t * 160f);
            }
            Normalize(d, 0.35f); Edges(d, 0.001f);
            return Make("Click", d);
        }

        // 노크 세 번
        public static AudioClip Knock()
        {
            float[] d = Buf(1.0f);
            System.Random r = new System.Random(51);
            float[] at = { 0f, 0.28f, 0.56f };
            foreach (float a in at)
            {
                int s = Mathf.RoundToInt(a * SR);
                for (int i = s; i < d.Length; i++)
                {
                    float t = (float)(i - s) / SR;
                    d[i] += (Mathf.Sin(2 * Mathf.PI * 180f * t) + Noise(r) * 0.4f) * Mathf.Exp(-t * 35f);
                }
            }
            Normalize(d, 0.6f); Edges(d, 0.002f);
            return Make("Knock", d);
        }

        // 방화 셔터가 올라가는 '드르르르'
        public static AudioClip Shutter()
        {
            float[] d = Buf(3.5f);
            System.Random r = new System.Random(53);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float rat = Mathf.Abs(Mathf.Sin(t * 2 * Mathf.PI * 22f));
                float env = Mathf.Clamp01(t * 3f) * Mathf.Clamp01((3.5f - t) * 2f);
                d[i] = (Noise(r) * rat * 0.7f + Mathf.Sin(2 * Mathf.PI * 55f * t) * 0.4f) * env;
            }
            float lp = 0f;
            for (int i = 0; i < d.Length; i++) { lp += (d[i] - lp) * 0.12f; d[i] = lp; }
            Normalize(d, 0.6f);
            return Make("Shutter", d);
        }

        // 아이템 획득 / 기록 저장 '띠링'
        public static AudioClip Pickup(bool save)
        {
            float[] notes = save ? new[] { 659.25f, 783.99f, 1046.5f } : new[] { 880f, 1318.5f };
            float[] d = Buf(save ? 1.3f : 0.6f);
            for (int n = 0; n < notes.Length; n++)
            {
                int s = Mathf.RoundToInt(n * 0.09f * SR);
                for (int i = s; i < d.Length; i++)
                {
                    float t = (float)(i - s) / SR;
                    d[i] += (Mathf.Sin(2 * Mathf.PI * notes[n] * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * notes[n] * 2f * t)) * Mathf.Exp(-t * (save ? 3f : 7f));
                }
            }
            Normalize(d, 0.3f); Edges(d, 0.003f);
            return Make(save ? "Save" : "Pickup", d);
        }

        // 본편 배경: 낮게 깔리는 웅웅 소리. red=true면 5층용 (더 불안한 소리)
        public static AudioClip Drone(bool red)
        {
            float len = 10f;
            float[] d = Buf(len, 22050);
            System.Random r = new System.Random(red ? 57 : 55);
            float a = 0f, b = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / 22050;
                // 루프가 자연스럽게 이어지도록 주기가 길이로 나누어 떨어지는 주파수만 사용
                float s = Mathf.Sin(2 * Mathf.PI * 4.8f * 10f * t / len * len) * 0f;
                s += Mathf.Sin(2 * Mathf.PI * 41f * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 41.4f * t) * 0.5f;
                if (red) s += Mathf.Sin(2 * Mathf.PI * 58.3f * t) * 0.35f + Mathf.Sin(2 * Mathf.PI * 233.1f * t) * 0.06f * (0.5f + 0.5f * Mathf.Sin(t * 0.6f * Mathf.PI));
                a += (Noise(r) - a) * 0.01f; b += (a - b) * 0.02f;
                d[i] = s * 0.6f + b * 3f;
            }
            Normalize(d, 0.5f);
            Edges(d, 0.05f, 22050);
            return Make(red ? "DroneRed" : "Drone", d, 22050);
        }

        // 멀리서 들리는 비명들 (배드 엔딩)
        public static AudioClip Screams()
        {
            float len = 6f;
            float[] d = Buf(len, 22050);
            System.Random r = new System.Random(59);
            for (int k = 0; k < 7; k++)
            {
                float start = (float)r.NextDouble() * 4.5f;
                float dur = 0.8f + (float)r.NextDouble() * 1.2f;
                float f0 = 500f + (float)r.NextDouble() * 600f;
                float ph = 0f;
                int s = Mathf.RoundToInt(start * 22050);
                for (int i = s; i < d.Length && i < s + dur * 22050; i++)
                {
                    float t = (float)(i - s) / 22050;
                    float f = f0 * (1f + 0.25f * Mathf.Sin(Mathf.PI * t / dur)) * (1f + 0.02f * Mathf.Sin(t * 40f));
                    ph += f / 22050;
                    float env = Mathf.Sin(Mathf.PI * t / dur);
                    d[i] += (((ph % 1f) * 2f - 1f) * 0.6f + Noise(r) * 0.4f) * env * 0.5f;
                }
            }
            float lp = 0f;
            for (int i = 0; i < d.Length; i++) { lp += (d[i] - lp) * 0.2f; d[i] = lp; }
            Normalize(d, 0.5f); Edges(d, 0.05f, 22050);
            return Make("Screams", d, 22050);
        }

        // ================================================================ 졸업가
        public struct Note { public float beat, len; public int midi; public int line; }

        public const float BPM = 92f;
        public const int IntroBars = 2;
        public static float BeatSec { get { return 60f / BPM; } }

        // 멜로디: (음, 길이) 쌍. 음 0 = 쉼표. 한 줄 = 8박
        static readonly int[][] P =
        {
            new[] { 64, 67, 67, 69, 67, 64, 67, 0 },
            new[] { 69, 67, 64, 62, 60, 0 },
            new[] { 62, 64, 65, 67, 69, 67, 64, 0 },
            new[] { 65, 64, 62, 64, 62, 0 },
            new[] { 67, 69, 71, 72, 71, 69, 67, 0 },
            new[] { 69, 67, 65, 64, 67, 0 },
            new[] { 72, 72, 71, 69, 67, 69, 71, 72, 0 },
            new[] { 74, 72, 71, 69, 67, 0 },
            new[] { 64, 67, 72, 71, 69, 67, 0 },
            new[] { 65, 69, 74, 72, 71, 69, 0 },
            new[] { 67, 67, 69, 71, 72, 74, 76, 74, 0 },
            new[] { 72, 71, 74, 71, 72 },
        };
        static readonly float[][] D =
        {
            new[] { 1f, .5f, .5f, 1f, 1f, 1f, 2f, 1f },
            new[] { 1f, 1f, 1f, 1f, 3f, 1f },
            new[] { 1f, .5f, .5f, 1f, 1f, 1f, 2f, 1f },
            new[] { 1f, 1f, 1f, 1f, 3f, 1f },
            new[] { 1f, .5f, .5f, 1f, 1f, 1f, 2f, 1f },
            new[] { 1f, 1f, 1f, 1f, 3f, 1f },
            new[] { 1f, .5f, .5f, 1f, 1f, 1f, 1f, 1f, 1f },
            new[] { 1f, 1f, 1f, 1f, 3f, 1f },
            new[] { 1f, 1f, 1f, 1f, 1f, 2f, 1f },
            new[] { 1f, 1f, 1f, 1f, 1f, 2f, 1f },
            new[] { .5f, .5f, 1f, 1f, 1f, 1f, 1f, 1f, 1f },
            new[] { 1.5f, .5f, 1f, 1f, 4f },
        };
        // 마디마다 화음 근음 (C=0) 과 장/단조
        static readonly int[] ChordRoot = { 0, 7, /*1*/0, 0, 5, 0, 7, 0, 5, 7, 0, 9, 5, 0, 9, 5, 7, 7, 0, 9, 5, 2, 7, 0, 7, 0, 0, 0 };
        static readonly bool[] ChordMinor = { false, false, false, false, false, false, false, false, false, false, false, true, false, false, true, false, false, false, false, true, false, true, false, false, false, false, false, false };

        public static List<Note> Melody()
        {
            List<Note> list = new List<Note>();
            float beat = IntroBars * 4f;
            for (int l = 0; l < P.Length; l++)
            {
                for (int i = 0; i < P[l].Length; i++)
                {
                    if (P[l][i] > 0) list.Add(new Note { beat = beat, len = D[l][i], midi = P[l][i], line = l });
                    beat += D[l][i];
                }
            }
            return list;
        }

        public static float TotalBeats { get { return (IntroBars + P.Length * 2 + 2) * 4f; } }
        public static float LineStartBeat(int line) { return IntroBars * 4f + line * 8f; }

        static float Hz(float midi) { return 440f * Mathf.Pow(2f, (midi - 69f) / 12f); }

        // 밝고 웅장한 행사용 편곡: 오르간 화음 + 멜로디 + 베이스 + 팀파니
        public static AudioClip GraduationSong()
        {
            const int sr = 22050;
            float bs = BeatSec;
            float total = TotalBeats * bs + 2.5f;
            float[] d = new float[Mathf.CeilToInt(total * sr)];

            // 화음 (마디 단위)
            int bars = Mathf.RoundToInt(TotalBeats / 4f);
            for (int bar = 0; bar < bars; bar++)
            {
                int root = ChordRoot[Mathf.Min(bar, ChordRoot.Length - 1)];
                bool minor = ChordMinor[Mathf.Min(bar, ChordMinor.Length - 1)];
                int[] tri = { 48 + root, 48 + root + (minor ? 3 : 4), 48 + root + 7, 60 + root };
                float t0 = bar * 4f * bs, dur = 4f * bs;
                if (bar == bars - 1) dur += 2f;
                foreach (int m in tri) AddTone(d, sr, Hz(m), t0, dur, 0.09f, 0.12f, 0.35f, 3);
                AddTone(d, sr, Hz(36 + root), t0, 2f * bs, 0.16f, 0.01f, 0.25f, 2);
                AddTone(d, sr, Hz(36 + root + 7), t0 + 2f * bs, 2f * bs, 0.12f, 0.01f, 0.25f, 2);
                // 팀파니 (첫 박)
                if (bar >= IntroBars) AddTimpani(d, sr, t0, Hz(36 + root));
            }
            // 시작 종소리
            AddBell(d, sr, 0f, Hz(84)); AddBell(d, sr, 2f * bs, Hz(79)); AddBell(d, sr, 4f * bs, Hz(76)); AddBell(d, sr, 6f * bs, Hz(72));

            // 멜로디 (합창 같은 음색, 옥타브 겹침)
            foreach (Note n in Melody())
            {
                float t0 = n.beat * bs, dur = n.len * bs * 0.95f;
                AddTone(d, sr, Hz(n.midi), t0, dur, 0.22f, 0.04f, 0.15f, 4, true);
                AddTone(d, sr, Hz(n.midi - 12), t0, dur, 0.07f, 0.05f, 0.15f, 2, true);
            }

            // 부드럽게 정리
            float lp = 0f;
            for (int i = 0; i < d.Length; i++) { lp += (d[i] - lp) * 0.6f; d[i] = (float)System.Math.Tanh(lp * 1.2f); }
            Normalize(d, 0.8f);
            Edges(d, 0.01f, sr);
            return Make("GraduationSong", d, sr);
        }

        static void AddTone(float[] d, int sr, float f, float t0, float dur, float amp, float attack, float release, int harmonics, bool vibrato = false)
        {
            int s0 = Mathf.RoundToInt(t0 * sr);
            int n = Mathf.RoundToInt((dur + release) * sr);
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float t = (float)i / sr;
                float env = Mathf.Clamp01(t / attack);
                if (t > dur) env *= Mathf.Clamp01(1f - (t - dur) / release);
                float vib = vibrato ? 1f + 0.004f * Mathf.Sin(2 * Mathf.PI * 5.2f * t) * Mathf.Clamp01(t * 3f) : 1f;
                float s = 0f;
                for (int h = 1; h <= harmonics; h++) s += Mathf.Sin(2 * Mathf.PI * f * h * vib * t) / (h * h * 0.8f + 0.2f);
                d[s0 + i] += s * env * amp;
            }
        }

        static void AddTimpani(float[] d, int sr, float t0, float f)
        {
            int s0 = Mathf.RoundToInt(t0 * sr);
            for (int i = 0; i < sr && s0 + i < d.Length; i++)
            {
                float t = (float)i / sr;
                d[s0 + i] += Mathf.Sin(2 * Mathf.PI * f * (1f + 0.3f * Mathf.Exp(-t * 20f)) * t) * Mathf.Exp(-t * 5f) * 0.25f;
            }
        }

        static void AddBell(float[] d, int sr, float t0, float f)
        {
            int s0 = Mathf.RoundToInt(t0 * sr);
            for (int i = 0; i < sr * 2 && s0 + i < d.Length; i++)
            {
                float t = (float)i / sr;
                float s = Mathf.Sin(2 * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * f * 2.76f * t) + 0.25f * Mathf.Sin(2 * Mathf.PI * f * 5.4f * t);
                d[s0 + i] += s * Mathf.Exp(-t * 2.5f) * 0.18f;
            }
        }
    }
}
