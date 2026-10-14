using UnityEngine;

namespace BlackShade
{
    // 가방(아이템 창)에서 고른 아이템의 3D 모델을 빙글빙글 돌려 보여 주는 장치.
    // 맵에서 아주 멀리(아래 1000m) 떨어진 곳에 작은 촬영장(카메라 + 조명)을 만들어
    // 그 화면을 그림(RenderTexture)으로 받아 메뉴에 붙여요.
    //   - 받은 모델이 있는 아이템: 그 모델 (열쇠, 건전지, 초코바, 캔, 손전등, 철쭉 명찰 장식, 구급상자, 밴드, 종이, 펜, 만년필)
    //   - 아직 모델이 없는 아이템: 아이템 색깔의 임시 상자
    public class ItemPreview : MonoBehaviour
    {
        public const int Layer = 30;                 // 미리보기 전용 레이어
        static ItemPreview inst;

        Camera cam;
        Transform pivot;
        GameObject model;
        string shownId;
        RenderTexture rt;

        public static RenderTexture Texture { get { return Get().rt; } }

        static ItemPreview Get()
        {
            if (inst != null) return inst;
            GameObject g = new GameObject("ItemPreviewRig");
            g.transform.position = new Vector3(0f, -1000f, 0f);
            inst = g.AddComponent<ItemPreview>();
            inst.Build();
            return inst;
        }

        void Build()
        {
            rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            rt.name = "ItemPreviewRT";

            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);

            GameObject cg = new GameObject("PreviewCamera");
            cg.transform.SetParent(transform, false);
            cg.transform.localPosition = new Vector3(0f, 0.32f, -2.3f);
            cg.transform.LookAt(pivot.position);
            cam = cg.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.085f, 0.09f, 0.11f, 1f);
            cam.cullingMask = 1 << Layer;
            cam.fieldOfView = 24f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 10f;
            cam.targetTexture = rt;
            cam.enabled = false;

            AddLight(new Vector3(35f, -35f, 0f), 1.15f, new Color(1f, 0.97f, 0.92f));
            AddLight(new Vector3(15f, 150f, 0f), 0.45f, new Color(0.75f, 0.8f, 1f));
            AddLight(new Vector3(-40f, 0f, 0f), 0.25f, Color.white);
        }

        void AddLight(Vector3 euler, float intensity, Color c)
        {
            GameObject g = new GameObject("PreviewLight");
            g.transform.SetParent(transform, false);
            g.transform.rotation = Quaternion.Euler(euler);
            Light l = g.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = c;
            l.cullingMask = 1 << Layer;
            l.shadows = LightShadows.None;
        }

        // id 의 아이템을 보여 줌 (같은 아이템이면 그대로 계속 돌림)
        public static void Show(string id)
        {
            ItemPreview p = Get();
            p.cam.enabled = true;
            if (p.shownId == id && p.model != null) return;
            p.shownId = id;
            if (p.model != null) Destroy(p.model);
            p.model = p.MakeModel(id);
            p.pivot.localRotation = Quaternion.Euler(0f, id == "badge" || id == "bandage" ? 0f : -30f, 0f);   // 명찰 장식·밴드 상자는 앞면부터 보이게
        }

        public static void Hide()
        {
            if (inst == null) return;
            inst.cam.enabled = false;
        }

        void Update()
        {
            if (cam.enabled) pivot.Rotate(0f, 40f * Time.unscaledDeltaTime, 0f, Space.World);   // 앞·뒤가 다 보이게 한 바퀴씩
        }

        // 아이템 → 받은 모델 이름
        public static string ModelKey(string id)
        {
            if (id.StartsWith("key_")) return "Key";
            switch (id)
            {
                case "battery": return "Battery";
                case "chocobar": return "ChocoBar";
                case "drink":
                case "empty_can": return "DrinkCan";
                case "flashlight": return "Torch";
                case "badge": return "Badge";
                case "medkit": return "Medkit";
                case "bandage": return "BandageBox";
                case "paper": return "PaperSheet";
                case "pen": return "Pen";
                case "fountain_pen": return "FountainPen";
                default: return null;
            }
        }

        GameObject MakeModel(string id)
        {
            ItemDef d = ItemDB.Get(id);
            Color c = d != null ? d.color : Color.grey;
            string key = ModelKey(id);
            GameObject holder = new GameObject("Item_" + id);
            holder.transform.SetParent(pivot, false);

            GameObject m = null;
            if (key != null && PropModels.Has(key))
            {
                m = PropModels.Place(key, holder.transform, pivot.position, Vector3.back, new Vector3(0.75f, 0.75f, 0.75f), PropModels.Fit.Inside, key);
                // 납작한 물건(열쇠·초코바)은 보기 좋게 세워서
                Bounds b0;
                if (m != null && PropModels.WorldBounds(m, out b0) && b0.size.y < Mathf.Max(b0.size.x, b0.size.z) * 0.3f)
                    holder.transform.localRotation = Quaternion.Euler(-60f, 0f, 0f);
            }
            if (m == null)
            {
                // 임시 모양 (모델을 받으면 위의 ModelKey 에 이름을 추가하면 돼요)
                Vector3 size; PrimitiveType shape = PrimitiveType.Cube;
                switch (id)
                {
                    case "medkit": size = new Vector3(0.7f, 0.45f, 0.28f); break;
                    case "bandage": size = new Vector3(0.6f, 0.04f, 0.32f); break;
                    case "paper":
                    case "score_piece":
                    case "score_full": size = new Vector3(0.52f, 0.7f, 0.01f); break;
                    case "pen":
                    case "fountain_pen": size = new Vector3(0.05f, 0.7f, 0.05f); shape = PrimitiveType.Cylinder; break;
                    case "badge": size = new Vector3(0.4f, 0.03f, 0.4f); shape = PrimitiveType.Cylinder; break;
                    default: size = new Vector3(0.45f, 0.45f, 0.45f); break;
                }
                m = GameObject.CreatePrimitive(shape);
                Destroy(m.GetComponent<Collider>());
                m.transform.SetParent(holder.transform, false);
                m.transform.localScale = shape == PrimitiveType.Cylinder ? new Vector3(size.x, size.y * 0.5f, size.z) : size;
                if (id == "badge") m.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prop.SetColor(m.GetComponent<Renderer>(), c);
            }

            // 가운데 맞추기
            Bounds b;
            if (PropModels.WorldBounds(holder, out b)) holder.transform.position += pivot.position - b.center;
            foreach (Transform t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (Renderer r in holder.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return holder;
        }
    }
}
