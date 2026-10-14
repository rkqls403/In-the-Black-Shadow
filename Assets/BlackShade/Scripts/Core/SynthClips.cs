using UnityEngine;

namespace BlackShade
{
    // 효과음 파일이 없어도 되도록 코드로 소리를 만들어 주는 곳.
    // 나중에 진짜 효과음이 생기면 AudioDirector 인스펙터 칸에 넣으면 그게 우선 사용돼요.
    public static class SynthClips
    {
        const int SR = 44100;

        static AudioClip Make(string name, float[] data)
        {
            AudioClip c = AudioClip.Create(name, data.Length, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        static float[] Buf(float seconds) { return new float[Mathf.Max(1, Mathf.CeilToInt(seconds * SR))]; }

        static void Normalize(float[] d, float peak)
        {
            float m = 0.0001f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        // 짧은 페이드 인/아웃(딸깍 소리 방지)
        static void Edges(float[] d, float fadeSeconds)
        {
            int n = Mathf.Min(d.Length / 2, Mathf.CeilToInt(fadeSeconds * SR));
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                d[i] *= t;
                d[d.Length - 1 - i] *= t;
            }
        }

        // 루프 이음매를 부드럽게: 끝부분을 앞부분에 겹쳐 섞는다
        static float[] LoopCrossfade(float[] d, float xfadeSeconds)
        {
            int x = Mathf.CeilToInt(xfadeSeconds * SR);
            int len = d.Length - x;
            float[] o = new float[len];
            for (int i = 0; i < len; i++) o[i] = d[i];
            for (int i = 0; i < x; i++)
            {
                float t = (float)i / x;
                o[i] = d[len + i] * (1f - t) + d[i] * t;
            }
            return o;
        }

        // 형광등 웅웅 소리 (60Hz 계열)
        public static AudioClip FluorescentHum()
        {
            float[] d = Buf(2f);
            System.Random r = new System.Random(3);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float s = 0.5f * Mathf.Sin(2 * Mathf.PI * 120f * t)
                        + 0.25f * Mathf.Sin(2 * Mathf.PI * 240f * t)
                        + 0.12f * Mathf.Sin(2 * Mathf.PI * 360f * t)
                        + 0.08f * Mathf.Sin(2 * Mathf.PI * 60f * t);
                lp += ((float)r.NextDouble() * 2f - 1f - lp) * 0.05f;
                d[i] = s + lp * 0.3f;
            }
            Normalize(d, 0.5f);
            return Make("Hum", d);
        }

        // 교실 공기 소리 (낮은 잡음)
        public static AudioClip RoomTone()
        {
            float[] d = Buf(5.5f);
            System.Random r = new System.Random(7);
            float a = 0f, b = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float w = (float)r.NextDouble() * 2f - 1f;
                a += (w - a) * 0.02f;
                b += (a - b) * 0.05f;
                d[i] = b;
            }
            d = LoopCrossfade(d, 0.5f);
            Normalize(d, 0.4f);
            return Make("RoomTone", d);
        }

        // 교내 방송 차임 ("딩동댕동"). up=true 올라가는 음, false 내려가는 음
        public static AudioClip Chime(bool up)
        {
            float[] notes = up ? new[] { 523.25f, 659.25f, 783.99f, 1046.5f } : new[] { 1046.5f, 783.99f, 659.25f, 523.25f };
            float step = 0.62f;
            float[] d = Buf(step * notes.Length + 1.6f);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = Mathf.RoundToInt(n * step * SR);
                float f = notes[n] * 0.997f; // 살짝 음이 나간 오래된 스피커 느낌
                for (int i = start; i < d.Length; i++)
                {
                    float t = (float)(i - start) / SR;
                    float env = Mathf.Exp(-t * 2.2f);
                    d[i] += env * (Mathf.Sin(2 * Mathf.PI * f * t) + 0.25f * Mathf.Sin(2 * Mathf.PI * f * 2.01f * t) + 0.1f * Mathf.Sin(2 * Mathf.PI * f * 2.76f * t));
                }
            }
            // 확성기처럼 거칠게
            for (int i = 0; i < d.Length; i++) d[i] = (float)System.Math.Tanh(d[i] * 1.6f);
            Normalize(d, 0.6f);
            Edges(d, 0.01f);
            return Make(up ? "ChimeUp" : "ChimeDown", d);
        }

        // 스피커 잡음 "치직—"
        public static AudioClip Static(float seconds)
        {
            float[] d = Buf(seconds);
            System.Random r = new System.Random(11);
            float burst = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                if (r.NextDouble() < 0.0008) burst = 1f;
                burst *= 0.9995f;
                float t = (float)i / d.Length;
                float env = Mathf.Sin(Mathf.PI * t);
                float n = (float)r.NextDouble() * 2f - 1f;
                float crackle = r.NextDouble() < 0.002 ? ((float)r.NextDouble() * 2f - 1f) * 3f : 0f;
                d[i] = (n * (0.35f + 0.65f * burst) + crackle) * env;
            }
            Normalize(d, 0.45f);
            Edges(d, 0.02f);
            return Make("Static", d);
        }

        // 전등 스위치 "철컥" + 차단기 내려가는 낮은 소리
        public static AudioClip PowerCut()
        {
            float[] d = Buf(0.9f);
            System.Random r = new System.Random(5);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float click = t < 0.012f ? ((float)r.NextDouble() * 2f - 1f) * (1f - t / 0.012f) : 0f;
                float click2 = (t > 0.05f && t < 0.06f) ? ((float)r.NextDouble() * 2f - 1f) * 0.6f : 0f;
                float thump = Mathf.Sin(2 * Mathf.PI * (70f - 30f * t) * t) * Mathf.Exp(-t * 7f) * 0.8f;
                d[i] = click + click2 + thump;
            }
            Normalize(d, 0.8f);
            Edges(d, 0.003f);
            return Make("PowerCut", d);
        }

        // 위층에서 무언가 무겁게 떨어지는 "쿵"
        public static AudioClip Thud()
        {
            float[] d = Buf(1.6f);
            System.Random r = new System.Random(9);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float f = 38f + 30f * Mathf.Exp(-t * 10f);
                float body = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 3.2f);
                lp += (((float)r.NextDouble() * 2f - 1f) - lp) * 0.03f;
                float dust = lp * Mathf.Exp(-t * 6f) * 1.5f;
                d[i] = body + dust;
            }
            for (int i = 0; i < d.Length; i++) d[i] = (float)System.Math.Tanh(d[i] * 2f);
            Normalize(d, 0.95f);
            Edges(d, 0.004f);
            return Make("Thud", d);
        }

        // 분필 긁는 소리
        public static AudioClip Chalk(float seconds)
        {
            float[] d = Buf(seconds);
            System.Random r = new System.Random(13);
            float prev = 0f;
            float strokeLen = 0.18f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float local = (t % strokeLen) / strokeLen;
                float stroke = Mathf.Sin(Mathf.PI * local);
                stroke *= stroke;
                float n = (float)r.NextDouble() * 2f - 1f;
                float hp = n - prev; prev = n; // 고음 강조
                float squeak = Mathf.Sin(2 * Mathf.PI * (2200f + 300f * Mathf.Sin(t * 13f)) * t) * 0.08f;
                d[i] = (hp * 0.5f + squeak) * stroke * (0.6f + 0.4f * (float)r.NextDouble());
            }
            Normalize(d, 0.35f);
            Edges(d, 0.02f);
            return Make("Chalk", d);
        }

        // 풍선 부는 소리 (숨소리 + 고무 늘어나는 소리)
        public static AudioClip Inflate(float seconds)
        {
            float[] d = Buf(seconds);
            System.Random r = new System.Random(17);
            float lp = 0f;
            float breaths = 3f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float u = t / seconds;
                float phase = (u * breaths) % 1f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(phase * 1.25f));
                lp += (((float)r.NextDouble() * 2f - 1f) - lp) * 0.15f;
                float rubber = Mathf.Sin(2 * Mathf.PI * (180f + 260f * u) * t) * 0.05f * env;
                d[i] = lp * env * 0.8f + rubber;
            }
            Normalize(d, 0.35f);
            Edges(d, 0.02f);
            return Make("Inflate", d);
        }

        // 고무줄/테이프 붙이는 "뽁"
        public static AudioClip Pop()
        {
            float[] d = Buf(0.15f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                d[i] = Mathf.Sin(2 * Mathf.PI * (520f - 1800f * t) * t) * Mathf.Exp(-t * 35f);
            }
            Normalize(d, 0.4f);
            Edges(d, 0.002f);
            return Make("Pop", d);
        }

        // 발소리
        public static AudioClip Footstep(int seed)
        {
            float[] d = Buf(0.12f);
            System.Random r = new System.Random(seed);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                lp += (((float)r.NextDouble() * 2f - 1f) - lp) * 0.25f;
                float body = Mathf.Sin(2 * Mathf.PI * 95f * t) * 0.6f;
                d[i] = (lp + body) * Mathf.Exp(-t * 45f);
            }
            Normalize(d, 0.3f);
            Edges(d, 0.002f);
            return Make("Step", d);
        }

        // 종이 바스락
        public static AudioClip Paper()
        {
            float[] d = Buf(0.5f);
            System.Random r = new System.Random(19);
            float prev = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float n = (float)r.NextDouble() * 2f - 1f;
                float hp = n - prev; prev = n;
                float env = Mathf.Exp(-t * 6f) * (0.5f + 0.5f * Mathf.Abs(Mathf.Sin(t * 60f)));
                d[i] = hp * env;
            }
            Normalize(d, 0.3f);
            Edges(d, 0.005f);
            return Make("Paper", d);
        }

        // 놀랐을 때 "훅" 하는 낮은 스팅 (너무 크지 않게)
        public static AudioClip Sting()
        {
            float[] d = Buf(1.4f);
            System.Random r = new System.Random(23);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float env = Mathf.Exp(-t * 2.5f) * Mathf.Clamp01(t * 60f);
                float cluster = Mathf.Sin(2 * Mathf.PI * 110f * t) + Mathf.Sin(2 * Mathf.PI * 116.5f * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * 233f * t);
                lp += (((float)r.NextDouble() * 2f - 1f) - lp) * 0.08f;
                d[i] = (cluster * 0.4f + lp * 1.2f) * env;
            }
            Normalize(d, 0.6f);
            Edges(d, 0.003f);
            return Make("Sting", d);
        }

        // 대사 글자 넘어갈 때 아주 작은 틱
        public static AudioClip Tick()
        {
            float[] d = Buf(0.025f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                d[i] = Mathf.Sin(2 * Mathf.PI * 1400f * t) * Mathf.Exp(-t * 220f);
            }
            Normalize(d, 0.12f);
            Edges(d, 0.001f);
            return Make("Tick", d);
        }

        // 바깥 겨울 바람 (밤)
        public static AudioClip Wind()
        {
            float[] d = Buf(8.5f);
            System.Random r = new System.Random(29);
            float a = 0f, b = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / SR;
                float w = (float)r.NextDouble() * 2f - 1f;
                float cut = 0.004f + 0.006f * (0.5f + 0.5f * Mathf.Sin(t * 0.7f) * Mathf.Sin(t * 0.23f + 1f));
                a += (w - a) * cut;
                b += (a - b) * cut;
                d[i] = b;
            }
            d = LoopCrossfade(d, 1f);
            Normalize(d, 0.5f);
            return Make("Wind", d);
        }
    }
}
