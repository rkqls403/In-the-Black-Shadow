using UnityEngine;

namespace BlackShade
{
    // 화면 가운데 근처에 있는 상호작용 물건을 골라 [E] 안내를 띄우고, 누르면 실행한다.
    // 벽 너머의 물건은 고르지 않고, 어두운 곳의 조사 대상은 '어두워서 보이지 않는다'로 막는다.
    public class PlayerInteractor : MonoBehaviour
    {
        public const string DarkMessage = "어두워서 아무것도 보이지 않는다.";
        public float maxAngle = 22f;

        FirstPersonController fpc;
        Interactable current;
        float hold;
        readonly RaycastHit[] hits = new RaycastHit[8];

        // 연출 중에는 꺼 둔다
        public bool Busy { get; set; }

        void Awake() { fpc = GetComponent<FirstPersonController>(); }

        void Update()
        {
            GameUI ui = GameUI.I;
            if (ui == null) return;

            if (Busy || UIState.ModalOpen || UIState.JustClosed || fpc.mode != FirstPersonController.Mode.Free)
            {
                ResetHold();
                current = null;
                ui.HidePrompt();
                return;
            }

            Interactable best = null;
            float bestAngle = 999f;
            Transform cam = fpc.Cam.transform;
            for (int i = 0; i < Interactable.All.Count; i++)
            {
                Interactable it = Interactable.All[i];
                if (!it.available) continue;
                Vector3 d = it.FocusPoint - cam.position;
                float dist = d.magnitude;
                if (dist > it.range) continue;
                float allowed = Mathf.Lerp(maxAngle * 1.6f, maxAngle, Mathf.InverseLerp(0.6f, it.range, dist));
                float a = Vector3.Angle(cam.forward, d);
                if (a < allowed && a < bestAngle && Visible(cam.position, it, dist)) { best = it; bestAngle = a; }
            }

            if (best != current) { ResetHold(); current = best; }
            if (current == null) { ui.HidePrompt(); return; }

            bool dark = current.needsLight && !LightCheck.IsLit(current.FocusPoint);
            if (dark)
            {
                ui.ShowPrompt("E", "어둡다", -1f);
                if (GameInput.InteractDown()) ui.Toast(DarkMessage);
                return;
            }

            if (current.holdSeconds <= 0f)
            {
                ui.ShowPrompt("E", current.Prompt, -1f);
                if (GameInput.InteractDown())
                {
                    Interactable used = current;
                    current = null;
                    ui.HidePrompt();
                    used.Use();
                }
            }
            else
            {
                if (GameInput.InteractHeld()) hold += Time.deltaTime / current.holdSeconds;
                else hold = Mathf.Max(0f, hold - Time.deltaTime * 1.5f);
                hold = Mathf.Clamp01(hold);
                if (current.onHoldProgress != null) current.onHoldProgress(hold);
                ui.ShowPrompt("E", current.Prompt + "  (길게 누르기)", hold);
                if (hold >= 1f)
                {
                    Interactable used = current;
                    current = null;
                    hold = 0f;
                    ui.HidePrompt();
                    used.Use();
                }
            }
        }

        // 카메라와 물건 사이를 다른 것이 막고 있지 않은지
        bool Visible(Vector3 from, Interactable it, float dist)
        {
            Vector3 dir = (it.FocusPoint - from) / Mathf.Max(dist, 0.0001f);
            int n = Physics.RaycastNonAlloc(from, dir, hits, Mathf.Max(0f, dist - 0.08f), ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || it.Owns(c)) continue;
                if (c.transform.IsChildOf(transform)) continue;
                return false;
            }
            return true;
        }

        void ResetHold()
        {
            if (current != null && hold > 0f && current.onHoldProgress != null) current.onHoldProgress(0f);
            hold = 0f;
        }
    }
}
