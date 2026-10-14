using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BlackShade
{
    // ==================================================================== 주울 수 있는 것 (아이템 / 문서)
    public class WorldPickup : MonoBehaviour
    {
        public string pid;          // 저장용 고유 이름
        public string item;         // 아이템 id (없으면 null)
        public int count = 1;
        public string doc;          // 문서 id (없으면 null)
        public bool keepVisual;     // 주운 뒤에도 물건이 남는지 (책, 소화전함 등)
        public string takenMessage; // 주웠을 때 따로 띄울 말
        public System.Action onTaken;
        Interactable it;

        public static WorldPickup Create(string pid, GameObject visual, Vector3 focus, string prompt, string item, string doc, bool keepVisual = false, bool needsLight = true)
        {
            if (GameState.Current.Flag("picked:" + pid))
            {
                if (!keepVisual && visual != null) Destroy(visual);
                return null;
            }
            GameObject host = visual != null ? visual : new GameObject("Pickup_" + pid);
            if (visual == null) host.transform.position = focus;
            WorldPickup p = host.AddComponent<WorldPickup>();
            p.pid = pid; p.item = item; p.doc = doc; p.keepVisual = keepVisual;
            p.it = host.AddComponent<Interactable>();
            p.it.prompt = prompt;
            p.it.focusPoint = focus;
            p.it.range = 2.2f;
            p.it.needsLight = needsLight;
            Renderer r = visual != null ? visual.GetComponentInChildren<Renderer>() : null;
            if (r != null && !keepVisual) p.it.glowRenderers = new[] { r };
            p.it.glowColor = new Color(0.9f, 0.8f, 0.55f) * 0.6f;
            p.it.onUse = p.Take;
            return p;
        }

        void Take()
        {
            GameState gs = GameState.Current;
            gs.SetFlag("picked:" + pid);
            AudioDirector a = AudioDirector.I;
            if (!string.IsNullOrEmpty(item))
            {
                gs.AddItem(item, count);
                a.Play(a.pickup, 0.6f);
                string nm = ItemDB.Name(item);
                GameUI.I.Toast(takenMessage ?? (Josa.Quote(nm) + " 얻었다." + (count > 1 ? " (" + count + "개)" : "")));
                if (item == "score_piece") MainDirector.I.OnScorePiece();
            }
            if (!string.IsNullOrEmpty(doc))
            {
                a.Play(a.paper, 0.7f);
                bool isNew = gs.AddDoc(doc);
                ModalUI.I.ShowDoc(doc, () =>
                {
                    if (isNew) GameUI.I.Toast("문서에 기록했다. [Tab] 메뉴에서 다시 볼 수 있다.");
                    MainDirector.I.OnDocRead(doc);
                    if (onTaken != null) onTaken();
                });
            }
            else if (onTaken != null) onTaken();
            if (!keepVisual) gameObject.SetActive(false);
        }
    }

    // ==================================================================== 작은 물건 모양
    public static class Looks
    {
        // 종이 한 장 (받은 종이 모델, 색을 주면 그 색으로 물들임)
        public static GameObject Paper(Vector3 p, float yaw, Color? c = null)
        {
            GameObject m = PropModels.Place("PaperSheet", null, p, Quaternion.Euler(0, yaw, 0) * Vector3.forward, new Vector3(0.21f, 0f, 0.297f), PropModels.Fit.Inside, "Paper");
            if (m != null) { if (c.HasValue) PropModels.Tint(m, Color.Lerp(c.Value, Color.white, 0.35f)); return m; }
            return Prop.Box(null, "Paper", p + Vector3.up * 0.003f, Quaternion.Euler(0, yaw, 0), new Vector3(0.21f, 0.005f, 0.29f), c ?? new Color(0.93f, 0.91f, 0.84f), false);
        }
        // 클립보드에 끼운 서류 (점검표·기록부)
        public static GameObject Clipboard(Vector3 p, float yaw)
        {
            GameObject m = PropModels.Place("Clipboard", null, p, Quaternion.Euler(0, yaw, 0) * Vector3.forward, new Vector3(0.23f, 0f, 0.32f), PropModels.Fit.Inside, "Clipboard");
            if (m != null) return m;
            return Paper(p, yaw);
        }
        // 펜 (받은 3색 볼펜 모델)
        public static GameObject Pen(Vector3 p, float yaw)
        {
            GameObject m = PropModels.Place("Pen", null, p, Quaternion.Euler(0, yaw, 0) * Vector3.forward, new Vector3(0.145f, 0f, 0f), PropModels.Fit.Inside, "Pen");
            if (m != null) return m;
            return Small(p, new Color(0.25f, 0.35f, 0.75f), new Vector3(0.14f, 0.012f, 0.012f));
        }
        // 밴드 상자 (뽀로로 키즈밴드 그림으로 만든 작은 상자, 세워 둠)
        public static GameObject Bandage(Vector3 p, float yaw)
        {
            GameObject m = PropModels.Place("BandageBox", null, p, Quaternion.Euler(0, yaw, 0) * Vector3.forward, new Vector3(0f, 0.097f, 0f), PropModels.Fit.Height, "Bandage");
            if (m != null) return m;
            return Prop.Box(null, "Bandage", p + Vector3.up * 0.015f, Quaternion.Euler(0, yaw, 0), new Vector3(0.1f, 0.03f, 0.06f), new Color(0.92f, 0.78f, 0.62f), false);
        }
        // 만년필 (받은 모델은 서 있으므로 책상 위에 눕혀 놓음, 길이 14cm)
        public static GameObject FountainPen(Vector3 p, float yaw)
        {
            Vector3 dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            GameObject m = PropModels.Place("FountainPen", null, p, dir, new Vector3(0f, 0.14f, 0f), PropModels.Fit.Height, "FountainPen");
            if (m == null) return Small(p, new Color(0.12f, 0.12f, 0.14f), new Vector3(0.14f, 0.014f, 0.014f));
            PropModels.Turn(m, Quaternion.AngleAxis(90f, Vector3.Cross(Vector3.up, dir)), p);
            return m;
        }
        // 철쭉 명찰 장식 (받은 그림으로 만든 모델): 그림이 facing 쪽을 보게 세우고 살짝 뒤로 기댐 (높이 11cm)
        public static GameObject Badge(Vector3 p, Vector3 facing)
        {
            GameObject m = PropModels.Place("Badge", null, p, facing, new Vector3(0f, 0.11f, 0f), PropModels.Fit.Height, "Badge");
            if (m == null)
                return Prop.Shape(PrimitiveType.Cylinder, null, "Badge", p + Vector3.up * 0.04f, Quaternion.LookRotation(Vector3.up, facing), new Vector3(0.08f, 0.002f, 0.08f), new Color(0.85f, 0.4f, 0.55f), false);
            PropModels.Turn(m, Quaternion.AngleAxis(-12f, Vector3.Cross(Vector3.up, facing)), p);
            return m;
        }
        public static GameObject Note(Vector3 p, float yaw)
        { return Prop.Box(null, "Note", p + Vector3.up * 0.003f, Quaternion.Euler(0, yaw, 0), new Vector3(0.1f, 0.005f, 0.08f), new Color(0.95f, 0.93f, 0.75f), false); }
        public static GameObject Book(Vector3 p, float yaw, Color c)
        { return Prop.Box(null, "Book", p + Vector3.up * 0.025f, Quaternion.Euler(0, yaw, 0), new Vector3(0.19f, 0.05f, 0.26f), c, false); }
        public static GameObject Key(Vector3 p, float yaw)
        {
            GameObject km = PropModels.Place("Key", null, p, Quaternion.Euler(0, yaw, 0) * Vector3.forward, new Vector3(0.1f, 0.03f, 0.17f), PropModels.Fit.Inside);
            if (km != null) return km;
            GameObject k = Prop.Box(null, "Key", p + Vector3.up * 0.005f, Quaternion.Euler(0, yaw, 0), new Vector3(0.07f, 0.008f, 0.025f), new Color(0.85f, 0.72f, 0.35f), false);
            Prop.Box(k.transform, "Tag", p + Vector3.up * 0.005f + Quaternion.Euler(0, yaw, 0) * new Vector3(0.06f, 0, 0), Quaternion.Euler(0, yaw, 0), new Vector3(0.05f, 0.006f, 0.035f), new Color(0.9f, 0.88f, 0.8f), false);
            return k;
        }
        public static GameObject Medkit(Vector3 p, float yaw)
        {
            GameObject mk = PropModels.Place("Medkit", null, p, Quaternion.Euler(0, yaw, 0) * Vector3.forward, new Vector3(0.34f, 0f, 0f), PropModels.Fit.Inside, "Medkit");
            if (mk != null) return mk;
            GameObject m = Prop.Box(null, "Medkit", p + Vector3.up * 0.06f, Quaternion.Euler(0, yaw, 0), new Vector3(0.3f, 0.12f, 0.2f), new Color(0.85f, 0.15f, 0.15f), false);
            Prop.Box(m.transform, "Cross", p + Vector3.up * 0.121f, Quaternion.Euler(0, yaw, 0), new Vector3(0.1f, 0.003f, 0.03f), Color.white, false);
            Prop.Box(m.transform, "Cross2", p + Vector3.up * 0.121f, Quaternion.Euler(0, yaw, 0), new Vector3(0.03f, 0.003f, 0.1f), Color.white, false);
            return m;
        }
        // 초코바 (모델이 없으면 갈색 상자)
        public static GameObject Choco(Vector3 p)
        {
            GameObject c = PropModels.Place("ChocoBar", null, p, Quaternion.Euler(0, Random.Range(0, 360), 0) * Vector3.forward, new Vector3(0.14f, 0.03f, 0.14f), PropModels.Fit.Inside);
            if (c != null) return c;
            return Small(p, new Color(0.45f, 0.28f, 0.2f), new Vector3(0.12f, 0.02f, 0.04f));
        }

        // 건전지 (모델이 없으면 노란 상자)
        public static GameObject Battery(Vector3 p)
        {
            GameObject b = PropModels.Place("Battery", null, p, Quaternion.Euler(0, Random.Range(0, 360), 0) * Vector3.forward, new Vector3(0f, 0.05f, 0f), PropModels.Fit.Height);
            if (b != null) return b;
            return Small(p, new Color(0.85f, 0.75f, 0.3f), new Vector3(0.05f, 0.05f, 0.1f));
        }

        public static GameObject Small(Vector3 p, Color c, Vector3 size)
        { return Prop.Box(null, "Item", p + Vector3.up * size.y * 0.5f, Quaternion.Euler(0, Random.Range(0, 360), 0), size, c, false); }
        // 캔 음료: 모델이 있으면 가운데가 원점인 빈 오브젝트에 모델을 담아서 돌려줌 (크기 1)
        public static GameObject Can(Vector3 p)
        {
            if (PropModels.Has("DrinkCan"))
            {
                GameObject w = new GameObject("Can");
                w.transform.position = p + Vector3.up * 0.06f;
                GameObject m = PropModels.Place("DrinkCan", null, p, Vector3.forward, new Vector3(0f, 0.12f, 0f), PropModels.Fit.Height, "CanModel");
                if (m != null) { m.transform.SetParent(w.transform, true); return w; }
                Prop.Kill(w);
            }
            return Prop.Shape(PrimitiveType.Cylinder, null, "Can", p + Vector3.up * 0.06f, Quaternion.identity, new Vector3(0.065f, 0.06f, 0.065f), new Color(0.4f, 0.75f, 0.85f), false); }

        // 가구 (임시 상자)
        public static GameObject Furniture(Transform parent, string name, Vector3 feet, float yaw, Vector3 size, Color c)
        {
            GameObject g = Prop.Box(parent, name, feet + Vector3.up * size.y * 0.5f, Quaternion.Euler(0, yaw, 0), size, c, true);
            NavMeshObstacle ob = g.AddComponent<NavMeshObstacle>();
            ob.shape = NavMeshObstacleShape.Box;
            ob.size = Vector3.one; // 로컬 스케일이 크기
            ob.carving = true;
            // 받은 모델이 있는 가구는 모습만 모델로 (충돌은 상자 그대로)
            string model = FurnitureModel(name);
            if (model != null) PropModels.Dress(g, model, PropModels.Fit.Inside);
            return g;
        }

        static string FurnitureModel(string name)
        {
            switch (name)
            {
                case "Sofa": return "Sofa";
                case "PrincipalDesk": return "OfficeDesk";   // 받은 사무용 책상은 교장실에만
                default: return null;
            }
        }
    }

    // ==================================================================== 자판기
    public class VendingMachine : MonoBehaviour
    {
        public string id;
        const int Stock = 3;

        public static VendingMachine Create(string id, Vector3 feet, float yaw)
        {
            Quaternion r = Quaternion.Euler(0, yaw, 0);
            GameObject g = Looks.Furniture(null, "Vending_" + id, feet, yaw, new Vector3(0.92f, 1.85f, 0.9f), new Color(0.75f, 0.12f, 0.12f));
            Vector3 fwd = r * Vector3.forward;
            GameObject panel = Prop.Box(g.transform, "Panel", feet + Vector3.up * 1.15f + fwd * 0.46f, r, new Vector3(0.75f, 0.9f, 0.02f), new Color(0.2f, 0.25f, 0.3f), false);
            Prop.SetEmission(panel.GetComponent<Renderer>(), new Color(0.25f, 0.35f, 0.45f) * 0.6f);
            Prop.Box(g.transform, "Slot", feet + Vector3.up * 0.3f + fwd * 0.46f, r, new Vector3(0.6f, 0.15f, 0.02f), Color.black, false);
            // 받은 자판기 모델이 있으면 모습만 교체 (상자는 충돌용)
            if (PropModels.Dress(g, "VendingMachine", PropModels.Fit.Height) != null)
                foreach (Renderer rr in g.GetComponentsInChildren<Renderer>(true)) rr.enabled = false;
            VendingMachine v = g.AddComponent<VendingMachine>();
            v.id = id;
            Interactable it = g.AddComponent<Interactable>();
            it.oneShot = false;
            it.focusPoint = feet + Vector3.up * 1.1f + fwd * 0.4f;
            it.range = 2f;
            it.dynamicPrompt = () => "음료 자판기";
            it.onUse = v.Use;
            // 자판기 불빛은 주변을 아주 조금 밝힘
            GameObject lg = new GameObject("Glow"); lg.transform.SetParent(g.transform, false);
            lg.transform.position = feet + Vector3.up * 1.2f + fwd * 0.8f;
            Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 3f; l.intensity = 0.5f; l.color = new Color(0.6f, 0.75f, 1f); l.renderMode = LightRenderMode.ForcePixel;
            return v;
        }

        void Use()
        {
            GameState gs = GameState.Current;
            if (!gs.Flag("vending_enabled")) { GameUI.I.Toast("음료 자판기다. 지금은 목이 마르지 않다."); return; }
            int used = gs.Counter("vend:" + id);
            if (used >= Stock) { GameUI.I.Toast("'품절' 불이 깜빡인다."); return; }
            gs.SetCounter("vend:" + id, used + 1);
            gs.AddItem("drink");
            AudioDirector.I.PlayAt(AudioDirector.I.vending, transform.position, 0.8f);
            GameUI.I.Toast(Josa.Quote("캔 음료") + " 뽑았다. (" + (Stock - used - 1) + "개 남음)");
            Noise.Emit(transform.position, 7f, 1);
        }
    }

    // ==================================================================== 게시판 (기록 저장)
    public class SaveBoard : MonoBehaviour
    {
        public string id;
        public string place;
        public Vector3 spawnFeet;
        public float spawnYaw;
        public static readonly List<SaveBoard> All = new List<SaveBoard>();

        public static SaveBoard Create(string id, string place, Vector3 feet, float yaw)
        {
            Quaternion r = Quaternion.Euler(0, yaw, 0);
            Vector3 fwd = r * Vector3.forward;
            GameObject g = new GameObject("SaveBoard_" + id);
            g.transform.position = feet;
            // 벽에 거는 게시판: feet = 벽면 바로 앞 바닥, fwd = 벽에서 나오는 방향
            // 받은 코르크 게시판 모델의 원래 비율(약 2:1)대로 폭 1.3m × 높이 0.66m
            const float BW = 1.3f, BH = 0.66f, CY = 1.45f;
            Vector3 c = feet + Vector3.up * CY + fwd * 0.025f;
            GameObject frame = Prop.Box(g.transform, "Frame", c, r, new Vector3(BW, BH, 0.05f), new Color(0.42f, 0.3f, 0.2f), true);
            GameObject cork = Prop.Box(g.transform, "Cork", c + fwd * 0.026f, r, new Vector3(BW - 0.08f, BH - 0.08f, 0.005f), new Color(0.72f, 0.55f, 0.36f), false);
            Color[] pc = { new Color(0.95f, 0.93f, 0.8f), new Color(0.85f, 0.92f, 0.95f), new Color(0.95f, 0.85f, 0.85f) };
            bool model = PropModels.Has("CorkBoard");
            if (model)
            {
                GameObject m = PropModels.Place("CorkBoard", g.transform, feet + Vector3.up * (CY - BH * 0.5f) + fwd * 0.02f, fwd, new Vector3(BW, BH, 0f), PropModels.Fit.Inside, "CorkBoard_Model");
                if (m != null) { frame.GetComponent<Renderer>().enabled = false; cork.GetComponent<Renderer>().enabled = false; }
            }
            // 종이는 게시판 앞면에 붙임
            float front = 0.055f;
            Transform placed = g.transform.Find("CorkBoard_Model");
            Bounds bb;
            if (placed != null && PropModels.WorldBounds(placed.gameObject, out bb))
                front = Vector3.Dot(bb.center - feet, fwd) + Mathf.Abs(bb.extents.x * fwd.x) + Mathf.Abs(bb.extents.z * fwd.z) + 0.004f;
            for (int i = 0; i < 5; i++)
                Prop.Box(g.transform, "Paper", feet + Vector3.up * (CY - 0.12f + (i % 2) * 0.22f) + fwd * front + (r * Vector3.right) * (-0.44f + i * 0.22f), r * Quaternion.Euler(0, 0, Random.Range(-6f, 6f)), new Vector3(0.16f, 0.2f, 0.003f), pc[i % 3], false);
            if (!model) Prop.SetEmission(cork.GetComponent<Renderer>(), new Color(0.25f, 0.18f, 0.1f) * 0.25f);

            SaveBoard b = g.AddComponent<SaveBoard>();
            b.id = id; b.place = place;
            b.spawnFeet = feet + fwd * 1.0f;
            b.spawnYaw = Quaternion.LookRotation(-fwd).eulerAngles.y;
            All.Add(b);
            Interactable it = g.AddComponent<Interactable>();
            it.oneShot = false;
            it.focusPoint = feet + Vector3.up * 1.45f + fwd * 0.08f;
            it.range = 2.2f;
            it.dynamicPrompt = () => "게시판";
            it.onUse = b.Use;
            return b;
        }

        void Use()
        {
            GameState gs = GameState.Current;
            if (!(gs.Has("paper") && gs.Has("pen")))
            {
                GameUI.I.Toast("게시판이다. " + (gs.Has("paper") || gs.Has("pen") ? "종이와 펜이 둘 다 있어야 기록을 남길 수 있다." : "종이와 펜이 있으면 기록을 남길 수 있을 것 같다."));
                return;
            }
            ModalUI.I.Confirm("'종이'와 '펜'을 사용해\n지금까지의 기록을 남기시겠습니까?", yes =>
            {
                if (!yes) return;
                gs.RemoveItem("paper"); gs.RemoveItem("pen");
                MainDirector.I.SaveAt(id, place);
                AudioDirector.I.Play(AudioDirector.I.saveChime, 0.6f);
                GameUI.I.Toast("게시판에 쪽지를 꽂아 기록을 남겼다.  (" + place + ")");
            });
        }

        public static SaveBoard Find(string id)
        {
            foreach (SaveBoard b in All) if (b.id == id) return b;
            return null;
        }
    }

    // ==================================================================== 비상용품함 (손전등 / 배터리)
    public class EmergencyBox : MonoBehaviour
    {
        public string id;

        public static EmergencyBox Create(string id, Vector3 feet, float yaw)
        {
            Quaternion r = Quaternion.Euler(0, yaw, 0);
            Vector3 fwd = r * Vector3.forward;
            GameObject g = Looks.Furniture(null, "EmergencyBox_" + id, feet, yaw, new Vector3(0.5f, 1.1f, 0.3f), new Color(0.75f, 0.68f, 0.2f));
            GameObject sign = Prop.Box(g.transform, "Sign", feet + Vector3.up * 0.9f + fwd * 0.16f, r, new Vector3(0.36f, 0.12f, 0.01f), new Color(0.1f, 0.6f, 0.25f), false);
            Prop.SetEmission(sign.GetComponent<Renderer>(), new Color(0.1f, 0.8f, 0.3f) * 0.8f);
            EmergencyBox e = g.AddComponent<EmergencyBox>();
            e.id = id;
            Interactable it = g.AddComponent<Interactable>();
            it.oneShot = false;
            it.focusPoint = feet + Vector3.up * 0.8f + fwd * 0.2f;
            it.range = 2f;
            it.dynamicPrompt = () => "비상용품함 열기";
            it.onUse = e.Use;
            return e;
        }

        void Use()
        {
            GameState gs = GameState.Current;
            AudioDirector.I.PlayAt(AudioDirector.I.cabinet, transform.position, 0.5f, 1.3f);
            if (!gs.Has("flashlight"))
            {
                gs.AddItem("flashlight");
                gs.battery = 100f;
                gs.SetFlag("used_box:" + id);
                AudioDirector.I.Play(AudioDirector.I.pickup, 0.6f);
                GameUI.I.Toast(Josa.Quote("비상 손전등") + " 얻었다.  [F] 켜기/끄기");
                MainDirector.I.OnFlashlightFound();
                return;
            }
            if (gs.Flag("used_box:" + id)) { GameUI.I.Toast("비상용품함은 비어 있다."); return; }
            gs.SetFlag("used_box:" + id);
            gs.battery = 100f;
            AudioDirector.I.Play(AudioDirector.I.pickup, 0.6f);
            GameUI.I.Toast("비상용 손전등 배터리를 새것으로 바꿨다. (100%)");
        }
    }

    // ==================================================================== 방화 셔터 (1층 계단)
    public class Shutter : MonoBehaviour
    {
        readonly List<Transform> panels = new List<Transform>();
        readonly List<Vector3> down = new List<Vector3>();
        readonly List<NavMeshObstacle> obs = new List<NavMeshObstacle>();
        bool raising;

        public static Shutter Create(string name, int floor, Vector2 a, Vector2 b, Vector2 c)
        {
            GameObject g = new GameObject("Shutter_" + name);
            Shutter s = g.AddComponent<Shutter>();
            s.AddPanel(floor, a, b);
            if (c != Vector2.zero) s.AddPanel(floor, b, c);
            return s;
        }

        void AddPanel(int floor, Vector2 a, Vector2 b)
        {
            Vector3 pa = MapSpace.GF(floor, a.x, 0f, a.y), pb = MapSpace.GF(floor, b.x, 0f, b.y);
            Vector3 mid = (pa + pb) * 0.5f;
            Vector3 along = pb - pa;
            Quaternion r = Quaternion.LookRotation(Vector3.Cross(along.normalized, Vector3.up), Vector3.up);
            GameObject p = Prop.Box(transform, "Panel", mid + Vector3.up * 1.6f, r, new Vector3(along.magnitude, 3.2f, 0.08f), new Color(0.48f, 0.5f, 0.52f), true);
            for (int i = 0; i < 10; i++)
                Prop.Box(p.transform, "Rib", mid + Vector3.up * (0.2f + i * 0.31f), r, new Vector3(along.magnitude, 0.03f, 0.1f), new Color(0.38f, 0.4f, 0.42f), false);
            panels.Add(p.transform);
            down.Add(p.transform.position);
            NavMeshObstacle o = p.AddComponent<NavMeshObstacle>();
            o.shape = NavMeshObstacleShape.Box; o.size = Vector3.one; o.carving = true;
            obs.Add(o);
            Interactable it = p.AddComponent<Interactable>();
            it.oneShot = false; it.range = 2.5f; it.focusPoint = mid + Vector3.up * 1.2f;
            it.prompt = "방화 셔터";
            it.onUse = () => { AudioDirector.I.PlayAt(AudioDirector.I.rattle, mid + Vector3.up, 0.7f, 0.7f); GameUI.I.Toast("방화 셔터가 내려와 있다. 꿈쩍도 하지 않는다."); };
        }

        public void SetOpenInstant()
        {
            foreach (Transform p in panels) p.gameObject.SetActive(false);
        }

        public IEnumerator Raise()
        {
            if (raising) yield break;
            raising = true;
            foreach (NavMeshObstacle o in obs) o.enabled = false;
            foreach (Interactable it in GetComponentsInChildren<Interactable>()) it.available = false;
            float t = 0f;
            while (t < 3.5f)
            {
                t += Time.deltaTime;
                for (int i = 0; i < panels.Count; i++) panels[i].position = down[i] + Vector3.up * Mathf.SmoothStep(0f, 3.1f, t / 3.5f);
                yield return null;
            }
            SetOpenInstant();
        }
    }

    // ==================================================================== 빈 캔 던지기
    public class CanThrower : MonoBehaviour
    {
        public bool Holding { get; private set; }
        GameObject held;
        FirstPersonController fpc;

        void Awake() { fpc = GetComponent<FirstPersonController>(); }

        public void Equip()
        {
            if (Holding) return;
            Holding = true;
            held = Looks.Can(Vector3.zero);
            held.transform.SetParent(fpc.Cam.transform, false);
            // 왼손에 듦 (오른손은 손전등) — 화면 왼쪽 아래
            held.transform.localPosition = new Vector3(-0.25f, -0.2f, 0.42f);
            held.transform.localRotation = Quaternion.Euler(10, 0, -15);
            if (!PropModels.Has("DrinkCan")) held.transform.localScale = new Vector3(0.065f, 0.06f, 0.065f);
            foreach (Renderer r in held.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.gameObject.layer = Flashlight.HeldLayer;   // 어두운 곳에서도 손에 든 물건이 살짝 보이게
            }
            foreach (Collider c in held.GetComponentsInChildren<Collider>(true)) Destroy(c);
            Flashlight.EnsureHandFill(fpc.Cam);
            GameUI.I.ShowCanHint(true);
        }

        public void Unequip()
        {
            Holding = false;
            if (held != null) Destroy(held);
            GameUI.I.ShowCanHint(false);
        }

        void Update()
        {
            if (!Holding || UIState.ModalOpen || fpc.mode != FirstPersonController.Mode.Free) return;
            if (GameInput.MouseDown(1)) { Unequip(); return; }
            if (GameInput.ThrowDown())
            {
                if (!GameState.Current.RemoveItem("empty_can")) { Unequip(); return; }
                Transform cam = fpc.Cam.transform;
                GameObject c = Looks.Can(cam.position + cam.forward * 0.5f - cam.right * 0.18f);   // 왼손에서 던짐
                CapsuleCollider cc = c.AddComponent<CapsuleCollider>();
                if (PropModels.Has("DrinkCan")) { cc.radius = 0.033f; cc.height = 0.12f; }
                Rigidbody rb = c.AddComponent<Rigidbody>();
                rb.mass = 0.1f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.velocity = cam.forward * 11f + Vector3.up * 2.2f;
                rb.angularVelocity = Random.insideUnitSphere * 12f;
                c.AddComponent<ThrownCan>();
                AudioDirector.I.Play(AudioDirector.I.doorSlide, 0.15f, 2.5f);
                Unequip();
                if (GameState.Current.Has("empty_can")) Equip();
            }
        }
    }

    public class ThrownCan : MonoBehaviour
    {
        bool landed;
        void OnCollisionEnter(Collision c)
        {
            if (landed) return;
            landed = true;
            AudioDirector.I.PlayAt(AudioDirector.I.canClatter, transform.position, 1f, Random.Range(0.9f, 1.1f));
            Noise.Emit(transform.position, 22f, 2);
            Destroy(gameObject, 20f);
        }
    }
}
