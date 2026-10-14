using UnityEngine;

namespace BlackShade
{
    // 미술실 이젤 + 캔버스.
    // 캔버스 그림: Assets/BlackShade/Resources/Paintings/ 폴더에 easel1, easel2, easel3 이라는 이름의
    // 그림 파일(.png 또는 .jpg)을 넣으면 그 그림이 걸려요. 없으면 하얀 빈 캔버스.
    // 그림 비율에 맞춰 캔버스 높이가 바뀌어요 (폭은 55cm 고정).
    public static class Easels
    {
        static readonly Color Wood = new Color(0.42f, 0.3f, 0.2f);

        const float ModelH = 1.75f;           // 이젤 모델 높이
        const float CanvasTilt = 13.3f;       // 모델 캔버스가 뒤로 기운 각도

        static bool WithModel(Transform t, Vector3 feet, Vector3 facing, int number)
        {
            GameObject m = PropModels.Place("Easel", t, feet, facing, new Vector3(0f, ModelH, 0f), PropModels.Fit.Height, "EaselModel");
            if (m == null) return false;
            Renderer canvas = null;
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>(true)) if (r.name.Contains("pintura")) { canvas = r; break; }
            if (canvas == null) return true;   // 캔버스를 못 찾으면 모델 그대로
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            Quaternion tilt = Quaternion.LookRotation(facing, Vector3.up) * Quaternion.Euler(-CanvasTilt, 0f, 0f);
            Bounds b = canvas.bounds;
            float cw = Mathf.Abs(Vector3.Dot(b.size, new Vector3(Mathf.Abs(right.x), 0f, Mathf.Abs(right.z))));
            float ch = b.size.y / Mathf.Cos(CanvasTilt * Mathf.Deg2Rad);
            // 그림 비율대로 캔버스 안에 쏙
            Texture2D tex = Resources.Load<Texture2D>("Paintings/easel" + number);
            float w = cw * 0.94f, h = ch * 0.94f;
            if (tex != null)
            {
                float a = tex.width / Mathf.Max(1f, (float)tex.height);
                if (w / h > a) w = h * a; else h = w / a;
            }
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Painting";
            Prop.Kill(quad.GetComponent<Collider>());
            quad.transform.SetParent(t, false);
            quad.transform.position = b.center + (tilt * Vector3.forward) * 0.03f;
            quad.transform.rotation = Quaternion.LookRotation(-(tilt * Vector3.forward), tilt * Vector3.up);
            quad.transform.localScale = new Vector3(w, h, 1f);
            Material mat = new Material(Shader.Find("Standard"));
            mat.SetFloat("_Glossiness", 0.15f);
            if (tex != null) mat.mainTexture = tex; else mat.color = new Color(0.93f, 0.92f, 0.88f);
            Renderer qr = quad.GetComponent<Renderer>();
            qr.sharedMaterial = mat;
            qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            EaselPainting ep = quad.AddComponent<EaselPainting>();
            ep.number = number; ep.fitW = cw * 0.94f; ep.fitH = ch * 0.94f;
            return true;
        }

        // feet: 이젤 바닥 가운데, facing: 그림이 보이는 쪽, number: 1, 2, 3 ...
        public static GameObject Create(Transform parent, Vector3 feet, Vector3 facing, int number)
        {
            facing.y = 0f; facing.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            Quaternion rot = Quaternion.LookRotation(facing, Vector3.up);
            GameObject root = new GameObject("Easel" + number);
            if (parent != null) root.transform.SetParent(parent, false);
            Transform t = root.transform;
            System.Func<float, float, float, Vector3> P = (x, y, z) => feet + right * x + Vector3.up * y + facing * z;

            // 충돌용 (안 보임)
            GameObject col = Looks.Furniture(t, "EaselCollision", feet, rot.eulerAngles.y, new Vector3(0.62f, 1.6f, 0.55f), Wood);
            Renderer cr = col.GetComponent<Renderer>(); if (cr != null) cr.enabled = false;

            // 받은 이젤 모델(캔버스 포함)이 있으면 그 캔버스 위에 그림을 붙임
            if (WithModel(t, feet, facing, number)) return root;

            // 다리 3개 + 받침
            Prop.Box(t, "LegL", P(-0.24f, 0.82f, 0.06f), rot * Quaternion.Euler(-8f, 0f, -6f), new Vector3(0.035f, 1.68f, 0.035f), Wood, false);
            Prop.Box(t, "LegR", P(0.24f, 0.82f, 0.06f), rot * Quaternion.Euler(-8f, 0f, 6f), new Vector3(0.035f, 1.68f, 0.035f), Wood, false);
            Prop.Box(t, "LegBack", P(0f, 0.75f, -0.22f), rot * Quaternion.Euler(20f, 0f, 0f), new Vector3(0.035f, 1.6f, 0.035f), Wood, false);
            Prop.Box(t, "Ledge", P(0f, 0.8f, 0.1f), rot * Quaternion.Euler(-8f, 0f, 0f), new Vector3(0.62f, 0.03f, 0.08f), Wood, false);

            // 그림
            Texture2D tex = Resources.Load<Texture2D>("Paintings/easel" + number);
            float w = 0.55f, h = 0.7f;
            if (tex != null) h = Mathf.Clamp(w * tex.height / Mathf.Max(1f, (float)tex.width), 0.3f, 0.95f);
            Quaternion tilt = rot * Quaternion.Euler(-8f, 0f, 0f);
            Vector3 center = P(0f, 0.82f + h * 0.5f, 0.1f) + (tilt * Vector3.up - Vector3.up) * (h * 0.5f);
            Prop.Box(t, "CanvasBoard", center - facing * 0.012f, tilt, new Vector3(w + 0.02f, h + 0.02f, 0.02f), new Color(0.85f, 0.84f, 0.8f), false);

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Painting";
            Prop.Kill(quad.GetComponent<Collider>());
            quad.transform.SetParent(t, false);
            quad.transform.position = center + (tilt * Vector3.forward) * 0.002f;
            quad.transform.rotation = Quaternion.LookRotation(-(tilt * Vector3.forward), tilt * Vector3.up);   // 쿼드 앞면이 facing 쪽
            quad.transform.localScale = new Vector3(w, h, 1f);
            Material m = new Material(Shader.Find("Standard"));
            m.SetFloat("_Glossiness", 0.15f);
            if (tex != null) m.mainTexture = tex;
            else m.color = new Color(0.93f, 0.92f, 0.88f);
            quad.GetComponent<Renderer>().sharedMaterial = m;
            quad.AddComponent<EaselPainting>().number = number;
            return root;
        }
    }

    // 게임을 시작할 때마다 그림 파일을 다시 읽어서 붙임 → 그림만 바꿀 땐 씬을 다시 만들 필요 없음
    // (그림 비율이 많이 바뀌었으면 '게임 씬 만들기'를 다시 하면 캔버스 크기도 맞춰져요)
    public class EaselPainting : MonoBehaviour
    {
        public int number = 1;
        public float fitW, fitH;   // 이젤 모델 캔버스 크기 (0 이면 크기는 그대로)
        void Start()
        {
            Texture2D tex = Resources.Load<Texture2D>("Paintings/easel" + number);
            Renderer r = GetComponent<Renderer>();
            if (tex == null || r == null) return;
            Material m = r.material;
            m.mainTexture = tex;
            m.color = Color.white;
            if (fitW > 0f && fitH > 0f)
            {
                float a = tex.width / Mathf.Max(1f, (float)tex.height), w = fitW, h = fitH;
                if (w / h > a) w = h * a; else h = w / a;
                transform.localScale = new Vector3(w, h, 1f);
            }
        }
    }
}
