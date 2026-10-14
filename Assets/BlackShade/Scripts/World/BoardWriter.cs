using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BlackShade
{
    // 칠판 위에 분필 글씨가 한 글자씩 써지는 연출.
    // 칠판 앞에 보이지 않는 월드 캔버스를 붙여서 글씨를 띄워요.
    public class BoardWriter : MonoBehaviour
    {
        public Color chalkColor = new Color(0.95f, 0.95f, 0.9f, 0.92f);

        Text main, sub, sign;
        CanvasGroup group;
        string mainFull, subFull, signFull;

        // center: 칠판 판 가운데(앞면), facing: 칠판을 바라보는 방향(보는 사람 → 칠판)
        public static BoardWriter Create(Vector3 center, Vector3 facing, float widthM, float heightM)
        {
            GameObject go = new GameObject("BoardWriting", typeof(RectTransform));
            BoardWriter bw = go.AddComponent<BoardWriter>();
            const float pxPerM = 500f;
            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(widthM * pxPerM, heightM * pxPerM);
            rt.position = center - facing.normalized * 0.015f; // 판보다 살짝 앞
            rt.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            rt.localScale = Vector3.one / pxPerM;
            bw.group = go.AddComponent<CanvasGroup>();

            bw.main = Make(rt, "Main", 150, new Vector2(0, 90), -2.5f, bw.chalkColor);
            bw.sub = Make(rt, "Sub", 70, new Vector2(-40, -110), 1f, bw.chalkColor);
            bw.sign = Make(rt, "Sign", 56, new Vector2(560, -220), -1.5f, bw.chalkColor);
            return bw;
        }

        static Text Make(RectTransform parent, string name, int size, Vector2 pos, float angle, Color col)
        {
            Text t = UIKit.NewText(name, parent, size, col, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIKit.Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(1800, size * 1.4f));
            t.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            t.text = "";
            return t;
        }

        public IEnumerator Write(string mainText, string subText, string signText, float charsPerSecond = 3.2f)
        {
            mainFull = mainText; subFull = subText; signFull = signText;
            yield return Type(main, mainText, charsPerSecond);
            yield return new WaitForSeconds(0.25f);
            yield return Type(sub, subText, charsPerSecond * 2.2f);
            yield return new WaitForSeconds(0.2f);
            yield return Type(sign, signText, charsPerSecond * 2.5f);
        }

        IEnumerator Type(Text t, string s, float cps)
        {
            float shown = 0f;
            while (shown < s.Length)
            {
                // E/스페이스를 누르면 빨리 쓰기
                float speed = GameInput.InteractHeld() ? cps * 3f : cps;
                shown += Time.deltaTime * speed;
                t.text = s.Substring(0, Mathf.Min(s.Length, Mathf.FloorToInt(shown)));
                yield return null;
            }
            t.text = s;
        }

        public void ShowFinal(string m, string su, string si)
        {
            mainFull = m; subFull = su; signFull = si;
            ShowInstant();
        }

        public void ShowInstant()
        {
            if (mainFull != null) { main.text = mainFull; sub.text = subFull; sign.text = signFull; }
        }

        // 정전 뒤 어둠 속에서 희미하게만 보이게
        public void SetBrightness(float k)
        {
            Color c = new Color(chalkColor.r * k, chalkColor.g * k, chalkColor.b * k, chalkColor.a);
            main.color = sub.color = sign.color = c;
        }
    }
}
