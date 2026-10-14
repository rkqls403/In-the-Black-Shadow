using UnityEngine;

namespace BlackShade
{
    // 개발자 모드 (유니티 에디터에서 재생할 때만 켜짐. 빌드에서도 쓰려면 Game 오브젝트의 DevMode > Enable In Build 체크)
    //
    //   [F1] 개발자 메뉴 열기/닫기 (구간 이동 버튼들)
    //   [F2] 밝게 보기      : 손전등 없이도 다 보이고, 어두운 곳 조사 제한도 꺼짐
    //   [F3] 대화 스킵      : 모든 대사를 기다리지 않고 바로 넘김
    //   [F4] 무적           : 선생님/최종장 공격에 다치지 않음
    //   [F5] 선생님 끄기/켜기
    //   [F6] 3배속          : 컷씬 기다리기 귀찮을 때
    //   (기존) 타이틀에서 [F9] 본편 바로 시작, 프롤로그 중 [F8] 무서운 얘기로
    public class DevMode : MonoBehaviour
    {
        public static bool Enabled { get; private set; }
        // 씬을 다시 불러와도 유지됨
        public static bool FullBright, SkipDialogue, God, MonsterOff, Fast;
        static int pendingJump = -1;

        [Tooltip("빌드한 게임에서도 개발자 모드를 쓰려면 체크")]
        public bool enableInBuild = false;

        bool panel;
        Vector2 scroll;
        float pendingTimer;

        static readonly string[] JumpNames =
        {
            "① 1층 로비 (시작 지점)",
            "② 2층 1학년 — 도서실 사건 직후",
            "③ 3층 2학년 — 과학실 악보 ① 획득 후",
            "④ 4층 3학년 — 양호실까지 조사 후",
            "⑤ 1층 교장실 앞 — 교장실 열쇠 획득 후",
            "⑥ 5층 계단 — 교장실 진실 + 명찰 장식 + 악보 완성",
            "⑦ 5층 강당 문 앞",
            "⑧ 최종장 바로 시작 (강당 안)",
        };

        void Awake()
        {
            Enabled = Application.isEditor || enableInBuild;
        }

        // ================================================================ 키
        void Update()
        {
            if (!Enabled) return;

            if (GameInput.KeyDown(KeyCode.F1)) SetPanel(!panel);
            else if (panel && GameInput.KeyDown(KeyCode.Escape)) SetPanel(false);

            if (GameInput.KeyDown(KeyCode.F2)) SetBright(!FullBright);
            if (GameInput.KeyDown(KeyCode.F3)) { SkipDialogue = !SkipDialogue; Notice("대화 스킵 " + OnOff(SkipDialogue)); }
            if (GameInput.KeyDown(KeyCode.F4)) { God = !God; Notice("무적 " + OnOff(God)); }
            if (GameInput.KeyDown(KeyCode.F5)) SetMonster(!MonsterOff);
            if (GameInput.KeyDown(KeyCode.F6)) { Fast = !Fast; Notice("3배속 " + OnOff(Fast)); }

            if (FullBright)
            {
                RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.64f);
                RenderSettings.fog = false;
                LightCheck.alwaysLit = true;
            }
            if (!UIState.ModalOpen) Time.timeScale = Fast ? 3f : 1f;

            // 씬을 다시 불러온 뒤 예약된 구간 이동
            MainDirector m = MainDirector.I;
            if (pendingJump >= 0 && m != null && m.Running && m.PlayerCanAct)
            {
                pendingTimer += Time.unscaledDeltaTime;
                if (pendingTimer > 0.5f) { int j = pendingJump; pendingJump = -1; pendingTimer = 0f; Jump(j); }
            }
        }

        static string OnOff(bool b) { return b ? "켬" : "끔"; }

        void Notice(string s)
        {
            if (GameUI.I != null) GameUI.I.Toast("[개발자] " + s, 1.6f);
            Debug.Log("[DevMode] " + s);
        }

        void SetPanel(bool on)
        {
            if (on == panel) return;
            if (on && UIState.ModalOpen) return; // 다른 창이 떠 있으면 안 엶
            panel = on;
            if (on) UIState.Push(); else UIState.Pop();
        }

        void SetBright(bool on)
        {
            FullBright = on;
            if (!on)
            {
                MainDirector m = MainDirector.I;
                bool main = m != null && m.Running;
                RenderSettings.fog = main;
                LightCheck.alwaysLit = !main;
                if (main) m.RefreshEnvironment();
                else
                {
                    LightingDirector ld = GetComponent<LightingDirector>();
                    if (ld != null && ld.Sun != null && ld.PowerOn) ld.Apply(ld.Progress);
                }
            }
            Notice("밝게 보기 " + OnOff(on));
        }

        void SetMonster(bool off)
        {
            MonsterOff = off;
            TeacherMonster t = TeacherMonster.I;
            if (t != null)
            {
                if (off && t.Active) t.Deactivate();
                else if (!off && !t.Active && GameState.Current.Flag("monster_active") && MainDirector.I != null && MainDirector.I.Running)
                    t.Activate(FarStair(MainDirector.I.Player.transform.position), false);
            }
            Notice("선생님 " + (off ? "끔" : "켬"));
        }

        static Vector3 FarStair(Vector3 from)
        {
            int f = MapSpace.FloorOf(from);
            Vector3 a = MapSpace.GF(f, 18.7f, 0f, 2.4f), b = MapSpace.GF(f, 81.5f, 0f, 2.6f);
            return Vector3.Distance(a, from) > Vector3.Distance(b, from) ? a : b;
        }

        // ================================================================ 구간 이동
        void RequestJump(int stage)
        {
            MainDirector m = MainDirector.I;
            SetPanel(false);
            if (m == null || !m.Running)
            {
                // 아직 본편이 아니면 본편으로 다시 시작한 뒤 이동
                pendingJump = stage;
                GameSession.startMainDirectly = true;
                MainDirector.Reload();
                return;
            }
            if (!m.PlayerCanAct) { pendingJump = stage; return; } // 컷씬이 끝나면 이동
            Jump(stage);
        }

        void Jump(int stage)
        {
            MainDirector m = MainDirector.I;
            if (HideSpot.PlayerHidden) { Notice("숨은 상태에서는 이동할 수 없어요. 먼저 나오세요."); return; }
            if (m.IsDead) return;
            GameState gs = GameState.Current;

            if (stage >= 1)
            {
                Give("flashlight", 1);
                gs.battery = 100f;
                Docs("note_shoe", "doc_program", "doc_expense", "doc_certificate", "book_folk");
                gs.SetFlag("monster_active");
                m.OpenShutters();
                Give("paper", 2); Give("pen", 2);
            }
            if (stage >= 2)
            {
                gs.SetFlag("vending_enabled");
                gs.SetFlag("chem_solved"); gs.SetFlag("reagent_open");
                Docs("note_vending", "note_wonyul", "doc_duty", "doc_report", "doc_report_revealed");
                Give("empty_can", 2);
                Score(1);
            }
            if (stage >= 3)
            {
                Docs("note_album", "note_confiscated", "doc_medical", "doc_accident", "note_nurse", "doc_counsel");
                Give("fountain_pen", 1); Give("medkit", 1);
                Unlock("3F_staff", "3F_nurse", "1F_counsel", "2F_science", "1F_admin");
            }
            if (stage >= 4)
            {
                Docs("diary_haeun", "doc_security", "doc_broadcast");
                Unlock("4F_staff");
                Give("key_principal", 1);
                Score(2);
            }
            if (stage >= 5)
            {
                gs.RemoveItem("key_principal", gs.Count("key_principal"));
                Docs("note_principal", "doc_principal_diary", "doc_ledger");
                gs.SetFlag("principal_done");
                gs.SetFlag("5f_autosaved");
                gs.SetFlag("badge_found");
                Unlock("1F_principal", "4F_store");
                FifthFloor.Apply();
                Give("badge", 1);
                Score(3);
            }
            gs.hp = gs.maxHp;
            foreach (Door d in Door.All) d.RefreshLock();

            Vector3 feet; float yaw = MapSpace.YawOf(0, 1);
            switch (stage)
            {
                case 0: feet = MapSpace.GF(1, 34.0f, 0.1f, 8.8f); yaw = MapSpace.YawOf(-1, 0.15f); break;
                case 1: feet = MapSpace.GF(2, 15.5f, 0.1f, 2.4f); yaw = MapSpace.YawOf(1, 0); break;
                case 2: feet = MapSpace.GF(3, 15.5f, 0.1f, 2.4f); yaw = MapSpace.YawOf(1, 0); break;
                case 3: feet = MapSpace.GF(4, 15.5f, 0.1f, 2.4f); yaw = MapSpace.YawOf(-1, 0); break;
                case 4: feet = MapSpace.GF(1, 16f, 0.1f, 2.2f); break;
                case 5: feet = MapSpace.GF(5, 19.6f, 0.1f, 2.3f); break;
                default: feet = MapSpace.GF(5, 82f, 0.1f, 10.6f); break;
            }
            m.Player.Teleport(feet, yaw);
            m.RefreshEnvironment();

            TeacherMonster t = TeacherMonster.I;
            if (t != null && stage >= 1 && !MonsterOff)
            {
                if (t.Active) t.Deactivate();
                t.Activate(FarStair(feet), false);
            }
            Notice(JumpNames[Mathf.Clamp(stage, 0, JumpNames.Length - 1)] + " 로 이동");

            if (stage >= 7 && FinaleDirector.I != null)
            {
                gs.RemoveItem("badge");
                FinaleDirector.I.Enter();
            }
        }

        static void Give(string id, int atLeast)
        {
            GameState gs = GameState.Current;
            int have = gs.Count(id);
            if (have < atLeast) gs.AddItem(id, atLeast - have);
        }

        static void Docs(params string[] ids) { foreach (string d in ids) GameState.Current.AddDoc(d); }

        static void Unlock(params string[] ids) { foreach (string d in ids) GameState.Current.SetFlag("unlocked:" + d); }

        static void Score(int pieces)
        {
            GameState gs = GameState.Current;
            gs.RemoveItem("score_piece", gs.Count("score_piece"));
            gs.RemoveItem("score_full", gs.Count("score_full"));
            if (pieces >= 3) gs.AddItem("score_full");
            else gs.AddItem("score_piece", pieces);
        }

        void GiveEverything()
        {
            foreach (string id in new[] { "flashlight", "key_admin", "key_science", "key_staff2", "key_nurse", "key_counsel", "key_principal", "key_storage", "badge", "fountain_pen" }) Give(id, 1);
            Give("paper", 5); Give("pen", 5); Give("medkit", 3); Give("drink", 2); Give("empty_can", 5); Give("battery", 3);
            Score(3);
            GameState.Current.SetFlag("vending_enabled");
            Notice("아이템을 전부 넣었어요");
        }

        void UnlockAllDoors()
        {
            foreach (Door d in Door.All)
            {
                if (d.lockType == DoorLock.Special) continue;
                GameState.Current.SetFlag("unlocked:" + d.id);
                d.RefreshLock();
            }
            Notice("모든 문의 잠금을 풀었어요 (특수학급 같은 영구 잠김 포함)");
        }

        // ================================================================ 화면
        GUIStyle box, title, btn, small;

        void Styles()
        {
            if (box != null) return;
            Font f = UIKit.UIFont;
            box = new GUIStyle(GUI.skin.box) { font = f, fontSize = 15, alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 6, 6) };
            box.normal.textColor = new Color(0.95f, 0.85f, 0.6f);
            title = new GUIStyle(GUI.skin.label) { font = f, fontSize = 20, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(0.96f, 0.78f, 0.5f);
            btn = new GUIStyle(GUI.skin.button) { font = f, fontSize = 16, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 8, 6, 6) };
            small = new GUIStyle(GUI.skin.label) { font = f, fontSize = 14, wordWrap = true };
            small.normal.textColor = new Color(0.75f, 0.76f, 0.78f);
        }

        void OnGUI()
        {
            if (!Enabled) return;
            Styles();
            float k = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
            float W = Screen.width / k;

            // 오른쪽 위 상태 표시
            string status = "DEV  [F1] 메뉴   " +
                            "[F2] 밝게 " + Mark(FullBright) + "   [F3] 대화스킵 " + Mark(SkipDialogue) + "   [F4] 무적 " + Mark(God) +
                            "   [F5] 선생님 " + (MonsterOff ? "꺼짐" : "켜짐") + "   [F6] 3배속 " + Mark(Fast);
            GUI.Box(new Rect(W - 900, 8, 890, 30), status, box);

            if (!panel) return;
            Rect r = new Rect(W * 0.5f - 330, 90, 660, 860);
            GUI.Box(r, "");
            GUI.Box(r, "");
            GUILayout.BeginArea(new Rect(r.x + 18, r.y + 14, r.width - 36, r.height - 28));
            GUILayout.Label("개발자 메뉴", title);
            GUILayout.Label("[F1]/[Esc] 닫기 · 이동하면 그 지점까지 얻었을 아이템·문서·진행 상태가 채워져요.", small);
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Space(6);
            GUILayout.Label("보기 / 진행", title);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("밝게 " + Mark(FullBright), btn)) SetBright(!FullBright);
            if (GUILayout.Button("대화 스킵 " + Mark(SkipDialogue), btn)) { SkipDialogue = !SkipDialogue; Notice("대화 스킵 " + OnOff(SkipDialogue)); }
            if (GUILayout.Button("무적 " + Mark(God), btn)) { God = !God; Notice("무적 " + OnOff(God)); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("선생님 " + (MonsterOff ? "켜기" : "끄기"), btn)) SetMonster(!MonsterOff);
            if (GUILayout.Button("3배속 " + Mark(Fast), btn)) { Fast = !Fast; Notice("3배속 " + OnOff(Fast)); }
            GUILayout.EndHorizontal();

            PrologueDirector pro = GetComponent<PrologueDirector>();
            if (pro != null && pro.Playing)
            {
                GUILayout.Space(8);
                GUILayout.Label("프롤로그", title);
                if (GUILayout.Button("무서운 얘기 장면으로 건너뛰기", btn)) { SetPanel(false); pro.DevSkipToStories(); }
                if (GUILayout.Button("프롤로그 끝내고 본편으로", btn)) RequestJump(0);
            }

            GUILayout.Space(8);
            GUILayout.Label("구간 이동 (본편)", title);
            for (int i = 0; i < JumpNames.Length; i++)
                if (GUILayout.Button(JumpNames[i], btn)) RequestJump(i);

            MainDirector m = MainDirector.I;
            if (m != null && m.Running)
            {
                GUILayout.Space(8);
                GUILayout.Label("도구", title);
                if (GUILayout.Button("체력 가득 · 배터리 가득", btn)) { GameState.Current.hp = GameState.Current.maxHp; GameState.Current.battery = 100f; Notice("체력/배터리 회복"); }
                if (GUILayout.Button("아이템 전부 받기", btn)) GiveEverything();
                if (GUILayout.Button("모든 문 잠금 풀기", btn)) UnlockAllDoors();
                if (GUILayout.Button("5층 붉게 변하기 (교장실 이후 모습)", btn)) { GameState.Current.SetFlag("principal_done"); FifthFloor.Apply(); m.RefreshEnvironment(); Notice("5층이 변했어요"); }
            }
            GUILayout.Space(8);
            if (GUILayout.Button("저장 기록 지우기", btn)) { SaveSystem.Delete(); Notice("저장 기록을 지웠어요"); }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static string Mark(bool b) { return b ? "●" : "○"; }
    }
}
