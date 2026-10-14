using System.Collections.Generic;
using UnityEngine;

namespace BlackShade
{
    // 교장실의 진실을 알고 나면 5층이 변한다:
    // 온통 붉은빛, 복도 벽과 창문에 오래된 학생 사진이 빽빽하게 붙어 있다.
    public static class FifthFloor
    {
        static GameObject root;
        public static bool Applied { get { return root != null; } }

        public static void Apply()
        {
            if (root != null) return;
            root = new GameObject("FifthFloor_Transformed");
            const int f = 5;

            // 붉은 조명: 복도·홀 천장 형광등 모양의 관이 붉게 빛나고, 그 아래에서 붉은 빛이 나옴
            RedFixtures(f);

            // 사진들
            Texture2D[] tex = new Texture2D[6];
            for (int i = 0; i < tex.Length; i++) tex[i] = PhotoTexture(i);
            Material[] mats = new Material[tex.Length];
            Shader sh = Shader.Find("Standard");
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = new Material(sh);
                mats[i].mainTexture = tex[i];
                mats[i].SetFloat("_Glossiness", 0.1f);
                mats[i].enableInstancing = true;
            }
            System.Random r = new System.Random(1985);
            GameObject photos = new GameObject("Photos");
            photos.transform.SetParent(root.transform, false);
            // 북쪽 벽(창문 포함) 안쪽 면 — 계단 자리(중앙계단 입구, 계단B 구멍 위)는 비움
            for (float x = 0.4f; x < MapSpace.E - 0.4f; x += 0.42f)
            {
                if (x > MapSpace.SN0 - 0.15f && x < MapSpace.SN1 + 0.15f) continue;
                if (x > MapSpace.SB0 - 0.3f) continue;
                for (int row = 0; row < 5; row++)
                {
                    if (r.NextDouble() < 0.12) continue;
                    float h = 0.85f + row * 0.36f + (float)(r.NextDouble() * 0.08);
                    Photo(photos.transform, MapSpace.GF(f, x + (float)(r.NextDouble() * 0.1), h, MapSpace.NORTH_T + 0.012f), MapSpace.Dir(0, 1), mats[r.Next(mats.Length)], r);
                }
            }
            // 남쪽 벽(교실 쪽) 복도 면 — 문 자리는 비움
            List<float> doors = new List<float>();
            foreach (string id in new[] { "5F_computer", "5F_art", "5F_music" })
            {
                RoomSpec rs = MapData.Get(id);
                if (rs != null) foreach (DoorSpec ds in rs.doors) doors.Add(ds.center);
            }
            for (float x = 0.4f; x < MapSpace.L - 0.2f; x += 0.42f)
            {
                bool nearDoor = false;
                foreach (float d in doors) if (Mathf.Abs(x - d) < 0.85f) nearDoor = true;
                if (nearDoor) continue;
                for (int row = 0; row < 5; row++)
                {
                    if (r.NextDouble() < 0.15) continue;
                    float h = 0.8f + row * 0.36f + (float)(r.NextDouble() * 0.08);
                    Photo(photos.transform, MapSpace.GF(f, x, h, MapSpace.CORR_END - 0.012f), MapSpace.Dir(0, -1), mats[r.Next(mats.Length)], r);
                }
            }
            StaticBatchingUtility.Combine(photos);

            // 사진 벽 조사 (문서)
            GameObject spot = new GameObject("PhotoWallClue");
            spot.transform.SetParent(root.transform, false);
            WorldPickup.Create("doc_photos", null, MapSpace.GF(f, 26.8f, 1.4f, MapSpace.NORTH_T + 0.05f), "벽의 사진들 살펴보기", null, "note_photos", true, false);
        }

        static Material redTube;

        static void RedFixtures(int f)
        {
            if (redTube == null)
            {
                redTube = new Material(Shader.Find("Standard"));
                redTube.color = new Color(0.5f, 0.05f, 0.04f);
                redTube.EnableKeyword("_EMISSION");
                redTube.SetColor("_EmissionColor", new Color(1f, 0.08f, 0.05f) * 2.2f);
                redTube.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            List<Transform> fixtures = CeilingFixtures.HallFixtures(f);
            if (fixtures.Count == 0)
            {
                // 형광등 모양이 없을 때 (씬을 다시 안 만들었을 때): 빛만
                for (float x = 3f; x < MapSpace.SB0 - 0.8f; x += 15f) RedLight(MapSpace.GF(f, x, 2.6f, 2.4f), 8f, 1.1f);
                RedLight(MapSpace.GF(f, 85.62f, 2.6f, 8.6f), 7f, 1f);
                RedLight(MapSpace.GF(f, 82f, 2.4f, 11.2f), 4f, 1.4f);
                return;
            }
            foreach (Transform fx in fixtures)
            {
                foreach (Renderer r in fx.GetComponentsInChildren<Renderer>(true))
                    if (CeilingFixtures.IsTubeObject(r))
                    {
                        Material[] m = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                        for (int i = 0; i < m.Length; i++) m[i] = redTube;
                        r.sharedMaterials = m;
                        r.enabled = true;
                    }
                Vector2 g = MapSpace.ToGL(fx.position);
                Vector3 below = MapSpace.GF(f, g.x, RoomLights.LampH, g.y);
                // 붉은 형광등마다 바로 아래에서 붉은 빛
                if (g.y > 11f) RedLight(below, 4f, 1.4f);                            // 강당 문 앞
                else RedLight(below, 5.5f, 1.05f);
            }
        }

        static void RedLight(Vector3 p, float range, float intensity)
        {
            GameObject g = new GameObject("RedLight");
            g.transform.SetParent(root.transform, false);
            g.transform.position = p;
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.12f, 0.08f);
            l.range = range;
            l.intensity = intensity;
            // 벽 너머 교실로 새지 않게 그림자를 켬
            RoomLights.MakeRoomBound(l);
            g.AddComponent<FlickerLight>();
        }

        static void Photo(Transform parent, Vector3 pos, Vector3 normal, Material m, System.Random r)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.position = pos;
            q.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up) * Quaternion.Euler(0, 0, (float)(r.NextDouble() * 12 - 6));
            q.transform.localScale = new Vector3(0.3f, 0.38f, 1f);
            Renderer rd = q.GetComponent<Renderer>();
            rd.sharedMaterial = m;
            rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 빛바랜 증명사진 느낌의 텍스처 (머리와 어깨 실루엣)
        static Texture2D PhotoTexture(int seed)
        {
            int w = 48, h = 60;
            Texture2D t = new Texture2D(w, h, TextureFormat.RGB24, false);
            System.Random r = new System.Random(seed * 77 + 5);
            float tone = 0.62f + (float)r.NextDouble() * 0.15f;
            float headY = 0.62f + (float)r.NextDouble() * 0.05f, headR = 0.17f + (float)r.NextDouble() * 0.03f;
            bool scratched = seed % 3 == 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    float bg = tone + (float)(r.NextDouble() * 0.05);
                    float c = bg;
                    float dh = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) / 0.8f + (v - headY) * (v - headY));
                    if (dh < headR) c = bg * 0.55f;                                   // 얼굴
                    if (dh < headR && v > headY + headR * 0.2f) c = bg * 0.18f;        // 머리카락
                    if (v < 0.36f && Mathf.Abs(u - 0.5f) < 0.38f - v * 0.3f) c = bg * 0.2f; // 교복
                    if (scratched && dh < headR * 0.9f && ((x + y * 3) % 7 < 2)) c = 0.9f;   // 긁힌 얼굴
                    if (x < 2 || y < 2 || x > w - 3 || y > h - 3) c = 0.92f;          // 흰 테두리
                    t.SetPixel(x, y, new Color(c * 1.05f, c * 0.95f, c * 0.8f));
                }
            t.Apply();
            t.filterMode = FilterMode.Bilinear;
            return t;
        }
    }

    // 살짝 지직거리는 불빛
    public class FlickerLight : MonoBehaviour
    {
        Light l; float baseI, timer;
        void Awake() { l = GetComponent<Light>(); baseI = l.intensity; }
        void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = Random.Range(0.03f, 0.5f);
                l.intensity = Random.value > 0.12f ? baseI * Random.Range(0.8f, 1.1f) : baseI * 0.1f;
            }
        }
    }
}
