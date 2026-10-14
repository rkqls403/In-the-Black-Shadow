using UnityEngine;

namespace BlackShade
{
    // 학교와 운동장을 둘러싸는 담장 + 정문(철문).
    // 정문은 현관과 일직선(남쪽)에 놓여요. 담장은 Wall_Panel(담 + 철책)과 Wall_Pillar(기둥)를 이어 붙여 만들어요.
    // 모델(Resources/Props/Wall_Panel 등)이 없으면 아무것도 만들지 않아요.
    public static class Perimeter
    {
        // 담장 사각형 (맵 좌표) — 건물(x 0~88.02, z -4.5~43.62) 바깥, 남쪽은 운동장
        public const float X0 = -12f, X1 = MapSpace.E + 12f, Z0 = -16f, Z1 = 78f;
        const float GroundY = -0.55f;     // 맵 지면 높이
        const float Scale = 0.72f;        // 원본(기둥 3.5m, 문 4.9m)이 커서 학교 담 정도로 줄임
        const float MaxSpan = 6.0f;       // 기둥 사이 최대 간격

        public static void Build(Transform root)
        {
            if (!PropModels.Has("Wall_Panel") || !PropModels.Has("Wall_Pillar")) return;
            GameObject g = new GameObject("Perimeter");
            if (root != null) g.transform.SetParent(root, false);
            Transform tr = g.transform;

            float gateX = (MapSpace.XS[2] + MapSpace.XS[3]) * 0.5f;     // 현관 한가운데
            float gateW = GateWidth();

            // 남쪽(정문 쪽)은 문을 사이에 두고 두 토막
            Side(tr, new Vector2(X0, Z1), new Vector2(gateX - gateW * 0.5f, Z1));
            Side(tr, new Vector2(gateX + gateW * 0.5f, Z1), new Vector2(X1, Z1));
            Side(tr, new Vector2(X1, Z1), new Vector2(X1, Z0));
            Side(tr, new Vector2(X1, Z0), new Vector2(X0, Z0));
            Side(tr, new Vector2(X0, Z0), new Vector2(X0, Z1));

            // 정문 (닫혀 있음)
            if (PropModels.Has("Gate"))
            {
                Vector3 feet = MapSpace.G(gateX, GroundY, Z1);
                PropModels.Place("Gate", tr, feet, MapSpace.Dir(0, 1), new Vector3(gateW, 0f, 0f), PropModels.Fit.Inside, "MainGate");
            }
        }

        static float GateWidth()
        {
            GameObject p = PropModels.Prefab("Gate");
            if (p == null) return 6.13f * Scale;
            GameObject tmp = Object.Instantiate(p);
            Bounds b;
            float w = PropModels.WorldBounds(tmp, out b) ? Mathf.Max(b.size.x, b.size.z) : 6.13f;
            float h = b.size.y;
            Object.DestroyImmediate(tmp);
            // 원래 비율대로 Scale 만큼 줄인 폭
            float targetH = 4.9f * Scale;
            return h > 0.01f ? w * targetH / h : 6.13f * Scale;
        }

        // 모델을 높이 h 로 (비율 유지) 줄였을 때의 폭
        static float NaturalWidth(string key, float h, float fallback)
        {
            GameObject p = PropModels.Prefab(key);
            if (p == null) return fallback;
            GameObject tmp = Object.Instantiate(p);
            Bounds b;
            float w = fallback;
            if (PropModels.WorldBounds(tmp, out b) && b.size.y > 0.01f) w = b.size.x * h / b.size.y;
            Object.DestroyImmediate(tmp);
            return w;
        }

        // a → b 로 담 한 줄: 양 끝과 사이사이에 기둥, 기둥 사이에 담판
        // 담판은 비율을 지킨 채 높이만 맞추고, 담판 수를 정해 기둥 사이에 꼭 맞게 늘어놓아요.
        static void Side(Transform tr, Vector2 a, Vector2 b)
        {
            float len = Vector2.Distance(a, b);
            if (len < 0.5f) return;
            float pillarW = 0.94f * Scale, pillarH = 3.47f * Scale, panelH = 2.98f * Scale;
            float panelW = NaturalWidth("Wall_Panel", panelH, MaxSpan);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / (panelW + pillarW) - 0.05f));
            Vector3 A = MapSpace.G(a.x, GroundY, a.y), B = MapSpace.G(b.x, GroundY, b.y);
            Vector3 dir = (B - A); dir.y = 0f; dir.Normalize();
            Vector3 face = Vector3.Cross(dir, Vector3.up);   // 담판의 +X 가 dir 를 따라가도록
            for (int i = 0; i <= n; i++)
            {
                Vector3 p = Vector3.Lerp(A, B, (float)i / n);
                PropModels.Place("Wall_Pillar", tr, p, face, new Vector3(0f, pillarH, 0f), PropModels.Fit.Height, "Pillar");
                if (i == n) break;
                Vector3 q = Vector3.Lerp(A, B, (i + 0.5f) / n);
                PropModels.Place("Wall_Panel", tr, q, face, new Vector3(0f, panelH, 0f), PropModels.Fit.Height, "Panel");
            }
        }
    }
}
