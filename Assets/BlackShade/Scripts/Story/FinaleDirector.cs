using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using S = BlackShade.PrologueScript;

namespace BlackShade
{
    // 최종장: 강당의 졸업식.
    //   의자와 하나가 된 수백 명의 3학년 → 무대의 마이크 → "졸업식을 시작하겠습니다."
    //   졸업가 리듬게임 [←][↓][↑][→] (방향키) + 선생님 괴물의 공격을 무작위 키로 피하기
    //   끝까지 부르고 "졸업 축하드립니다." → 해피 엔딩 / 도중에 쓰러지면 → 배드 엔딩
    public class FinaleDirector : MonoBehaviour
    {
        public static FinaleDirector I { get; private set; }
        public bool Active { get; private set; }
        public bool InCeremony { get; private set; }

        const int F = 5;
        static readonly KeyCode[] Lanes = { KeyCode.LeftArrow, KeyCode.DownArrow, KeyCode.UpArrow, KeyCode.RightArrow };
        static readonly string[] LaneNames = { "←", "↓", "↑", "→" };
        static readonly KeyCode[] DodgeKeys = { KeyCode.X, KeyCode.G, KeyCode.H, KeyCode.V, KeyCode.B, KeyCode.N, KeyCode.R, KeyCode.T, KeyCode.Y, KeyCode.U, KeyCode.C, KeyCode.Z, KeyCode.M, KeyCode.P, KeyCode.L, KeyCode.O };

        MainDirector main;
        GameUI ui;
        AudioDirector sfx;
        GameObject hall;
        readonly List<Transform> students = new List<Transform>();
        readonly List<Transform> heads = new List<Transform>();
        readonly List<GameObject> tears = new List<GameObject>();
        readonly List<Transform> teachers = new List<Transform>();
        Transform curtainL, curtainR;
        readonly List<Transform> sideCurtains = new List<Transform>();
        ParticleSystem petals;
        Interactable mic;
        AudioSource music;
        Vector3 stageFront, micSpot, doorIn;

        void Awake() { I = this; }

        public void Init(MainDirector m)
        {
            main = m;
            ui = GameUI.I;
            sfx = AudioDirector.I;
        }

        Vector3 P(float x, float z, float h = 0f) { return MapSpace.GF(F, x, h, z); }

        // ================================================================ 강당 만들기
        void Build()
        {
            if (hall != null) return;
            hall = new GameObject("Auditorium");
            Material chairMat = Mat(new Color(0.22f, 0.16f, 0.12f));
            Material uniform = Mat(new Color(0.07f, 0.08f, 0.12f));
            Material skin = Mat(new Color(0.42f, 0.4f, 0.4f));
            Material ink = Mat(new Color(0.01f, 0.01f, 0.01f));
            Material melt = Mat(new Color(0.05f, 0.05f, 0.07f));
            Material stage = Mat(new Color(0.32f, 0.22f, 0.14f));
            Material curtain = Mat(new Color(0.45f, 0.04f, 0.06f));

            // 무대 + 계단
            Solid("Stage", P(77.9f, 40.45f, 0.45f), new Vector3(17.8f, 0.9f, 5.7f), stage);
            for (int i = 0; i < 4; i++)
                Solid("Step" + i, P(78.25f, 37.6f - 0.3f * (3 - i) - 0.15f, 0.1125f * (i + 1)), new Vector3(2.6f, 0.225f * (i + 1), 0.3f), stage);
            // 무대 왼쪽의 그랜드 피아노 (건반은 마이크 쪽)
            Pianos.Grand(hall.transform, P(73.62f, 40.6f, 0.9f), MapSpace.Dir(1, 0));
            stageFront = P(78.25f, 37.3f, 0.9f);
            micSpot = P(78.25f, 39.1f, 0.9f);
            doorIn = P(82f, 13.6f);

            // 무대 뒤 커튼 (해피 엔딩 때 걷힘) + 창문 커튼
            curtainL = Mk(PrimitiveType.Cube, hall.transform, P(73f, 43.15f, 1.95f), Quaternion.identity, new Vector3(9.8f, 2.1f, 0.12f), curtain).transform;
            curtainR = Mk(PrimitiveType.Cube, hall.transform, P(82.8f, 43.15f, 1.95f), Quaternion.identity, new Vector3(9.8f, 2.1f, 0.12f), curtain).transform;
            foreach (float z in new[] { 18f, 25f, 32f, 39f })
            {
                sideCurtains.Add(Mk(PrimitiveType.Cube, hall.transform, P(68.17f, z, 1.5f), Quaternion.identity, new Vector3(0.06f, 1.6f, 3.3f), curtain).transform);
                sideCurtains.Add(Mk(PrimitiveType.Cube, hall.transform, P(87.66f, z, 1.5f), Quaternion.identity, new Vector3(0.06f, 1.6f, 3.3f), curtain).transform);
            }
            // 현수막
            GameObject banner = Mk(PrimitiveType.Cube, hall.transform, P(77.9f, 43.05f, 2.75f), Quaternion.identity, new Vector3(8f, 0.45f, 0.03f), Mat(new Color(0.85f, 0.82f, 0.75f)));
            CanvasText(banner.transform.position - MapSpace.Dir(0, 1) * 0.03f, MapSpace.Dir(0, 1), "제 1 회   졸 업 식", 7.5f, 0.42f, new Color(0.35f, 0.05f, 0.05f));

            // 마이크 + 보면대
            Material metal = Mat(new Color(0.2f, 0.2f, 0.22f));
            GameObject micPole = Mk(PrimitiveType.Cylinder, hall.transform, micSpot + MapSpace.Dir(0, -1) * 0.45f + Vector3.up * 0.75f, Quaternion.identity, new Vector3(0.025f, 0.75f, 0.025f), metal);
            GameObject micHead = Mk(PrimitiveType.Sphere, hall.transform, micSpot + MapSpace.Dir(0, -1) * 0.45f + Vector3.up * 1.5f, Quaternion.identity, new Vector3(0.07f, 0.09f, 0.07f), metal);
            GameObject standPole = Mk(PrimitiveType.Cylinder, hall.transform, micSpot + MapSpace.Dir(-1, -0.4f) * 0.75f + Vector3.up * 0.55f, Quaternion.identity, new Vector3(0.02f, 0.55f, 0.02f), metal);
            GameObject standDesk = Mk(PrimitiveType.Cube, hall.transform, micSpot + MapSpace.Dir(-1, -0.4f) * 0.75f + Vector3.up * 1.12f, Quaternion.LookRotation(MapSpace.Dir(0, 1)) * Quaternion.Euler(-25, 0, 0), new Vector3(0.5f, 0.35f, 0.02f), metal);
            // 받은 모델이 있으면 모습만 교체 (마이크 머리의 상호작용은 그대로)
            if (PropModels.Place("Microphone", hall.transform, micSpot + MapSpace.Dir(0, -1) * 0.45f, MapSpace.Dir(0, 1), new Vector3(0f, 1.62f, 0f), PropModels.Fit.Height, "MicStand") != null)
            {
                micPole.GetComponent<Renderer>().enabled = false;
                micHead.GetComponent<Renderer>().enabled = false;
            }
            if (PropModels.Place("MusicStand", hall.transform, micSpot + MapSpace.Dir(-1, -0.4f) * 0.75f, MapSpace.Dir(0, 1), new Vector3(0f, 1.2f, 0f), PropModels.Fit.Height, "ScoreStand") != null)
            {
                standPole.GetComponent<Renderer>().enabled = false;
                standDesk.GetComponent<Renderer>().enabled = false;
            }
            mic = micHead.AddComponent<Interactable>();
            mic.prompt = "마이크 잡기";
            mic.range = 2.4f;
            mic.available = false;
            mic.onUse = () => StartCoroutine(Ceremony());

            // 의자 + 의자와 하나가 된 학생들
            GameObject chairs = new GameObject("Chairs");
            chairs.transform.SetParent(hall.transform, false);
            Quaternion face = Quaternion.LookRotation(MapSpace.Dir(0, 1)); // 무대(남쪽)를 봄
            List<float> xs = new List<float>();
            for (int i = 0; i < 11; i++) xs.Add(69.4f + i * 0.7f);
            for (int i = 0; i < 3; i++) xs.Add(79.6f + i * 0.62f);
            for (int i = 0; i < 6; i++) xs.Add(83.3f + i * 0.68f);
            System.Random rnd = new System.Random(298);
            bool chairModel = PropModels.Has("FoldingChair");
            GameObject chairModels = new GameObject("ChairModels");
            chairModels.transform.SetParent(hall.transform, false);
            for (int row = 0; row < 14; row++)
            {
                float z = 16.0f + row * 1.05f;
                foreach (float x in xs)
                {
                    Vector3 seat = P(x, z);
                    if (chairModel)
                        PropModels.Place("FoldingChair", chairModels.transform, seat, MapSpace.Dir(0, 1), new Vector3(0f, 0.82f, 0f), PropModels.Fit.Height, "Chair");
                    else
                    {
                        Mk(PrimitiveType.Cube, chairs.transform, seat + Vector3.up * 0.44f, face, new Vector3(0.48f, 0.05f, 0.46f), chairMat);
                        Mk(PrimitiveType.Cube, chairs.transform, seat + Vector3.up * 0.75f - MapSpace.Dir(0, 1) * 0.22f, face, new Vector3(0.48f, 0.6f, 0.05f), chairMat);
                    }

                    GameObject st = new GameObject("Student");
                    st.transform.SetParent(hall.transform, false);
                    st.transform.position = seat;
                    st.transform.rotation = face;
                    Mk(PrimitiveType.Sphere, st.transform, seat + Vector3.up * 0.47f, face, new Vector3(0.55f, 0.12f, 0.55f), melt);           // 녹아 붙은 몸
                    Mk(PrimitiveType.Capsule, st.transform, seat + Vector3.up * 0.82f - MapSpace.Dir(0, 1) * 0.05f, face * Quaternion.Euler(14, 0, 0), new Vector3(0.38f, 0.38f, 0.3f), uniform);
                    Transform head = new GameObject("Head").transform;
                    head.SetParent(st.transform, false);
                    head.position = seat + Vector3.up * 1.22f + MapSpace.Dir(0, 1) * 0.04f;
                    head.rotation = face * Quaternion.Euler(38f + (float)rnd.NextDouble() * 12f, (float)rnd.NextDouble() * 20f - 10f, (float)rnd.NextDouble() * 16f - 8f);
                    Mk(PrimitiveType.Sphere, head, head.position, head.rotation, new Vector3(0.2f, 0.24f, 0.21f), skin);
                    Mk(PrimitiveType.Sphere, head, head.position + head.rotation * new Vector3(0, 0.08f, -0.03f), head.rotation, new Vector3(0.22f, 0.16f, 0.22f), ink); // 머리카락
                    // 눈 없이 흘러내리는 검은 눈물
                    GameObject tl = Mk(PrimitiveType.Cube, head, head.position + head.rotation * new Vector3(-0.045f, -0.03f, 0.1f), head.rotation, new Vector3(0.022f, 0.16f, 0.01f), ink);
                    GameObject tr = Mk(PrimitiveType.Cube, head, head.position + head.rotation * new Vector3(0.045f, -0.03f, 0.1f), head.rotation, new Vector3(0.022f, 0.16f, 0.01f), ink);
                    tears.Add(tl); tears.Add(tr);
                    Mk(PrimitiveType.Cylinder, st.transform, seat + Vector3.up * 0.005f, Quaternion.identity, new Vector3(0.5f, 0.003f, 0.5f), ink); // 바닥에 고인 잉크
                    students.Add(st.transform);
                    heads.Add(head);
                }
            }
            StaticBatchingUtility.Combine(chairs);

            // 조명: 무대 위 따뜻한 스포트라이트 + 아주 어두운 객석
            Spot(P(78.25f, 33.5f, 2.9f), micSpot + Vector3.up * 1.2f, new Color(1f, 0.85f, 0.65f), 2.2f, 34f, 12f);
            Spot(P(72f, 30f, 2.9f), P(72f, 22f), new Color(0.4f, 0.45f, 0.65f), 0.6f, 70f, 12f);
            Spot(P(84f, 30f, 2.9f), P(84f, 22f), new Color(0.4f, 0.45f, 0.65f), 0.6f, 70f, 12f);

            // 꽃보라 (해피 엔딩)
            petals = MakePetals();

            hall.SetActive(true);
        }

        Material Mat(Color c)
        {
            Material m = new Material(Shader.Find("Standard"));
            m.color = c;
            m.SetFloat("_Glossiness", 0.15f);
            m.enableInstancing = true;
            return m;
        }

        GameObject Mk(PrimitiveType t, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(t);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, true);
            g.transform.position = pos;
            g.transform.rotation = rot;
            Vector3 ps = parent.lossyScale;
            g.transform.localScale = new Vector3(scale.x / ps.x, scale.y / ps.y, scale.z / ps.z);
            Renderer r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        void Solid(string name, Vector3 center, Vector3 size, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(hall.transform, false);
            g.transform.position = center;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
        }

        void Spot(Vector3 from, Vector3 to, Color c, float intensity, float angle, float range)
        {
            GameObject g = new GameObject("Spot");
            g.transform.SetParent(hall.transform, false);
            g.transform.position = from;
            g.transform.rotation = Quaternion.LookRotation(to - from);
            Light l = g.AddComponent<Light>();
            l.type = LightType.Spot; l.color = c; l.intensity = intensity; l.spotAngle = angle; l.range = range;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
        }

        void CanvasText(Vector3 pos, Vector3 facing, string s, float w, float h, Color c)
        {
            GameObject go = new GameObject("BannerText", typeof(RectTransform));
            go.transform.SetParent(hall.transform, true);
            Canvas cv = go.AddComponent<Canvas>(); cv.renderMode = RenderMode.WorldSpace;
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(w * 200f, h * 200f);
            rt.position = pos; rt.rotation = Quaternion.LookRotation(facing); rt.localScale = Vector3.one / 200f;
            Text t = UIKit.NewText("T", rt, 60, c, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(t.rectTransform);
            t.text = s;
        }

        ParticleSystem MakePetals()
        {
            GameObject g = new GameObject("Petals");
            g.transform.SetParent(hall.transform, false);
            g.transform.position = P(77.9f, 27f, 2.9f);
            ParticleSystem ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var mainM = ps.main;
            mainM.duration = 10f; mainM.loop = true;
            mainM.startLifetime = 7f;
            mainM.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.0f);
            mainM.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
            mainM.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            mainM.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.72f, 0.82f), new Color(1f, 0.95f, 0.95f));
            mainM.gravityModifier = 0.06f;
            mainM.simulationSpace = ParticleSystemSimulationSpace.World;
            mainM.maxParticles = 2500;
            var em = ps.emission; em.rateOverTime = 260f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(19f, 0.2f, 30f);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.5f; noise.frequency = 0.4f;
            ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
            Material m = new Material(Shader.Find("Sprites/Default"));
            m.mainTexture = PetalTexture();
            r.material = m;
            return ps;
        }

        static Texture2D PetalTexture()
        {
            int n = 32;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float d = u * u / 0.35f + v * v;
                    float notch = (v > 0.6f && Mathf.Abs(u) < 0.12f) ? 0f : 1f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 3f) * notch));
                }
            t.Apply();
            return t;
        }

        // ================================================================ 입장
        public void Enter() { StartCoroutine(EnterRoutine()); }

        IEnumerator EnterRoutine()
        {
            Active = true;
            main.SetCutscene(true);
            sfx.PlayAt(sfx.knock, P(82f, 12.6f, 1.2f), 0.9f);
            yield return new WaitForSeconds(1.4f);
            yield return ui.Fade(1f, 0.9f);

            if (TeacherMonster.I != null) TeacherMonster.I.Deactivate();
            Build();
            Door d = Door.Find("5F_hall");
            if (d != null) d.Close();
            sfx.StopAllLoops(0.5f);
            sfx.StartLoop("hallTone", sfx.roomTone, 0.15f, null, 2f);
            RenderSettings.ambientLight = new Color(0.035f, 0.035f, 0.05f);
            RenderSettings.fogColor = Color.black;
            RenderSettings.fogDensity = 0.025f;

            FirstPersonController p = main.Player;
            p.SetMode(FirstPersonController.Mode.Cutscene);
            p.Teleport(doorIn, MapSpace.YawOf(0, 1));
            p.eyeHeight = p.standEyeHeight; p.pitch = 2f; p.roll = 0f;
            yield return new WaitForSeconds(0.6f);
            yield return ui.Fade(0f, 1.6f);

            yield return ui.Say(S.Me, "……", 1.5f, true);
            yield return ui.Say(S.Me, "다들… 여기 있었구나.", -1f, true);
            ui.HideDialogue();
            ui.SetObjective("졸업식", new[] { "무대 위 마이크 앞으로 가기" });
            mic.available = true;
            p.SetMode(FirstPersonController.Mode.Free);
            main.SetCutscene(false);
        }

        // ================================================================ 졸업식
        IEnumerator Ceremony()
        {
            InCeremony = true;
            main.SetCutscene(true);
            ui.HideObjective();
            FirstPersonController p = main.Player;
            p.SetMode(FirstPersonController.Mode.Cutscene);
            if (Flashlight.I != null) Flashlight.I.Set(false);
            yield return p.TweenView(micSpot, MapSpace.YawOf(0, -1), 6f, 0f, p.standEyeHeight, 1.2f);
            ui.Toast("악보를 보면대 위에 펼쳤다.", 2.2f);
            yield return new WaitForSeconds(1.5f);

            sfx.Play(sfx.chimeUp, 0.7f);
            yield return new WaitForSeconds(2.6f);
            yield return ui.Broadcast("졸업식을 시작하겠습니다.", 2.4f);
            yield return ui.HideBroadcast(0.5f);

            // 선생님들이 강당 문으로 들어온다
            SpawnTeachers();
            p.SetMode(FirstPersonController.Mode.LookOnly);
            p.lookOnlyYawRange = 40f;

            bool survived = false;
            yield return RhythmGame(ok => survived = ok);
            if (survived) yield return HappyEnding();
            else yield return BadEnding();
        }

        void SpawnTeachers()
        {
            Transform src = TeacherMonster.I != null ? TeacherMonster.I.transform.Find("Body") : null;
            float[] xs = { 82.1f, 78.25f, 82.1f };
            for (int i = 0; i < 3; i++)
            {
                GameObject t = src != null ? Instantiate(src.gameObject) : new GameObject("Teacher");
                t.name = "Teacher" + i;
                t.transform.SetParent(hall.transform, true);
                t.transform.position = P(xs[i], 12.95f - i * 0.8f);
                t.transform.rotation = Quaternion.LookRotation(MapSpace.Dir(0, 1));
                t.SetActive(true);
                teachers.Add(t.transform);
            }
            sfx.PlayAt(sfx.doorCreak, P(82f, 12.6f, 1.2f), 1f, 0.7f);
        }

        // ---------------------------------------------------------------- 리듬게임
        class NoteObj { public float time; public int lane; public bool done; public Image img; }

        IEnumerator RhythmGame(System.Action<bool> result)
        {
            GameObject canvasGo = new GameObject("RhythmUI", typeof(RectTransform));
            Canvas cv = canvasGo.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 120;
            CanvasScaler sc = canvasGo.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            RectTransform root = (RectTransform)canvasGo.transform;

            // 레인
            const float laneW = 116f, hitY = -400f, topY = 120f, travel = 1.5f;
            Color[] laneCol = { new Color(0.98f, 0.62f, 0.7f), new Color(0.98f, 0.85f, 0.55f), new Color(0.6f, 0.85f, 0.75f), new Color(0.6f, 0.75f, 0.98f) };
            Image panel = UIKit.NewImage("Lanes", root, new Color(0, 0, 0, 0.45f), UIKit.Rounded, true);
            UIKit.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -180), new Vector2(laneW * 4 + 24, 640));
            Image[] keyCaps = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * laneW;
                Image lane = UIKit.NewImage("Lane", root, new Color(1, 1, 1, 0.04f));
                UIKit.Anchor(lane.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -180), new Vector2(laneW - 8, 620));
                Image cap = UIKit.NewImage("Key", root, new Color(laneCol[i].r, laneCol[i].g, laneCol[i].b, 0.35f), UIKit.Rounded, true);
                UIKit.Anchor(cap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, hitY), new Vector2(laneW - 14, 34));
                Text kt = UIKit.NewText("T", cap.transform, 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(kt.rectTransform); kt.text = LaneNames[i];
                keyCaps[i] = cap;
            }
            Text judge = UIKit.NewText("Judge", root, 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(judge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -320), new Vector2(500, 50));
            UIKit.AddShadow(judge, 0.9f);
            Text lyric = UIKit.NewText("Lyric", root, 54, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(lyric.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 400), new Vector2(1600, 80));
            lyric.supportRichText = true;
            UIKit.AddShadow(lyric, 0.9f, 3f);
            Text nextLyric = UIKit.NewText("Next", root, 30, new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter);
            UIKit.Anchor(nextLyric.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 340), new Vector2(1600, 50));
            Text help = UIKit.NewText("Help", root, 22, new Color(1, 1, 1, 0.65f), TextAnchor.MiddleCenter);
            UIKit.Anchor(help.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 280), new Vector2(1600, 40));
            help.text = "졸업가를 끝까지 부르자 — 음표가 선에 닿을 때 방향키 [←] [↓] [↑] [→]   ·   선생님이 손을 휘두르면 원 안의 키를 눌러 피하기";

            // 회피 원
            Image ringBg = UIKit.NewImage("DodgeBg", root, new Color(0, 0, 0, 0.6f), UIKit.Circle);
            UIKit.Anchor(ringBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520, 40), new Vector2(190, 190));
            Image ring = UIKit.NewImage("DodgeRing", ringBg.transform, new Color(0.95f, 0.25f, 0.2f), UIKit.Ring);
            UIKit.Stretch(ring.rectTransform);
            ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360; ring.fillOrigin = (int)Image.Origin360.Top; ring.fillClockwise = false;
            Text ringKey = UIKit.NewText("Key", ringBg.transform, 84, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(ringKey.rectTransform);
            ringBg.gameObject.SetActive(false);

            // 음표
            float bs = SynthMore.BeatSec;
            List<NoteObj> notes = new List<NoteObj>();
            foreach (SynthMore.Note n in SynthMore.Melody())
                notes.Add(new NoteObj { time = n.beat * bs, lane = Mathf.Clamp((n.midi - 60) / 4, 0, 3) });
            float songLen = SynthMore.TotalBeats * bs;
            float[] attacks = { 9f, 14.5f, 20f, 25f, 30f, 35f, 40f, 44.5f, 49f, 53.5f, 58f, 62f, 66f, 69.5f };
            int nextAttack = 0;
            bool dodgeActive = false; float dodgeStart = 0f, dodgeDur = 1.5f; KeyCode dodgeKey = KeyCode.X; Transform attacker = null;
            int hits = 0, misses = 0, combo = 0;
            float judgeTimer = 0f;

            // 음악 시작 (오디오 시계 기준)
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.clip = sfx.Song(); src.volume = 0.9f; src.playOnAwake = false;
            music = src;
            double startDsp = AudioSettings.dspTime + 1.2;
            src.PlayScheduled(startDsp);

            GameState gs = GameState.Current;
            bool dead = false;
            while (true)
            {
                float t = (float)(AudioSettings.dspTime - startDsp);
                if (t > songLen + 0.3f) break;

                // 음표 이동/생성
                foreach (NoteObj n in notes)
                {
                    if (n.done) continue;
                    float dt = n.time - t;
                    if (dt > travel) continue;
                    if (n.img == null)
                    {
                        n.img = UIKit.NewImage("Note", root, laneCol[n.lane], UIKit.Rounded, true);
                        n.img.rectTransform.sizeDelta = new Vector2(laneW - 18, 26);
                        n.img.rectTransform.anchorMin = n.img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    }
                    n.img.rectTransform.anchoredPosition = new Vector2((n.lane - 1.5f) * laneW, hitY + (dt / travel) * (topY - hitY));
                    if (dt < -0.18f)
                    {
                        n.done = true; misses++; combo = 0;
                        n.img.color = new Color(0.6f, 0.1f, 0.1f, 0.4f);
                        Destroy(n.img.gameObject, 0.25f);
                        judge.text = "놓침"; judge.color = new Color(0.9f, 0.4f, 0.4f); judgeTimer = 0.5f;
                    }
                }
                // 키 입력
                for (int lane = 0; lane < 4; lane++)
                {
                    bool down = GameInput.KeyDown(Lanes[lane]);
                    keyCaps[lane].color = GameInput.Key(Lanes[lane]) ? laneCol[lane] : new Color(laneCol[lane].r, laneCol[lane].g, laneCol[lane].b, 0.35f);
                    if (!down) continue;
                    NoteObj best = null; float bestD = 0.18f;
                    foreach (NoteObj n in notes)
                    {
                        if (n.done || n.lane != lane) continue;
                        float d = Mathf.Abs(n.time - t);
                        if (d < bestD) { bestD = d; best = n; }
                    }
                    if (best != null)
                    {
                        best.done = true; hits++; combo++;
                        if (best.img != null) Destroy(best.img.gameObject);
                        judge.text = (bestD < 0.07f ? "완벽!" : bestD < 0.13f ? "좋아!" : "아슬아슬") + (combo > 4 ? "   " + combo + " 연속" : "");
                        judge.color = bestD < 0.07f ? new Color(1f, 0.9f, 0.55f) : Color.white;
                        judgeTimer = 0.45f;
                    }
                }
                judgeTimer -= Time.deltaTime;
                judge.enabled = judgeTimer > 0f;

                // 가사 (한 줄 = 8박)
                float beat = t / bs - SynthMore.IntroBars * 4f;
                int line = Mathf.FloorToInt(beat / 8f);
                if (line >= 0 && line < SongData.Lines.Length)
                {
                    string s = SongData.Lines[line];
                    int sung = Mathf.Clamp(Mathf.RoundToInt((beat - line * 8f) / 7f * s.Length), 0, s.Length);
                    lyric.text = "<color=#F5C780>" + s.Substring(0, sung) + "</color>" + s.Substring(sung);
                    nextLyric.text = line + 1 < SongData.Lines.Length ? SongData.Lines[line + 1] : "";
                }
                else if (line < 0) { lyric.text = "♪"; nextLyric.text = SongData.Lines[0]; }
                else { lyric.text = ""; nextLyric.text = ""; }

                // 학생들이 노래가 진행될수록 고개를 든다
                float prog = Mathf.Clamp01(t / songLen);
                for (int i = 0; i < heads.Count; i += 7)
                {
                    Vector3 e = heads[i].localEulerAngles;
                    heads[i].localRotation = Quaternion.Slerp(heads[i].localRotation, Quaternion.Euler(Mathf.Lerp(40f, 8f, prog), e.y, e.z), Time.deltaTime * 0.5f);
                }

                // 선생님들이 다가옴
                for (int i = 0; i < teachers.Count; i++)
                {
                    if (teachers[i] == attacker) continue;
                    Vector3 target = P(i == 1 ? 78.25f : 76.3f + i * 2.2f, Mathf.Lerp(14f, 35.2f, Mathf.Clamp01((t + 4f) / 22f)) - i * 0.5f);
                    teachers[i].position = Vector3.MoveTowards(teachers[i].position, target, Time.deltaTime * 1.4f);
                    teachers[i].rotation = Quaternion.LookRotation(MapSpace.Dir(0, 1));
                }

                // 공격 → 회피
                if (!dodgeActive && nextAttack < attacks.Length && t >= attacks[nextAttack])
                {
                    nextAttack++;
                    dodgeActive = true;
                    dodgeStart = t;
                    dodgeDur = Mathf.Lerp(1.6f, 1.1f, prog);
                    dodgeKey = DodgeKeys[Random.Range(0, DodgeKeys.Length)];
                    attacker = teachers[Random.Range(0, teachers.Count)];
                    ringBg.gameObject.SetActive(true);
                    ringKey.text = dodgeKey.ToString();
                    sfx.Play(sfx.sting, 0.35f, 1.3f);
                }
                if (dodgeActive)
                {
                    float k = (t - dodgeStart) / dodgeDur;
                    ring.fillAmount = 1f - k;
                    ringBg.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 18f) * 0.03f);
                    // 공격하는 선생님이 무대 앞까지 튀어나와 팔을 치켜듦
                    attacker.position = Vector3.MoveTowards(attacker.position, stageFront + MapSpace.Dir(0, -1) * 0.2f, Time.deltaTime * 9f);
                    attacker.rotation = Quaternion.LookRotation(MapSpace.Dir(0, 1)) * Quaternion.Euler(-10f * k, 0, 0);
                    bool pressed = GameInput.KeyDown(dodgeKey);
                    if (pressed)
                    {
                        dodgeActive = false;
                        ringBg.gameObject.SetActive(false);
                        sfx.Play(sfx.doorSlide, 0.5f, 2.2f);
                        main.Player.Shake(0.015f, 0.2f);
                        attacker.position += MapSpace.Dir(0, -1) * 2.5f;
                        judge.text = "피했다!"; judge.color = new Color(0.6f, 0.9f, 1f); judgeTimer = 0.6f;
                        attacker = null;
                    }
                    else if (k >= 1f)
                    {
                        dodgeActive = false;
                        ringBg.gameObject.SetActive(false);
                        if (!DevMode.God) gs.hp = Mathf.Max(0f, gs.hp - 25f);
                        ui.FlashDamage();
                        sfx.Play(sfx.hit, 0.9f);
                        main.Player.Shake(0.06f, 0.5f);
                        attacker.position += MapSpace.Dir(0, -1) * 2f;
                        attacker = null;
                        if (gs.hp <= 0f) { dead = true; break; }
                    }
                }
                yield return null;
            }

            Destroy(canvasGo);
            if (dead) { result(false); yield break; }

            // 마지막 인사
            ui.ShowBanner(new[] { "Enter" }, "마지막 인사를 전한다");
            float wait = 0f;
            while (!(GameInput.KeyDown(KeyCode.Return) || GameInput.KeyDown(KeyCode.KeypadEnter) || GameInput.KeyDown(KeyCode.Space) || GameInput.KeyDown(KeyCode.E)) && wait < 30f)
            { wait += Time.deltaTime; yield return null; }
            ui.HideBanner();
            float cps = ui.charsPerSecond; ui.charsPerSecond = 9f;
            yield return ui.Say(S.Me, SongData.FinalWords, 2.5f);
            ui.charsPerSecond = cps;
            ui.HideDialogue();
            Debug.Log("[Finale] 맞춤 " + hits + " / 놓침 " + misses);
            result(true);
        }

        // ================================================================ 해피 엔딩: 해방의 졸업식
        IEnumerator HappyEnding()
        {
            FirstPersonController p = main.Player;
            sfx.Play(sfx.chimeDown, 0.6f);
            ui.SetTint(new Color(1f, 1f, 1f, 0.85f));
            StartCoroutine(ui.TintTo(new Color(1f, 1f, 1f, 0f), 2.5f));

            // 괴물들이 사라짐
            foreach (Transform t in teachers) StartCoroutine(Vanish(t, 0.8f));
            foreach (GameObject t in tears) t.SetActive(false);
            petals.Play();

            // 커튼이 걷히고 따뜻한 빛
            StartCoroutine(OpenCurtains());
            StartCoroutine(WarmLight());
            yield return new WaitForSeconds(1.5f);

            // 3학년들이 자리에서 일어나 강당 문으로 뛰쳐나감
            StartCoroutine(StudentsLeave(true));
            p.lookOnlyYawRange = 80f;
            yield return new WaitForSeconds(5.5f);

            yield return ui.Say(S.Haeun, "…고마워.", 2.8f, true);
            yield return ui.Say(S.Haeun, "졸업 축하해.", 3.2f, true);
            ui.HideDialogue();
            yield return new WaitForSeconds(2f);
            GameState.Current.SetFlag("ending_happy");
            ModalUI.I.Ending("해방의 졸업식", "HAPPY ENDING",
                "끝나지 못했던 제1회 졸업식이, 마침내 끝났다.\n강당을 가득 채웠던 298명의 졸업생과 선생님들이\n따뜻한 빛 속으로 문을 나섰다.",
                new Color(0.98f, 0.82f, 0.6f), new[] { "타이틀로" }, i => main.ToTitle());
        }

        // ================================================================ 배드 엔딩: 미완의 졸업식
        IEnumerator BadEnding()
        {
            FirstPersonController p = main.Player;
            StartCoroutine(SongDies());
            StartCoroutine(ui.TintTo(new Color(0.55f, 0f, 0f, 0.55f), 1.5f));
            p.SetMode(FirstPersonController.Mode.Cutscene);
            yield return p.TweenView(p.transform.position, p.yaw, 25f, 55f, 0.45f, 1.2f);
            sfx.Play(sfx.hit, 0.6f, 0.7f);
            yield return new WaitForSeconds(1.2f);

            // 학생들이 선생님들의 손발이 되어 좀비처럼 문밖으로
            StartCoroutine(StudentsLeave(false));
            foreach (Transform t in teachers) StartCoroutine(WalkTo(t, P(82f, 12.8f), 1.3f, false));
            for (int i = 0; i < 6; i++)
            {
                sfx.PlayAt(sfx.RandomHeavyStep(), P(80f + Random.Range(-3f, 3f), Random.Range(15f, 25f)), 0.8f, Random.Range(0.8f, 1f));
                yield return new WaitForSeconds(0.5f);
            }
            sfx.Play(sfx.Screams(), 0.5f);
            yield return new WaitForSeconds(3f);
            yield return ui.Fade(1f, 3f);
            yield return new WaitForSeconds(1f);
            GameState.Current.SetFlag("ending_bad");
            ModalUI.I.Ending("미완의 졸업식", "BAD ENDING",
                "노래는 끝나지 않았다.\n졸업하지 못한 아이들이 선생님을 따라 강당 문을 나섰다.\n저 멀리서 비명과 발소리가 들려온다.",
                new Color(0.8f, 0.15f, 0.12f), new[] { "마지막 기록에서 다시 하기", "타이틀로" },
                i => { if (i == 0) main.Retry(); else main.ToTitle(); });
        }

        IEnumerator SongDies()
        {
            if (music == null) yield break;
            float t = 0f;
            while (t < 2.5f && music != null)
            {
                t += Time.deltaTime;
                music.pitch = Mathf.Lerp(1f, 0.25f, t / 2.5f);
                music.volume = Mathf.Lerp(0.9f, 0f, t / 2.5f);
                yield return null;
            }
            if (music != null) music.Stop();
        }

        IEnumerator Vanish(Transform t, float dur)
        {
            Vector3 s0 = t.localScale; float k = 0f;
            while (k < dur) { k += Time.deltaTime; t.localScale = Vector3.Lerp(s0, new Vector3(0f, s0.y * 1.4f, 0f), k / dur); yield return null; }
            t.gameObject.SetActive(false);
        }

        IEnumerator OpenCurtains()
        {
            Vector3 l0 = curtainL.position, r0 = curtainR.position;
            List<Vector3> s0 = new List<Vector3>();
            foreach (Transform c in sideCurtains) s0.Add(c.position);
            float t = 0f;
            while (t < 4f)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / 4f);
                curtainL.position = l0 + MapSpace.Dir(-1, 0) * 8.5f * k;
                curtainR.position = r0 + MapSpace.Dir(1, 0) * 8.5f * k;
                for (int i = 0; i < sideCurtains.Count; i++) sideCurtains[i].position = s0[i] + Vector3.up * 1.4f * k;
                yield return null;
            }
        }

        IEnumerator WarmLight()
        {
            Camera cam = main.Player.Cam;
            GameObject g = new GameObject("Sunrise");
            g.transform.SetParent(hall.transform, false);
            Light sun = g.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.72f, 0.42f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.LookRotation(MapSpace.Dir(0, -1) * Mathf.Cos(0.25f) + Vector3.down * Mathf.Sin(0.25f));
            Color amb0 = RenderSettings.ambientLight, bg0 = cam.backgroundColor;
            float t = 0f;
            while (t < 5f)
            {
                t += Time.deltaTime;
                float k = t / 5f;
                sun.intensity = Mathf.Lerp(0f, 1.3f, k);
                RenderSettings.ambientLight = Color.Lerp(amb0, new Color(0.38f, 0.3f, 0.24f), k);
                cam.backgroundColor = Color.Lerp(bg0, new Color(0.95f, 0.66f, 0.38f), k);
                RenderSettings.fogColor = Color.Lerp(Color.black, new Color(0.6f, 0.45f, 0.3f), k);
                yield return null;
            }
        }

        IEnumerator StudentsLeave(bool happy)
        {
            Vector3 exit = P(82f, 12.9f);
            for (int i = 0; i < students.Count; i++)
            {
                StartCoroutine(WalkTo(students[i], exit, happy ? Random.Range(2.6f, 3.6f) : Random.Range(0.9f, 1.4f), happy, Random.Range(0f, happy ? 3.5f : 5f)));
            }
            yield break;
        }

        IEnumerator WalkTo(Transform t, Vector3 exit, float speed, bool happy, float delay = 0f)
        {
            yield return new WaitForSeconds(delay);
            // 일어서기
            Vector3 up = t.position + Vector3.up * 0.25f;
            float k = 0f;
            Vector3 p0 = t.position;
            while (k < 0.6f) { k += Time.deltaTime; t.position = Vector3.Lerp(p0, up, k / 0.6f) + (happy ? Vector3.zero : Random.insideUnitSphere * 0.03f); yield return null; }
            // 가운데 통로로 → 문으로
            Vector2 g = MapSpace.ToGL(t.position);
            float aisleX = g.x < 79.2f ? 78.25f : 82.1f;
            Vector3 aisle = MapSpace.GF(F, aisleX, 0.25f, g.y);
            Vector3[] path = { aisle, exit + Vector3.up * 0.25f };
            foreach (Vector3 target in path)
            {
                while ((t.position - target).sqrMagnitude > 0.05f)
                {
                    Vector3 d = target - t.position; d.y = 0f;
                    if (d.sqrMagnitude > 0.001f) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(d), Time.deltaTime * 6f);
                    t.position = Vector3.MoveTowards(t.position, target, Time.deltaTime * speed) + (happy ? Vector3.zero : new Vector3(0, Mathf.Sin(Time.time * 9f + delay) * 0.004f, 0));
                    yield return null;
                }
            }
            if (happy) yield return Vanish(t, 0.5f);
            else t.gameObject.SetActive(false);
        }
    }
}
