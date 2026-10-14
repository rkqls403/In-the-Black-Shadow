using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BlackShade
{
    // 문. 교실은 맵에 원래 있는 미닫이문을 움직이고, 다른 방은 경첩 문을 새로 만들어 끼운다.
    // 잠금 종류: 열림 / 항상 잠김 / 열쇠 / 번호 자물쇠 / 특별(강당)
    public class Door : MonoBehaviour
    {
        public static readonly List<Door> All = new List<Door>();

        public string id;
        public string label;
        public DoorLock lockType;
        public string key, code, lockedMsg;
        public System.Action onSpecialUse;   // 강당 문처럼 따로 처리할 때
        public bool IsOpen { get; private set; }
        public Vector3 Center { get; private set; }

        bool sliding;
        readonly List<Transform> movers = new List<Transform>();
        readonly List<Vector3> closedPos = new List<Vector3>();
        readonly List<Vector3> openPos = new List<Vector3>();
        readonly List<Quaternion> closedRot = new List<Quaternion>();
        readonly List<Quaternion> openRot = new List<Quaternion>();
        float t, target;
        float speed = 2.2f;
        NavMeshObstacle blocker;
        Interactable it;
        GameObject lockModel;   // 번호 자물쇠 모델 (풀리면 사라짐)

        public bool Locked { get { return lockType != DoorLock.Open && !GameState.Current.Flag("unlocked:" + id); } }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        // ---------------------------------------------------------------- 만들기
        public static Door CreateSliding(ModuleFrame.SlidingDoorParts p, DoorSpec spec, string label)
        {
            GameObject go = new GameObject("Door_" + spec.id);
            go.transform.position = p.center;
            Door d = go.AddComponent<Door>();
            d.Setup(spec, label, p.center);
            d.sliding = true;
            p.pivot.SetParent(go.transform, true);
            d.movers.Add(p.pivot);
            d.closedPos.Add(p.pivot.localPosition);
            d.openPos.Add(p.pivot.localPosition + go.transform.InverseTransformVector(p.slide));
            d.closedRot.Add(p.pivot.localRotation);
            d.openRot.Add(p.pivot.localRotation);
            d.MakeBlocker(p.width, Vector3.Cross(Vector3.up, p.slide.normalized));
            return d;
        }

        public static Door CreateHinged(RoomSpec room, DoorSpec spec, Color color)
        {
            float tw = room.wallIn;
            Vector2 alongG, inwardG;
            float plane;
            switch (spec.side)
            {
                case Side.N: alongG = new Vector2(1, 0); inwardG = new Vector2(0, 1); plane = room.z0 + tw * 0.5f; break;
                case Side.S: alongG = new Vector2(1, 0); inwardG = new Vector2(0, -1); plane = room.z1 - tw * 0.5f; break;
                case Side.W: alongG = new Vector2(0, 1); inwardG = new Vector2(1, 0); plane = room.x0 + tw * 0.5f; break;
                default: alongG = new Vector2(0, 1); inwardG = new Vector2(-1, 0); plane = room.x1 - tw * 0.5f; break;
            }
            float h0 = 0f;
            System.Func<float, float, Vector3> W = (along, height) =>
            {
                float gx = alongG.x != 0 ? along : plane;
                float gz = alongG.y != 0 ? along : plane;
                return MapSpace.GF(room.floor, gx, h0 + height, gz);
            };

            Vector3 center = W(spec.center, 1.1f);
            GameObject go = new GameObject("Door_" + spec.id);
            go.transform.position = center;
            Door d = go.AddComponent<Door>();
            d.Setup(spec, room.name, center);

            Vector3 along = MapSpace.Dir(alongG.x, alongG.y);
            Vector3 inward = MapSpace.Dir(inwardG.x, inwardG.y);
            Quaternion face = Quaternion.LookRotation(inward, Vector3.up);
            string kind = KindOf(spec.id);

            // ---- 강당 양문: 틀과 문짝이 한 덩어리인 모델 (열리는 모습 없음 — 들어가는 건 연출로)
            if (kind == "hall" && PropModels.Has("HallDoor"))
            {
                PropModels.Place("HallDoor", go.transform, W(spec.center, 0f), inward, new Vector3(spec.width, 0f, 0f), PropModels.Fit.Inside, "HallDoorModel");
                GameObject col = Prop.Box(go.transform, "Collision", W(spec.center, 1.43f), face, new Vector3(spec.width, 2.86f, 0.12f), color, true);
                col.GetComponent<Renderer>().enabled = false;
                d.speed = 1.6f;
                d.MakeBlocker(spec.width, inward);
                return d;
            }

            // ---- 특수 교실 문: 틀(벽 구멍에 꼭 맞음) + 세로로 긴 유리 문짝
            bool classDoor = kind == "class" && PropModels.Has("ClassDoorLeaf") && PropModels.Has("ClassDoorFrame");
            const float ClassH = 2.2f, ClassSide = 0.064f;      // 틀 높이(= 벽 구멍 높이), 틀 옆 두께
            if (classDoor)
                PropModels.Place("ClassDoorFrame", go.transform, W(spec.center, 0f), inward, new Vector3(0f, ClassH, 0f), PropModels.Fit.Height, "DoorFrame");

            int leaves = spec.width > 1.6f ? 2 : 1;
            float lw = classDoor ? spec.width - ClassSide * 2f : spec.width / leaves - 0.006f;
            for (int i = 0; i < leaves; i++)
            {
                float edge = classDoor ? ClassSide : 0.003f;
                float hingeAlong = i == 0 ? spec.center - spec.width * 0.5f + edge : spec.center + spec.width * 0.5f - edge;
                Vector3 hinge = W(hingeAlong, 0f);
                Vector3 toCenter = i == 0 ? along : -along;
                GameObject pivot = new GameObject("Leaf" + i);
                pivot.transform.SetParent(go.transform, true);
                pivot.transform.position = hinge;
                float leafH = classDoor ? 2.12f : 2.12f;
                GameObject panel = Prop.Box(pivot.transform, "Panel", hinge + toCenter * (lw * 0.5f) + Vector3.up * leafH * 0.5f, face, new Vector3(lw, leafH, 0.05f), color, true);
                Prop.Box(pivot.transform, "Window", hinge + toCenter * (lw * 0.5f) + Vector3.up * 1.6f, face, new Vector3(lw * 0.45f, 0.45f, 0.06f), new Color(0.12f, 0.14f, 0.16f), false);
                Prop.Box(pivot.transform, "Handle", hinge + toCenter * (lw - 0.1f) + Vector3.up * 1.0f, face, new Vector3(0.12f, 0.03f, 0.1f), new Color(0.6f, 0.6f, 0.58f), false);
                Prop.SetLayerShadow(panel, true);
                GameObject dm;
                if (classDoor)
                {
                    // 문짝: 틀 안쪽에 딱 맞게 (모델 비율 그대로), 손잡이는 경첩 반대쪽
                    dm = PropModels.Place("ClassDoorLeaf", pivot.transform, hinge + toCenter * (lw * 0.5f), inward, new Vector3(0f, leafH, 0f), PropModels.Fit.Height, "DoorModel");
                    if (dm != null) FlipIfHandleAtHinge(dm, "Cube.013", hinge, toCenter);
                }
                else
                {
                    // 받은 문 모델(나무+유리): 벽 구멍이 이 문 크기에 맞춰져 있어요 (폭 약 1.01m, 높이 2.12m)
                    dm = PropModels.Place("WoodDoor", pivot.transform, hinge + toCenter * (lw * 0.5f), Vector3.Cross(-toCenter, Vector3.up), new Vector3(lw, leafH, 0f), PropModels.Fit.Inside, "DoorModel");
                }
                if (dm != null)
                    foreach (Renderer r in pivot.GetComponentsInChildren<Renderer>(true))
                        if (!r.transform.IsChildOf(dm.transform)) r.enabled = false;
                // 번호 자물쇠 문: 복도 쪽 손잡이에 자물쇠를 건다
                if (i == 0 && spec.lockType == DoorLock.Code)
                {
                    Vector3 at = hinge + toCenter * (lw - 0.1f) + Vector3.up * 0.82f - inward * 0.05f;
                    d.lockModel = PropModels.Place("Padlock", pivot.transform, at, -inward, new Vector3(0f, 0.1f, 0f), PropModels.Fit.Height, "Padlock");
                    if (d.lockModel != null) d.lockModel.SetActive(d.Locked);
                }
                float sign = Mathf.Sign(Vector3.Cross(toCenter, inward).y);
                d.movers.Add(pivot.transform);
                d.closedPos.Add(pivot.transform.localPosition);
                d.openPos.Add(pivot.transform.localPosition);
                d.closedRot.Add(pivot.transform.localRotation);
                d.openRot.Add(Quaternion.AngleAxis(sign * 100f, Vector3.up) * pivot.transform.localRotation);
            }
            d.speed = 1.6f;
            d.MakeBlocker(spec.width, inward);
            return d;
        }

        // 문 종류: 특수 교실(세로 유리 문) / 강당(양문) / 그 밖(나무+유리 문)
        public static string KindOf(string id)
        {
            if (id == "5F_hall") return "hall";
            if (id.StartsWith("5F_computer") || id.StartsWith("5F_art") || id.StartsWith("5F_music") || id == "2F_science") return "class";
            return "wood";
        }

        // 손잡이가 경첩 쪽에 붙어 있으면 문짝 모델을 세로축으로 반 바퀴 돌려 반대쪽으로
        static void FlipIfHandleAtHinge(GameObject model, string handleName, Vector3 hinge, Vector3 toCenter)
        {
            Transform h = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) if (t.name == handleName) { h = t; break; }
            if (h == null) return;
            Renderer hr = h.GetComponent<Renderer>();
            Vector3 hp = hr != null ? hr.bounds.center : h.position;
            Bounds b;
            if (!PropModels.WorldBounds(model, out b)) return;
            if (Vector3.Dot(hp - b.center, toCenter) < 0f)   // 손잡이가 경첩 쪽 절반에 있음
                model.transform.RotateAround(new Vector3(b.center.x, 0f, b.center.z), Vector3.up, 180f);
        }

        void Setup(DoorSpec spec, string roomLabel, Vector3 center)
        {
            id = spec.id;
            label = roomLabel;
            lockType = spec.lockType;
            key = spec.key;
            code = spec.code;
            lockedMsg = spec.lockedMsg;
            Center = center;

            it = gameObject.AddComponent<Interactable>();
            it.oneShot = false;
            it.range = 2.1f;
            it.focusPoint = center;
            it.dynamicPrompt = () => IsOpen ? "문 닫기" : "문 열기";
            it.onUse = OnUse;
        }

        void MakeBlocker(float width, Vector3 normal)
        {
            GameObject b = new GameObject("NavBlocker");
            b.transform.SetParent(transform, false);
            b.transform.position = Center;
            b.transform.rotation = Quaternion.LookRotation(normal, Vector3.up);
            blocker = b.AddComponent<NavMeshObstacle>();
            blocker.shape = NavMeshObstacleShape.Box;
            blocker.size = new Vector3(width, 2.2f, 0.5f);
            blocker.carving = true;
            blocker.enabled = Locked;
        }

        // ---------------------------------------------------------------- 사용
        void OnUse()
        {
            AudioDirector a = AudioDirector.I;
            if (!Locked) { if (IsOpen) Close(); else Open(); return; }

            switch (lockType)
            {
                case DoorLock.Locked:
                    a.PlayAt(a.rattle, Center, 0.8f);
                    GameUI.I.Toast(lockedMsg ?? "문이 잠겨 있다.");
                    break;

                case DoorLock.Key:
                    if (GameState.Current.Has(key))
                    {
                        string nm = ItemDB.Name(key);
                        ModalUI.I.Confirm(Josa.Quote(nm) + " 사용하시겠습니까?", yes =>
                        {
                            if (!yes) return;
                            GameState.Current.RemoveItem(key);
                            a.PlayAt(a.unlock, Center, 0.9f);
                            Unlock();
                            GameUI.I.Toast(label + " 문이 열렸다.");
                            Open();
                        });
                    }
                    else
                    {
                        a.PlayAt(a.rattle, Center, 0.8f);
                        GameUI.I.Toast(lockedMsg ?? "문이 잠겨 있다. 열쇠가 필요하다.");
                    }
                    break;

                case DoorLock.Code:
                    ModalUI.I.Keypad(code.Length, lockedMsg ?? "번호 자물쇠", entered =>
                    {
                        if (entered == null) return;
                        if (entered == code)
                        {
                            a.PlayAt(a.unlock, Center, 0.9f);
                            Unlock();
                            GameUI.I.Toast("철컥. 자물쇠가 풀렸다.");
                            Open();
                        }
                        else
                        {
                            a.PlayAt(a.rattle, Center, 0.6f);
                            GameUI.I.Toast("자물쇠가 열리지 않는다.");
                        }
                    });
                    break;

                case DoorLock.Special:
                    if (onSpecialUse != null) onSpecialUse();
                    break;
            }
        }

        public void Unlock()
        {
            GameState.Current.SetFlag("unlocked:" + id);
            if (blocker != null) blocker.enabled = false;
            if (lockModel != null) lockModel.SetActive(false);
        }

        public void Open(bool slam = false)
        {
            if (IsOpen) return;
            IsOpen = true;
            target = 1f;
            AudioDirector a = AudioDirector.I;
            if (a != null) a.PlayAt(sliding ? a.doorSlide : a.doorCreak, Center, slam ? 1f : 0.7f, slam ? 1.25f : Random.Range(0.95f, 1.05f));
            if (slam) t = Mathf.Max(t, 0.3f);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            target = 0f;
            AudioDirector a = AudioDirector.I;
            if (a != null) a.PlayAt(sliding ? a.doorSlide : a.doorCreak, Center, 0.6f, 0.9f);
        }

        // 저장된 상태 적용용 (소리 없이)
        public void RefreshLock()
        {
            if (blocker != null) blocker.enabled = Locked;
            if (lockModel != null) lockModel.SetActive(Locked);
        }

        void Update()
        {
            // 괴물은 잠기지 않은 문을 밀고 들어온다
            if (!IsOpen && !Locked && TeacherMonster.I != null && TeacherMonster.I.Active)
            {
                if ((TeacherMonster.I.transform.position - Center).sqrMagnitude < 2.2f * 2.2f) Open(true);
            }

            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.deltaTime * speed);
            float k = t * t * (3f - 2f * t);
            for (int i = 0; i < movers.Count; i++)
            {
                if (movers[i] == null) continue;
                movers[i].localPosition = Vector3.Lerp(closedPos[i], openPos[i], k);
                movers[i].localRotation = Quaternion.Slerp(closedRot[i], openRot[i], k);
            }
        }

        public static Door Find(string id)
        {
            foreach (Door d in All) if (d.id == id) return d;
            return null;
        }
    }
}
