using UnityEngine;

namespace BlackShade
{
    // 피아노 — 받은 모델(UprightPiano / GrandPiano)이 있으면 그 모델, 없으면 상자로 만든 임시 모양
    //   Upright : 음악실의 작은(업라이트) 피아노
    //   Grand   : 강당 무대의 그랜드 피아노 (뚜껑이 열려 있음)
    // facing = 건반이 향하는 쪽 (연주자가 앉는 쪽)
    public static class Pianos
    {
        static readonly Color Lacquer = new Color(0.035f, 0.035f, 0.04f);
        static readonly Color Ivory = new Color(0.9f, 0.88f, 0.82f);
        static readonly Color Ebony = new Color(0.02f, 0.02f, 0.02f);
        static readonly Color Brass = new Color(0.7f, 0.55f, 0.25f);

        // ---------------------------------------------------------------- 업라이트
        public static GameObject Upright(Transform parent, Vector3 feet, Vector3 facing)
        {
            Frame fr = new Frame(feet, facing);
            GameObject root = new GameObject("UprightPiano");
            if (parent != null) root.transform.SetParent(parent, false);
            Transform t = root.transform;

            // 받은 모델이 있으면 그걸로 (충돌용 투명 상자 + 의자만 따로)
            if (PropModels.Place("UprightPiano", t, fr.P(0f, 0f, 0.1f), facing, new Vector3(1.55f, 1.25f, 0f), PropModels.Fit.Inside, "PianoModel") != null)
            {
                GameObject c = Looks.Furniture(t, "Collision", fr.P(0f, 0f, 0.1f), fr.Yaw, new Vector3(1.5f, 1.25f, 0.65f), Lacquer);
                Renderer rr = c.GetComponent<Renderer>(); if (rr != null) rr.enabled = false;
                Looks.Furniture(t, "Bench", fr.P(0f, 0f, 0.95f), fr.Yaw, new Vector3(0.75f, 0.48f, 0.36f), Lacquer);
                return root;
            }

            // 몸통 (충돌 + 괴물 길찾기 장애물)
            Looks.Furniture(t, "Body", fr.P(0f, 0f, 0f), fr.Yaw, new Vector3(1.48f, 1.24f, 0.58f), Lacquer);
            Box(t, fr, "Top", 0f, 1.255f, 0f, new Vector3(1.52f, 0.03f, 0.62f), Lacquer);
            Box(t, fr, "Keybed", 0f, 0.71f, 0.42f, new Vector3(1.44f, 0.08f, 0.28f), Lacquer);
            Keys(t, fr, 0.765f, 0.43f);
            Box(t, fr, "CheekL", -0.68f, 0.79f, 0.42f, new Vector3(0.07f, 0.07f, 0.28f), Lacquer);
            Box(t, fr, "CheekR", 0.68f, 0.79f, 0.42f, new Vector3(0.07f, 0.07f, 0.28f), Lacquer);
            Box(t, fr, "MusicDesk", 0f, 0.98f, 0.31f, new Vector3(1.3f, 0.3f, 0.03f), Lacquer, -8f);
            Box(t, fr, "LegL", -0.66f, 0.335f, 0.5f, new Vector3(0.06f, 0.67f, 0.06f), Lacquer);
            Box(t, fr, "LegR", 0.66f, 0.335f, 0.5f, new Vector3(0.06f, 0.67f, 0.06f), Lacquer);
            Pedals(t, fr, 0.33f);
            Looks.Furniture(t, "Bench", fr.P(0f, 0f, 0.95f), fr.Yaw, new Vector3(0.75f, 0.48f, 0.36f), Lacquer);
            return root;
        }

        // ---------------------------------------------------------------- 그랜드
        public static GameObject Grand(Transform parent, Vector3 feet, Vector3 facing)
        {
            Frame fr = new Frame(feet, facing);
            GameObject root = new GameObject("GrandPiano");
            if (parent != null) root.transform.SetParent(parent, false);
            Transform t = root.transform;
            const float W = 1.5f, Len = 1.9f, caseBot = 0.62f, caseTop = 0.95f;

            // 받은 모델(의자 포함)이 있으면 그걸로
            // (모델 비율 그대로: 폭 약 1.85m, 길이 약 2.8m, 건반 높이 약 0.7m — 의자 포함)
            GameObject gm = PropModels.Place("GrandPiano", t, fr.P(0f, 0f, -0.55f), facing, new Vector3(1.85f, 0f, 2.9f), PropModels.Fit.Inside, "GrandPianoModel");
            if (gm != null)
            {
                GameObject c = Looks.Furniture(t, "Collision", fr.P(0f, 0f, -0.95f), fr.Yaw, new Vector3(1.75f, 1.0f, 1.95f), Lacquer);
                Renderer rr = c.GetComponent<Renderer>(); if (rr != null) rr.enabled = false;
                return root;
            }

            // 충돌용 투명 상자
            GameObject col = Looks.Furniture(t, "Collision", fr.P(0f, 0f, -Len * 0.5f), fr.Yaw, new Vector3(W, caseTop, Len), Lacquer);
            Renderer cr = col.GetComponent<Renderer>(); if (cr != null) cr.enabled = false;

            // 옆판: 왼쪽(저음)은 곧고, 오른쪽(고음)은 꼬리 쪽으로 둥글게 들어감
            const int N = 10;
            for (int i = 0; i < N; i++)
            {
                float a = (float)i / N, b = (float)(i + 1) / N, m = (a + b) * 0.5f;
                float xr = RightEdge(m, W);
                float x0 = -W * 0.5f;
                Box(t, fr, "Case" + i, (x0 + xr) * 0.5f, (caseBot + caseTop) * 0.5f, -Len * m, new Vector3(xr - x0, caseTop - caseBot, Len / N + 0.01f), Lacquer);
            }

            // 뚜껑: 곧은 쪽에 경첩, 고음 쪽을 들어 올림
            GameObject lid = new GameObject("Lid");
            lid.transform.SetParent(t, false);
            lid.transform.position = fr.P(-W * 0.5f, caseTop + 0.01f, 0f);
            lid.transform.rotation = fr.Rot * Quaternion.Euler(0f, 0f, 32f);
            for (int i = 0; i < N; i++)
            {
                float a = (float)i / N, b = (float)(i + 1) / N, m = (a + b) * 0.5f;
                float w = RightEdge(m, W) + W * 0.5f;
                Vector3 lp = new Vector3(w * 0.5f, 0.01f, -Len * m);
                Prop.Box(lid.transform, "LidPart" + i, lid.transform.TransformPoint(lp), lid.transform.rotation, new Vector3(w, 0.02f, Len / N + 0.01f), Lacquer, false);
            }
            // 뚜껑 받침대
            Box(t, fr, "LidStick", 0.5f, caseTop + 0.32f, -0.75f, new Vector3(0.025f, 0.66f, 0.025f), Lacquer, 0f, -14f);

            // 건반부
            Box(t, fr, "Keybed", 0f, 0.7f, 0.13f, new Vector3(1.44f, 0.1f, 0.27f), Lacquer);
            Keys(t, fr, 0.765f, 0.14f);
            Box(t, fr, "CheekL", -0.68f, 0.79f, 0.13f, new Vector3(0.07f, 0.07f, 0.27f), Lacquer);
            Box(t, fr, "CheekR", 0.68f, 0.79f, 0.13f, new Vector3(0.07f, 0.07f, 0.27f), Lacquer);
            Box(t, fr, "MusicDesk", 0f, 1.08f, -0.12f, new Vector3(0.8f, 0.28f, 0.02f), Lacquer, -15f);

            // 다리 3개 + 페달
            foreach (Vector3 lp in new[] { new Vector3(-0.62f, 0f, -0.08f), new Vector3(0.62f, 0f, -0.08f), new Vector3(-0.42f, 0f, -Len + 0.25f) })
                Prop.Shape(PrimitiveType.Cylinder, t, "Leg", fr.P(lp.x, caseBot * 0.5f, lp.z), fr.Rot, new Vector3(0.09f, caseBot * 0.5f, 0.09f), Lacquer, false);
            Box(t, fr, "Lyre", 0f, 0.32f, -0.3f, new Vector3(0.18f, 0.6f, 0.05f), Lacquer);
            Pedals(t, fr, -0.27f);
            Looks.Furniture(t, "Bench", fr.P(0f, 0f, 0.7f), fr.Yaw, new Vector3(0.8f, 0.48f, 0.38f), Lacquer);
            return root;
        }

        // 고음 쪽 옆판의 x 위치 (0 = 건반 쪽, 1 = 꼬리)
        static float RightEdge(float t, float W)
        {
            float k = Mathf.Clamp01((t - 0.28f) / 0.72f);
            k = k * k * (3f - 2f * k);
            return W * 0.5f - (W * 0.72f) * k;
        }

        // 흰건반 52개 길이만큼 한 판 + 검은건반 36개
        static void Keys(Transform t, Frame fr, float y, float z)
        {
            const float keysW = 1.24f;
            Box(t, fr, "WhiteKeys", 0f, y, z, new Vector3(keysW, 0.025f, 0.15f), Ivory);
            float ww = keysW / 52f;
            // A0 부터: A B C D E F G ... (A, C, D, F, G 뒤에 검은건반)
            string notes = "ABCDEFG";
            for (int k = 0; k < 51; k++)
            {
                char n = notes[k % 7];
                if (n == 'B' || n == 'E') continue;
                float x = -keysW * 0.5f + ww * (k + 1);
                Box(t, fr, "Black", x, y + 0.022f, z - 0.03f, new Vector3(ww * 0.55f, 0.02f, 0.09f), Ebony);
            }
        }

        static void Pedals(Transform t, Frame fr, float z)
        {
            for (int i = -1; i <= 1; i++)
                Box(t, fr, "Pedal", i * 0.06f, 0.05f, z, new Vector3(0.03f, 0.015f, 0.09f), Brass);
        }

        static GameObject Box(Transform t, Frame fr, string name, float x, float y, float z, Vector3 size, Color c, float tiltX = 0f, float tiltZ = 0f)
        {
            return Prop.Box(t, name, fr.P(x, y, z), fr.Rot * Quaternion.Euler(tiltX, 0f, tiltZ), size, c, false);
        }

        struct Frame
        {
            public Vector3 o, r, u, f;
            public Quaternion Rot;
            public float Yaw;
            public Frame(Vector3 feet, Vector3 facing)
            {
                facing.y = 0f;
                if (facing.sqrMagnitude < 1e-6f) facing = Vector3.forward;
                f = facing.normalized; u = Vector3.up; r = Vector3.Cross(u, f); o = feet;
                Rot = Quaternion.LookRotation(f, u);
                Yaw = Rot.eulerAngles.y;
            }
            public Vector3 P(float x, float y, float z) { return o + r * x + u * y + f * z; }
        }
    }
}
