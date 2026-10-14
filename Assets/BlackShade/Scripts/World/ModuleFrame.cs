using System.Collections.Generic;
using UnityEngine;

namespace BlackShade
{
    // 교실 모듈(Class_1학년5반 등) 하나의 기준 좌표.
    //   u = 칠판(앞)에서 뒤쪽으로, v = 복도 쪽 벽에서 창문 쪽으로, h = 바닥에서 높이
    // 프롤로그의 ClassroomFrame과 같은 방식인데, 어느 교실에나 쓸 수 있게 만든 것.
    public class ModuleFrame
    {
        public Transform root;
        public Vector3 Origin, Forward, ToWindow;

        public Vector3 P(float u, float v, float h) { return Origin + Forward * u + ToWindow * v + Vector3.up * h; }
        public float Yaw(float du, float dv)
        {
            Vector3 d = Forward * du + ToWindow * dv; d.y = 0f;
            return d.sqrMagnitude < 0.0001f ? 0f : Quaternion.LookRotation(d).eulerAngles.y;
        }
        public float U(Vector3 w) { return Vector3.Dot(w - Origin, Forward); }
        public float V(Vector3 w) { return Vector3.Dot(w - Origin, ToWindow); }

        public static ModuleFrame Compute(Transform cls)
        {
            if (cls == null) return null;
            Transform floor = ClassroomFrame.FindChild(cls, "floor");
            Transform stage = ClassroomFrame.FindChild(cls, "stage");
            Transform doors = ClassroomFrame.FindChild(cls, "board");
            Bounds fb, sb, db;
            if (floor == null || stage == null || doors == null) return null;
            if (!ClassroomFrame.TryBounds(floor, out fb) || !ClassroomFrame.TryBounds(stage, out sb) || !ClassroomFrame.TryBounds(doors, out db)) return null;

            Vector3 c = fb.center;
            Vector3 toStage = sb.center - c;
            Vector3 toDoors = db.center - c;
            ModuleFrame m = new ModuleFrame { root = cls };
            float halfLen, halfWid;
            if (Mathf.Abs(toStage.x) > Mathf.Abs(toStage.z))
            {
                m.Forward = new Vector3(-Mathf.Sign(toStage.x), 0f, 0f);
                m.ToWindow = new Vector3(0f, 0f, -Mathf.Sign(toDoors.z));
                halfLen = fb.extents.x; halfWid = fb.extents.z;
            }
            else
            {
                m.Forward = new Vector3(0f, 0f, -Mathf.Sign(toStage.z));
                m.ToWindow = new Vector3(-Mathf.Sign(toDoors.x), 0f, 0f);
                halfLen = fb.extents.z; halfWid = fb.extents.x;
            }
            Vector3 o = c - m.Forward * halfLen - m.ToWindow * halfWid;
            o.y = fb.max.y;
            m.Origin = o;
            return m;
        }

        // ---------------------------------------------------------------- 미닫이문 찾기
        public class SlidingDoorParts
        {
            public Transform pivot;      // 움직이는 문짝 (+유리, 창틀)
            public Vector3 slide;        // 열릴 때 이동량
            public Vector3 center;       // 열린 자리 가운데 (높이 1.1m)
            public float width;
            public bool front;
        }

        // 교실 복도 쪽의 앞문/뒷문을 찾아서, 움직일 수 있게 묶어 준다.
        public List<SlidingDoorParts> PrepareSlidingDoors()
        {
            List<SlidingDoorParts> result = new List<SlidingDoorParts>();
            Transform board = ClassroomFrame.FindChild(root, "board");
            if (board == null) return result;
            Transform group = board.parent; // window+door+board

            List<Transform> panels = new List<Transform>();
            for (int i = 0; i < board.childCount; i++) panels.Add(board.GetChild(i));
            if (panels.Count < 4) return result;

            Dictionary<Transform, Bounds> pb = new Dictionary<Transform, Bounds>();
            foreach (Transform p in panels) { Bounds b; if (ClassroomFrame.TryBounds(p, out b)) pb[p] = b; }
            panels.RemoveAll(p => !pb.ContainsKey(p));
            panels.Sort((a, b) => U(pb[a].center).CompareTo(U(pb[b].center)));

            // 움직일 수 있는 후보 (창틀, 유리)
            List<Transform> extras = new List<Transform>();
            foreach (Renderer r in group.GetComponentsInChildren<Renderer>(true))
                if (!r.transform.IsChildOf(board)) extras.Add(r.transform);

            for (int k = 0; k + 1 < panels.Count && k < 4; k += 2)
            {
                Transform a = panels[k], b = panels[k + 1];
                // 교실 안쪽 레일에 있는 문짝(복도에서 더 먼 것)이 열린다
                Transform inner = V(pb[a].center) > V(pb[b].center) ? a : b;
                Transform outer = inner == a ? b : a;
                Bounds ib = pb[inner];
                float uMin, uMax, vMin, vMax;
                Range(ib, out uMin, out uMax, out vMin, out vMax);
                float width = uMax - uMin;
                float dir = Mathf.Sign(U(pb[outer].center) - U(ib.center));

                GameObject pivot = new GameObject(k == 0 ? "SlidingDoor_Front" : "SlidingDoor_Back");
                pivot.transform.SetParent(root, true);
                pivot.transform.position = ib.center;
                inner.SetParent(pivot.transform, true);
                foreach (Transform x in extras)
                {
                    if (x == null || x.IsChildOf(pivot.transform)) continue;
                    Bounds xb;
                    if (!ClassroomFrame.TryBounds(x, out xb)) continue;
                    float a0, a1, b0, b1;
                    Range(xb, out a0, out a1, out b0, out b1);
                    bool inside = a0 >= uMin - 0.03f && a1 <= uMax + 0.03f && b0 >= vMin - 0.04f && b1 <= vMax + 0.04f
                                  && xb.min.y >= ib.min.y + 0.05f && xb.max.y <= ib.max.y + 0.05f;
                    if (inside) x.SetParent(pivot.transform, true);
                }

                Vector3 cen = ib.center; cen.y = Origin.y + 1.1f;
                result.Add(new SlidingDoorParts
                {
                    pivot = pivot.transform,
                    slide = Forward * dir * (width - 0.03f),
                    center = cen,
                    width = width,
                    front = k == 0,
                });
            }
            return result;
        }

        void Range(Bounds b, out float u0, out float u1, out float v0, out float v1)
        {
            float ua = U(b.min), ub = U(b.max), va = V(b.min), vb = V(b.max);
            u0 = Mathf.Min(ua, ub); u1 = Mathf.Max(ua, ub);
            v0 = Mathf.Min(va, vb); v1 = Mathf.Max(va, vb);
        }
    }
}
