using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlackShade
{
    // 임시 UI 전체. 실행하면 코드가 캔버스를 직접 만들어요.
    // 나중에 직접 디자인한 UI로 바꿀 때는 이 파일의 '색/크기 설정' 값부터 바꾸거나,
    // 같은 이름의 함수(Say, ShowBanner, ShowPrompt...)를 가진 새 UI로 교체하면 돼요.
    public class GameUI : MonoBehaviour
    {
        public static GameUI I { get; private set; }

        [Header("색/크기 설정")]
        public Color panelColor = new Color(0.04f, 0.05f, 0.07f, 0.86f);
        public Color textColor = new Color(0.94f, 0.93f, 0.9f);
        public Color mutedColor = new Color(0.62f, 0.63f, 0.66f);
        public Color accentColor = new Color(0.96f, 0.78f, 0.5f);
        public Color dangerColor = new Color(0.86f, 0.26f, 0.22f);
        public int dialogueFontSize = 34;
        public float charsPerSecond = 30f;

        readonly Dictionary<string, Color> speakerColors = new Dictionary<string, Color>();

        Canvas canvas;
        RectTransform root;

        Image fade, lidTop, lidBottom, barTop, barBottom, crosshair;

        CanvasGroup dialogueGroup;
        Text dialogueName, dialogueText, dialogueNext;
        Coroutine dialogueFade;

        CanvasGroup promptGroup;
        Text promptKey, promptLabel;
        RectTransform promptKeyBox;
        Image promptBarFill;
        GameObject promptBar;

        CanvasGroup bannerGroup;
        RectTransform bannerRow;
        Coroutine bannerAnim;

        CanvasGroup objectiveGroup;
        Text objectiveTitle;
        readonly List<Text> objectiveItems = new List<Text>();
        readonly List<bool> objectiveDone = new List<bool>();

        CanvasGroup broadcastGroup;
        Text broadcastText;

        CanvasGroup captionGroup;
        Text captionText;
        Coroutine captionAnim;

        CanvasGroup paperGroup;
        RectTransform paperNotes;
        Text paperTitle;

        float eyesOpen = 1f;

        // ---- 본편 HUD
        CanvasGroup toastGroup; Text toastText; Coroutine toastAnim;
        CanvasGroup batteryGroup; Image batteryFill; Text batteryLabel;
        Image hurtVignette, flashVignette, tint;
        CanvasGroup hideHint, canHint;
        Text hideHintText;
        float hurtLevel, flashLevel;

        void Awake()
        {
            I = this;
            Build();
        }

        public void SetSpeakerColor(string speaker, Color c) { speakerColors[speaker] = c; }

        // ================================================================ 생성
        void Build()
        {
            GameObject cg = new GameObject("GameUI_Canvas", typeof(RectTransform));
            cg.transform.SetParent(transform, false);
            canvas = cg.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler sc = cg.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            root = (RectTransform)cg.transform;

            // 그리는 순서 = 만드는 순서 (뒤에 만든 게 위)
            crosshair = UIKit.NewImage("Crosshair", root, new Color(1f, 1f, 1f, 0.55f), UIKit.Circle);
            UIKit.Anchor(crosshair.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7, 7));

            BuildObjective();
            BuildPrompt();
            BuildBanner();
            BuildCaption();
            BuildDialogue();
            BuildBroadcast();
            BuildPaper();
            BuildHud();

            barTop = UIKit.NewImage("LetterboxTop", root, Color.black);
            UIKit.Anchor(barTop.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
            barBottom = UIKit.NewImage("LetterboxBottom", root, Color.black);
            UIKit.Anchor(barBottom.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 0));

            lidTop = UIKit.NewImage("EyelidTop", root, Color.black, UIKit.LidTop);
            lidBottom = UIKit.NewImage("EyelidBottom", root, Color.black, UIKit.LidBottom);
            SetEyes(1f);

            fade = UIKit.NewImage("Fade", root, new Color(0, 0, 0, 0));
            UIKit.Stretch(fade.rectTransform);

            // 대사창은 페이드보다 위에 (암전 중에도 대사가 보이게)
            dialogueGroup.transform.SetAsLastSibling();
            toastGroup.transform.SetAsLastSibling();
            broadcastGroup.transform.SetAsLastSibling();
            captionGroup.transform.SetAsLastSibling();
        }

        void BuildDialogue()
        {
            RectTransform box = UIKit.NewRect("Dialogue", root);
            UIKit.Anchor(box, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 300));
            dialogueGroup = box.gameObject.AddComponent<CanvasGroup>();
            dialogueGroup.alpha = 0f;

            Image bg = UIKit.NewImage("Bg", box, new Color(0, 0, 0, 0.88f), UIKit.GradientUp);
            UIKit.Stretch(bg.rectTransform);

            dialogueName = UIKit.NewText("Name", box, 28, accentColor, TextAnchor.LowerLeft, FontStyle.Bold);
            UIKit.Anchor(dialogueName.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 178), new Vector2(1200, 40));
            UIKit.AddShadow(dialogueName);

            dialogueText = UIKit.NewText("Line", box, dialogueFontSize, textColor, TextAnchor.UpperLeft);
            UIKit.Anchor(dialogueText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, 168), new Vector2(1200, 120));
            UIKit.AddShadow(dialogueText);

            dialogueNext = UIKit.NewText("Next", box, 22, mutedColor, TextAnchor.MiddleRight);
            UIKit.Anchor(dialogueNext.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1200, 30));
            dialogueNext.text = "▼";
        }

        void BuildPrompt()
        {
            RectTransform box = UIKit.NewRect("Prompt", root);
            UIKit.Anchor(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(600, 80));
            promptGroup = box.gameObject.AddComponent<CanvasGroup>();
            promptGroup.alpha = 0f;

            Image key = UIKit.NewImage("KeyCap", box, new Color(1, 1, 1, 0.92f), UIKit.Rounded, true);
            promptKeyBox = key.rectTransform;
            UIKit.Anchor(promptKeyBox, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(40, 40));
            promptKey = UIKit.NewText("Key", key.transform, 22, new Color(0.08f, 0.08f, 0.1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(promptKey.rectTransform);

            promptLabel = UIKit.NewText("Label", box, 24, textColor, TextAnchor.UpperCenter);
            UIKit.Anchor(promptLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(600, 34));
            UIKit.AddShadow(promptLabel, 0.8f);

            Image barBg = UIKit.NewImage("HoldBar", box, new Color(1, 1, 1, 0.18f), UIKit.Rounded, true);
            UIKit.Anchor(barBg.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -88), new Vector2(220, 6));
            promptBar = barBg.gameObject;
            promptBarFill = UIKit.NewImage("Fill", barBg.transform, accentColor);
            UIKit.Anchor(promptBarFill.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 0));
        }

        void BuildBanner()
        {
            RectTransform box = UIKit.NewRect("TutorialBanner", root);
            UIKit.Anchor(box, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(10, 64));
            bannerGroup = box.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            Image bg = UIKit.NewImage("Bg", box, panelColor, UIKit.Rounded, true);
            UIKit.Stretch(bg.rectTransform);

            HorizontalLayoutGroup h = box.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(26, 30, 12, 12);
            h.spacing = 10;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            ContentSizeFitter f = box.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            bannerRow = box;
            // 배경은 레이아웃에서 제외
            LayoutElement le = bg.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        void BuildObjective()
        {
            RectTransform box = UIKit.NewRect("Objective", root);
            UIKit.Anchor(box, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -52), new Vector2(560, 300));
            objectiveGroup = box.gameObject.AddComponent<CanvasGroup>();
            objectiveGroup.alpha = 0f;

            Image line = UIKit.NewImage("Line", box, accentColor);
            UIKit.Anchor(line.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -4), new Vector2(3, 30));

            objectiveTitle = UIKit.NewText("Title", box, 26, accentColor, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Anchor(objectiveTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, 0), new Vector2(540, 36));
            UIKit.AddShadow(objectiveTitle, 0.8f);
        }

        void BuildCaption()
        {
            RectTransform box = UIKit.NewRect("Caption", root);
            UIKit.Anchor(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1000, 50));
            captionGroup = box.gameObject.AddComponent<CanvasGroup>();
            captionGroup.alpha = 0f;
            captionText = UIKit.NewText("Text", box, 26, new Color(0.85f, 0.85f, 0.88f), TextAnchor.MiddleCenter, FontStyle.Italic);
            UIKit.Stretch(captionText.rectTransform);
            UIKit.AddShadow(captionText, 0.9f);
        }

        void BuildBroadcast()
        {
            RectTransform box = UIKit.NewRect("Broadcast", root);
            UIKit.Anchor(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1500, 160));
            broadcastGroup = box.gameObject.AddComponent<CanvasGroup>();
            broadcastGroup.alpha = 0f;

            Text head = UIKit.NewText("Header", box, 22, dangerColor, TextAnchor.UpperCenter, FontStyle.Bold);
            UIKit.Anchor(head.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 30));
            head.text = "●  교내 방송";

            broadcastText = UIKit.NewText("Text", box, 40, new Color(0.92f, 0.9f, 0.86f), TextAnchor.UpperCenter);
            UIKit.Anchor(broadcastText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -46), new Vector2(0, 110));
            Outline o = broadcastText.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.6f, 0.05f, 0.05f, 0.35f);
            o.effectDistance = new Vector2(2, -2);
        }

        void BuildPaper()
        {
            RectTransform box = UIKit.NewRect("RollingPaper", root);
            UIKit.Stretch(box);
            paperGroup = box.gameObject.AddComponent<CanvasGroup>();
            paperGroup.alpha = 0f;

            Image dim = UIKit.NewImage("Dim", box, new Color(0, 0, 0, 0.55f));
            UIKit.Stretch(dim.rectTransform);

            Image paper = UIKit.NewImage("Paper", box, new Color(0.96f, 0.93f, 0.85f), UIKit.Rounded, true);
            UIKit.Anchor(paper.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1080, 680));
            paper.rectTransform.localRotation = Quaternion.Euler(0, 0, -1.2f);
            UIKit.AddShadow(paper, 0.5f, 8f);

            paperTitle = UIKit.NewText("Title", paper.transform, 40, new Color(0.2f, 0.18f, 0.16f), TextAnchor.UpperCenter, FontStyle.Bold);
            UIKit.Anchor(paperTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(0, 56));

            paperNotes = UIKit.NewRect("Notes", paper.transform);
            UIKit.Stretch(paperNotes);

            Text hint = UIKit.NewText("Hint", box, 24, mutedColor, TextAnchor.MiddleCenter);
            UIKit.Anchor(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(600, 40));
            hint.text = "[E] 닫기";
        }


        void BuildHud()
        {
            // 다쳤을 때 붉은 가장자리
            hurtVignette = UIKit.NewImage("Hurt", root, new Color(0.55f, 0f, 0f, 0f), UIKit.Vignette);
            UIKit.Stretch(hurtVignette.rectTransform);
            flashVignette = UIKit.NewImage("HitFlash", root, new Color(0.8f, 0.05f, 0.02f, 0f), UIKit.Vignette);
            UIKit.Stretch(flashVignette.rectTransform);
            tint = UIKit.NewImage("Tint", root, new Color(0f, 0f, 0f, 0f));
            UIKit.Stretch(tint.rectTransform);

            // 짧은 알림 문구
            RectTransform tb = UIKit.NewRect("Toast", root);
            UIKit.Anchor(tb, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 330), new Vector2(1400, 60));
            toastGroup = tb.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            Image tbg = UIKit.NewImage("Bg", tb, new Color(0, 0, 0, 0.55f), UIKit.Rounded, true);
            UIKit.Stretch(tbg.rectTransform);
            toastText = UIKit.NewText("Text", tb, 27, textColor, TextAnchor.MiddleCenter);
            UIKit.Stretch(toastText.rectTransform);
            UIKit.AddShadow(toastText, 0.9f);

            // 손전등 배터리 (왼쪽 아래)
            RectTransform bb = UIKit.NewRect("Battery", root);
            UIKit.Anchor(bb, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(48, 44), new Vector2(260, 46));
            batteryGroup = bb.gameObject.AddComponent<CanvasGroup>();
            batteryGroup.alpha = 0f;
            batteryLabel = UIKit.NewText("Label", bb, 20, mutedColor, TextAnchor.UpperLeft);
            UIKit.Anchor(batteryLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), Vector2.zero, new Vector2(0, 24));
            Image bbg = UIKit.NewImage("Bar", bb, new Color(1, 1, 1, 0.15f), UIKit.Rounded, true);
            UIKit.Anchor(bbg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(180, 10));
            batteryFill = UIKit.NewImage("Fill", bbg.transform, new Color(0.9f, 0.85f, 0.6f), UIKit.Rounded, true);
            UIKit.Anchor(batteryFill.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(180, 0));

            hideHint = MakeHint("HideHint", "숨어 있다 · 숨을 죽이고 있으면 들키지 않는다      [E] 나가기", out hideHintText);
            Text dummy;
            canHint = MakeHint("CanHint", "빈 캔을 들고 있다      [마우스 왼쪽] 던지기    [마우스 오른쪽] 내려놓기", out dummy);
        }

        CanvasGroup MakeHint(string name, string text, out Text t)
        {
            RectTransform r = UIKit.NewRect(name, root);
            UIKit.Anchor(r, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 46), new Vector2(1200, 40));
            CanvasGroup g = r.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            t = UIKit.NewText("Text", r, 24, mutedColor, TextAnchor.MiddleCenter);
            UIKit.Stretch(t.rectTransform);
            UIKit.AddShadow(t, 0.9f);
            t.text = text;
            return g;
        }

        void Update()
        {
            // 체력이 낮을수록 화면 가장자리가 붉어짐 + 맞았을 때 번쩍
            GameState gs = GameState.Current;
            float hpFrac = gs.maxHp > 0f ? gs.hp / gs.maxHp : 1f;
            float targetHurt = hpFrac < 0.999f ? Mathf.Lerp(0.75f, 0.1f, hpFrac) : 0f;
            if (hpFrac < 0.35f) targetHurt += Mathf.Sin(Time.unscaledTime * 6f) * 0.08f;
            hurtLevel = Mathf.MoveTowards(hurtLevel, showHud ? targetHurt : 0f, Time.unscaledDeltaTime);
            hurtVignette.color = new Color(0.55f, 0f, 0f, hurtLevel);
            flashLevel = Mathf.MoveTowards(flashLevel, 0f, Time.unscaledDeltaTime * 1.4f);
            flashVignette.color = new Color(0.85f, 0.05f, 0.02f, flashLevel);
        }

        bool showHud = true;
        public void SetHudVisible(bool on) { showHud = on; }

        public void FlashDamage() { flashLevel = 0.9f; }

        public void SetTint(Color c) { tint.color = c; }

        public IEnumerator TintTo(Color c, float dur)
        {
            Color from = tint.color; float t = 0f;
            while (t < dur) { t += Time.deltaTime; tint.color = Color.Lerp(from, c, t / dur); yield return null; }
            tint.color = c;
        }

        public void Toast(string text, float seconds = 2.8f)
        {
            if (toastAnim != null) StopCoroutine(toastAnim);
            toastAnim = StartCoroutine(ToastRoutine(text, seconds));
        }

        IEnumerator ToastRoutine(string text, float seconds)
        {
            toastText.text = text;
            float w = Mathf.Min(1700f, 80f + toastText.preferredWidth);
            ((RectTransform)toastGroup.transform).sizeDelta = new Vector2(w, 60);
            float t = 0f;
            while (t < 0.15f) { t += Time.unscaledDeltaTime; toastGroup.alpha = t / 0.15f; yield return null; }
            toastGroup.alpha = 1f;
            t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            t = 0f;
            while (t < 0.5f) { t += Time.unscaledDeltaTime; toastGroup.alpha = 1f - t / 0.5f; yield return null; }
            toastGroup.alpha = 0f;
        }

        public void SetBattery(float frac, bool visible, bool on)
        {
            batteryGroup.alpha = visible && showHud ? 1f : 0f;
            if (!visible) return;
            batteryFill.rectTransform.sizeDelta = new Vector2(180f * Mathf.Clamp01(frac), 0f);
            batteryFill.color = frac < 0.15f ? dangerColor : (on ? new Color(0.95f, 0.88f, 0.6f) : new Color(0.6f, 0.58f, 0.5f));
            batteryLabel.text = "손전등 " + Mathf.CeilToInt(frac * 100f) + "%" + (on ? "  · 켜짐" : "  · 꺼짐  [F]");
        }

        public void ShowHideHint(bool on) { hideHint.alpha = on ? 1f : 0f; }
        public void ShowCanHint(bool on) { canHint.alpha = on ? 1f : 0f; }

        // ================================================================ 대사
        public IEnumerator Say(string speaker, string line, float auto = -1f, bool whisper = false)
        {
            Color nameColor;
            if (!speakerColors.TryGetValue(speaker, out nameColor)) nameColor = accentColor;
            dialogueName.text = speaker;
            dialogueName.color = nameColor;
            dialogueText.fontStyle = whisper ? FontStyle.Italic : FontStyle.Normal;
            dialogueText.color = whisper ? new Color(textColor.r, textColor.g, textColor.b, 0.75f) : textColor;
            dialogueText.text = "";
            dialogueNext.enabled = false;
            if (dialogueFade != null) StopCoroutine(dialogueFade);
            dialogueFade = StartCoroutine(FadeGroup(dialogueGroup, 1f, 0.15f));

            if (DevMode.SkipDialogue)
            {
                // 개발자 모드: 대사를 한 줄 보여 주고 바로 넘어감
                dialogueText.text = line;
                yield return null;
                yield break;
            }
            yield return null; // 같은 프레임 입력 무시
            float shown = 0f;
            int last = 0;
            bool skip = false;
            while (last < line.Length)
            {
                if (GameInput.AdvanceDown()) skip = true;
                shown += Time.deltaTime * charsPerSecond * (whisper ? 0.7f : 1f);
                int n = skip ? line.Length : Mathf.Min(line.Length, Mathf.FloorToInt(shown));
                if (n != last)
                {
                    if (!skip && n / 3 != last / 3 && AudioDirector.I != null && line[n - 1] != ' ')
                        AudioDirector.I.Play(AudioDirector.I.tick, 0.5f, Random.Range(0.95f, 1.05f));
                    last = n;
                    dialogueText.text = line.Substring(0, n);
                }
                yield return null;
            }

            dialogueNext.enabled = true;
            float wait = 0f;
            yield return null;
            while (true)
            {
                dialogueNext.color = new Color(mutedColor.r, mutedColor.g, mutedColor.b, 0.5f + 0.5f * Mathf.Sin(Time.time * 5f));
                if (GameInput.AdvanceDown()) break;
                wait += Time.deltaTime;
                if (auto > 0f && wait >= auto) break;
                yield return null;
            }
            dialogueNext.enabled = false;
        }

        public void HideDialogue()
        {
            if (dialogueFade != null) StopCoroutine(dialogueFade);
            dialogueFade = StartCoroutine(FadeGroup(dialogueGroup, 0f, 0.2f));
        }

        // ================================================================ 상호작용 안내
        public void ShowPrompt(string key, string label, float progress)
        {
            promptKey.text = key;
            promptLabel.text = label;
            promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, 1f, Time.deltaTime * 8f);
            promptBar.SetActive(progress >= 0f);
            if (progress >= 0f)
                promptBarFill.rectTransform.sizeDelta = new Vector2(220f * Mathf.Clamp01(progress), 0f);
        }

        public void HidePrompt()
        {
            if (promptGroup != null) promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, 0f, Time.deltaTime * 10f);
        }

        public void SetCrosshair(bool on) { crosshair.enabled = on; }

        // ================================================================ 튜토리얼 배너
        // keys 예: {"W","A","S","D"}
        public void ShowBanner(string[] keys, string text)
        {
            for (int i = bannerRow.childCount - 1; i >= 0; i--)
            {
                Transform c = bannerRow.GetChild(i);
                if (c.name != "Bg") Destroy(c.gameObject);
            }
            if (keys != null)
                foreach (string k in keys)
                {
                    Image cap = UIKit.NewImage("Key_" + k, bannerRow, new Color(1, 1, 1, 0.92f), UIKit.Rounded, true);
                    LayoutElement le = cap.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = Mathf.Max(40f, 18f * k.Length + 22f);
                    le.preferredHeight = 40f;
                    Text kt = UIKit.NewText("T", cap.transform, 20, new Color(0.08f, 0.08f, 0.1f), TextAnchor.MiddleCenter, FontStyle.Bold);
                    UIKit.Stretch(kt.rectTransform);
                    kt.text = k;
                }
            Text t = UIKit.NewText("Text", bannerRow, 26, textColor, TextAnchor.MiddleLeft);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = "  " + text;
            if (bannerAnim != null) StopCoroutine(bannerAnim);
            bannerAnim = StartCoroutine(FadeGroup(bannerGroup, 1f, 0.3f));
        }

        public void HideBanner()
        {
            if (bannerAnim != null) StopCoroutine(bannerAnim);
            bannerAnim = StartCoroutine(FadeGroup(bannerGroup, 0f, 0.3f));
        }

        // ================================================================ 목표
        public void SetObjective(string title, string[] items)
        {
            objectiveTitle.text = title;
            foreach (Text t in objectiveItems) Destroy(t.gameObject);
            objectiveItems.Clear();
            objectiveDone.Clear();
            for (int i = 0; i < items.Length; i++)
            {
                Text t = UIKit.NewText("Item" + i, objectiveGroup.transform, 23, textColor, TextAnchor.UpperLeft);
                UIKit.Anchor(t.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -46 - i * 36), new Vector2(540, 34));
                UIKit.AddShadow(t, 0.8f);
                objectiveItems.Add(t);
                objectiveDone.Add(false);
                t.text = "□  " + items[i];
            }
            StartCoroutine(FadeGroup(objectiveGroup, 1f, 0.4f));
        }

        public void CheckObjective(int index)
        {
            if (index < 0 || index >= objectiveItems.Count || objectiveDone[index]) return;
            objectiveDone[index] = true;
            Text t = objectiveItems[index];
            t.text = "■  " + t.text.Substring(3);
            t.color = mutedColor;
            StartCoroutine(Punch(t.rectTransform));
        }

        public void HideObjective() { StartCoroutine(FadeGroup(objectiveGroup, 0f, 0.5f)); }

        IEnumerator Punch(RectTransform r)
        {
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float s = 1f + 0.12f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.3f));
                r.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            r.localScale = Vector3.one;
        }

        // ================================================================ 효과음 자막 (예: "쿵")
        public void ShowCaption(string text, float hold)
        {
            if (captionAnim != null) StopCoroutine(captionAnim);
            captionAnim = StartCoroutine(CaptionRoutine(text, hold));
        }

        IEnumerator CaptionRoutine(string text, float hold)
        {
            captionText.text = text;
            yield return FadeGroup(captionGroup, 1f, 0.15f);
            yield return new WaitForSeconds(hold);
            yield return FadeGroup(captionGroup, 0f, 0.6f);
        }

        // ================================================================ 교내 방송
        public IEnumerator Broadcast(string text, float hold)
        {
            broadcastText.text = text;
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                broadcastGroup.alpha = Random.value > 0.3f ? t / 0.5f : 0.15f;
                yield return null;
            }
            t = 0f;
            while (t < hold)
            {
                t += Time.deltaTime;
                broadcastGroup.alpha = Random.value > 0.04f ? 1f : 0.35f;
                broadcastText.rectTransform.anchoredPosition = new Vector2(Random.value > 0.97f ? Random.Range(-6f, 6f) : 0f, -46f);
                yield return null;
            }
        }

        public IEnumerator HideBroadcast(float dur)
        {
            yield return FadeGroup(broadcastGroup, 0f, dur);
        }

        // ================================================================ 롤링페이퍼
        public struct Note
        {
            public string text;
            public Color color;
            public Vector2 pos;   // 종이 중심 기준
            public float angle;
            public int size;
            public Note(string text, Color color, Vector2 pos, float angle, int size)
            {
                this.text = text; this.color = color; this.pos = pos; this.angle = angle; this.size = size;
            }
        }

        public IEnumerator ShowRollingPaper(string title, Note[] notes)
        {
            paperTitle.text = title;
            for (int i = paperNotes.childCount - 1; i >= 0; i--) Destroy(paperNotes.GetChild(i).gameObject);
            foreach (Note n in notes)
            {
                Text t = UIKit.NewText("Note", paperNotes, n.size, n.color, TextAnchor.MiddleCenter);
                UIKit.Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), n.pos, new Vector2(440, 120));
                t.rectTransform.localRotation = Quaternion.Euler(0, 0, n.angle);
                t.text = n.text;
            }
            SetCrosshair(false);
            yield return FadeGroup(paperGroup, 1f, 0.25f);
            yield return null;
            while (!GameInput.CloseDown()) yield return null;
            yield return FadeGroup(paperGroup, 0f, 0.2f);
            SetCrosshair(true);
        }

        // ================================================================ 화면 연출
        public void SetFade(float a) { fade.color = new Color(0, 0, 0, a); }

        public IEnumerator Fade(float to, float dur)
        {
            float from = fade.color.a, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                SetFade(Mathf.Lerp(from, to, t / dur));
                yield return null;
            }
            SetFade(to);
        }

        // 0 = 눈 감음, 1 = 완전히 뜸
        public void SetEyes(float open)
        {
            eyesOpen = open;
            float a = Mathf.Lerp(0.2f, 1.02f, open);
            float b = Mathf.Lerp(0.8f, -0.02f, open);
            RectTransform t = lidTop.rectTransform;
            t.anchorMin = new Vector2(0, a); t.anchorMax = new Vector2(1, 1.02f);
            t.offsetMin = Vector2.zero; t.offsetMax = Vector2.zero;
            RectTransform bt = lidBottom.rectTransform;
            bt.anchorMin = new Vector2(0, -0.02f); bt.anchorMax = new Vector2(1, b);
            bt.offsetMin = Vector2.zero; bt.offsetMax = Vector2.zero;
            lidTop.enabled = lidBottom.enabled = open < 0.999f;
        }

        public IEnumerator Eyes(float to, float dur)
        {
            float from = eyesOpen, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                SetEyes(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / dur)));
                yield return null;
            }
            SetEyes(to);
        }

        public IEnumerator Blink(float speed = 1f)
        {
            yield return Eyes(0f, 0.09f / speed);
            yield return new WaitForSeconds(0.05f / speed);
            yield return Eyes(1f, 0.16f / speed);
        }

        public IEnumerator Letterbox(bool on, float dur)
        {
            float from = barTop.rectTransform.sizeDelta.y;
            float to = on ? 110f : 0f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float h = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / dur));
                barTop.rectTransform.sizeDelta = new Vector2(0, h);
                barBottom.rectTransform.sizeDelta = new Vector2(0, h);
                yield return null;
            }
            barTop.rectTransform.sizeDelta = new Vector2(0, to);
            barBottom.rectTransform.sizeDelta = new Vector2(0, to);
        }

        public void SetLetterbox(bool on)
        {
            float h = on ? 110f : 0f;
            barTop.rectTransform.sizeDelta = new Vector2(0, h);
            barBottom.rectTransform.sizeDelta = new Vector2(0, h);
        }

        IEnumerator FadeGroup(CanvasGroup g, float to, float dur)
        {
            float from = g.alpha, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                g.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            g.alpha = to;
        }
    }
}
