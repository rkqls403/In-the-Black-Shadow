using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BlackShade
{
    // 숨을 곳: 교실 청소함(캐비닛), 화장실 칸.
    // 안에 들어가 문을 닫으면 선생님은 플레이어를 볼 수 없다.
    // 단, 쫓기는 중에 들어가는 걸 들키면 문을 열고 끌어낸다.
    public class HideSpot : MonoBehaviour
    {
        public static readonly List<HideSpot> All = new List<HideSpot>();
        public static HideSpot Current { get; private set; }
        public static bool PlayerHidden { get { return Current != null; } }

        public string label = "청소함";
        public Vector3 insideFeet;
        public float insideYaw;
        public Vector3 outsideFeet;
        public bool SeenEntering { get; set; }
        public Vector3 FrontPoint { get { return outsideFeet; } }

        Transform doorPivot;
        Quaternion closedRot, openRot;
        float t = 1f, target = 0f;
        Interactable it;
        bool busy;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        // ---------------------------------------------------------------- 청소함
        // feet: 청소함 바닥 가운데, facing: 문이 향하는 방향(방 안쪽)
        public static HideSpot CreateCabinet(Vector3 feet, Vector3 facing)
        {
            facing.y = 0f; facing.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            Quaternion rot = Quaternion.LookRotation(facing, Vector3.up);
            float W = 0.78f, D = 0.62f, H = 1.95f, th = 0.03f;
            Color metal = new Color(0.42f, 0.47f, 0.45f);

            GameObject go = new GameObject("Cabinet");
            go.transform.position = feet;
            go.transform.rotation = rot;
            HideSpot h = go.AddComponent<HideSpot>();
            h.label = "청소함";
            Transform tr = go.transform;

            Prop.Box(tr, "Back", feet - facing * (D * 0.5f - th * 0.5f) + Vector3.up * H * 0.5f, rot, new Vector3(W, H, th), metal, true);
            Prop.Box(tr, "Left", feet - right * (W * 0.5f - th * 0.5f) + Vector3.up * H * 0.5f, rot, new Vector3(th, H, D), metal, true);
            Prop.Box(tr, "Right", feet + right * (W * 0.5f - th * 0.5f) + Vector3.up * H * 0.5f, rot, new Vector3(th, H, D), metal, true);
            Prop.Box(tr, "Top", feet + Vector3.up * (H - th * 0.5f), rot, new Vector3(W, th, D), metal, true);
            Prop.Box(tr, "Bottom", feet + Vector3.up * 0.02f, rot, new Vector3(W, 0.04f, D), metal, false);
            // 안에 걸린 대걸레
            Prop.Shape(PrimitiveType.Cylinder, tr, "Mop", feet - facing * 0.18f + right * 0.25f + Vector3.up * 0.8f, Quaternion.Euler(4, 0, 6), new Vector3(0.025f, 0.8f, 0.025f), new Color(0.5f, 0.4f, 0.3f), false);

            // 문 (눈높이에 환기 구멍 = 틈 사이로 밖이 보임)
            Vector3 hinge = feet + facing * (D * 0.5f) - right * (W * 0.5f);
            GameObject pivot = new GameObject("DoorPivot");
            pivot.transform.SetParent(tr, true);
            pivot.transform.position = hinge;
            pivot.transform.rotation = rot;
            float doorW = W - 0.01f;
            Vector3 mid = hinge + right * doorW * 0.5f;
            Prop.Box(pivot.transform, "Low", mid + Vector3.up * 0.68f, rot, new Vector3(doorW, 1.32f, th), metal * 1.05f, false);
            Prop.Box(pivot.transform, "High", mid + Vector3.up * 1.87f, rot, new Vector3(doorW, 0.12f, th), metal * 1.05f, false);
            // 문 자리를 막는 고정된 투명 벽 (움직이는 문짝에는 충돌체가 없어서, 나올 때 문에 밀려 다시 갇히지 않음)
            GameObject block = Prop.Box(tr, "DoorBlock", feet + facing * (D * 0.5f - th * 0.5f) + Vector3.up * H * 0.5f, rot, new Vector3(W, H, th), metal, true);
            block.GetComponent<Renderer>().enabled = false;
            for (int i = 0; i < 6; i++)
            {
                float y = 1.36f + i * 0.075f;
                Prop.Box(pivot.transform, "Slat" + i, mid + Vector3.up * (y + 0.025f), rot, new Vector3(doorW, 0.045f, th), metal * 0.95f, false);
            }
            Prop.Box(pivot.transform, "Handle", hinge + right * (doorW - 0.08f) + facing * 0.03f + Vector3.up * 1.05f, rot, new Vector3(0.03f, 0.14f, 0.03f), new Color(0.2f, 0.2f, 0.2f), false);

            h.doorPivot = pivot.transform;
            h.closedRot = pivot.transform.localRotation;
            h.openRot = Quaternion.AngleAxis(-100f, Vector3.up) * h.closedRot;
            UseCabinetModel(h, tr, feet, facing, right, rot, new Vector3(W, H, D));
            h.insideFeet = feet - facing * 0.04f;
            h.insideYaw = Quaternion.LookRotation(facing).eulerAngles.y;
            h.outsideFeet = feet + facing * (D * 0.5f + 0.65f);
            h.Finish(go, feet + facing * (D * 0.5f) + Vector3.up * 1.1f, new Vector3(W, H, D));
            return h;
        }

        // 받은 캐비닛 모델이 있으면 모습을 바꾸고, 모델의 문짝(경첩이 중심)을 여닫이로 쓴다.
        // 충돌체는 원래 상자 그대로라서 숨기/끌어내기 동작은 똑같아요.
        static void UseCabinetModel(HideSpot h, Transform tr, Vector3 feet, Vector3 facing, Vector3 right, Quaternion rot, Vector3 size)
        {
            GameObject m = PropModels.Place("Cabinet", tr, feet, facing, size, PropModels.Fit.Inside, "CabinetModel");
            if (m == null) return;
            Transform door = null;
            foreach (Transform t in m.GetComponentsInChildren<Transform>(true)) if (t.name == "door") { door = t; break; }
            // 원래 상자 모습 숨김 (대걸레는 그대로)
            foreach (Renderer r in tr.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.IsChildOf(m.transform) || r.name == "Mop") continue;
                r.enabled = false;
            }
            if (door == null) return;   // 문짝을 못 찾으면 모습만 바꿈 (원래 문은 안 보이게 움직임)

            GameObject pivot = new GameObject("ModelDoorPivot");
            pivot.transform.SetParent(tr, true);
            pivot.transform.position = door.position;   // 모델 문짝의 기준점 = 경첩
            pivot.transform.rotation = rot;
            door.SetParent(pivot.transform, true);
            // 경첩이 오른쪽에 있는 모델이면 반대로 돈다
            Bounds b = new Bounds(door.position, Vector3.zero);
            foreach (Renderer r in door.GetComponentsInChildren<Renderer>(true)) b.Encapsulate(r.bounds);
            float side = Vector3.Dot(b.center - pivot.transform.position, right) >= 0f ? -1f : 1f;
            h.doorPivot = pivot.transform;
            h.closedRot = pivot.transform.localRotation;
            h.openRot = Quaternion.AngleAxis(side * 100f, Vector3.up) * h.closedRot;
        }

        // ---------------------------------------------------------------- 화장실 칸
        // feet: 칸 바닥 가운데, facing: 문 쪽 방향, width: 칸 폭, depth: 칸 깊이
        public static HideSpot CreateStall(Vector3 feet, Vector3 facing, float width, float depth)
        {
            facing.y = 0f; facing.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            Quaternion rot = Quaternion.LookRotation(facing, Vector3.up);
            Color panel = new Color(0.62f, 0.66f, 0.7f);
            float th = 0.04f;

            GameObject go = new GameObject("ToiletStall");
            go.transform.position = feet;
            go.transform.rotation = rot;
            HideSpot h = go.AddComponent<HideSpot>();
            h.label = "화장실 칸";
            Transform tr = go.transform;

            // 받은 칸막이 모델 (비율 유지, 폭에 맞춤 → 깊이·높이는 모델 비율대로)
            GameObject cub = PropModels.Place("Cubicle", tr, feet, facing, new Vector3(width, 0f, 0f), PropModels.Fit.Inside, "CubicleModel");
            float H = 1.8f;
            if (cub != null)
            {
                Bounds cb;
                if (PropModels.WorldBounds(cub, out cb))
                {
                    depth = Mathf.Abs(Vector3.Dot(cb.size, new Vector3(Mathf.Abs(facing.x), 0f, Mathf.Abs(facing.z))));
                    H = cb.size.y;
                }
            }

            // 옆판·앞판 충돌체 (모델이 있으면 안 보이게)
            GameObject sl = Prop.Box(tr, "SideL", feet - right * width * 0.5f + Vector3.up * H * 0.5f, rot, new Vector3(th, H, depth), panel, true);
            GameObject sr = Prop.Box(tr, "SideR", feet + right * width * 0.5f + Vector3.up * H * 0.5f, rot, new Vector3(th, H, depth), panel, true);
            // 앞판 = 문 자리를 막는 투명 벽. 문짝 자체에는 충돌체가 없어서, 문이 움직여도 플레이어를 밀지 않아요.
            GameObject block = Prop.Box(tr, "DoorBlock", feet + facing * (depth * 0.5f - 0.03f) + Vector3.up * H * 0.5f, rot, new Vector3(width, H, 0.05f), panel, true);
            block.GetComponent<Renderer>().enabled = false;
            if (cub != null) { sl.GetComponent<Renderer>().enabled = false; sr.GetComponent<Renderer>().enabled = false; }
            // 변기
            GameObject toilet = Prop.Box(tr, "Toilet", feet - facing * (depth * 0.5f - 0.3f) + Vector3.up * 0.22f, rot, new Vector3(0.38f, 0.44f, 0.5f), new Color(0.92f, 0.92f, 0.9f), false);
            // 받은 변기 모델: 물탱크가 뒤 벽 쪽, 앉는 쪽이 문 쪽
            if (PropModels.Place("Toilet", tr, feet - facing * (depth * 0.5f - 0.42f), facing, new Vector3(0f, 0.78f, 0f), PropModels.Fit.Height, "ToiletModel") != null)
                toilet.GetComponent<Renderer>().enabled = false;

            // 문: 모델 안의 문짝(StallDoor)을 경첩 기준으로 안쪽으로 연다
            Transform door = null;
            if (cub != null)
                foreach (Transform t in cub.GetComponentsInChildren<Transform>(true)) if (t.name == "StallDoor") { door = t; break; }
            GameObject pivot = new GameObject("DoorPivot");
            pivot.transform.SetParent(tr, true);
            pivot.transform.rotation = rot;
            float openAngle = 88f;
            if (door != null)
            {
                pivot.transform.position = door.position;   // 문짝 기준점 = 경첩
                door.SetParent(pivot.transform, true);
                // 안쪽(칸 안)으로 열리는 방향 찾기
                Quaternion c0 = pivot.transform.localRotation;
                pivot.transform.localRotation = Quaternion.AngleAxis(openAngle, Vector3.up) * c0;
                Bounds db = new Bounds(door.position, Vector3.zero); bool any = false;
                foreach (Renderer r in door.GetComponentsInChildren<Renderer>(true)) { if (!any) { db = r.bounds; any = true; } else db.Encapsulate(r.bounds); }
                if (any && Vector3.Dot(db.center - pivot.transform.position, facing) > 0f) openAngle = -openAngle;
                pivot.transform.localRotation = c0;
            }
            else if (cub == null)
            {
                // 모델이 없을 때만 임시 문 (충돌체 없음)
                Vector3 hinge = feet + facing * depth * 0.5f - right * width * 0.5f;
                pivot.transform.position = hinge;
                float doorW = width - 0.04f;
                Prop.Box(pivot.transform, "Door", hinge + right * doorW * 0.5f + Vector3.up * 1.08f, rot, new Vector3(doorW, 1.72f, th), panel * 0.95f, false);
                openAngle = 88f;   // 왼쪽 경첩 → 안쪽으로
            }
            else pivot.transform.position = feet;          // 문짝 없는 모델: 여닫는 모습 없음

            h.doorPivot = pivot.transform;
            h.closedRot = pivot.transform.localRotation;
            h.openRot = Quaternion.AngleAxis(openAngle, Vector3.up) * h.closedRot;
            h.insideFeet = feet + facing * 0.05f;   // 변기 앞
            h.insideYaw = Quaternion.LookRotation(facing).eulerAngles.y;
            h.outsideFeet = feet + facing * (depth * 0.5f + 0.75f);
            h.label = "화장실 칸";
            h.Finish(go, feet + facing * depth * 0.5f + Vector3.up * 1.1f, new Vector3(width, 2f, depth));
            return h;
        }

        void Finish(GameObject go, Vector3 focus, Vector3 size)
        {
            it = go.AddComponent<Interactable>();
            it.oneShot = false;
            it.range = 1.9f;
            it.focusPoint = focus;
            it.dynamicPrompt = () => label + "에 숨기";
            it.onUse = () => { if (!busy) StartCoroutine(Enter()); };

            NavMeshObstacle ob = go.AddComponent<NavMeshObstacle>();
            ob.shape = NavMeshObstacleShape.Box;
            ob.size = size;
            ob.center = new Vector3(0f, size.y * 0.5f, 0f);
            ob.carving = true;
            SetDoor(0f, true);
        }

        void SetDoor(float open, bool instant)
        {
            target = open;
            if (instant) { t = open; doorPivot.localRotation = Quaternion.Slerp(closedRot, openRot, t); }
        }

        void Update()
        {
            if (!Mathf.Approximately(t, target))
            {
                t = Mathf.MoveTowards(t, target, Time.deltaTime * 3.5f);
                doorPivot.localRotation = Quaternion.Slerp(closedRot, openRot, t * t * (3f - 2f * t));
            }
            if (Current == this && !busy && !UIState.ModalOpen && !UIState.JustClosed && GameInput.InteractDown()) StartCoroutine(Exit(false));
        }

        // ---------------------------------------------------------------- 들어가기 / 나오기
        IEnumerator Enter()
        {
            FirstPersonController p = MainDirector.I.Player;
            busy = true;
            it.available = false;
            MainDirector.I.Interactor.Busy = true;
            p.SetMode(FirstPersonController.Mode.Cutscene);
            SeenEntering = TeacherMonster.I != null && TeacherMonster.I.Active && TeacherMonster.I.CanSeePlayerNow();

            AudioDirector.I.PlayAt(AudioDirector.I.cabinet, transform.position, 0.7f, 1.1f);
            SetDoor(1f, false);
            yield return p.TweenView(outsideFeet, insideYaw + 180f, 5f, 0f, p.standEyeHeight, 0.25f);
            yield return p.TweenView(insideFeet, insideYaw + 180f, 5f, 0f, 1.55f, 0.35f);
            // 안에서 뒤돌아 문 쪽을 봄
            yield return p.TweenView(insideFeet, insideYaw, 2f, 0f, 1.55f, 0.35f);
            SetDoor(0f, false);
            AudioDirector.I.PlayAt(AudioDirector.I.cabinet, transform.position, 0.5f, 0.9f);
            Current = this;
            p.SetMode(FirstPersonController.Mode.LookOnly);
            p.lookOnlyYawRange = 30f;
            GameUI.I.ShowHideHint(true);
            yield return new WaitForSeconds(0.4f);
            busy = false;
        }

        public IEnumerator Exit(bool forced)
        {
            FirstPersonController p = MainDirector.I.Player;
            busy = true;
            GameUI.I.ShowHideHint(false);
            p.SetMode(FirstPersonController.Mode.Cutscene);
            AudioDirector.I.PlayAt(AudioDirector.I.cabinet, transform.position, forced ? 1f : 0.6f, forced ? 0.8f : 1f);
            if (forced) { t = 0.9f; SetDoor(1f, false); } else SetDoor(1f, false);
            yield return new WaitForSeconds(forced ? 0.05f : 0.25f);
            Current = null;
            yield return p.TweenView(outsideFeet, insideYaw, 2f, 0f, p.standEyeHeight, forced ? 0.25f : 0.45f);
            SetDoor(0f, false);
            SeenEntering = false;
            p.SetMode(FirstPersonController.Mode.Free);
            MainDirector.I.Interactor.Busy = false;
            it.available = true;
            busy = false;
        }

        // 괴물이 문을 열어젖히고 끌어냄
        public void Yank()
        {
            if (Current != this) return;
            StartCoroutine(Exit(true));
        }

        public static void ForceReset()
        {
            if (Current != null) { Current.SeenEntering = false; }
            Current = null;
        }
    }
}
