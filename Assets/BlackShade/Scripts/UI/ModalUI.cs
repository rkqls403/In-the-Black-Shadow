using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackShade
{
    // 게임을 멈추고 띄우는 창들: 확인 팝업, 번호 자물쇠, 선택지, 문서 읽기, 악보 보기,
    // 사망 화면, 엔딩, 타이틀, 일시정지.  (전부 임시 디자인. 색/크기는 위쪽 값으로 조절)
    public class ModalUI : MonoBehaviour
    {
        public static ModalUI I { get; private set; }

        public Color panelColor = new Color(0.06f, 0.065f, 0.08f, 0.97f);
        public Color textColor = new Color(0.93f, 0.92f, 0.89f);
        public Color accent = new Color(0.96f, 0.78f, 0.5f);
        public Color muted = new Color(0.6f, 0.61f, 0.64f);

        Canvas canvas;
        RectTransform root;
        // 열린 창 스택 (위에 있는 것만 키보드를 받음)
        class Win { public GameObject go; public List<Button> buttons = new List<Button>(); public int sel; public System.Action cancel; public System.Action<Win> custom; public bool vertical = true; public int openedFrame; }
        readonly List<Win> stack = new List<Win>();

        void Awake()
        {
            I = this;
            GameObject cg = new GameObject("ModalUI_Canvas", typeof(RectTransform));
            cg.transform.SetParent(transform, false);
            canvas = cg.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler sc = cg.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            cg.AddComponent<GraphicRaycaster>();
            root = (RectTransform)cg.transform;
            EnsureEventSystem();
        }

        public static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            System.Type t = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) es.AddComponent(t); else es.AddComponent<StandaloneInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        public bool IsOpen { get { return stack.Count > 0; } }

        // ================================================================ 공통
        Win Open(string name, out RectTransform panel, Vector2 size, bool dim = true, bool pause = true)
        {
            Win w = new Win();
            w.openedFrame = Time.frameCount;
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root, false);
            UIKit.Stretch((RectTransform)go.transform);
            w.go = go;
            if (dim)
            {
                Image d = UIKit.NewImage("Dim", go.transform, new Color(0, 0, 0, 0.6f));
                d.raycastTarget = true;
                UIKit.Stretch(d.rectTransform);
            }
            Image p = UIKit.NewImage("Panel", go.transform, panelColor, UIKit.Rounded, true);
            UIKit.Anchor(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            panel = p.rectTransform;
            stack.Add(w);
            UIState.pauseGame = pause;
            UIState.Push();
            UIState.pauseGame = true;
            return w;
        }

        void Close(Win w)
        {
            if (!stack.Contains(w)) return;
            stack.Remove(w);
            Destroy(w.go);
            UIState.Pop();
        }

        public void CloseAll()
        {
            while (stack.Count > 0) Close(stack[stack.Count - 1]);
        }

        Text Label(Transform parent, string text, int size, Color c, TextAnchor a, Vector2 pos, Vector2 sz, FontStyle st = FontStyle.Normal)
        {
            Text t = UIKit.NewText("Text", parent, size, c, a, st);
            UIKit.Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, sz);
            t.text = text;
            return t;
        }

        Button Btn(Win w, Transform parent, string label, Vector2 pos, Vector2 size, System.Action onClick)
        {
            Button b = UIKit.NewButton("Button", parent, label, 26, null);
            UIKit.Anchor((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            b.onClick.AddListener(() => onClick());
            w.buttons.Add(b);
            return b;
        }

        void Update()
        {
            if (stack.Count == 0) return;
            Win w = stack[stack.Count - 1];
            if (Time.frameCount <= w.openedFrame) return; // 창을 연 키 입력이 그대로 눌리지 않게
            if (w.custom != null) { w.custom(w); return; }
            if (w.buttons.Count > 0)
            {
                bool prev = w.vertical ? GameInput.UpDown() : GameInput.LeftDown();
                bool next = w.vertical ? GameInput.DownDown() : GameInput.RightDown();
                if (prev) w.sel = (w.sel + w.buttons.Count - 1) % w.buttons.Count;
                if (next) w.sel = (w.sel + 1) % w.buttons.Count;
                for (int i = 0; i < w.buttons.Count; i++)
                {
                    Image img = w.buttons[i].targetGraphic as Image;
                    if (img != null) img.color = i == w.sel ? new Color(accent.r, accent.g, accent.b, 0.35f) : new Color(1f, 1f, 1f, 0.08f);
                }
                if (GameInput.KeyDown(KeyCode.Return) || GameInput.KeyDown(KeyCode.KeypadEnter) || GameInput.KeyDown(KeyCode.Space) || GameInput.KeyDown(KeyCode.E))
                {
                    w.buttons[w.sel].onClick.Invoke();
                    return;
                }
            }
            if (GameInput.CancelDown() && w.cancel != null) w.cancel();
        }

        // ================================================================ 확인 팝업
        // 예: Confirm("'행정실 열쇠'를 사용하시겠습니까?", yes => { ... })
        public void Confirm(string question, System.Action<bool> result)
        {
            RectTransform p;
            Win w = Open("Confirm", out p, new Vector2(820, 300));
            w.vertical = false;
            Label(p, question, 30, textColor, TextAnchor.MiddleCenter, new Vector2(0, 50), new Vector2(740, 150));
            System.Action<bool> done = v => { Close(w); if (result != null) result(v); };
            Btn(w, p, "예", new Vector2(-130, -90), new Vector2(200, 64), () => done(true));
            Btn(w, p, "아니오", new Vector2(130, -90), new Vector2(200, 64), () => done(false));
            w.cancel = () => done(false);
        }

        // 그냥 알림 (확인 버튼 하나)
        public void Alert(string text, System.Action closed = null)
        {
            RectTransform p;
            Win w = Open("Alert", out p, new Vector2(860, 320));
            Label(p, text, 28, textColor, TextAnchor.MiddleCenter, new Vector2(0, 40), new Vector2(780, 200));
            System.Action done = () => { Close(w); if (closed != null) closed(); };
            Btn(w, p, "확인", new Vector2(0, -105), new Vector2(220, 60), done);
            w.cancel = done;
        }

        // ================================================================ 선택지
        public void Choice(string title, string[] options, System.Action<int> result)
        {
            RectTransform p;
            float h = 170 + options.Length * 78;
            Win w = Open("Choice", out p, new Vector2(820, h));
            Label(p, title, 28, textColor, TextAnchor.MiddleCenter, new Vector2(0, h * 0.5f - 70), new Vector2(740, 100));
            System.Action<int> done = v => { Close(w); if (result != null) result(v); };
            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                Btn(w, p, options[i], new Vector2(0, h * 0.5f - 160 - i * 78), new Vector2(620, 64), () => done(idx));
            }
            w.cancel = () => done(-1);
        }

        // ================================================================ 번호 자물쇠
        public void Keypad(int digits, string title, System.Action<string> result)
        {
            RectTransform p;
            Win w = Open("Keypad", out p, new Vector2(760, 470));
            Label(p, title, 26, textColor, TextAnchor.MiddleCenter, new Vector2(0, 175), new Vector2(700, 80));
            Label(p, "숫자 키 입력 · [A/D] 칸 이동 · [W/S] 숫자 바꾸기 · [Enter] 확인 · [Esc] 그만두기", 18, muted, TextAnchor.MiddleCenter, new Vector2(0, -205), new Vector2(720, 30));
            int[] val = new int[digits];
            int cur = 0;
            Text[] slots = new Text[digits];
            Image[] frames = new Image[digits];
            float startX = -(digits - 1) * 60f;
            for (int i = 0; i < digits; i++)
            {
                int k = i;
                Image f = UIKit.NewImage("Slot", p, new Color(1, 1, 1, 0.08f), UIKit.Rounded, true);
                UIKit.Anchor(f.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(startX + i * 120f, 20), new Vector2(96, 130));
                frames[i] = f;
                slots[i] = Label(f.transform, "0", 64, textColor, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(96, 130), FontStyle.Bold);
                Button up = UIKit.NewButton("Up", p, "▲", 22, () => { val[k] = (val[k] + 1) % 10; cur = k; });
                UIKit.Anchor((RectTransform)up.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(startX + i * 120f, 110), new Vector2(80, 38));
                Button dn = UIKit.NewButton("Down", p, "▼", 22, () => { val[k] = (val[k] + 9) % 10; cur = k; });
                UIKit.Anchor((RectTransform)dn.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(startX + i * 120f, -70), new Vector2(80, 38));
            }
            System.Action<string> done = v => { Close(w); if (result != null) result(v); };
            System.Func<string> code = () => { string s = ""; foreach (int v in val) s += v; return s; };
            Button ok = UIKit.NewButton("OK", p, "확인", 26, () => done(code()));
            UIKit.Anchor((RectTransform)ok.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110, -140), new Vector2(190, 58));
            Button no = UIKit.NewButton("Cancel", p, "그만두기", 26, () => done(null));
            UIKit.Anchor((RectTransform)no.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(110, -140), new Vector2(190, 58));

            w.custom = win =>
            {
                int d = GameInput.DigitDown();
                if (d >= 0) { val[cur] = d; cur = Mathf.Min(digits - 1, cur + 1); }
                if (GameInput.LeftDown()) cur = Mathf.Max(0, cur - 1);
                if (GameInput.RightDown()) cur = Mathf.Min(digits - 1, cur + 1);
                if (GameInput.UpDown()) val[cur] = (val[cur] + 1) % 10;
                if (GameInput.DownDown()) val[cur] = (val[cur] + 9) % 10;
                if (GameInput.KeyDown(KeyCode.Backspace)) { cur = Mathf.Max(0, cur - 1); }
                for (int i = 0; i < digits; i++)
                {
                    slots[i].text = val[i].ToString();
                    frames[i].color = i == cur ? new Color(accent.r, accent.g, accent.b, 0.3f) : new Color(1, 1, 1, 0.08f);
                }
                if (GameInput.KeyDown(KeyCode.Return) || GameInput.KeyDown(KeyCode.KeypadEnter)) done(code());
                else if (GameInput.KeyDown(KeyCode.Escape)) done(null);
            };
        }

        // ================================================================ 문서 읽기
        public void ShowDoc(string docId, System.Action closed = null)
        {
            DocDef d = DocDB.Get(docId);
            if (d == null) { if (closed != null) closed(); return; }
            RectTransform p;
            Win w = Open("Doc", out p, new Vector2(1100, 960));
            Color paper, ink; FontStyle fs = FontStyle.Normal; int size = 28;
            switch (d.style)
            {
                case DocStyle.Typed: paper = new Color(0.86f, 0.82f, 0.7f); ink = new Color(0.16f, 0.14f, 0.12f); size = 26; break;
                case DocStyle.Handwritten: paper = new Color(0.93f, 0.92f, 0.88f); ink = new Color(0.15f, 0.2f, 0.42f); fs = FontStyle.Italic; break;
                case DocStyle.Diary: paper = new Color(0.95f, 0.88f, 0.86f); ink = new Color(0.32f, 0.2f, 0.25f); fs = FontStyle.Italic; break;
                case DocStyle.Book: paper = new Color(0.8f, 0.72f, 0.55f); ink = new Color(0.2f, 0.15f, 0.1f); size = 26; break;
                case DocStyle.Chat: paper = new Color(0.95f, 0.95f, 0.9f); ink = new Color(0.12f, 0.12f, 0.15f); break;
                default: paper = new Color(0.16f, 0.16f, 0.17f); ink = new Color(0.86f, 0.84f, 0.8f); break;
            }
            ((Image)p.GetComponent<Image>()).color = paper;
            p.localRotation = Quaternion.Euler(0, 0, Random.Range(-0.8f, 0.8f));
            Label(p, d.title, 34, ink, TextAnchor.MiddleCenter, new Vector2(0, 425), new Vector2(1000, 60), FontStyle.Bold);
            Label(p, d.where, 18, new Color(ink.r, ink.g, ink.b, 0.55f), TextAnchor.MiddleCenter, new Vector2(0, 385), new Vector2(1000, 30));
            Text body = Label(p, d.body, size, ink, TextAnchor.UpperLeft, new Vector2(0, -20), new Vector2(960, 760), fs);
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 16;
            body.resizeTextMaxSize = size;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            Text hint = Label(w.go.transform, "[E] / [Esc] 닫기", 22, muted, TextAnchor.MiddleCenter, new Vector2(0, -510), new Vector2(400, 40));
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            System.Action done = () => { Close(w); if (closed != null) closed(); };
            w.cancel = done;
            float opened = Time.unscaledTime;
            w.custom = win => { if (Time.unscaledTime - opened > 0.25f && (GameInput.CloseDown() || GameInput.MouseDown(0))) done(); };
        }

        // ================================================================ 악보 보기
        public void ShowScore(int pieces, System.Action closed = null)
        {
            RectTransform p;
            Win w = Open("Score", out p, new Vector2(1100, 820));
            ((Image)p.GetComponent<Image>()).color = new Color(0.88f, 0.84f, 0.72f);
            Color ink = new Color(0.18f, 0.15f, 0.12f);
            Label(p, "졸업가 악보  (" + Mathf.Min(pieces, 3) + " / 3)", 34, ink, TextAnchor.MiddleCenter, new Vector2(0, 350), new Vector2(1000, 60), FontStyle.Bold);
            for (int i = 0; i < 3; i++)
            {
                bool have = i < pieces;
                Image strip = UIKit.NewImage("Piece", p, have ? new Color(0.95f, 0.92f, 0.82f) : new Color(0.5f, 0.47f, 0.4f, 0.25f), UIKit.Rounded, true);
                UIKit.Anchor(strip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 180 - i * 200), new Vector2(980, 180));
                strip.rectTransform.localRotation = Quaternion.Euler(0, 0, have ? Random.Range(-1.2f, 1.2f) : 0f);
                // 오선
                for (int l = 0; l < 5; l++)
                {
                    Image line = UIKit.NewImage("Staff", strip.transform, new Color(ink.r, ink.g, ink.b, have ? 0.35f : 0.08f));
                    UIKit.Anchor(line.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -22 - l * 9), new Vector2(-60, 2));
                }
                Label(strip.transform, have ? SongData.PieceLyrics[i] : "(찢겨 나간 부분)", 26, have ? ink : new Color(ink.r, ink.g, ink.b, 0.35f), TextAnchor.MiddleCenter, new Vector2(0, -30), new Vector2(940, 110), have ? FontStyle.Normal : FontStyle.Italic);
            }
            Label(w.go.transform, "[E] / [Esc] 닫기", 22, muted, TextAnchor.MiddleCenter, new Vector2(0, -450), new Vector2(400, 40));
            System.Action done = () => { Close(w); if (closed != null) closed(); };
            w.cancel = done;
            float opened = Time.unscaledTime;
            w.custom = win => { if (Time.unscaledTime - opened > 0.25f && (GameInput.CloseDown() || GameInput.MouseDown(0))) done(); };
        }

        // ================================================================ 사망 화면
        public void Death(System.Action retry, System.Action toTitle)
        {
            RectTransform p;
            Win w = Open("Death", out p, new Vector2(900, 420), false, false);
            ((Image)p.GetComponent<Image>()).color = new Color(0, 0, 0, 0f);
            Image bg = UIKit.NewImage("Black", w.go.transform, new Color(0, 0, 0, 0.92f));
            UIKit.Stretch(bg.rectTransform); bg.transform.SetAsFirstSibling(); bg.raycastTarget = true;
            Label(p, "GAME OVER", 64, new Color(0.75f, 0.12f, 0.1f), TextAnchor.MiddleCenter, new Vector2(0, 120), new Vector2(860, 90), FontStyle.Bold);
            Label(p, "졸업식에 참석하지 않은 학생이 확인되었습니다.", 26, muted, TextAnchor.MiddleCenter, new Vector2(0, 40), new Vector2(860, 40), FontStyle.Italic);
            Btn(w, p, SaveSystem.HasSave ? "마지막 기록에서 다시 하기" : "처음부터 다시 하기", new Vector2(0, -60), new Vector2(460, 64), () => { Close(w); retry(); });
            Btn(w, p, "타이틀로", new Vector2(0, -140), new Vector2(460, 64), () => { Close(w); toTitle(); });
        }

        // ================================================================ 엔딩
        public void Ending(string title, string subtitle, string body, Color titleColor, string[] buttons, System.Action<int> picked)
        {
            RectTransform p;
            Win w = Open("Ending", out p, new Vector2(1100, 640), false, false);
            ((Image)p.GetComponent<Image>()).color = new Color(0, 0, 0, 0.75f);
            Label(p, subtitle, 26, muted, TextAnchor.MiddleCenter, new Vector2(0, 230), new Vector2(1000, 40));
            Label(p, title, 60, titleColor, TextAnchor.MiddleCenter, new Vector2(0, 160), new Vector2(1000, 90), FontStyle.Bold);
            Label(p, body, 25, textColor, TextAnchor.MiddleCenter, new Vector2(0, 10), new Vector2(980, 200));
            for (int i = 0; i < buttons.Length; i++)
            {
                int k = i;
                Btn(w, p, buttons[i], new Vector2(0, -150 - i * 76), new Vector2(460, 62), () => { Close(w); picked(k); });
            }
        }

        // ================================================================ 타이틀
        public void Title(bool hasSave, string saveInfo, System.Action newGame, System.Action cont, System.Action quit)
        {
            RectTransform p;
            Win w = Open("Title", out p, new Vector2(10, 10), false, false);
            Image bg = UIKit.NewImage("Black", w.go.transform, Color.black);
            UIKit.Stretch(bg.rectTransform); bg.transform.SetAsFirstSibling(); bg.raycastTarget = true;
            Text t = Label(w.go.transform, "검은 그늘 속에서", 84, new Color(0.88f, 0.86f, 0.82f), TextAnchor.MiddleCenter, new Vector2(0, 230), new Vector2(1400, 130), FontStyle.Bold);
            UIKit.AddShadow(t, 0.9f, 4f);
            Label(w.go.transform, "졸업식 전날 밤", 28, muted, TextAnchor.MiddleCenter, new Vector2(0, 140), new Vector2(800, 40), FontStyle.Italic);
            Btn(w, w.go.transform, "새로 시작", new Vector2(0, -20), new Vector2(420, 66), () => { Close(w); newGame(); });
            if (hasSave) Btn(w, w.go.transform, "이어하기", new Vector2(0, -100), new Vector2(420, 66), () => { Close(w); cont(); });
            Btn(w, w.go.transform, "종료", new Vector2(0, hasSave ? -180 : -100), new Vector2(420, 66), () => quit());
            if (hasSave) Label(w.go.transform, "마지막 기록 : " + saveInfo, 20, muted, TextAnchor.MiddleCenter, new Vector2(0, -270), new Vector2(900, 30));
            Label(w.go.transform, "[W/S] 고르기  [Enter] 결정        (개발용: F9 = 본편부터 바로 시작)", 18, new Color(muted.r, muted.g, muted.b, 0.6f), TextAnchor.MiddleCenter, new Vector2(0, -470), new Vector2(1200, 30));
            w.custom = null;
            System.Action<Win> baseUpdate = null;
            w.custom = win =>
            {
                if (GameInput.DebugMainDown()) { Close(w); GameSession.startMainDirectly = true; newGame(); return; }
                // 버튼 이동은 기본 처리
                win.custom = null; Update(); win.custom = baseUpdate;
            };
            baseUpdate = w.custom;
        }

        // ================================================================ 일시정지
        public void Pause(System.Action toTitle)
        {
            if (stack.Count > 0) return;
            RectTransform p;
            Win w = Open("Pause", out p, new Vector2(620, 420));
            Label(p, "일시정지", 40, textColor, TextAnchor.MiddleCenter, new Vector2(0, 140), new Vector2(560, 60), FontStyle.Bold);
            Btn(w, p, "계속하기", new Vector2(0, 30), new Vector2(380, 64), () => Close(w));
            Btn(w, p, "타이틀로", new Vector2(0, -50), new Vector2(380, 64), () => { Close(w); toTitle(); });
            Btn(w, p, "게임 종료", new Vector2(0, -130), new Vector2(380, 64), () => Application.Quit());
            w.cancel = () => Close(w);
        }
    }
}
