using System.Collections.Generic;
using UnityEngine;

namespace BlackShade
{
    // 맵(School_5F_Map) 안에서 '3학년 1반' 교실을 찾아 교실 기준 좌표계를 만든다.
    //
    // 교실 기준 좌표 (단위 m):
    //   u = 칠판(앞) 벽에서 뒤쪽으로 얼마나 떨어졌는지   (0 ~ 10.16)
    //   v = 복도 쪽 벽에서 창문 쪽으로 얼마나 떨어졌는지 (0 ~ 8.3)
    //   h = 교실 바닥에서 높이
    // 모든 소품/캐릭터 위치는 P(u, v, h)로 정해서, 맵을 옮기거나 다른 반으로 바꿔도 그대로 따라가요.
    public class ClassroomFrame : MonoBehaviour
    {
        // 원본 교실 모델 기준 수치 (build_school.py / 교실 모델에서 측정)
        public static class Layout
        {
            public const float Length = 10.16f;   // u 방향 (앞 → 뒤)
            public const float Width = 8.3f;      // v 방향 (복도 → 창문)
            public const float Height = 3.0f;

            // 책상: 5줄(u) x 4분단(v). 값은 '책상+의자' 한 세트의 가운데.
            public static readonly float[] DeskRowU = { 3.35f, 4.47f, 5.56f, 6.68f, 7.76f };
            public static readonly float[] DeskColV = { 2.31f, 3.60f, 4.72f, 6.01f };
            public const float DeskTopH = 0.77f;

            // 칠판(초록 판) 앞면
            public const float BoardU = 0.20f;
            public const float BoardVMin = 2.17f, BoardVMax = 6.14f;
            public const float BoardHMin = 0.92f, BoardHMax = 2.08f;
            public const float StageEndU = 1.07f;    // 교단 끝
            public const float StageH = 0.2f;

            // 복도 쪽 문 (앞문 / 뒷문)
            public const float FrontDoorU = 1.30f, BackDoorU = 9.03f;
            public const float WindowV = 8.15f;      // 창문 벽
            public const float BackWallU = 10.16f;

            public static readonly float[] LightU = { 3.21f, 5.56f, 7.92f };
            public static readonly float[] LightV = { 1.62f, 4.15f, 6.69f };
            public const float LightH = 2.88f;
        }

        public string classroomNodeName = "Class_3학년1반";

        public Vector3 Origin { get; private set; }     // (u=0, v=0) 바닥 모서리
        public Vector3 Forward { get; private set; }    // 앞(칠판) → 뒤
        public Vector3 ToWindow { get; private set; }   // 복도 → 창문
        public Transform Classroom { get; private set; }
        public bool IsFallback { get; private set; }
        public readonly List<Renderer> lightTubes = new List<Renderer>();
        GameObject invisibleBounds;

        // 본편에서는 교실 밖으로 나갈 수 있어야 하니 보이지 않는 벽을 치운다
        public void RemoveInvisibleBounds()
        {
            if (invisibleBounds != null) Destroy(invisibleBounds);
        }

        public Vector3 P(float u, float v, float h)
        {
            return Origin + Forward * u + ToWindow * v + Vector3.up * h;
        }

        public float YawOf(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return 0f;
            return Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles.y;
        }

        // 교실 기준 방향 → 월드 yaw (예: 칠판 쪽 보기 = Yaw(-1, 0))
        public float Yaw(float du, float dv)
        {
            return YawOf(Forward * du + ToWindow * dv);
        }

        public bool Build()
        {
            Classroom = FindByName(classroomNodeName);
            if (Classroom == null)
            {
                Debug.LogWarning("[ClassroomFrame] 맵에서 '" + classroomNodeName + "' 를 못 찾아서 임시 회색 교실을 만들어요. " +
                                 "Tools > Black Shade > 프롤로그 씬 만들기 를 먼저 실행했는지 확인하세요.");
                BuildFallback();
                return false;
            }

            Transform floor = FindChild(Classroom, "floor");
            Transform stage = FindChild(Classroom, "stage");
            Transform doors = FindChild(Classroom, "board");
            Bounds fb, sb, db;
            if (floor == null || stage == null || doors == null || !TryBounds(floor, out fb) || !TryBounds(stage, out sb) || !TryBounds(doors, out db))
            {
                Debug.LogWarning("[ClassroomFrame] 교실 안의 floor / stage / board 오브젝트를 못 찾았어요. 임시 교실로 대체합니다.");
                BuildFallback();
                return false;
            }

            Vector3 c = fb.center;
            Vector3 toStage = sb.center - c;
            Vector3 toDoors = db.center - c;
            bool lengthIsX = Mathf.Abs(toStage.x) > Mathf.Abs(toStage.z);
            Vector3 fwd, win;
            float halfLen, halfWid;
            if (lengthIsX)
            {
                fwd = new Vector3(-Mathf.Sign(toStage.x), 0f, 0f);
                win = new Vector3(0f, 0f, -Mathf.Sign(toDoors.z));
                halfLen = fb.extents.x; halfWid = fb.extents.z;
            }
            else
            {
                fwd = new Vector3(0f, 0f, -Mathf.Sign(toStage.z));
                win = new Vector3(-Mathf.Sign(toDoors.x), 0f, 0f);
                halfLen = fb.extents.z; halfWid = fb.extents.x;
            }
            Forward = fwd;
            ToWindow = win;
            Vector3 o = c - fwd * halfLen - win * halfWid;
            o.y = fb.max.y;
            Origin = o;

            // 천장 형광등(빛나는 부분)
            foreach (Renderer r in Classroom.GetComponentsInChildren<Renderer>(true))
            {
                string path = PathOf(r.transform);
                if (path.Contains("/light/emissive/")) lightTubes.Add(r);
            }

            AddRuntimeColliders();
            Debug.Log("[ClassroomFrame] 3학년 1반 찾음. 원점 " + Origin + ", 앞→뒤 " + Forward + ", 복도→창문 " + ToWindow);
            return true;
        }

        void AddRuntimeColliders()
        {
            // 소품(책상, 사물함, 교탁 등)에 상자 충돌체. 이미 있으면 건드리지 않음.
            foreach (MeshFilter mf in Classroom.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (mf.GetComponent<Collider>() != null) continue;
                string path = PathOf(mf.transform);
                if (path.Contains("/light/")) continue;
                if (path.Contains("/Item/") || path.Contains("/Items/") || path.Contains("/stage/"))
                    mf.gameObject.AddComponent<BoxCollider>();
            }

            // 바닥과 벽은 보이지 않는 상자로 확실하게 막는다 (맵 충돌체가 없어도 걸어다닐 수 있게)
            GameObject root = new GameObject("Classroom_InvisibleBounds");
            invisibleBounds = root;
            root.transform.SetParent(transform, false);
            float L = Layout.Length, W = Layout.Width, H = Layout.Height;
            MakeBox(root.transform, "Floor", P(L * 0.5f, W * 0.5f, -0.1f), new Vector3(W + 1f, 0.2f, L + 1f));
            MakeBox(root.transform, "Wall_Front", P(-0.15f, W * 0.5f, H * 0.5f), new Vector3(W + 1f, H, 0.3f));
            MakeBox(root.transform, "Wall_Back", P(L + 0.15f, W * 0.5f, H * 0.5f), new Vector3(W + 1f, H, 0.3f));
            MakeBox(root.transform, "Wall_Corridor", P(L * 0.5f, -0.15f, H * 0.5f), new Vector3(0.3f, H, L + 1f));
            MakeBox(root.transform, "Wall_Window", P(L * 0.5f, W + 0.15f, H * 0.5f), new Vector3(0.3f, H, L + 1f));
        }

        // size는 (창문방향 폭, 높이, 앞뒤 길이) 순서
        void MakeBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, true);
            g.transform.position = center;
            g.transform.rotation = Quaternion.LookRotation(Forward, Vector3.up);
            BoxCollider bc = g.AddComponent<BoxCollider>();
            bc.size = size;
        }

        // ---------------------------------------------------------------- 임시 회색 교실
        void BuildFallback()
        {
            IsFallback = true;
            Forward = Vector3.right;
            ToWindow = Vector3.forward;
            Origin = new Vector3(0f, 0f, 0f);
            GameObject root = new GameObject("FallbackClassroom");
            Classroom = root.transform;
            float L = Layout.Length, W = Layout.Width, H = Layout.Height;
            Color wall = new Color(0.78f, 0.78f, 0.76f);
            Prop.Box(root.transform, "Floor", P(L / 2, W / 2, -0.1f), Quaternion.LookRotation(Forward), new Vector3(W, 0.2f, L), new Color(0.55f, 0.45f, 0.35f), true);
            Prop.Box(root.transform, "Ceiling", P(L / 2, W / 2, H + 0.1f), Quaternion.LookRotation(Forward), new Vector3(W, 0.2f, L), new Color(0.9f, 0.9f, 0.88f), true);
            Prop.Box(root.transform, "Wall_Front", P(-0.1f, W / 2, H / 2), Quaternion.LookRotation(Forward), new Vector3(W, H, 0.2f), wall, true);
            Prop.Box(root.transform, "Wall_Back", P(L + 0.1f, W / 2, H / 2), Quaternion.LookRotation(Forward), new Vector3(W, H, 0.2f), wall, true);
            Prop.Box(root.transform, "Wall_Corridor", P(L / 2, -0.1f, H / 2), Quaternion.LookRotation(Forward), new Vector3(0.2f, H, L), wall, true);
            Prop.Box(root.transform, "Wall_Window_Low", P(L / 2, W + 0.1f, 0.45f), Quaternion.LookRotation(Forward), new Vector3(0.2f, 0.9f, L), wall, true);
            Prop.Box(root.transform, "Wall_Window_High", P(L / 2, W + 0.1f, 2.8f), Quaternion.LookRotation(Forward), new Vector3(0.2f, 0.4f, L), wall, true);
            Prop.Box(root.transform, "Chalkboard", P(0.05f, (Layout.BoardVMin + Layout.BoardVMax) / 2, (Layout.BoardHMin + Layout.BoardHMax) / 2), Quaternion.LookRotation(Forward),
                     new Vector3(Layout.BoardVMax - Layout.BoardVMin, Layout.BoardHMax - Layout.BoardHMin, 0.05f), new Color(0.12f, 0.25f, 0.18f), false);
            Prop.Box(root.transform, "Stage", P(0.6f, W / 2, 0.1f), Quaternion.LookRotation(Forward), new Vector3(4.4f, 0.2f, 0.9f), new Color(0.5f, 0.38f, 0.28f), true);
            foreach (float u in Layout.DeskRowU)
                foreach (float v in Layout.DeskColV)
                {
                    Prop.Box(root.transform, "Desk", P(u - 0.15f, v, Layout.DeskTopH - 0.02f), Quaternion.LookRotation(Forward), new Vector3(0.65f, 0.04f, 0.45f), new Color(0.6f, 0.45f, 0.3f), true);
                    Prop.Box(root.transform, "Chair", P(u + 0.3f, v, 0.45f), Quaternion.LookRotation(Forward), new Vector3(0.4f, 0.04f, 0.4f), new Color(0.3f, 0.3f, 0.32f), true);
                }
            AddRuntimeColliders();
        }

        // ---------------------------------------------------------------- 도우미
        public static Transform FindByName(string name)
        {
            Transform[] all = Object.FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }

        public static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform t = FindChild(root.GetChild(i), name);
                if (t != null) return t;
            }
            return null;
        }

        public static bool TryBounds(Transform t, out Bounds b)
        {
            b = new Bounds();
            bool has = false;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return has;
        }

        public static string PathOf(Transform t)
        {
            string p = "/" + t.name + "/";
            while (t.parent != null)
            {
                t = t.parent;
                p = "/" + t.name + p;
            }
            return p;
        }
    }
}
