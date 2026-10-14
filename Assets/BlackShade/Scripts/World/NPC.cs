using UnityEngine;

namespace BlackShade
{
    // 친구 캐릭터. 지금 모델들은 뼈대(리그)가 없어서 서 있는 자세 그대로예요.
    // 대신 숨쉬는 느낌의 미세한 움직임과, 말하는 사람/플레이어 쪽으로 몸을 돌리는 기능만 있어요.
    // 나중에 Mixamo로 리깅한 모델을 넣고 Animator Controller를 붙이면 그대로 애니메이션이 재생돼요.
    public class NPC : MonoBehaviour
    {
        public string displayName;
        public Transform lookTarget;
        public float turnSpeed = 3f;
        public bool breathe = true;
        public bool speaking;   // 말하는 중이면 고개를 살짝 끄덕이는 느낌

        public float homeYaw;   // 아무도 안 볼 때 돌아가는 방향 (하던 일 하는 방향)
        public float Height { get; private set; }
        Transform breath;
        float seed;

        public Vector3 HeadPoint { get { return transform.position + Vector3.up * (Height * 0.93f); } }
        public Vector3 ChestPoint { get { return transform.position + Vector3.up * (Height * 0.72f); } }

        // prefab: 친구 모델(.glb 프리팹). 비어 있으면 임시 캡슐로 만든다.
        // modelYawOffset: 모델이 뒤를 보고 서 있으면 180으로 바꾸세요.
        public static NPC Create(GameObject prefab, string displayName, float targetHeight, float modelYawOffset, Color placeholderColor, RuntimeAnimatorController anim = null)
        {
            GameObject root = new GameObject("NPC_" + displayName);
            NPC npc = root.AddComponent<NPC>();
            npc.displayName = displayName;
            npc.seed = Random.value * 10f;

            npc.breath = new GameObject("Breath").transform;
            npc.breath.SetParent(root.transform, false);

            GameObject model;
            if (prefab != null)
            {
                model = Instantiate(prefab, npc.breath);
                model.name = "Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, modelYawOffset, 0f);
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
                // Mixamo로 리깅한 모델이면 애니메이터 컨트롤러(Idle 등)를 연결
                if (anim != null)
                {
                    Animator a = model.GetComponentInChildren<Animator>();
                    if (a == null) a = model.AddComponent<Animator>();
                    a.runtimeAnimatorController = anim;
                    a.applyRootMotion = false;
                    npc.breathe = false;
                }
            }
            else
            {
                model = new GameObject("Model");
                model.transform.SetParent(npc.breath, false);
                Prop.Shape(PrimitiveType.Capsule, model.transform, "Body", new Vector3(0f, 0.85f, 0f), Quaternion.identity, new Vector3(0.45f, 0.85f, 0.35f), placeholderColor, false);
                Prop.Shape(PrimitiveType.Sphere, model.transform, "Head", new Vector3(0f, 1.55f, 0f), Quaternion.identity, Vector3.one * 0.24f, new Color(0.95f, 0.8f, 0.7f), false);
            }

            // 키를 targetHeight로 맞추고 발이 바닥(루트 위치)에 오도록 정렬
            Bounds b;
            if (ClassroomFrame.TryBounds(model.transform, out b) && b.size.y > 0.01f)
            {
                float s = targetHeight / b.size.y;
                model.transform.localScale *= s;
                ClassroomFrame.TryBounds(model.transform, out b);
                Vector3 off = new Vector3(root.transform.position.x - b.center.x, root.transform.position.y - b.min.y, root.transform.position.z - b.center.z);
                model.transform.position += off;
            }
            npc.Height = targetHeight;

            // 플레이어가 몸을 통과하지 않도록
            CapsuleCollider cap = root.AddComponent<CapsuleCollider>();
            cap.radius = 0.22f;
            cap.height = targetHeight;
            cap.center = new Vector3(0f, targetHeight * 0.5f, 0f);
            return npc;
        }

        public void Place(Vector3 feet, float yaw)
        {
            transform.position = feet;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            homeYaw = yaw;
        }

        public void FaceInstant(Vector3 point)
        {
            Vector3 d = point - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(d);
                homeYaw = transform.eulerAngles.y;
            }
        }

        public void SetVisible(bool v) { gameObject.SetActive(v); }

        // 카메라에 보이는지 (화면 안 + 앞쪽)
        public bool IsSeenBy(Camera cam)
        {
            if (!gameObject.activeInHierarchy) return false;
            Vector3[] pts = { HeadPoint, ChestPoint, transform.position + Vector3.up * 0.3f };
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 vp = cam.WorldToViewportPoint(pts[i]);
                if (vp.z > 0f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f) return true;
            }
            return false;
        }

        void Update()
        {
            Quaternion want = Quaternion.Euler(0f, homeYaw, 0f);
            if (lookTarget != null)
            {
                Vector3 d = lookTarget.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) want = Quaternion.LookRotation(d);
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, want, Time.deltaTime * turnSpeed);
            if (breathe && breath != null)
            {
                float s = 1f + Mathf.Sin(Time.time * 1.7f + seed) * 0.005f;
                breath.localScale = new Vector3(1f, s, 1f);
                float nod = speaking ? Mathf.Abs(Mathf.Sin(Time.time * 7f + seed)) * 1.4f : 0f;
                breath.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 0.6f + seed) * 0.6f + nod, 0f, Mathf.Sin(Time.time * 0.45f + seed * 2f) * 0.5f);
            }
        }
    }
}
