using UnityEngine;
using UnityEngine.UI;

namespace BlackShade
{
    // 임시 UI를 코드로 만들 때 쓰는 도우미 (글꼴, 둥근 사각형, 그라데이션 등)
    public static class UIKit
    {
        static Font font;
        static Sprite rounded, gradientUp, lidTop, lidBottom, circle, vignette, ring;

        // 글꼴: Assets/Resources/Fonts/ 안에 'UIFont' 라는 이름의 .ttf/.otf를 넣으면 그걸 우선 사용.
        // 없으면 컴퓨터에 설치된 한글 글꼴(맑은 고딕 등)을 씁니다.
        public static Font UIFont
        {
            get
            {
                if (font != null) return font;
                font = Resources.Load<Font>("Fonts/UIFont");
                if (font == null)
                {
                    try
                    {
                        font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "AppleSDGothicNeo-Regular", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic", "Arial" }, 32);
                    }
                    catch { font = null; }
                }
                if (font == null)
                {
                    try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { font = null; }
                }
                if (font == null)
                {
                    try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { font = null; }
                }
                return font;
            }
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            GameObject g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            return (RectTransform)g.transform;
        }

        public static RectTransform Anchor(RectTransform r, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = min; r.anchorMax = max; r.pivot = pivot;
            r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        public static RectTransform Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            return r;
        }

        public static Image NewImage(string name, Transform parent, Color c, Sprite sprite = null, bool sliced = false)
        {
            RectTransform r = NewRect(name, parent);
            Image img = r.gameObject.AddComponent<Image>();
            img.color = c;
            img.sprite = sprite;
            img.raycastTarget = false;
            if (sliced) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Text NewText(string name, Transform parent, int size, Color c, TextAnchor align, FontStyle style = FontStyle.Normal)
        {
            RectTransform r = NewRect(name, parent);
            Text t = r.gameObject.AddComponent<Text>();
            t.font = UIFont;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.raycastTarget = false;
            t.lineSpacing = 1.15f;
            return t;
        }

        public static void AddShadow(Graphic g, float alpha = 0.6f, float dist = 2f)
        {
            Shadow s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, alpha);
            s.effectDistance = new Vector2(dist, -dist);
        }

        // ------------------------------------------------------------- 스프라이트
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                int n = 64, rad = 18;
                Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, rad, n - rad);
                        float cy = Mathf.Clamp(y + 0.5f, rad, n - rad);
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        float a = Mathf.Clamp01(rad - d + 0.5f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                rounded = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(rad, rad, rad, rad));
                return rounded;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                int n = 64;
                Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(n / 2f - d)));
                    }
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return circle;
            }
        }

        // 가장자리가 진하고 가운데가 투명한 원형 그라데이션 (다쳤을 때 붉은 화면 가장자리)
        public static Sprite Vignette
        {
            get
            {
                if (vignette != null) return vignette;
                int n = 128;
                Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d))));
                    }
                tex.Apply();
                vignette = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return vignette;
            }
        }

        // 테두리 고리 (회피 키 원)
        public static Sprite Ring
        {
            get
            {
                if (ring != null) return ring;
                int n = 128;
                Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                        float a = Mathf.Clamp01(n / 2f - d) * Mathf.Clamp01(d - (n / 2f - 10f));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                ring = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return ring;
            }
        }

        public static Button NewButton(string name, Transform parent, string label, int size, System.Action onClick)
        {
            Image bg = NewImage(name, parent, new Color(1f, 1f, 1f, 0.08f), Rounded, true);
            bg.raycastTarget = true;
            Button b = bg.gameObject.AddComponent<Button>();
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.6f, 1.5f, 1.3f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.selectedColor = new Color(1.6f, 1.5f, 1.3f, 1f);
            cb.colorMultiplier = 2f;
            b.colors = cb;
            b.targetGraphic = bg;
            Text t = NewText("Label", bg.transform, size, new Color(0.94f, 0.93f, 0.9f), TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            t.text = label;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }

        // 아래가 진하고 위로 갈수록 투명해지는 그라데이션 (대사창 배경)
        public static Sprite GradientUp
        {
            get
            {
                if (gradientUp != null) return gradientUp;
                gradientUp = VerticalGradient(t => Mathf.SmoothStep(1f, 0f, t));
                return gradientUp;
            }
        }

        // 눈꺼풀: 가장자리가 부드러운 검정
        public static Sprite LidTop
        {
            get
            {
                if (lidTop != null) return lidTop;
                lidTop = VerticalGradient(t => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.35f)));
                return lidTop;
            }
        }

        public static Sprite LidBottom
        {
            get
            {
                if (lidBottom != null) return lidBottom;
                lidBottom = VerticalGradient(t => Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((t - 0.65f) / 0.35f)));
                return lidBottom;
            }
        }

        // t: 0 = 아래, 1 = 위
        static Sprite VerticalGradient(System.Func<float, float> alpha)
        {
            int h = 128;
            Texture2D tex = new Texture2D(4, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                float a = alpha((float)y / (h - 1));
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
