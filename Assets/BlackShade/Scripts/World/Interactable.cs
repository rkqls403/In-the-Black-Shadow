using System.Collections.Generic;
using UnityEngine;

namespace BlackShade
{
    // E 키로 상호작용할 수 있는 물건.
    //   holdSeconds > 0 : E를 '길게' 눌러야 함 (풍선 불기 등)
    //   oneShot          : 한 번 쓰면 끝 (문, 스위치처럼 여러 번 쓰는 건 false)
    //   needsLight       : 불 꺼진 곳에서는 손전등이나 교실 불이 있어야 조사 가능
    public class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        public string prompt = "살펴보기";
        public float holdSeconds = 0f;
        public float range = 2.4f;
        public Transform focus;
        public Vector3? focusPoint;
        public bool available = true;
        public bool oneShot = true;
        public bool needsLight = false;

        [Header("반짝임 표시")]
        public Renderer[] glowRenderers;
        public Color glowColor = new Color(1f, 0.85f, 0.55f);

        public System.Action onUse;
        public System.Action<float> onHoldProgress; // 0~1
        public System.Func<string> dynamicPrompt;   // 상황에 따라 바뀌는 안내 (문 열기/닫기 등)

        public Vector3 FocusPoint
        {
            get
            {
                if (focus != null) return focus.position;
                if (focusPoint.HasValue) return focusPoint.Value;
                return transform.position;
            }
        }

        public string Prompt { get { return dynamicPrompt != null ? dynamicPrompt() : prompt; } }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Update()
        {
            if (glowRenderers == null) return;
            bool show = available && (!needsLight || LightCheck.IsLit(FocusPoint));
            float k = show ? (0.35f + 0.35f * Mathf.Sin(Time.time * 3f)) : 0f;
            for (int i = 0; i < glowRenderers.Length; i++)
                Prop.SetEmission(glowRenderers[i], glowColor * k);
        }

        public void Use()
        {
            if (oneShot)
            {
                available = false;
                if (glowRenderers != null)
                    for (int i = 0; i < glowRenderers.Length; i++) Prop.SetEmission(glowRenderers[i], Color.black);
            }
            if (onUse != null) onUse();
        }

        // 다른 물건에 가려져 있는지 확인할 때, 이 물건(또는 그 자식)의 충돌체에 맞은 건 괜찮다
        public bool Owns(Collider c) { return c != null && c.transform.IsChildOf(transform); }
    }

    // 지금 그 자리가 보일 만큼 밝은가? (손전등 켜짐 / 방 불 켜짐 / 원래 밝은 곳)
    public static class LightCheck
    {
        public static System.Func<Vector3, bool> roomLit;   // RoomLights가 채움
        public static System.Func<bool> flashlightOn;       // Flashlight가 채움
        public static bool alwaysLit = true;                // 프롤로그처럼 밝은 상황이면 true

        public static bool IsLit(Vector3 p)
        {
            if (alwaysLit) return true;
            if (flashlightOn != null && flashlightOn()) return true;
            return roomLit != null && roomLit(p);
        }
    }
}
