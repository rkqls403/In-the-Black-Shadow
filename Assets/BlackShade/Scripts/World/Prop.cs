using UnityEngine;

namespace BlackShade
{
    // 임시 소품(풍선, 종이, 분필, 가랜드 등)을 기본 도형으로 만드는 도우미.
    // 나중에 진짜 모델이 생기면 이 도형들 대신 모델을 넣으면 돼요.
    public static class Prop
    {
        public static GameObject Shape(PrimitiveType type, Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Color color, bool collider)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            if (parent != null) g.transform.SetParent(parent, true);
            g.transform.position = pos;
            g.transform.rotation = rot;
            // 부모가 늘어나 있어도 원하는 크기가 되도록
            Vector3 ps = parent != null ? parent.lossyScale : Vector3.one;
            g.transform.localScale = new Vector3(scale.x / Mathf.Max(ps.x, 0.0001f), scale.y / Mathf.Max(ps.y, 0.0001f), scale.z / Mathf.Max(ps.z, 0.0001f));
            if (!collider)
            {
                Collider c = g.GetComponent<Collider>();
                if (c != null) Kill(c);
            }
            Renderer r = g.GetComponent<Renderer>();
            SetColor(r, color);
            return g;
        }

        // 게임 중이면 Destroy, 편집 화면(씬 만들기)에서는 DestroyImmediate
        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        // 편집 화면에서는 renderer.material 대신 새 재질을 직접 만들어 붙임 (경고/누수 방지)
        static Material OwnMaterial(Renderer r)
        {
            if (Application.isPlaying) return r.material;
            Material m = new Material(r.sharedMaterial);
            r.sharedMaterial = m;
            return m;
        }

        public static GameObject Box(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 size, Color color, bool collider)
        {
            return Shape(PrimitiveType.Cube, parent, name, pos, rot, size, color, collider);
        }

        // 기본 도형의 재질(URP Lit / Built-in Standard 둘 다)을 복제해서 색만 바꾼다
        public static void SetColor(Renderer r, Color c, float smoothness = 0.25f)
        {
            if (r == null) return;
            Material m = OwnMaterial(r); // 인스턴스
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        }

        public static void SetEmission(Renderer r, Color c)
        {
            if (r == null) return;
            Material m = OwnMaterial(r);
            if (c.maxColorComponent <= 0.0001f)
            {
                m.SetColor("_EmissionColor", Color.black);
                return;
            }
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", c);
        }

        public static void SetLayerShadow(GameObject g, bool cast)
        {
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = cast ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
