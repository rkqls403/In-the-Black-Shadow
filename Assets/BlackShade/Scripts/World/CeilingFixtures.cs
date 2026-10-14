using System.Collections.Generic;
using UnityEngine;

namespace BlackShade
{
    // 교실 천장의 형광등 모양(맵 안 3학년 1반 교실의 'light' 묶음)을 하나 떠서
    // 복도·로비·일반 방 천장에 같은 모양으로 달아 준다.
    //   - 복도/로비: 모양만 (불은 꺼져 있는 형광등)
    //   - 일반 방: 방 스위치를 켜면 형광등 관이 빛남 (RoomLights 가 관리)
    public static class CeilingFixtures
    {
        static GameObject template;
        static readonly List<string> tubeNames = new List<string>();   // 템플릿 안에서 빛나는 관 오브젝트 이름
        public static bool Ready { get { return template != null; } }
        const float CeilingH = 3.0f;    // 층 바닥에서 천장 아랫면까지

        // ---------------------------------------------------------------- 템플릿 뜨기
        public static void Init()
        {
            if (template != null) return;
            tubeNames.Clear();
            Transform cls = ClassroomFrame.FindByName("Class_3학년1반");
            if (cls == null) { Debug.LogWarning("[CeilingFixtures] 교실 모듈을 못 찾아 천장 조명을 만들지 않아요."); return; }
            Transform light = FindDeep(cls, "light");
            if (light == null) { Debug.LogWarning("[CeilingFixtures] 교실의 light 묶음을 못 찾았어요."); return; }

            List<Renderer> all = new List<Renderer>(light.GetComponentsInChildren<Renderer>(true));
            // 기준: 가장 길쭉한 흰 등갓 하나
            Renderer seed = null; float best = 0f;
            foreach (Renderer r in all)
            {
                if (IsTube(r)) continue;
                Vector3 s = r.bounds.size;
                float len = Mathf.Max(s.x, s.z);
                if (len > best && len < 2f && s.y < 0.3f) { best = len; seed = r; }
            }
            if (seed == null) return;
            Bounds sb = seed.bounds; sb.Expand(new Vector3(0.12f, 0.12f, 0.12f));
            List<Renderer> parts = new List<Renderer>();
            foreach (Renderer r in all) if (sb.Contains(r.bounds.center)) parts.Add(r);

            bool alongX = seed.bounds.size.x >= seed.bounds.size.z;
            template = new GameObject("CeilingFixture_Template");
            template.transform.position = new Vector3(seed.bounds.center.x, seed.bounds.max.y, seed.bounds.center.z);
            template.transform.rotation = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);

            // 같은 재질끼리 메시 하나로 합치기 (가능하면) — 안 되면 하나씩 복사
            bool readable = true;
            foreach (Renderer r in parts)
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) { readable = false; break; }
            }
            Matrix4x4 toLocal = template.transform.worldToLocalMatrix;
            if (readable)
            {
                Dictionary<Material, List<CombineInstance>> groups = new Dictionary<Material, List<CombineInstance>>();
                Dictionary<Material, bool> tubeMat = new Dictionary<Material, bool>();
                foreach (Renderer r in parts)
                {
                    Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    Material[] ms = r.sharedMaterials;
                    for (int s = 0; s < mesh.subMeshCount && s < ms.Length; s++)
                    {
                        List<CombineInstance> l;
                        if (!groups.TryGetValue(ms[s], out l)) { l = new List<CombineInstance>(); groups[ms[s]] = l; }
                        l.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = toLocal * r.transform.localToWorldMatrix });
                        if (IsTube(r)) tubeMat[ms[s]] = true;
                    }
                }
                int i = 0;
                foreach (var kv in groups)
                {
                    Mesh m = new Mesh();
                    m.name = "Fixture_" + i;
                    m.CombineMeshes(kv.Value.ToArray(), true, true);
                    GameObject g = new GameObject(tubeMat.ContainsKey(kv.Key) ? "Tube_" + i : "Body_" + i);
                    g.transform.SetParent(template.transform, false);
                    g.AddComponent<MeshFilter>().sharedMesh = m;
                    MeshRenderer mr = g.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = kv.Key;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (tubeMat.ContainsKey(kv.Key)) tubeNames.Add(g.name);
                    i++;
                }
            }
            else
            {
                int i = 0;
                foreach (Renderer r in parts)
                {
                    MeshFilter src = r.GetComponent<MeshFilter>();
                    if (src == null) continue;
                    GameObject g = new GameObject((IsTube(r) ? "Tube_" : "Body_") + i++);
                    g.transform.SetParent(template.transform, false);
                    g.transform.position = r.transform.position;
                    g.transform.rotation = r.transform.rotation;
                    g.transform.localScale = Divide(r.transform.lossyScale, template.transform.lossyScale);
                    g.AddComponent<MeshFilter>().sharedMesh = src.sharedMesh;
                    MeshRenderer mr = g.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = r.sharedMaterials;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (IsTube(r)) tubeNames.Add(g.name);
                }
            }
            template.SetActive(false);
        }

        static bool IsTube(Renderer r) { return ClassroomFrame.PathOf(r.transform).Contains("/emissive/"); }

        // ---------------------------------------------------------------- 달기
        // f층, 맵 좌표 (gx, gz), alongX: 긴 쪽이 맵 x 방향이면 true. tubes 에 빛나는 관 렌더러를 담아 줌.
        public static GameObject Place(int f, float gx, float gz, bool alongX, Transform parent, List<Renderer> tubes = null, bool lit = false)
        {
            if (template == null) return null;
            GameObject g = Object.Instantiate(template);
            g.name = "CeilingFixture_" + f + "F";
            g.SetActive(true);
            g.transform.position = MapSpace.GF(f, gx, CeilingH, gz);
            g.transform.rotation = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            if (parent != null) g.transform.SetParent(parent, true);
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            {
                if (!tubeNames.Contains(r.gameObject.name)) continue;
                if (tubes != null) tubes.Add(r);
                else if (!lit) RoomLights.SetTubeOff(r);   // 복도 형광등은 꺼진 관
            }
            return g;
        }

        // 일반 방(교실 모듈이 아닌 방) 천장: Decor/RoomFixtures/<방 id> 아래에 둠
        // 관은 여기서 끄지 않아요 — 게임이 시작되면 RoomLights 가 찾아서 스위치로 켜고 끔.
        public static void BuildRooms(Transform root)
        {
            if (template == null) return;
            GameObject all = new GameObject("RoomFixtures");
            if (root != null) all.transform.SetParent(root, false);
            foreach (RoomSpec room in MapData.Rooms)
            {
                if (room.IsModule || !room.lights) continue;
                GameObject grp = new GameObject(room.id);
                grp.transform.SetParent(all.transform, false);
                bool alongX = (room.x1 - room.x0) >= (room.z1 - room.z0);
                foreach (Vector2 s in RoomLights.SpotsGL(room))
                    Place(room.floor, s.x, s.y, alongX, grp.transform, new List<Renderer>());
            }
        }

        // 방 하나의 형광등 묶음 (없으면 null)
        public static Transform RoomGroup(string roomId)
        {
            Transform d = Decor.Find();
            if (d == null) return null;
            Transform all = d.Find("RoomFixtures");
            return all != null ? all.Find(roomId) : null;
        }

        public static bool IsTubeObject(Renderer r) { return r != null && r.gameObject.name.StartsWith("Tube_"); }

        // f층 복도·홀에 달린 형광등들 (Decor/CorridorFixtures 아래)
        public static List<Transform> HallFixtures(int f)
        {
            List<Transform> list = new List<Transform>();
            Transform d = Decor.Find();
            Transform all = d != null ? d.Find("CorridorFixtures") : null;
            if (all == null) return list;
            string n = "CeilingFixture_" + f + "F";
            foreach (Transform c in all) if (c.name == n) list.Add(c);
            return list;
        }

        // 다 달고 나면 본(템플릿)은 지움
        public static void DisposeTemplate()
        {
            if (template != null) Prop.Kill(template);
            template = null;
        }

        // 복도·홀·로비 (모양만)
        public static void BuildHalls(Transform root)
        {
            if (template == null) return;
            GameObject g = new GameObject("CorridorFixtures");
            if (root != null) g.transform.SetParent(root, false);
            Transform t = g.transform;
            float E = MapSpace.E, SB0 = MapSpace.SB0;
            for (int f = 1; f <= 5; f++)
            {
                // 북쪽 복도
                for (float x = 3f; x < E - 1f; x += 7.5f)   // 5층 붉은 조명과 같은 자리
                {
                    if (x > SB0 - 0.8f) continue;                                                  // 계단B 구멍
                    if (f == 1 && x > MapSpace.XS[2] - 0.6f && x < MapSpace.XS[3] + 0.6f) continue; // 현관 모델에 이미 조명이 있음
                    Place(f, x, 2.4f, true, t);
                }
                // 북동 홀
                Place(f, MapSpace.VC0 + 2.4f, 8.6f, false, t);
                Place(f, E - 2.4f, 8.6f, false, t);
                if (f == 5) Place(f, 82f, 11.2f, true, t);    // 강당 문 앞 (5층이 변하면 붉은 불이 켜짐)
                // 세로 복도 (1~4층)
                if (f <= 4)
                    for (float z = MapSpace.WING_END + 3.5f; z < MapSpace.BOT - 1f; z += 7.5f)
                        Place(f, (MapSpace.VC0 + MapSpace.VC1) * 0.5f, z, false, t);
                // 2~4층 로비
                if (f >= 2 && f <= 4)
                {
                    float c = (MapSpace.XS[2] + MapSpace.XS[3]) * 0.5f;
                    foreach (float dx in new[] { -9f, -3f, 3f, 9f })
                        foreach (float z in new[] { 6.6f, 10.2f })
                            Place(f, c + dx, z, true, t);
                }
            }
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                Transform f = FindDeep(c, name);
                if (f != null) return f;
            }
            return null;
        }

        static Vector3 Divide(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x / Mathf.Max(1e-6f, b.x), a.y / Mathf.Max(1e-6f, b.y), a.z / Mathf.Max(1e-6f, b.z));
        }
    }
}
