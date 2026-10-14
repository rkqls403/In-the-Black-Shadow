using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackShade
{
    // 팝업/메뉴가 열려 있는지 (열려 있으면 플레이어 조작과 상호작용을 막음)
    // 메뉴/팝업이 열리면 게임을 멈추고 마우스 커서를 보여 준다. 다 닫히면 원래대로.
    public static class UIState
    {
        static int count;
        public static bool ModalOpen { get { return count > 0; } }
        public static bool pauseGame = true;
        public static int lastCloseFrame = -10;
        // 창을 닫은 바로 그 프레임의 키 입력이 게임에 다시 들어가지 않게
        public static bool JustClosed { get { return Time.frameCount <= lastCloseFrame + 1; } }

        public static void Push()
        {
            count++;
            if (pauseGame) Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Pop()
        {
            count = Mathf.Max(0, count - 1);
            lastCloseFrame = Time.frameCount;
            if (count == 0)
            {
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public static void Reset() { count = 0; Time.timeScale = 1f; }
    }

    // 한국어 조사 붙이기: '열쇠'를 / '명찰 장식'을
    public static class Josa
    {
        public static bool HasBatchim(string word)
        {
            for (int i = word.Length - 1; i >= 0; i--)
            {
                char c = word[i];
                if (c >= 0xAC00 && c <= 0xD7A3) return (c - 0xAC00) % 28 != 0;
                if (c >= '0' && c <= '9') return "0136780".IndexOf(c) >= 0 && c != '2' && c != '4' && c != '5' && c != '9';
                if (char.IsLetter(c)) return "lmnLMN".IndexOf(c) >= 0;
                // 괄호, 공백, 기호는 건너뜀
            }
            return false;
        }
        // '열쇠'를 / '명찰 장식'을  (따옴표 포함)
        public static string Quote(string w) { return "'" + w + "'" + (HasBatchim(w) ? "을" : "를"); }
        public static string EulReul(string w) { return w + (HasBatchim(w) ? "을" : "를"); }
        public static string IGa(string w) { return w + (HasBatchim(w) ? "이" : "가"); }
        public static string EunNeun(string w) { return w + (HasBatchim(w) ? "은" : "는"); }
        public static string WaGwa(string w) { return w + (HasBatchim(w) ? "과" : "와"); }
    }

    // 씬을 다시 불러와도 남아 있는 값 (죽어서 다시 시작할 때 등)
    public static class GameSession
    {
        public static bool loadOnStart;       // 저장된 기록에서 시작
        public static bool startMainDirectly; // 프롤로그 없이 본편부터 (개발용)
        public static bool skipTitle;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public string spawnId;
        public float hp;
        public float battery;
        public List<string> itemIds = new List<string>();
        public List<int> itemCounts = new List<int>();
        public List<string> docs = new List<string>();
        public List<string> flags = new List<string>();
        public List<string> counterKeys = new List<string>();
        public List<int> counterValues = new List<int>();
        public string savedAt;
        public string placeName;
    }

    // 현재 진행 상태: 가방, 문서, 이야기 진행 표시(flag), 체력, 손전등 배터리
    public class GameState
    {
        public static GameState Current = new GameState();

        public float maxHp = 100f;
        public float hp = 100f;
        public float battery = 100f;

        readonly Dictionary<string, int> items = new Dictionary<string, int>();
        readonly List<string> itemOrder = new List<string>();
        readonly List<string> docs = new List<string>();
        readonly HashSet<string> flags = new HashSet<string>();
        readonly Dictionary<string, int> counters = new Dictionary<string, int>();

        public event Action Changed;
        void Notify() { if (Changed != null) Changed(); }

        // ---------------------------------------------------------------- 아이템
        public int Count(string id) { int n; return items.TryGetValue(id, out n) ? n : 0; }
        public bool Has(string id, int n = 1) { return Count(id) >= n; }

        public void AddItem(string id, int n = 1)
        {
            if (!items.ContainsKey(id)) { items[id] = 0; itemOrder.Add(id); }
            items[id] += n;
            Notify();
        }

        public bool RemoveItem(string id, int n = 1)
        {
            int have = Count(id);
            if (have < n) return false;
            if (have - n <= 0) { items.Remove(id); itemOrder.Remove(id); }
            else items[id] = have - n;
            Notify();
            return true;
        }

        public List<string> ItemsIn(ItemCategory cat)
        {
            List<string> r = new List<string>();
            foreach (string id in itemOrder)
            {
                ItemDef d = ItemDB.Get(id);
                if (d != null && d.category == cat) r.Add(id);
            }
            return r;
        }

        // ---------------------------------------------------------------- 문서
        public bool HasDoc(string id) { return docs.Contains(id); }
        public bool AddDoc(string id)
        {
            if (docs.Contains(id)) return false;
            docs.Add(id);
            Notify();
            return true;
        }
        public List<string> DocsIn(DocCategory cat)
        {
            List<string> r = new List<string>();
            foreach (string id in docs)
            {
                DocDef d = DocDB.Get(id);
                if (d != null && d.category == cat) r.Add(id);
            }
            return r;
        }

        // ---------------------------------------------------------------- 진행 표시
        public bool Flag(string f) { return flags.Contains(f); }
        public void SetFlag(string f, bool on = true)
        {
            if (on) flags.Add(f); else flags.Remove(f);
            Notify();
        }

        public int Counter(string k, int def = 0) { int v; return counters.TryGetValue(k, out v) ? v : def; }
        public void SetCounter(string k, int v) { counters[k] = v; Notify(); }

        public void Heal(float amount) { hp = Mathf.Min(maxHp, hp + amount); Notify(); }

        // ---------------------------------------------------------------- 저장
        public SaveData ToSave(string spawnId, string placeName)
        {
            SaveData s = new SaveData();
            s.spawnId = spawnId;
            s.placeName = placeName;
            s.hp = hp;
            s.battery = battery;
            foreach (string id in itemOrder) { s.itemIds.Add(id); s.itemCounts.Add(items[id]); }
            s.docs.AddRange(docs);
            s.flags.AddRange(flags);
            foreach (var kv in counters) { s.counterKeys.Add(kv.Key); s.counterValues.Add(kv.Value); }
            s.savedAt = DateTime.Now.ToString("yyyy.MM.dd HH:mm");
            return s;
        }

        public static GameState FromSave(SaveData s)
        {
            GameState g = new GameState();
            if (s == null) return g;
            g.hp = s.hp > 0f ? s.hp : g.maxHp;
            g.battery = s.battery;
            for (int i = 0; i < s.itemIds.Count && i < s.itemCounts.Count; i++)
            {
                g.items[s.itemIds[i]] = s.itemCounts[i];
                g.itemOrder.Add(s.itemIds[i]);
            }
            g.docs.AddRange(s.docs);
            foreach (string f in s.flags) g.flags.Add(f);
            for (int i = 0; i < s.counterKeys.Count && i < s.counterValues.Count; i++) g.counters[s.counterKeys[i]] = s.counterValues[i];
            return g;
        }
    }

    // 기록 저장 (PlayerPrefs에 JSON으로)
    public static class SaveSystem
    {
        const string KeyName = "BlackShade_Save_v1";

        public static bool HasSave { get { return PlayerPrefs.HasKey(KeyName); } }

        public static void Save(SaveData d)
        {
            PlayerPrefs.SetString(KeyName, JsonUtility.ToJson(d));
            PlayerPrefs.Save();
        }

        public static SaveData Load()
        {
            if (!HasSave) return null;
            try { return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(KeyName)); }
            catch { return null; }
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(KeyName);
            PlayerPrefs.Save();
        }
    }
}
