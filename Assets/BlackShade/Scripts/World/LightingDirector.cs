using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlackShade
{
    // 해 지는 과정, 교실 형광등, 정전, 비상구 유도등을 관리.
    // 시간 단계: 0 = 석양, 1 = 노을 짙어짐, 2 = 해 거의 짐, 3 = 밤
    public class LightingDirector : MonoBehaviour
    {
        [System.Serializable]
        public struct TimeOfDay
        {
            public Color sunColor;
            public float sunIntensity;
            public float sunElevation;
            public Color ambient;
            public float skyExposure;
            public float moonIntensity;
            public float reflection;
        }

        public TimeOfDay[] stages =
        {
            new TimeOfDay { sunColor = new Color(1f, 0.56f, 0.28f), sunIntensity = 1.7f, sunElevation = 9f, ambient = new Color(0.40f, 0.31f, 0.27f), skyExposure = 1.0f, moonIntensity = 0f, reflection = 0.8f },
            new TimeOfDay { sunColor = new Color(1f, 0.42f, 0.2f), sunIntensity = 1.15f, sunElevation = 5f, ambient = new Color(0.30f, 0.22f, 0.21f), skyExposure = 0.7f, moonIntensity = 0f, reflection = 0.6f },
            new TimeOfDay { sunColor = new Color(0.75f, 0.28f, 0.25f), sunIntensity = 0.45f, sunElevation = 1.5f, ambient = new Color(0.17f, 0.14f, 0.19f), skyExposure = 0.35f, moonIntensity = 0.05f, reflection = 0.35f },
            new TimeOfDay { sunColor = new Color(0.3f, 0.2f, 0.3f), sunIntensity = 0f, sunElevation = -8f, ambient = new Color(0.08f, 0.085f, 0.12f), skyExposure = 0.12f, moonIntensity = 0.18f, reflection = 0.15f },
        };

        [Header("교실 형광등")]
        public float roomLightIntensity = 0.65f;
        public float roomLightRange = 5f;
        public Color roomLightColor = new Color(1f, 0.97f, 0.92f);

        [Header("정전 후")]
        public Color blackoutAmbient = new Color(0.02f, 0.022f, 0.035f);
        public float blackoutMoon = 0.12f;

        public Light Sun { get; private set; }
        public Light Moon { get; private set; }
        public Light Emergency { get; private set; }
        public float Progress { get; private set; }
        public bool PowerOn { get; private set; }

        ClassroomFrame frame;
        readonly List<Light> roomLights = new List<Light>();
        Material sky;
        Vector3 sunHorizontal, moonHorizontal;
        Coroutine tween;
        Renderer exitSign;

        public void Setup(ClassroomFrame f)
        {
            frame = f;

            // 씬에 있던 다른 방향광은 끄고 직접 만든 해/달만 쓴다
            foreach (Light l in Object.FindObjectsOfType<Light>())
                if (l.type == LightType.Directional) l.enabled = false;

            Sun = MakeDirectional("Sun", true);
            Moon = MakeDirectional("Moon", true);
            Moon.color = new Color(0.55f, 0.65f, 0.95f);
            RenderSettings.sun = Sun;

            // 해: 창문 쪽 + 칠판 쪽(서쪽)에서 낮게 들어오는 빛
            sunHorizontal = (-frame.ToWindow * 0.75f + frame.Forward * 0.65f).normalized;
            moonHorizontal = (-frame.ToWindow * 0.9f - frame.Forward * 0.3f).normalized;

            if (RenderSettings.skybox != null)
            {
                sky = new Material(RenderSettings.skybox);
                RenderSettings.skybox = sky;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;

            // 형광등
            GameObject lights = new GameObject("ClassroomLights");
            lights.transform.SetParent(transform, false);
            foreach (float u in ClassroomFrame.Layout.LightU)
                foreach (float v in ClassroomFrame.Layout.LightV)   // 천장 형광등 9개(3×3) 바로 아래마다
                {
                    GameObject g = new GameObject("RoomLight");
                    g.transform.SetParent(lights.transform, false);
                    g.transform.position = frame.P(u, v, ClassroomFrame.Layout.LightH - 0.1f);
                    Light l = g.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = roomLightRange;
                    l.intensity = roomLightIntensity;
                    l.color = roomLightColor;
                    l.shadows = LightShadows.None;
                    l.renderMode = LightRenderMode.ForcePixel;
                    roomLights.Add(l);
                }

            // 복도 비상구 유도등 (초록) - 정전 후에만 켜짐
            GameObject em = new GameObject("EmergencyExitLight");
            em.transform.SetParent(transform, false);
            // 복도 건너편 쪽에 두어서, 복도를 지나가는 것이 '실루엣'으로 보이게 함
            em.transform.position = frame.P(ClassroomFrame.Layout.FrontDoorU + 1.2f, -2.5f, 2.5f);
            Emergency = em.AddComponent<Light>();
            Emergency.type = LightType.Point;
            Emergency.color = new Color(0.25f, 1f, 0.45f);
            Emergency.range = 9f;
            Emergency.intensity = 0f;
            Emergency.shadows = LightShadows.Soft; // 벽에 막히고 문 유리로만 새어 들어오게
            Emergency.renderMode = LightRenderMode.ForcePixel;
            GameObject sign = Prop.Box(em.transform, "ExitSign", frame.P(ClassroomFrame.Layout.FrontDoorU + 1.2f, -2.5f, 2.78f), Quaternion.LookRotation(frame.ToWindow), new Vector3(0.45f, 0.16f, 0.04f), new Color(0.1f, 0.45f, 0.2f), false);
            exitSign = sign.GetComponent<Renderer>();
            Prop.SetLayerShadow(sign, false);

            PowerOn = true;
            Apply(0f);
        }

        Light MakeDirectional(string n, bool shadows)
        {
            GameObject g = new GameObject(n);
            g.transform.SetParent(transform, false);
            Light l = g.AddComponent<Light>();
            l.type = LightType.Directional;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.85f;
            return l;
        }

        static Quaternion DirRot(Vector3 horizontal, float elevationDeg)
        {
            float e = elevationDeg * Mathf.Deg2Rad;
            Vector3 d = horizontal * Mathf.Cos(e) + Vector3.down * Mathf.Sin(e);
            return Quaternion.LookRotation(d, Vector3.up);
        }

        public void Apply(float p)
        {
            Progress = p;
            int a = Mathf.Clamp(Mathf.FloorToInt(p), 0, stages.Length - 1);
            int b = Mathf.Min(a + 1, stages.Length - 1);
            float t = Mathf.Clamp01(p - a);
            TimeOfDay A = stages[a], B = stages[b];

            Sun.color = Color.Lerp(A.sunColor, B.sunColor, t);
            Sun.intensity = Mathf.Lerp(A.sunIntensity, B.sunIntensity, t);
            Sun.enabled = Sun.intensity > 0.001f;
            Sun.transform.rotation = DirRot(sunHorizontal, Mathf.Lerp(A.sunElevation, B.sunElevation, t));

            Moon.intensity = Mathf.Lerp(A.moonIntensity, B.moonIntensity, t);
            Moon.enabled = Moon.intensity > 0.001f;
            Moon.transform.rotation = DirRot(moonHorizontal, 32f);

            if (PowerOn)
            {
                RenderSettings.ambientLight = Color.Lerp(A.ambient, B.ambient, t);
                RenderSettings.reflectionIntensity = Mathf.Lerp(A.reflection, B.reflection, t);
            }
            if (sky != null && sky.HasProperty("_Exposure"))
                sky.SetFloat("_Exposure", Mathf.Lerp(A.skyExposure, B.skyExposure, t));
        }

        public void TweenTo(float target, float duration)
        {
            if (tween != null) StopCoroutine(tween);
            tween = StartCoroutine(TweenRoutine(target, duration));
        }

        IEnumerator TweenRoutine(float target, float duration)
        {
            float from = Progress, t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                Apply(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / duration)));
                yield return null;
            }
            Apply(target);
            tween = null;
        }

        public Vector3 RoomCenterCeiling()
        {
            return frame.P(ClassroomFrame.Layout.Length * 0.5f, ClassroomFrame.Layout.Width * 0.5f, 2.8f);
        }

        // 형광등이 잠깐 깜빡
        public IEnumerator Flicker(int times)
        {
            for (int i = 0; i < times; i++)
            {
                SetRoomLights(false);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.09f));
                SetRoomLights(true);
                yield return new WaitForSeconds(Random.Range(0.06f, 0.2f));
            }
        }

        void SetRoomLights(bool on)
        {
            for (int i = 0; i < roomLights.Count; i++) roomLights[i].enabled = on;
            for (int i = 0; i < frame.lightTubes.Count; i++) frame.lightTubes[i].enabled = on;
        }

        // 정전
        public void Blackout()
        {
            PowerOn = false;
            if (tween != null) StopCoroutine(tween);
            SetRoomLights(false);
            Sun.enabled = false;
            Moon.intensity = blackoutMoon;
            Moon.enabled = true;
            RenderSettings.ambientLight = blackoutAmbient;
            RenderSettings.reflectionIntensity = 0.05f;
            if (sky != null && sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 0.08f);
        }

        // 본편으로 넘어갈 때: 프롤로그용 해/달/교실 조명을 모두 끈다 (본편은 RoomLights가 담당)
        public void ShutdownForMain()
        {
            if (tween != null) StopCoroutine(tween);
            PowerOn = false;
            if (Sun != null) Sun.enabled = false;
            if (Moon != null) Moon.enabled = false;
            if (Emergency != null) Emergency.enabled = false;
            foreach (Light l in roomLights) if (l != null) l.enabled = false;
            if (exitSign != null) exitSign.gameObject.SetActive(false);
        }

        public IEnumerator EmergencyOn(float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration) * (Random.value > 0.1f ? 1f : 0.4f);
                Emergency.intensity = 1.6f * k;
                Prop.SetEmission(exitSign, new Color(0.2f, 1f, 0.4f) * 1.5f * k);
                yield return null;
            }
            Emergency.intensity = 1.6f;
            Prop.SetEmission(exitSign, new Color(0.2f, 1f, 0.4f) * 1.5f);
        }
    }
}
