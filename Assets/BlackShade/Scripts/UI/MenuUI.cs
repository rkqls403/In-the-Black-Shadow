using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlackShade
{
    // [Tab] 메뉴: 가방 / 문서 / 지도
    //   [Q]/[R] 위쪽 탭 바꾸기 · [A]/[D] 분류 바꾸기 · [W]/[S] 고르기 · [Enter] 사용/읽기 · [Tab]/[Esc] 닫기
    public class MenuUI : MonoBehaviour
    {
        public static MenuUI I { get; private set; }
        public bool IsOpen { get; private set; }

        static readonly string[] Tabs = { "가방", "문서", "지도" };
        Color panel = new Color(0.05f, 0.055f, 0.07f, 0.96f);
        Color text = new Color(0.93f, 0.92f, 0.89f);
        Color muted = new Color(0.58f, 0.59f, 0.62f);
        Color accent = new Color(0.96f, 0.78f, 0.5f);

        RectTransform root, content;
        int tab, bagCat, docCat, sel, mapFloor = 1;
        List<string> listIds = new List<string>();
        int openedFrame;

        void Awake()
        {
            I = this;
            GameObject cg = new GameObject("MenuUI_Canvas", typeof(RectTransform));
            cg.transform.SetParent(transform, false);
            Canvas c = cg.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 150;
            CanvasScaler sc = cg.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            cg.AddComponent<GraphicRaycaster>();
            root = (RectTransform)cg.transform;
            cg.SetActive(false);
        }

        public void Open(int startTab = 0)
        {
            if (IsOpen) return;
            IsOpen = true;
            tab = startTab; sel = 0;
            mapFloor = MainDirector.I != null ? MapSpace.FloorOf(MainDirector.I.Player.transform.position) : 1;
            root.gameObject.SetActive(true);
            UIState.Push();
            openedFrame = Time.frameCount;
            Rebuild();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ItemPreview.Hide();
            root.gameObject.SetActive(false);
            UIState.Pop();
        }

        void Update()
        {
            if (!IsOpen || Time.frameCount <= openedFrame) return;
            if (ModalUI.I != null && ModalUI.I.IsOpen) return; // 위에 확인창이 떠 있으면 대기
            if (GameInput.KeyDown(KeyCode.Tab) || GameInput.KeyDown(KeyCode.I) || GameInput.KeyDown(KeyCode.Escape)) { Close(); return; }
            bool dirty = false;
            if (GameInput.TabPrevDown()) { tab = (tab + 2) % 3; sel = 0; dirty = true; }
            if (GameInput.TabNextDown()) { tab = (tab + 1) % 3; sel = 0; dirty = true; }
            if (GameInput.LeftDown()) { Sub(-1); dirty = true; }
            if (GameInput.RightDown()) { Sub(1); dirty = true; }
            if (listIds.Count > 0)
            {
                if (GameInput.UpDown()) { sel = (sel + listIds.Count - 1) % listIds.Count; dirty = true; }
                if (GameInput.DownDown()) { sel = (sel + 1) % listIds.Count; dirty = true; }
                if (GameInput.KeyDown(KeyCode.Return) || GameInput.KeyDown(KeyCode.Space) || GameInput.KeyDown(KeyCode.E)) Activate();
            }
            if (dirty) Rebuild();
        }

        void Sub(int d)
        {
            sel = 0;
            if (tab == 0) bagCat = (bagCat + 4 + d) % 4;
            else if (tab == 1) docCat = (docCat + 2 + d) % 2;
            else mapFloor = Mathf.Clamp(mapFloor + d, 1, 5);
        }

        void Activate()
        {
            if (sel < 0 || sel >= listIds.Count) return;
            if (tab == 0) UseItem(listIds[sel]);
            else if (tab == 1) ModalUI.I.ShowDoc(listIds[sel]);
        }

        // ================================================================ 화면 다시 그리기
        void Rebuild()
        {
            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
            Image dim = UIKit.NewImage("Dim", root, new Color(0, 0, 0, 0.7f));
            dim.raycastTarget = true;
            UIKit.Stretch(dim.rectTransform);
            Image bg = UIKit.NewImage("Panel", root, panel, UIKit.Rounded, true);
            UIKit.Anchor(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640, 940));
            content = bg.rectTransform;

            // 위쪽 탭
            for (int i = 0; i < Tabs.Length; i++)
            {
                int k = i;
                Button b = UIKit.NewButton("Tab", content, Tabs[i], 30, () => { tab = k; sel = 0; Rebuild(); });
                Place(b.transform, new Vector2(-170 + i * 170, 420), new Vector2(150, 56));
                ((Image)b.targetGraphic).color = i == tab ? new Color(accent.r, accent.g, accent.b, 0.4f) : new Color(1, 1, 1, 0.06f);
            }
            Txt(content, "[Q] ◀", 20, muted, TextAnchor.MiddleRight, new Vector2(-330, 420), new Vector2(120, 40));
            Txt(content, "▶ [R]", 20, muted, TextAnchor.MiddleLeft, new Vector2(330, 420), new Vector2(120, 40));

            // 상태 (체력 / 배터리)
            GameState gs = GameState.Current;
            string status = "체력 " + Mathf.CeilToInt(gs.hp) + " / " + Mathf.CeilToInt(gs.maxHp) + (gs.Has("flashlight") ? "      손전등 " + Mathf.CeilToInt(gs.battery) + "%" : "");
            Txt(content, status, 22, muted, TextAnchor.MiddleRight, new Vector2(560, 420), new Vector2(420, 40));
            Txt(content, "[A/D] 분류   [W/S] 고르기   [Enter] " + (tab == 0 ? "사용" : tab == 1 ? "읽기" : "") + "   [Tab] 닫기", 19, muted, TextAnchor.MiddleCenter, new Vector2(0, -440), new Vector2(1500, 34));

            Image line = UIKit.NewImage("Line", content, new Color(1, 1, 1, 0.08f));
            Place(line.transform, new Vector2(0, 380), new Vector2(1560, 2));

            listIds.Clear();
            ItemPreview.Hide();
            if (tab == 0) BuildBag();
            else if (tab == 1) BuildDocs();
            else BuildMap();
        }

        // ---------------------------------------------------------------- 가방
        void BuildBag()
        {
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                Button b = UIKit.NewButton("Cat", content, ItemDB.CategoryNames[i], 24, () => { bagCat = k; sel = 0; Rebuild(); });
                Place(b.transform, new Vector2(-690 + i * 130, 335), new Vector2(118, 46));
                ((Image)b.targetGraphic).color = i == bagCat ? new Color(accent.r, accent.g, accent.b, 0.3f) : new Color(1, 1, 1, 0.05f);
            }
            List<string> ids = GameState.Current.ItemsIn((ItemCategory)bagCat);
            listIds.AddRange(ids);
            if (ids.Count == 0) Txt(content, "비어 있다.", 26, muted, TextAnchor.MiddleLeft, new Vector2(-560, 260), new Vector2(400, 40));
            sel = Mathf.Clamp(sel, 0, Mathf.Max(0, ids.Count - 1));
            for (int i = 0; i < ids.Count; i++)
            {
                int k = i;
                ItemDef d = ItemDB.Get(ids[i]);
                int n = GameState.Current.Count(ids[i]);
                string label = (d != null ? d.name : ids[i]) + (n > 1 ? "  × " + n : "");
                if (ids[i] == "score_piece") label = d.name + "  (" + n + " / 3)";
                Button b = UIKit.NewButton("Item", content, label, 24, () => { if (sel == k) Activate(); else { sel = k; Rebuild(); } });
                Place(b.transform, new Vector2(-480, 260 - i * 62), new Vector2(560, 54));
                Text t = b.GetComponentInChildren<Text>(); t.alignment = TextAnchor.MiddleLeft; t.rectTransform.offsetMin = new Vector2(64, 0);
                ((Image)b.targetGraphic).color = i == sel ? new Color(accent.r, accent.g, accent.b, 0.3f) : new Color(1, 1, 1, 0.05f);
                Image icon = UIKit.NewImage("Icon", b.transform, d != null ? d.color : Color.grey, UIKit.Rounded, true);
                UIKit.Anchor(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(34, 34));
            }

            // 오른쪽 설명
            if (ids.Count > 0)
            {
                ItemDef d = ItemDB.Get(ids[sel]);
                Image card = UIKit.NewImage("Card", content, new Color(1, 1, 1, 0.04f), UIKit.Rounded, true);
                Place(card.transform, new Vector2(330, 20), new Vector2(860, 620));
                // 아이템 3D 모델 (빙글빙글 돎) — 아래에 이름과 설명
                ItemPreview.Show(ids[sel]);
                GameObject pv = new GameObject("Preview", typeof(RectTransform));
                pv.transform.SetParent(card.transform, false);
                RawImage raw = pv.AddComponent<RawImage>();
                raw.texture = ItemPreview.Texture;
                raw.raycastTarget = false;
                Place(pv.transform, new Vector2(0, 150), new Vector2(290, 290));
                Txt(card.transform, d.name, 34, text, TextAnchor.MiddleCenter, new Vector2(0, -22), new Vector2(800, 50), FontStyle.Bold);
                Txt(card.transform, ItemDB.CategoryNames[(int)d.category] + " 아이템", 20, muted, TextAnchor.MiddleCenter, new Vector2(0, -58), new Vector2(800, 30));
                Txt(card.transform, d.desc, 24, text, TextAnchor.UpperCenter, new Vector2(0, -150), new Vector2(760, 150));
                string verb = d.use == ItemUse.ViewScore ? "보기" : "사용";
                Button use = UIKit.NewButton("Use", card.transform, verb, 26, Activate);
                Place(use.transform, new Vector2(0, -265), new Vector2(240, 56));
            }
        }

        void UseItem(string id)
        {
            ItemDef d = ItemDB.Get(id);
            if (d == null) return;
            if (d.use == ItemUse.ViewScore) { ModalUI.I.ShowScore(MainDirector.I.ScorePieces); return; }
            if (d.use == ItemUse.OnlyInPlace || d.use == ItemUse.None)
            {
                ModalUI.I.Alert(d.placeHint ?? "지금은 쓸 곳이 없다.");
                return;
            }
            ModalUI.I.Confirm(Josa.Quote(d.name) + " 사용하시겠습니까?", yes =>
            {
                if (!yes) return;
                bool close = MainDirector.I.UseItem(id);
                if (close) Close(); else Rebuild();
            });
        }

        // ---------------------------------------------------------------- 문서
        void BuildDocs()
        {
            for (int i = 0; i < 2; i++)
            {
                int k = i;
                Button b = UIKit.NewButton("Cat", content, DocDB.CategoryNames[i], 24, () => { docCat = k; sel = 0; Rebuild(); });
                Place(b.transform, new Vector2(-690 + i * 130, 335), new Vector2(118, 46));
                ((Image)b.targetGraphic).color = i == docCat ? new Color(accent.r, accent.g, accent.b, 0.3f) : new Color(1, 1, 1, 0.05f);
            }
            List<string> ids = GameState.Current.DocsIn((DocCategory)docCat);
            listIds.AddRange(ids);
            if (ids.Count == 0) Txt(content, "아직 발견한 " + DocDB.CategoryNames[docCat] + "가 없다.", 26, muted, TextAnchor.MiddleLeft, new Vector2(-480, 260), new Vector2(560, 40));
            sel = Mathf.Clamp(sel, 0, Mathf.Max(0, ids.Count - 1));
            for (int i = 0; i < ids.Count; i++)
            {
                int k = i;
                DocDef d = DocDB.Get(ids[i]);
                Button b = UIKit.NewButton("Doc", content, d.title, 23, () => { if (sel == k) Activate(); else { sel = k; Rebuild(); } });
                Place(b.transform, new Vector2(-480, 260 - i * 58), new Vector2(560, 50));
                Text t = b.GetComponentInChildren<Text>(); t.alignment = TextAnchor.MiddleLeft; t.rectTransform.offsetMin = new Vector2(24, 0);
                ((Image)b.targetGraphic).color = i == sel ? new Color(accent.r, accent.g, accent.b, 0.3f) : new Color(1, 1, 1, 0.05f);
            }
            if (ids.Count > 0)
            {
                DocDef d = DocDB.Get(ids[sel]);
                Image card = UIKit.NewImage("Card", content, new Color(1, 1, 1, 0.04f), UIKit.Rounded, true);
                Place(card.transform, new Vector2(330, 20), new Vector2(860, 620));
                Txt(card.transform, d.title, 30, text, TextAnchor.MiddleCenter, new Vector2(0, 250), new Vector2(800, 50), FontStyle.Bold);
                Txt(card.transform, "발견한 곳 : " + d.where, 20, muted, TextAnchor.MiddleCenter, new Vector2(0, 205), new Vector2(800, 30));
                string preview = d.body.Length > 240 ? d.body.Substring(0, 240) + " …" : d.body;
                Txt(card.transform, preview, 21, new Color(text.r, text.g, text.b, 0.8f), TextAnchor.UpperLeft, new Vector2(0, -20), new Vector2(760, 380));
                Button read = UIKit.NewButton("Read", card.transform, "읽기", 26, Activate);
                Place(read.transform, new Vector2(0, -250), new Vector2(240, 60));
            }
        }

        // ---------------------------------------------------------------- 지도 (임시 구조도)
        void BuildMap()
        {
            for (int f = 1; f <= 5; f++)
            {
                int k = f;
                Button b = UIKit.NewButton("Floor", content, f + "층", 24, () => { mapFloor = k; Rebuild(); });
                Place(b.transform, new Vector2(-690 + (f - 1) * 110, 335), new Vector2(100, 46));
                ((Image)b.targetGraphic).color = f == mapFloor ? new Color(accent.r, accent.g, accent.b, 0.3f) : new Color(1, 1, 1, 0.05f);
            }
            string[] floorName = { "", "1층 · 행정동", "2층 · 1학년", "3층 · 2학년", "4층 · 3학년", "5층 · 특별실 / 강당" };
            Txt(content, floorName[mapFloor] + "   (임시 구조도)", 24, muted, TextAnchor.MiddleRight, new Vector2(560, 335), new Vector2(600, 40));

            RectTransform area = UIKit.NewRect("Map", content);
            Place(area, new Vector2(0, -40), new Vector2(1500, 700));
            float s = Mathf.Min(1480f / (MapSpace.E + 4f), 680f / 50f);
            System.Func<float, float, Vector2> M = (gx, gz) => new Vector2((gx - MapSpace.E * 0.5f) * s, -(gz - 19.5f) * s);

            FirstPersonController p = MainDirector.I.Player;
            RoomSpec here = MapData.RoomAt(p.transform.position);
            foreach (RoomSpec h in MapData.Halls(mapFloor)) DrawRoom(area, h, M, new Color(0.25f, 0.26f, 0.28f, 0.9f), h.name, 16);
            foreach (RoomSpec r in MapData.Rooms)
            {
                if (r.floor != mapFloor) continue;
                bool cur = r == here;
                bool open = true;
                foreach (DoorSpec ds in r.doors) if (ds.lockType != DoorLock.Open && !GameState.Current.Flag("unlocked:" + ds.id)) open = false;
                Color c = cur ? new Color(accent.r, accent.g, accent.b, 0.55f) : (open ? new Color(0.36f, 0.38f, 0.42f, 0.95f) : new Color(0.2f, 0.2f, 0.22f, 0.95f));
                DrawRoom(area, r, M, c, r.name + (open ? "" : "\n(잠김)"), 17);
            }
            foreach (SaveBoard b in SaveBoard.All)
            {
                if (MapSpace.FloorOf(b.transform.position) != mapFloor) continue;
                Vector2 g = MapSpace.ToGL(b.transform.position);
                Image m = UIKit.NewImage("Board", area, new Color(0.75f, 0.55f, 0.36f), UIKit.Rounded, true);
                Place(m.transform, M(g.x, g.y), new Vector2(18, 18));
            }
            if (MapSpace.FloorOf(p.transform.position) == mapFloor)
            {
                Vector2 g = MapSpace.ToGL(p.transform.position);
                Image me = UIKit.NewImage("Me", area, new Color(0.95f, 0.3f, 0.25f), UIKit.Circle);
                Place(me.transform, M(g.x, g.y), new Vector2(22, 22));
                Txt(area, "나", 18, new Color(0.95f, 0.4f, 0.35f), TextAnchor.MiddleCenter, M(g.x, g.y) + new Vector2(0, 22), new Vector2(40, 24), FontStyle.Bold);
            }
            Txt(content, "■ 게시판 (기록 저장)    ● 현재 위치    어두운 방 = 잠긴 방", 19, muted, TextAnchor.MiddleLeft, new Vector2(-260, -405), new Vector2(1000, 30));
        }

        void DrawRoom(RectTransform area, RoomSpec r, System.Func<float, float, Vector2> M, Color c, string name, int fs)
        {
            Vector2 a = M(r.x0, r.z0), b = M(r.x1, r.z1);
            Vector2 center = (a + b) * 0.5f;
            Vector2 size = new Vector2(Mathf.Abs(b.x - a.x) - 3f, Mathf.Abs(b.y - a.y) - 3f);
            Image img = UIKit.NewImage("Room", area, c);
            Place(img.transform, center, size);
            if (!string.IsNullOrEmpty(name) && size.x > 40f && size.y > 24f)
            {
                Text t = Txt(img.transform, name, fs, text, TextAnchor.MiddleCenter, Vector2.zero, size);
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 10; t.resizeTextMaxSize = fs;
            }
        }

        // ---------------------------------------------------------------- 도우미
        static void Place(Transform t, Vector2 pos, Vector2 size)
        {
            RectTransform r = (RectTransform)t;
            UIKit.Anchor(r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        }

        static Text Txt(Transform parent, string s, int size, Color c, TextAnchor a, Vector2 pos, Vector2 sz, FontStyle st = FontStyle.Normal)
        {
            Text t = UIKit.NewText("Text", parent, size, c, a, st);
            Place(t.transform, pos, sz);
            t.text = s;
            return t;
        }
    }
}
