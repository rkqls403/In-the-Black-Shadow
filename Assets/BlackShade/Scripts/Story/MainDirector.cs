using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using S = BlackShade.PrologueScript;
using LY = BlackShade.ClassroomFrame.Layout;

namespace BlackShade
{
    // 본편 감독. 프롤로그가 끝나면(또는 저장된 기록에서) 시작한다.
    //   · 맵에 문, 형광등 스위치, 숨을 곳, 단서, 자판기, 게시판, 비상용품함을 배치
    //   · 이야기 진행: 도서실 책 → 선생님 등장 / 교장실 문서 3장 → "거기 있었구나?" + 5층 변화
    //   · 체력, 사망, 기록 저장/불러오기
    // 챕터 구분 없이 자연스럽게 이어지도록 했어요. 아래 '단서 배치' 부분이 1~5장 내용이에요.
    public class MainDirector : MonoBehaviour
    {
        public static MainDirector I { get; private set; }

        [Header("(선택) 선생님 괴물 모델. 비우면 임시 모습")]
        public GameObject monsterModel;

        public FirstPersonController Player { get; private set; }
        public PlayerInteractor Interactor { get; private set; }
        public CanThrower Thrower { get; private set; }
        public bool Running { get; private set; }

        public bool PlayerCanAct { get { return Running && !dead && !cutscene && !UIState.ModalOpen; } }
        public bool AllowPause { get { return !dead && (FinaleDirector.I == null || !FinaleDirector.I.InCeremony); } }
        public int ScorePieces { get { GameState g = GameState.Current; return g.Has("score_full") ? 3 : g.Count("score_piece"); } }

        GameUI ui;
        AudioDirector sfx;
        FinaleDirector finale;
        bool dead, cutscene, built;
        readonly Dictionary<string, ModuleFrame> frames = new Dictionary<string, ModuleFrame>();
        GameObject reagentLock;   // 시약장 번호 자물쇠 모델
        Shutter shutterN, shutterB;
        readonly List<Light> lobbyLights = new List<Light>();
        readonly List<Renderer> lobbyGlow = new List<Renderer>();
        float lobbyTimer;
        int lastFloor = -1;
        Transform worldRoot;

        static readonly Color Wood = new Color(0.42f, 0.3f, 0.2f);
        static readonly Color DarkWood = new Color(0.25f, 0.16f, 0.1f);
        static readonly Color Steel = new Color(0.5f, 0.53f, 0.55f);
        static readonly Color White = new Color(0.85f, 0.85f, 0.83f);

        void Awake() { I = this; }

        public void UsePlayer(FirstPersonController p, PlayerInteractor i)
        {
            Player = p; Interactor = i;
            Thrower = p.GetComponent<CanThrower>();
            if (Thrower == null) Thrower = p.gameObject.AddComponent<CanThrower>();
        }

        public void SetCutscene(bool on) { cutscene = on; Interactor.Busy = on; }

        // ================================================================ 시작
        // 프롤로그가 끝난 직후 (화면은 이미 깜깜하고 눈은 감긴 상태)
        public void StartAfterPrologue()
        {
            GameState.Current = new GameState();
            BuildWorld();
            SaveAt("start", "1층 로비");
            StartCoroutine(WakeInLobby());
        }

        // 개발용: 프롤로그 없이 본편부터
        public void StartNew()
        {
            ui = GameUI.I;
            ui.SetFade(1f);
            ui.SetEyes(0f);
            StartAfterPrologue();
        }

        public void LoadFromSave(SaveData data)
        {
            ui = GameUI.I;
            GameState.Current = GameState.FromSave(data);
            ui.SetFade(1f);
            ui.SetEyes(0f);
            BuildWorld();
            StartCoroutine(WakeAtSave(data));
        }

        // ================================================================ 세계 만들기
        void BuildWorld()
        {
            if (built) return;
            built = true;
            ui = GameUI.I;
            sfx = AudioDirector.I;
            worldRoot = new GameObject("MainWorld").transform;

            // 프롤로그 정리
            LightingDirector ld = GetComponent<LightingDirector>();
            if (ld != null && ld.Sun != null) ld.ShutdownForMain();
            ClassroomFrame cf = GetComponent<ClassroomFrame>();
            if (cf != null) cf.RemoveInvisibleBounds();

            // 어둠: 하늘 없음, 아주 낮은 주변광, 검은 안개 (손전등 끝이 어둠에 먹힘)
            LightCheck.alwaysLit = false;
            RoomLights.Init();
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.021f, 0.028f);
            RenderSettings.reflectionIntensity = 0.05f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.black;
            RenderSettings.fogDensity = 0.055f;
            Player.Cam.clearFlags = CameraClearFlags.SolidColor;
            Player.Cam.backgroundColor = Color.black;
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 8);

            // 맵의 형광등 발광 부분은 기본으로 끔 (스위치를 켜야 켜짐)
            foreach (Renderer r in FindObjectsOfType<Renderer>(true))
            {
                string path = ClassroomFrame.PathOf(r.transform);
                if (path.Contains("/light/emissive/")) r.enabled = false;
                if (path.Contains("/Entrance/") && path.Contains("/emissive/")) { r.enabled = true; lobbyGlow.Add(r); }
            }

            Flashlight.Create(Player.Cam);
            Decor.Ensure();                  // 가구·천장 조명·소품 모델·담장 (씬에 구워져 있으면 그대로 씀)
            BuildRooms();
            BuildFloorsCommon();
            BuildLobby();
            PlaceClues();
            ScatterItems();
            PlaceModels();

            // 선생님 괴물
            TeacherMonster m = TeacherMonster.Create(monsterModel, Player);
            m.SetStairSpots(new[] { MapSpace.GF(1, 18.7f, 0f, 2.4f), MapSpace.GF(1, 81.5f, 0f, 2.6f) });
            if (GameState.Current.Flag("principal_done")) FifthFloor.Apply();

            finale = GetComponent<FinaleDirector>();
            if (finale == null) finale = gameObject.AddComponent<FinaleDirector>();
            finale.Init(this);
        }

        // ---------------------------------------------------------------- 방 (문, 불, 숨을 곳)
        void BuildRooms()
        {
            foreach (RoomSpec r in MapData.Rooms)
            {
                ModuleFrame mf = null;
                if (r.IsModule)
                {
                    Transform node = ClassroomFrame.FindByName(r.mapNode);
                    mf = ModuleFrame.Compute(node);
                    if (mf == null) { Debug.LogWarning("[Main] 교실 모듈을 못 찾음: " + r.mapNode); continue; }
                    frames[r.id] = mf;
                    foreach (ModuleFrame.SlidingDoorParts p in mf.PrepareSlidingDoors())
                    {
                        DoorSpec spec = r.doors.Find(d => d.id.EndsWith(p.front ? "_F" : "_B"));
                        if (spec != null) Door.CreateSliding(p, spec, r.name);
                    }
                    if (r.hideSpot) HideSpot.CreateCabinet(mf.P(0.55f, 7.72f, 0f), mf.Forward);
                }
                else
                {
                    foreach (DoorSpec d in r.doors)
                        Door.CreateHinged(r, d, r.id == "5F_hall" ? new Color(0.35f, 0.08f, 0.08f) : r.id == "1F_principal" ? DarkWood : Wood);
                }
                if (r.lights) RoomLights.Create(r, mf);
                if (r.toilet) BuildStalls(r);
            }
            // 일반 방의 청소함 (문에서 먼 구석)
            Cabinet("1F_admin", 65.5f, 11.9f, 0, -1);
            Cabinet("1F_library", 80.25f, 25.85f, 1, 0);
            Cabinet("1F_principal", 2.0f, 11.9f, 0, -1);
            Cabinet("1F_counsel", 68.42f, 25.9f, 1, 0);
            Cabinet("2F_staff", 68.42f, 26.9f, 1, 0);
            Cabinet("2F_science", 68.42f, 42.9f, 1, 0);
            Cabinet("3F_staff", 68.42f, 26.9f, 1, 0);
            Cabinet("3F_nurse", 68.42f, 39.5f, 1, 0);
            Cabinet("4F_staff", 68.42f, 26.9f, 1, 0);
            Cabinet("4F_broadcast", 68.42f, 42.9f, 1, 0);
            Cabinet("5F_computer", 1.0f, 11.9f, 0, -1);
            Cabinet("5F_art", 46.3f, 11.9f, 0, -1);
            Cabinet("5F_music", 48.0f, 11.9f, 0, -1);
        }

        void Cabinet(string roomId, float x, float z, float fx, float fz)
        {
            RoomSpec r = MapData.Get(roomId);
            if (r == null) return;
            HideSpot.CreateCabinet(MapSpace.GF(r.floor, x, 0f, z), MapSpace.Dir(fx, fz));
        }

        // 화장실 칸 4개 (문 반대쪽 벽을 따라)
        void BuildStalls(RoomSpec r)
        {
            bool west = r.doors.Count > 0 && r.doors[0].side == Side.E; // 문이 동쪽이면 칸은 서쪽 벽
            // 칸막이 모델은 폭 1.0m 기준으로 깊이 약 1.97m (모델 비율 그대로)
            float width = 1.0f;
            float depth = PropModels.Has("Cubicle") ? 1.97f : 1.5f;
            float wallX = west ? r.x0 + 0.15f : r.x1 - 0.15f;
            float cx = west ? wallX + depth * 0.5f : wallX - depth * 0.5f;
            Vector3 facing = MapSpace.Dir(west ? 1 : -1, 0);
            float z0 = r.z0 + 0.45f;
            for (int i = 0; i < 4; i++)
            {
                float z = z0 + width * (i + 0.5f);
                if (z + width * 0.5f > r.z1 - 0.3f) break;
                HideSpot.CreateStall(MapSpace.GF(r.floor, cx, 0f, z), facing, width, depth);
            }
            // 세면대
            Looks.Furniture(worldRoot, "Sink", MapSpace.GF(r.floor, west ? r.x1 - 0.6f : r.x0 + 0.6f, 0f, (r.z0 + r.z1) * 0.5f + 1.5f), 0f, new Vector3(0.5f, 0.85f, 1.6f), White);
        }

        // ---------------------------------------------------------------- 층마다 공통
        void BuildFloorsCommon()
        {
            for (int f = 1; f <= 5; f++)
            {
                // 게시판(기록 저장)은 벽에 걸려 있음: 2~4층은 로비 서쪽 벽, 1·5층은 복도 남쪽 벽
                if (f >= 2 && f <= 4)
                    SaveBoard.Create(f + "F", f + "층 로비 게시판", MapSpace.GF(f, MapSpace.XS[2] + 0.005f, 0f, 8.36f), MapSpace.YawOf(1, 0));
                else
                    SaveBoard.Create(f + "F", f + "층 복도 게시판", MapSpace.GF(f, f == 1 ? 12.5f : 15.5f, 0f, MapSpace.CORR_END - 0.005f), MapSpace.YawOf(0, -1));
                if (f >= 2)
                {
                    EmergencyBox.Create(f + "F", MapSpace.GF(f, 13.6f, 0f, 3.95f), MapSpace.YawOf(0, -1));
                    if (f <= 4) VendingMachine.Create(f + "F", MapSpace.GF(f, 22.2f, 0f, 11.78f), MapSpace.YawOf(0, -1));
                }
                // 비상구 유도등 (초록) - 계단 쪽
                ExitSign(MapSpace.GF(f, 15.9f, 2.4f, 0.65f), MapSpace.Dir(0, 1));
                ExitSign(MapSpace.GF(f, 83.6f, 2.4f, 3.2f), MapSpace.Dir(-1, 0));
            }
            // 1층 계단 방화 셔터 (도서실 사건 전까지)
            GameState gs = GameState.Current;
            shutterN = Shutter.Create("N", 1, new Vector2(16.74f, 0.08f), new Vector2(20.74f, 0.08f), Vector2.zero);
            shutterB = Shutter.Create("B", 1, new Vector2(83.9f, 0.62f), new Vector2(83.9f, 5.75f), new Vector2(87.74f, 5.75f));
            if (gs.Flag("shutter_open")) { shutterN.SetOpenInstant(); shutterB.SetOpenInstant(); }
        }

        void ExitSign(Vector3 pos, Vector3 normal)
        {
            GameObject s = Prop.Box(worldRoot, "ExitSign", pos, Quaternion.LookRotation(normal), new Vector3(0.42f, 0.15f, 0.05f), new Color(0.1f, 0.5f, 0.2f), false);
            Prop.SetEmission(s.GetComponent<Renderer>(), new Color(0.15f, 0.9f, 0.35f) * 0.9f);
            // 받은 비상구 유도등 모델 (스스로 빛나는 초록 그림)
            if (PropModels.Place("ExitSign", s.transform, pos - normal * 0.025f - Vector3.up * 0.09f + normal * 0.09f, normal, new Vector3(0.42f, 0f, 0f), PropModels.Fit.Inside, "ExitSignModel") != null)
                s.GetComponent<Renderer>().enabled = false;
            GameObject lg = new GameObject("ExitGlow"); lg.transform.SetParent(s.transform, false);
            lg.transform.position = pos + normal * 0.3f;
            Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.2f, 1f, 0.4f); l.range = 3.5f; l.intensity = 0.35f;
            l.renderMode = LightRenderMode.ForcePixel;   // 밀려나서 바닥 전체를 밝히지 않게
        }

        // ---------------------------------------------------------------- 1층 로비
        void BuildLobby()
        {
            // 지직거리는 로비 불빛 (이 범위는 손전등 없이도 보임)
            RoomLights.litZones.Add(new KeyValuePair<int, Rect>(1, new Rect(20.74f, 0f, 26.32f, 10.1f)));
            // 빛은 현관 모델의 천장 형광등 바로 아래에서 (홀 3×2 + 북쪽 복도 줄 3개). 그림자를 켜서 2·3층으로 새지 않게.
            foreach (Vector2 p in new[] { new Vector2(30.93f, 5.15f), new Vector2(36.98f, 5.15f), new Vector2(30.93f, 8.56f), new Vector2(36.98f, 8.56f),
                                          new Vector2(27.33f, 2.1f), new Vector2(33.9f, 2.1f), new Vector2(40.48f, 2.1f) })
            {
                GameObject g = new GameObject("LobbyLight");
                g.transform.SetParent(worldRoot, false);
                g.transform.position = MapSpace.GF(1, p.x, 2.75f, p.y);
                Light l = g.AddComponent<Light>();
                l.type = LightType.Point; l.range = 5f; l.intensity = 0.7f; l.color = new Color(0.85f, 0.9f, 1f);
                RoomLights.MakeRoomBound(l);
                lobbyLights.Add(l);
            }
            EmergencyBox.Create("1F", MapSpace.GF(1, 28.15f, 0f, 8.85f), MapSpace.YawOf(1, 0));   // 신발장 옆 벽 (유리문 앞을 막지 않게)

            // 현관 유리문: 열리지 않는다
            GameObject glass = new GameObject("FrontDoor");
            glass.transform.SetParent(worldRoot, false);
            glass.transform.position = MapSpace.GF(1, 35.45f, 1.1f, 9.9f);
            Interactable it = glass.AddComponent<Interactable>();
            it.oneShot = false; it.range = 2.4f; it.prompt = "현관문";
            it.onUse = () => StartCoroutine(FrontDoor());
        }

        IEnumerator FrontDoor()
        {
            sfx.PlayAt(sfx.rattle, MapSpace.GF(1, 35.45f, 1.1f, 9.9f), 0.9f, 0.8f);
            ui.Toast("현관문이 열리지 않는다.");
            if (GameState.Current.Flag("tried_front")) yield break;
            GameState.Current.SetFlag("tried_front");
            SetCutscene(true);
            yield return new WaitForSeconds(0.8f);
            sfx.Play(sfx.staticNoise, 0.35f, 1.3f);
            yield return ui.Say(S.Me, "…밖이 아무것도 안 보여.", -1f, true);
            yield return ui.Say(S.Me, "운동장도, 가로등도… 그냥 새까매.", -1f, true);
            ui.HideDialogue();
            SetCutscene(false);
        }

        // ================================================================ 단서 배치 (1~5장)
        Vector3 G(int f, float x, float z, float h = 0f) { return MapSpace.GF(f, x, h, z); }

        // 일반 가구
        GameObject Furn(int f, string name, float x, float z, Vector3 size, Color c)
        { return Looks.Furniture(worldRoot, name, G(f, x, z), 0f, size, c); }

        // 교실 책상 위 위치
        Vector3 Desk(string roomId, int row, int col, float du = 0f, float dv = 0f)
        {
            ModuleFrame mf;
            if (!frames.TryGetValue(roomId, out mf)) return Vector3.zero;
            return mf.P(LY.DeskRowU[row] - 0.15f + du, LY.DeskColV[col] + dv, LY.DeskTopH + 0.01f);
        }

        // 교실 교탁 (앞쪽) 만들고 위치 돌려줌
        Vector3 Lectern(string roomId)
        {
            ModuleFrame mf;
            if (!frames.TryGetValue(roomId, out mf)) return Vector3.zero;
            Looks.Furniture(worldRoot, "Lectern", mf.P(1.55f, 4.15f, 0f), mf.Yaw(1, 0), new Vector3(0.9f, 1.05f, 0.5f), Wood);
            return mf.P(1.55f, 4.15f, 1.06f);
        }

        void Pick(string pid, GameObject visual, Vector3 focus, string prompt, string item, string doc, bool keep = false, System.Action taken = null, int count = 1)
        {
            WorldPickup p = WorldPickup.Create(pid, visual, focus, prompt, item, doc, keep);
            if (p != null) { p.count = count; p.onTaken = taken; }
        }

        // 기록(저장)용 종이와 펜은 서로 다른 곳에 하나씩 놓여 있어요 (짝으로 붙어 있지 않음)
        void PaperAt(string pid, Vector3 at)
        {
            if (at == Vector3.zero) return;   // 교실을 못 찾음
            Pick(pid + "_paper", Looks.Paper(at, Random.Range(0, 360)), at, "종이 줍기", "paper", null);
        }
        void PenAt(string pid, Vector3 at)
        {
            if (at == Vector3.zero) return;
            Pick(pid + "_pen", Looks.Pen(at, Random.Range(0, 360)), at + Vector3.up * 0.02f, "펜 줍기", "pen", null);
        }
        // 회복·소모 아이템 하나 (item: medkit / bandage / chocobar / battery / drink)
        void ItemAt(string pid, string item, Vector3 at)
        {
            if (at == Vector3.zero) return;
            GameObject v; string prompt;
            switch (item)
            {
                case "medkit": v = Looks.Medkit(at, Random.Range(0, 360)); prompt = "구급상자 줍기"; break;
                case "bandage": v = Looks.Bandage(at, Random.Range(0, 360)); prompt = "밴드 줍기"; break;
                case "chocobar": v = Looks.Choco(at); prompt = "초코바 줍기"; break;
                case "battery": v = Looks.Battery(at); prompt = "건전지 줍기"; break;
                default: v = Looks.Can(at); prompt = "캔 음료 줍기"; break;
            }
            Pick(pid, v, at + Vector3.up * 0.05f, prompt, item, null);
        }

        // 여기저기 흩어 둔 종이·펜·회복 아이템 (2~4층 교실은 모두 열려 있음)
        void ScatterItems()
        {
            // ---- 종이 (8장)
            PaperAt("admin", G(1, 52.6f, 10.15f, 0.745f));          // 1층 행정실 책상
            PaperAt("counsel", G(1, 71.3f, 17f, 0.73f));            // 1층 상담실 탁자
            PaperAt("lib", G(1, 83.9f, 16.7f, 0.76f));              // 1층 도서실 탁자
            PaperAt("c1_1", Desk("2F_1-1", 1, 2));
            PaperAt("c1_6", Desk("2F_1-6", 0, 3));
            PaperAt("c2_4", Desk("3F_2-4", 4, 0));
            PaperAt("c3_5", Desk("4F_3-5", 2, 0));
            PaperAt("computer", G(5, 4.45f, 5.95f, 0.745f));        // 5층 컴퓨터실 앞줄 책상
            // ---- 펜 (8자루) — 종이와는 다른 방에
            PenAt("lib", G(1, 82.95f, 21.75f, 0.76f));              // 1층 도서실 다른 탁자
            PenAt("staff1", G(2, 70.05f, 23.6f, 0.745f));           // 2층 1학년 교무실 책상
            PenAt("c1_2", Desk("2F_1-2", 3, 1));
            PenAt("c2_1", Desk("3F_2-1", 0, 0));
            PenAt("c2_7", Desk("3F_2-7", 2, 3));
            PenAt("nurse", G(3, 73.95f, 40.6f, 0.745f));            // 3층 양호실 책상
            PenAt("c3_7", Desk("4F_3-7", 4, 2));
            PenAt("music", G(5, 55.0f, 9.6f, 0.725f));              // 5층 음악실 책상
            // ---- 회복·건전지
            ItemAt("lib_drink", "drink", G(1, 84.15f, 16.25f, 0.76f));
            ItemAt("c1_2_bandage", "bandage", Desk("2F_1-2", 0, 2));
            ItemAt("c1_4_choco", "chocobar", Desk("2F_1-4", 1, 3));
            ItemAt("c1_7_battery", "battery", Desk("2F_1-7", 4, 1));
            ItemAt("staff2_drink", "drink", G(3, 70.15f, 15.65f, 0.745f));
            ItemAt("c2_2_drink", "drink", Desk("3F_2-2", 1, 0));
            ItemAt("c2_3_choco", "chocobar", Desk("3F_2-3", 2, 1));
            ItemAt("c2_5_battery", "battery", Desk("3F_2-5", 3, 2));
            ItemAt("c3_2_medkit", "medkit", Desk("4F_3-2", 0, 1));
            ItemAt("c3_4_bandage", "bandage", Desk("4F_3-4", 2, 3));
            ItemAt("c3_6_choco", "chocobar", Desk("4F_3-6", 3, 0));
            ItemAt("store_battery", "battery", G(4, 71.1f, 11.85f, 0.482f));   // 4층 창고 선반 아래 칸 빈자리
            ItemAt("music_choco", "chocobar", G(5, 60.0f, 5.7f, 0.725f));
        }

        void PlaceClues()
        {
            Color paperC = new Color(0.93f, 0.91f, 0.84f);

            // ---------------------------------------------------------- 1층 (1장)
            // 로비 신발장: 숨는 법을 알려 주는 쪽지
            Pick("shoe_note", null, G(1, 39.55f, 6.0f, 1.2f), "신발장 살펴보기", null, "note_shoe", true);
            // 소화전함 → 행정실 열쇠
            Vector3 hyd = G(1, 54.5f, 0.72f, 1.0f);
            GameObject hb = Prop.Box(worldRoot, "Hydrant", hyd, Quaternion.LookRotation(MapSpace.Dir(0, 1)), new Vector3(0.75f, 0.95f, 0.22f), new Color(0.75f, 0.1f, 0.08f), true);
            Prop.Box(hb.transform, "Label", hyd + MapSpace.Dir(0, 1) * 0.12f + Vector3.up * 0.3f, Quaternion.LookRotation(MapSpace.Dir(0, 1)), new Vector3(0.5f, 0.1f, 0.01f), White, false);
            // 받은 소화전함 모델 (벽에 붙임) — 상자는 충돌용으로만
            if (PropModels.Place("Hydrant", hb.transform, G(1, 54.5f, MapSpace.NORTH_T + 0.1f, 0.42f), MapSpace.Dir(0, 1), new Vector3(0f, 1.16f, 0f), PropModels.Fit.Height, "HydrantModel") != null)
                foreach (Renderer r in hb.GetComponentsInChildren<Renderer>(true)) if (r.transform.parent == hb.transform || r.gameObject == hb) r.enabled = false;
            Pick("key_admin", hb, hyd + MapSpace.Dir(0, 1) * 0.15f, "소화전함 열어 보기", "key_admin", null, true, () =>
            {
                sfx.PlayAt(sfx.cabinet, hyd, 0.6f, 1.2f);
                ui.Toast("소화전 호스 사이에 열쇠가 끼워져 있었다. " + Josa.Quote("행정실 열쇠") + " 얻었다.");
            });

            // 행정실
            Pick("doc_program", Looks.Paper(G(1, 52.75f, 8.55f, 0.745f), 8f, new Color(0.82f, 0.76f, 0.6f)), G(1, 52.75f, 8.55f, 0.8f), "오래된 서류 살펴보기", null, "doc_program");
            Pick("doc_expense", Looks.Paper(G(1, 56.75f, 8.55f, 0.745f), -5f), G(1, 56.75f, 8.55f, 0.8f), "서류 살펴보기", null, "doc_expense");
            Pick("doc_certificate", null, G(1, 61f, 11.65f, 1.0f), "서류함 뒤져 보기", null, "doc_certificate");

            // 도서실
            Pick("book_folk", Looks.Book(G(1, 83.5f, 21.5f, 0.76f), 20f, new Color(0.35f, 0.12f, 0.1f)), G(1, 83.5f, 21.5f, 0.82f), "오래된 책 펼쳐 보기", null, "book_folk", true);
            Pick("lib_bandage", Looks.Bandage(G(1, 83.2f, 16.4f, 0.76f), 20f), G(1, 83.2f, 16.4f, 0.82f), "밴드 줍기", "bandage", null);

            // 교장실 (나중에)
            Pick("note_principal", Looks.Paper(G(1, 5.6f, 9.1f, 0.885f), 12f, paperC), G(1, 5.6f, 9.1f, 0.92f), "책상 위 메모 살펴보기", null, "note_principal", false, CheckPrincipal);
            Pick("doc_principal_diary", null, G(1, 6.6f, 8.6f, 0.65f), "책상 서랍 열어 보기", null, "doc_principal_diary", false, CheckPrincipal);
            Pick("doc_ledger", Looks.Book(G(1, 0.85f, 8f, 1.2f), 90f, new Color(0.15f, 0.12f, 0.1f)), G(1, 0.85f, 8f, 1.25f), "장부 꺼내 보기", null, "doc_ledger", false, CheckPrincipal);
            Pick("key_storage", Looks.Key(G(1, 6.2f, 9.3f, 0.885f), 30f), G(1, 6.2f, 9.3f, 0.92f), "열쇠 줍기", "key_storage", null);

            // 상담실 (나중에)
            Pick("doc_counsel", null, G(1, 68.9f, 21.5f, 1.0f), "상담 기록철 살펴보기", null, "doc_counsel");

            // ---------------------------------------------------------- 2층 1학년 (2장)
            Pick("note_vending", Looks.Note(G(2, Decor.BenchX[0] + 0.2f, Decor.BenchZ - 0.09f, 0.475f), 15f), G(2, Decor.BenchX[0] + 0.2f, Decor.BenchZ - 0.09f, 0.52f), "벤치 위 쪽지 줍기", null, "note_vending", false, () =>
            {
                GameState.Current.SetFlag("vending_enabled");
                ui.ShowBanner(null, "이제 음료 자판기를 쓸 수 있다 · 빈 캔은 던져서 선생님을 유인할 수 있다");
                StartCoroutine(HideBannerLater(6f));
            });
            Pick("c1_3_choco", Looks.Choco(Desk("2F_1-3", 3, 0)), Desk("2F_1-3", 3, 0), "초코바 줍기", "chocobar", null);
            Pick("note_wonyul", Looks.Book(Desk("2F_1-5", 2, 1), 5f, new Color(0.25f, 0.45f, 0.7f)), Desk("2F_1-5", 2, 1) + Vector3.up * 0.05f, "공책 살펴보기 (이원율)", null, "note_wonyul", true);

            // 1학년 교무실: 과학실 열쇠 + 업무 분장표
            Pick("key_science", null, G(2, 68.35f, 16f, 1.2f), "열쇠함 열어 보기", "key_science", null);
            Pick("doc_duty", Looks.Paper(G(2, 72.55f, 23.55f, 0.745f), -8f, new Color(0.82f, 0.76f, 0.6f)), G(2, 72.55f, 23.55f, 0.8f), "낡은 서류 살펴보기", null, "doc_duty");

            // 과학실: 화학 퍼즐 → 시약장 → 악보 조각 ①
            Pick("doc_report", Looks.Paper(G(2, 69.95f, 31.75f, 0.83f), 4f), G(2, 69.95f, 31.75f, 0.88f), "실험 보고서 살펴보기", null, "doc_report");
            Vector3 bottles = G(2, 69.95f, 33.6f, 0.83f);   // 시약장 옆 실험대
            if (PropModels.Place("Flasks", worldRoot, bottles, MapSpace.Dir(1, 0), new Vector3(0.36f, 0f, 0f), PropModels.Fit.Inside, "Flasks") == null)
            {
                Looks.Small(bottles + MapSpace.Dir(0, -1) * 0.3f, new Color(0.8f, 0.85f, 0.9f), new Vector3(0.08f, 0.22f, 0.08f));
                Looks.Small(bottles, new Color(0.7f, 0.85f, 0.75f), new Vector3(0.08f, 0.22f, 0.08f));
                Looks.Small(bottles + MapSpace.Dir(0, 1) * 0.3f, new Color(0.9f, 0.9f, 0.95f), new Vector3(0.08f, 0.22f, 0.08f));
            }
            MakeUse("chem", bottles + Vector3.up * 0.15f, "실험대의 시약병 살펴보기", true, ChemPuzzle);
            MakeUse("reagent", G(2, Decor.ReagentX + 0.45f, Decor.ReagentZ, 1.2f), "시약장 (번호 자물쇠)", true, ReagentCabinet);

            // ---------------------------------------------------------- 3층 2학년 (3장)
            Vector3 lec = Lectern("3F_2-2");
            Pick("key_staff2", null, lec, "교탁 서랍 열어 보기", "key_staff2", null);
            Pick("c2_4_bandage", Looks.Bandage(Desk("3F_2-4", 2, 2), 160f), Desk("3F_2-4", 2, 2) + Vector3.up * 0.05f, "밴드 줍기", "bandage", null);
            ModuleFrame mf6;
            if (frames.TryGetValue("3F_2-6", out mf6))
            {
                Looks.Furniture(worldRoot, "ClassLibrary", mf6.P(9.45f, 0.55f, 0f), mf6.Yaw(-1, 0), new Vector3(0.8f, 1.2f, 0.35f), Wood);
                Pick("note_album", null, mf6.P(9.3f, 0.55f, 0.9f), "학급문고 뒤져 보기", null, "note_album");
            }
            // 2학년 교무실: 압수물품(만년필 + 쪽지, 책상 위) + 양호실 열쇠
            Vector3 fpen = G(3, 72.75f, 15.72f, 0.745f);
            Pick("fountain_pen", Looks.FountainPen(fpen, 80f), fpen + Vector3.up * 0.03f, "만년필 줍기", "fountain_pen", null, false, () =>
                ui.Toast("이름이 검게 지워진 만년필이다. 옆에 압수된 쪽지도 한 장 있다."));
            Pick("note_confiscated", Looks.Note(G(3, 72.45f, 15.55f, 0.745f), 30f), G(3, 72.45f, 15.55f, 0.8f), "압수된 쪽지 살펴보기", null, "note_confiscated");
            Pick("key_nurse", null, G(3, 68.35f, 16f, 1.2f), "열쇠함 열어 보기", "key_nurse", null);
            // 양호실
            for (int i = 0; i < 5; i++)  // 핏자국
                Prop.Box(worldRoot, "Blood", G(3, 69.9f + i * 0.35f, 32.5f + Mathf.Sin(i) * 0.4f, 0.005f), Quaternion.Euler(0, i * 40f, 0), new Vector3(0.35f - i * 0.04f, 0.004f, 0.25f), new Color(0.25f, 0.02f, 0.02f), false);
            Pick("doc_medical", Looks.Clipboard(G(3, 73.2f, 40.55f, 0.745f), 6f), G(3, 73.2f, 40.55f, 0.8f), "진료 기록부 살펴보기", null, "doc_medical");
            Pick("doc_accident", null, G(3, 71f, 42.95f, 1.2f), "약장 안의 서류 꺼내 보기", null, "doc_accident");
            Pick("note_nurse", Looks.Note(G(3, 69.8f, 33.2f, 0.01f), 70f), G(3, 69.8f, 33.2f, 0.05f), "구겨진 메모 줍기", null, "note_nurse");
            Pick("key_counsel", null, G(3, 73.9f, 40.1f, 0.6f), "책상 서랍 열어 보기", "key_counsel", null);
            Pick("nurse_medkit", Looks.Medkit(G(3, 69.0f, 34.6f, 0.56f), 0f), G(3, 69.0f, 34.6f, 0.62f), "구급상자 줍기", "medkit", null);

            // ---------------------------------------------------------- 4층 3학년 (4장)
            Pick("diary_haeun", Looks.Book(Desk("4F_3-1", 3, 3), -12f, new Color(0.75f, 0.45f, 0.5f)), Desk("4F_3-1", 3, 3) + Vector3.up * 0.05f, "낡은 일기장 펼쳐 보기", null, "diary_haeun", true);
            Pick("c3_3_battery", Looks.Battery(Desk("4F_3-3", 1, 1)), Desk("4F_3-3", 1, 1), "건전지 줍기", "battery", null);
            Vector3 lec4 = Lectern("4F_3-4");
            Pick("score_2", null, lec4, "교탁 서랍 열어 보기", "score_piece", null, false, null);
            DecorateSeniorClasses();
            // 3학년 교무실 (번호 자물쇠 0215): 비상문 문서 + 교장실 열쇠
            Pick("doc_security", Looks.Paper(G(4, 70.05f, 23.55f, 0.745f), 3f, new Color(0.82f, 0.76f, 0.6f)), G(4, 70.05f, 23.55f, 0.8f), "서류 살펴보기", null, "doc_security");
            Pick("key_principal", Looks.Key(G(4, 70.4f, 23.75f, 0.745f), 70f), G(4, 70.4f, 23.75f, 0.8f), "열쇠 줍기", "key_principal", null);
            // 방송실
            GameObject onair = Prop.Box(worldRoot, "OnAir", G(4, 71.5f, 27.75f, 2.3f), Quaternion.LookRotation(MapSpace.Dir(0, 1)), new Vector3(0.6f, 0.2f, 0.05f), new Color(0.4f, 0.05f, 0.05f), false);
            Prop.SetEmission(onair.GetComponent<Renderer>(), new Color(1f, 0.1f, 0.05f) * 1.5f);
            // 받은 ON AIR 모델: 벽(z 27.71)에 붙여 폭 0.8m (비율 그대로 → 높이 약 0.26m, 두께 0.21m)
            if (PropModels.Place("OnAirSign", worldRoot, G(4, 71.5f, 27.71f + 0.11f, 2.17f), MapSpace.Dir(0, 1), new Vector3(0.8f, 0f, 0f), PropModels.Fit.Inside, "OnAirSign") != null)
                onair.GetComponent<Renderer>().enabled = false;
            Pick("doc_broadcast", Looks.Clipboard(G(4, 71.0f, 33f, 0.91f), 10f), G(4, 71.0f, 33f, 0.95f), "점검표 살펴보기", null, "doc_broadcast");
            MakeUse("tape", G(4, 72.2f, 33f, 1.0f), "녹음기 살펴보기", false, () => StartCoroutine(TapeDeck()));
            // 3학년 창고: 명찰 장식
            // 선반(70.7) 위에서 네 번째 칸(높이 1.22m)의 빈자리에 세워 둠
            Vector3 bp = G(4, 70.3f, 11.8f, 1.226f);
            Pick("badge", Looks.Badge(bp, MapSpace.Dir(0, -1)), bp + Vector3.up * 0.06f, "철쭉 명찰 장식 집기", "badge", null, false, () => StartCoroutine(BadgeFound()));

            // ---------------------------------------------------------- 5층 (5장)
            Pick("score_3", Looks.Paper(G(5, Decor.PianoX + 0.08f, Decor.PianoZ, 1.225f), 90f, new Color(0.85f, 0.8f, 0.65f)), G(5, Decor.PianoX + 0.08f, Decor.PianoZ, 1.26f), "피아노 위의 악보 살펴보기", "score_piece", null);
            Pick("art_medkit", Looks.Medkit(G(5, 33.2f, 6.5f, 0.745f), 10f), G(5, 33.2f, 6.5f, 0.9f), "구급상자 줍기", "medkit", null);
            Pick("art_choco", Looks.Choco(G(5, 33.9f, 6.3f, 0.745f)), G(5, 33.9f, 6.3f, 0.8f), "초코바 줍기", "chocobar", null);
            Pick("computer_battery", Looks.Battery(G(5, 9.35f, 10.05f, 0.745f)), G(5, 9.35f, 10.05f, 0.8f), "건전지 줍기", "battery", null);

            // 강당 문: 명찰 장식 + 완성된 악보가 있어야
            Door hall = Door.Find("5F_hall");
            if (hall != null) hall.onSpecialUse = HallDoor;
            GameObject sign = Prop.Box(worldRoot, "HallSign", G(5, 82f, 12.48f, 1.55f), Quaternion.LookRotation(MapSpace.Dir(0, -1)), new Vector3(0.3f, 0.42f, 0.005f), White, false);
            Prop.SetEmission(sign.GetComponent<Renderer>(), new Color(0.2f, 0.2f, 0.2f));
        }

        // 3학년 교실들: 조금 꾸며진 채로 버려진 느낌 (풍선 몇 개, 떨어진 가랜드)
        void DecorateSeniorClasses()
        {
            Color[] cols = { new Color(0.75f, 0.25f, 0.3f), new Color(0.3f, 0.45f, 0.7f), new Color(0.8f, 0.8f, 0.78f) };
            int k = 0;
            foreach (var kv in frames)
            {
                if (!kv.Key.StartsWith("4F_") || kv.Key == "4F_3-1") continue;
                ModuleFrame mf = kv.Value;
                for (int i = 0; i < 3; i++)
                {
                    float v = 2.5f + i * 1.6f;
                    // 바람이 빠져 바닥에 떨어진 풍선
                    GameObject bl = PropModels.Place("Balloon", worldRoot, mf.P(9.4f - i * 0.3f, v, 0f), Quaternion.Euler(0, i * 70f + k * 25f, 0) * Vector3.forward, new Vector3(0f, 0.26f, 0f), PropModels.Fit.Height, "OldBalloon");
                    if (bl != null)
                    {
                        // 쭈그러진 느낌: 납작하게 눕혀 놓기
                        bl.transform.rotation = Quaternion.Euler(80f + i * 5f, i * 70f + k * 25f, 0f) * Quaternion.identity;
                        bl.transform.localScale = Vector3.Scale(bl.transform.localScale, new Vector3(1f, 1f, 0.6f));
                        Bounds bb;
                        if (PropModels.WorldBounds(bl, out bb)) bl.transform.position += Vector3.up * (mf.P(0, 0, 0).y - bb.min.y);
                        PropModels.Tint(bl, Color.Lerp(cols[(i + k) % 3], Color.white, 0.35f));
                    }
                    else Prop.Shape(PrimitiveType.Sphere, worldRoot, "OldBalloon", mf.P(9.4f - i * 0.3f, v, 0.08f), Quaternion.identity, new Vector3(0.22f, 0.14f, 0.22f), cols[(i + k) % 3], false);
                }
                Prop.Box(worldRoot, "FallenGarland", mf.P(5f, 7.9f, 0.02f), Quaternion.LookRotation(mf.Forward), new Vector3(0.05f, 0.02f, 6f), cols[k % 3], false);
                // 졸업식 날 꾸며 둔 그대로: 뒷벽에 붙은 풍선 + 책상에 묶인 풍선 (색이 바랜 채)
                if (PropModels.Has("Balloon"))
                {
                    float[] wv = { 1.8f, 2.25f, 3.9f, 4.35f, 6.0f, 6.45f };
                    float[] wh = { 2.3f, 2.05f, 2.4f, 2.15f, 2.3f, 2.05f };
                    for (int i = 0; i < wv.Length; i++)
                    {
                        if ((i + k) % 5 == 4) continue;   // 몇 개는 떨어져 나감
                        Color bc = Color.Lerp(cols[(i + k) % 3], new Color(0.45f, 0.42f, 0.4f), 0.35f);
                        GameObject wb = Prop.Shape(PrimitiveType.Sphere, worldRoot, "WallBalloon", mf.P(ClassroomFrame.Layout.BackWallU - 0.2f, wv[i], wh[i]), Quaternion.identity, new Vector3(0.28f, 0.33f, 0.28f), bc, false);
                        PropModels.BalloonInside(wb, bc);
                    }
                    float[] du = ClassroomFrame.Layout.DeskRowU, dv = ClassroomFrame.Layout.DeskColV;
                    for (int i = 0; i < 2; i++)
                    {
                        float u = du[(1 + i * 2 + k) % du.Length] - 0.15f, vv = dv[(i * 3 + k) % dv.Length];
                        Color bc = Color.Lerp(cols[(i + k + 1) % 3], new Color(0.45f, 0.42f, 0.4f), 0.3f);
                        PropModels.BalloonOnString(worldRoot, mf.P(u + 0.12f, vv + 0.18f, ClassroomFrame.Layout.DeskTopH), 1.15f + i * 0.2f, 0.27f, bc, "DeskBalloon");
                    }
                }
                k++;
            }
        }

        // ---------------------------------------------------------------- 받은 소품 모델 배치 (임시 위치)
        // 모델이 없으면 PropModels.Place 가 null 을 돌려주고 아무것도 안 생겨요.
        void PlaceModels()
        {
            // 과학실 시약장: 번호 자물쇠
            reagentLock = PropModels.Place("Padlock", worldRoot, G(2, Decor.ReagentX + 0.27f, Decor.ReagentZ, 0.8f), MapSpace.Dir(1, 0), new Vector3(0f, 0.1f, 0f), PropModels.Fit.Height, "ReagentPadlock");
            if (reagentLock != null && GameState.Current.Flag("reagent_open")) reagentLock.SetActive(false);
        }

        // 조사만 하는 상호작용
        void MakeUse(string id, Vector3 focus, string prompt, bool needsLight, System.Action use)
        {
            GameObject g = new GameObject("Use_" + id);
            g.transform.SetParent(worldRoot, false);
            g.transform.position = focus;
            Interactable it = g.AddComponent<Interactable>();
            it.oneShot = false; it.prompt = prompt; it.needsLight = needsLight; it.range = 2.2f;
            it.onUse = use;
        }

        // ================================================================ 퍼즐 / 사건
        void ChemPuzzle()
        {
            GameState gs = GameState.Current;
            if (gs.Flag("chem_solved")) { ui.Toast("이미 비밀 글씨를 확인했다. 시약장 : 4 · 1 · 7"); return; }
            if (!gs.HasDoc("doc_report"))
            {
                ui.Toast("염산, 수산화나트륨 수용액, 증류수가 든 시약병이 있다.");
                return;
            }
            ModalUI.I.Choice("실험 보고서 뒷면에 어떤 용액을 뿌려 볼까?",
                new[] { "염산 (HCl)", "수산화나트륨 수용액 (NaOH)", "증류수", "그만두기" }, i =>
            {
                if (i == 1)
                {
                    gs.SetFlag("chem_solved");
                    sfx.Play(sfx.pickup, 0.5f, 0.8f);
                    gs.AddDoc("doc_report_revealed");
                    ModalUI.I.ShowDoc("doc_report_revealed", () => ui.Toast("시약장 비밀번호를 알아냈다."));
                }
                else if (i == 0) ui.Toast("종이 귀퉁이가 살짝 누렇게 변했다. 아무것도 나타나지 않는다.");
                else if (i == 2) ui.Toast("종이가 축축해졌을 뿐, 아무것도 나타나지 않는다.");
            });
        }

        void ReagentCabinet()
        {
            GameState gs = GameState.Current;
            if (gs.Flag("reagent_open")) { ui.Toast("시약장은 비어 있다."); return; }
            ModalUI.I.Keypad(3, "시약장에 세 자리 번호 자물쇠가 걸려 있다.", code =>
            {
                if (code == null) return;
                if (code == "417")
                {
                    gs.SetFlag("reagent_open");
                    if (reagentLock != null) reagentLock.SetActive(false);
                    sfx.Play(sfx.unlock, 0.8f);
                    gs.AddItem("score_piece");
                    sfx.Play(sfx.pickup, 0.6f);
                    ui.Toast("시약병 뒤에 누렇게 바랜 종이가 접혀 있었다. " + Josa.Quote("찢어진 악보 조각") + " 얻었다.");
                    OnScorePiece();
                }
                else { sfx.Play(sfx.rattle, 0.6f); ui.Toast("자물쇠가 열리지 않는다."); }
            });
        }

        IEnumerator TapeDeck()
        {
            SetCutscene(true);
            sfx.Play(sfx.staticNoise, 0.5f, 0.8f);
            yield return ui.Broadcast("…졸업식이… 시작되었습니다…", 2.4f);
            yield return ui.HideBroadcast(0.3f);
            sfx.Play(sfx.staticNoise, 0.4f, 0.7f);
            yield return ui.Say(S.Me, "전원이 꺼져 있는데… 테이프가 혼자 돌아가고 있어.", -1f, true);
            ui.HideDialogue();
            SetCutscene(false);
        }

        // 도서실의 오래된 책을 읽으면: 선생님이 깨어난다
        IEnumerator LibraryEvent()
        {
            GameState gs = GameState.Current;
            gs.SetFlag("monster_active");
            gs.SetFlag("shutter_open");
            SetCutscene(true);
            yield return new WaitForSeconds(0.8f);
            RoomLights rl = RoomLights.For(MapData.Get("1F_library"));
            if (rl != null && rl.On) { yield return rl.Flicker(3); rl.Set(false); }
            sfx.Play(sfx.staticNoise, 0.7f);
            yield return new WaitForSeconds(1.0f);
            sfx.Play(sfx.chimeUp, 0.6f, 0.94f);
            yield return new WaitForSeconds(2.8f);
            yield return ui.Broadcast("졸업식에 참석하지 않은 학생이 확인되었습니다.", 2.8f);
            yield return ui.HideBroadcast(0.3f);
            yield return ui.Broadcast("지금 선생님이 학생을 데리러 갑니다.", 3.0f);
            yield return ui.HideBroadcast(0.6f);
            sfx.Play(sfx.shutter, 0.35f, 0.9f);
            StartCoroutine(shutterN.Raise());
            StartCoroutine(shutterB.Raise());
            yield return new WaitForSeconds(1.2f);
            yield return ui.Say(S.Me, "…어딘가에서 셔터가 올라가는 소리가 났어.", 2.5f, true);
            ui.HideDialogue();

            // 1층 서쪽 복도 끝에서 나타나 도서실 쪽으로 온다
            TeacherMonster m = TeacherMonster.I;
            m.Activate(MapSpace.GF(1, 3f, 0f, 2.2f), false);
            m.Investigate(MapSpace.GF(1, 79.0f, 0f, 18.0f));
            SetCutscene(false);
            ui.ShowBanner(new[] { "E" }, "무언가 다가온다 — 청소함이나 화장실 칸에 들어가 숨자");
            StartCoroutine(HideBannerLater(8f));
            ui.SetObjective("살아남기", new[] { "선생님을 피해 단서를 찾자", "위층으로 가는 셔터가 열렸다" });
        }

        void CheckPrincipal()
        {
            GameState gs = GameState.Current;
            if (gs.Flag("principal_done")) return;
            if (gs.HasDoc("note_principal") && gs.HasDoc("doc_principal_diary") && gs.HasDoc("doc_ledger"))
                StartCoroutine(PrincipalReveal());
        }

        // 교장의 정체를 알게 되면: "거기 있었구나?"
        IEnumerator PrincipalReveal()
        {
            GameState gs = GameState.Current;
            gs.SetFlag("principal_done");
            yield return new WaitForSeconds(1.2f);
            SetCutscene(true);
            sfx.Play(sfx.staticNoise, 0.8f, 0.7f);
            yield return new WaitForSeconds(1.2f);
            yield return ui.Broadcast("거기 있었구나?", 2.6f);
            yield return ui.HideBroadcast(0.2f);
            FifthFloor.Apply();
            sfx.Play(sfx.sting, 0.6f, 0.8f);
            TeacherMonster m = TeacherMonster.I;
            int f = MapSpace.FloorOf(Player.transform.position);
            Vector3 from = MapSpace.GF(f, f == 1 ? 3f : 40f, 0f, 2.2f);
            m.Activate(from, true);
            SetCutscene(false);
            ui.SetObjective("교장실", new[] { "4층 3학년 창고로 가자", "5층의 강당" });
        }

        IEnumerator BadgeFound()
        {
            GameState.Current.SetFlag("badge_found");
            yield return new WaitForSeconds(1.5f);
            ui.SetSpeakerColor(S.Haeun, S.HaeunColor);
            yield return ui.Say(S.Unknown, "…그거, 잃어버린 줄 알았는데.", 2.8f, true);
            ui.HideDialogue();
            yield return ui.Say(S.Me, "…하은이 목소리?", -1f, true);
            ui.HideDialogue();
        }

        public void OnScorePiece()
        {
            GameState gs = GameState.Current;
            if (gs.Count("score_piece") >= 3)
            {
                gs.RemoveItem("score_piece", gs.Count("score_piece"));
                gs.AddItem("score_full");
                sfx.Play(sfx.saveChime, 0.5f);
                ui.Toast("찢어진 조각들을 이어 붙였다. 졸업가 악보가 완성되었다!", 3.5f);
            }
        }

        public void OnDocRead(string doc)
        {
            if (doc == "book_folk" && !GameState.Current.Flag("monster_active")) StartCoroutine(LibraryEvent());
        }

        public void OnFlashlightFound()
        {
            ui.ShowBanner(new[] { "F" }, "손전등 켜기/끄기 · 어두운 곳은 불을 켜거나 손전등으로 비춰야 조사할 수 있다");
            StartCoroutine(HideBannerLater(7f));
            ui.SetObjective("학교 둘러보기", new[] { "졸업식과 관련된 단서 찾기 (행정실, 도서실)" });
        }

        // 강당 문
        void HallDoor()
        {
            GameState gs = GameState.Current;
            if (!gs.Has("badge"))
            {
                sfx.PlayAt(sfx.rattle, MapSpace.GF(5, 82f, 1.1f, 12.6f), 0.7f);
                ModalUI.I.Alert("문에 A4 용지가 붙어 있다.\n\n<졸업식 행사 중>\n졸업생만 노크 후 들어오세요.");
                return;
            }
            ModalUI.I.Confirm(Josa.Quote(ItemDB.Name("badge")) + " 사용하시겠습니까?", yes =>
            {
                if (!yes) return;
                if (ScorePieces < 3)
                {
                    ModalUI.I.Alert("문 너머에서 희미한 노랫소리가 들린다.\n하지만 지금은 노래를 끝까지 부를 수 없을 것 같다.\n\n(찢어진 악보 조각 " + ScorePieces + " / 3)");
                    return;
                }
                SaveAt("hall", "5층 강당 앞");   // 배드 엔딩 후 다시 하기는 여기서 (명찰 장식을 가진 채로)
                gs.RemoveItem("badge");
                finale.Enter();
            });
        }

        // ================================================================ 아이템 사용 (가방에서)
        // true를 돌려주면 메뉴를 닫는다
        public bool UseItem(string id)
        {
            GameState gs = GameState.Current;
            ItemDef d = ItemDB.Get(id);
            switch (d.use)
            {
                case ItemUse.Heal:
                    if (gs.hp >= gs.maxHp) { ui.Toast("지금은 다친 곳이 없다."); return false; }
                    gs.RemoveItem(id); gs.Heal(d.amount);
                    sfx.Play(sfx.paper, 0.5f, 1.3f);
                    ui.Toast(Josa.Quote(d.name) + " 사용했다. 체력이 회복되었다.");
                    return false;
                case ItemUse.Drink:
                    gs.RemoveItem(id); gs.Heal(d.amount); gs.AddItem("empty_can");
                    sfx.Play(sfx.gulp, 0.6f);
                    ui.Toast("음료를 마셨다. 빈 캔이 남았다.");
                    return false;
                case ItemUse.EquipCan:
                    Thrower.Equip();
                    return true;
                case ItemUse.Flashlight:
                    if (Flashlight.I != null) Flashlight.I.Set(!Flashlight.I.On);
                    return true;
                case ItemUse.Battery:
                    if (gs.battery >= 99f) { ui.Toast("배터리가 이미 가득 차 있다."); return false; }
                    gs.RemoveItem(id); gs.battery = Mathf.Min(100f, gs.battery + d.amount);
                    sfx.Play(sfx.click, 0.6f);
                    ui.Toast("손전등에 건전지를 갈아 끼웠다. (" + Mathf.CeilToInt(gs.battery) + "%)");
                    return false;
            }
            return false;
        }

        // ================================================================ 체력 / 사망
        float invuln;

        public void DamagePlayer(float dmg, Vector3 from)
        {
            if (dead || !Running || invuln > 0f || DevMode.God) return;
            invuln = 0.8f;
            GameState gs = GameState.Current;
            gs.hp = Mathf.Max(0f, gs.hp - dmg);
            ui.FlashDamage();
            sfx.Play(sfx.hit, 0.9f, Random.Range(0.9f, 1.05f));
            Player.Shake(0.07f, 0.45f);
            if (gs.hp <= 0f) StartCoroutine(Die());
        }

        IEnumerator Die()
        {
            dead = true;
            SetCutscene(true);
            if (MenuUI.I != null) MenuUI.I.Close();
            Thrower.Unequip();
            HideSpot.ForceReset();
            ui.ShowHideHint(false);
            Player.SetMode(FirstPersonController.Mode.Cutscene);
            StartCoroutine(Player.TweenView(Player.transform.position, Player.yaw + 30f, 40f, 80f, 0.25f, 0.9f));
            StartCoroutine(ui.TintTo(new Color(0.4f, 0f, 0f, 0.5f), 0.9f));
            yield return new WaitForSeconds(1.5f);
            if (TeacherMonster.I != null) TeacherMonster.I.Deactivate();
            sfx.Play(sfx.staticNoise, 0.6f);
            yield return ui.Broadcast("학생 한 명이 확인되었습니다.", 2.4f);
            yield return ui.HideBroadcast(0.3f);
            yield return ui.Broadcast("졸업식에 참석하지 않은 학생입니다.", 2.6f);
            yield return ui.HideBroadcast(0.5f);
            yield return ui.Fade(1f, 1.2f);
            ModalUI.I.Death(Retry, ToTitle);
        }

        public void Retry()
        {
            GameSession.loadOnStart = true;
            Reload();
        }

        public void ToTitle()
        {
            GameSession.loadOnStart = false;
            GameSession.startMainDirectly = false;
            Reload();
        }

        // 층 분위기(안개/주변광)를 다시 계산하게 함 (개발자 모드 '밝게' 끌 때)
        public void RefreshEnvironment() { lastFloor = -1; }

        public void OpenShutters()
        {
            GameState.Current.SetFlag("shutter_open");
            if (shutterN != null) shutterN.SetOpenInstant();
            if (shutterB != null) shutterB.SetOpenInstant();
        }

        public bool IsDead { get { return dead; } }

        public static void Reload()
        {
            UIState.Reset();
            Interactable.All.Clear();
            Door.All.Clear();
            RoomLights.All.Clear();
            RoomLights.litZones.Clear();
            HideSpot.ForceReset();
            HideSpot.All.Clear();
            SaveBoard.All.Clear();
            LightCheck.alwaysLit = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ================================================================ 저장
        public void SaveAt(string spawnId, string place)
        {
            SaveSystem.Save(GameState.Current.ToSave(spawnId, place));
        }

        void Spawn(string id, out Vector3 feet, out float yaw)
        {
            SaveBoard b;
            if (id == "start") { feet = MapSpace.GF(1, 34.0f, 0.1f, 8.8f); yaw = MapSpace.YawOf(-1, 0.15f); return; }
            if (id == "5F_stair") { feet = MapSpace.GF(5, 19.6f, 0.1f, 2.3f); yaw = MapSpace.YawOf(0, 1); return; }
            if (id == "hall") { feet = MapSpace.GF(5, 82f, 0.1f, 10.6f); yaw = MapSpace.YawOf(0, 1); return; }
            b = SaveBoard.Find(id);
            if (b != null) { feet = b.spawnFeet + Vector3.up * 0.1f; yaw = b.spawnYaw; return; }
            feet = MapSpace.GF(1, 34.0f, 0.1f, 8.8f); yaw = MapSpace.YawOf(-1, 0.15f);
        }

        // ================================================================ 깨어나는 연출
        IEnumerator WakeInLobby()
        {
            SetCutscene(true);
            ui.SetLetterbox(false);
            ui.SetCrosshair(false);
            Vector3 feet; float yaw;
            Spawn("start", out feet, out yaw);
            Player.SetMode(FirstPersonController.Mode.Cutscene);
            Player.Teleport(feet, yaw);
            Player.eyeHeight = 0.25f; Player.pitch = -25f; Player.roll = 75f; Player.fov = 62f;
            sfx.StartLoop("drone", sfx.drone, 0.22f, null, 3f);
            Running = true;
            yield return new WaitForSeconds(1.5f);
            ui.SetFade(0f);
            yield return ui.Eyes(0.3f, 1.6f);
            yield return ui.Eyes(0.05f, 0.6f);
            yield return new WaitForSeconds(0.6f);
            yield return ui.Eyes(0.7f, 1.2f);
            yield return ui.Say(S.Me, "…….", 1.6f, true);
            yield return ui.Blink(0.8f);
            yield return ui.Eyes(1f, 0.6f);
            yield return Player.TweenView(feet, yaw, 10f, 0f, 0.9f, 1.4f);
            yield return ui.Say(S.Me, "…여기, 1층 로비…?");
            yield return Player.TweenView(feet, yaw - 60f, 0f, 0f, 0.9f, 1.4f);
            yield return Player.TweenView(feet, yaw + 50f, 0f, 0f, 0.9f, 1.6f);
            yield return ui.Say(S.Me, "얘들아? 민재야? …도현아?");
            yield return new WaitForSeconds(0.8f);
            yield return ui.Say(S.Me, "…아무도 없어.", 2.2f, true);
            yield return Player.TweenView(feet, yaw, 0f, 0f, Player.standEyeHeight, 1.2f);
            ui.HideDialogue();
            ui.SetCrosshair(true);
            ui.ShowBanner(new[] { "Tab" }, "가방 · 문서 · 지도         [Esc] 일시정지");
            StartCoroutine(HideBannerLater(6f));
            ui.SetObjective("학교 둘러보기", new[] { "비상용품함을 찾아 손전등 챙기기" });
            Player.SetMode(FirstPersonController.Mode.Free);
            SetCutscene(false);
        }

        IEnumerator WakeAtSave(SaveData data)
        {
            SetCutscene(true);
            ui.SetLetterbox(false);
            Vector3 feet; float yaw;
            Spawn(data != null ? data.spawnId : "start", out feet, out yaw);
            Player.SetMode(FirstPersonController.Mode.Cutscene);
            Player.Teleport(feet, yaw);
            Player.eyeHeight = Player.standEyeHeight; Player.pitch = 5f; Player.roll = 0f; Player.fov = 65f;
            sfx.StartLoop("drone", sfx.drone, 0.22f, null, 2f);
            Running = true;
            GameState gs = GameState.Current;
            if (gs.Flag("monster_active"))
            {
                // 저장한 곳에서 멀리 떨어진 계단에서 다시 나타남
                int f = MapSpace.FloorOf(feet);
                Vector3 a = MapSpace.GF(f, 18.7f, 0f, 2.4f), b = MapSpace.GF(f, 81.5f, 0f, 2.6f);
                TeacherMonster.I.Activate(Vector3.Distance(a, feet) > Vector3.Distance(b, feet) ? a : b, false);
            }
            yield return new WaitForSeconds(0.6f);
            ui.SetFade(0f);
            yield return ui.Eyes(1f, 1.4f);
            ui.SetCrosshair(true);
            ui.Toast(data != null ? data.placeName + " — 마지막 기록에서 다시 시작" : "다시 시작", 3f);
            Player.SetMode(FirstPersonController.Mode.Free);
            SetCutscene(false);
        }

        IEnumerator HideBannerLater(float s)
        {
            yield return new WaitForSeconds(s);
            ui.HideBanner();
        }

        // ================================================================ 매 프레임
        void Update()
        {
            if (!Running) return;
            invuln -= Time.deltaTime;

            // [Tab] 메뉴
            if (PlayerCanAct && !UIState.JustClosed && GameInput.MenuDown() && MenuUI.I != null && !Thrower.Holding)
                MenuUI.I.Open();

            // 1층 로비 불빛 지직거림
            lobbyTimer -= Time.deltaTime;
            if (lobbyTimer <= 0f && lobbyLights.Count > 0)
            {
                lobbyTimer = Random.Range(0.04f, 0.7f);
                bool on = Random.value > 0.18f;
                float k = Random.Range(0.4f, 1f);
                foreach (Light l in lobbyLights) { l.enabled = on; l.intensity = 0.7f * k; }
                foreach (Renderer r in lobbyGlow) if (r != null) r.enabled = on;
            }

            // 층마다 분위기
            int f = MapSpace.FloorOf(Player.transform.position);
            if (f != lastFloor && (finale == null || !finale.Active) && !DevMode.FullBright)
            {
                lastFloor = f;
                bool red = f == 5 && FifthFloor.Applied;
                RenderSettings.fogColor = red ? new Color(0.09f, 0.0f, 0.0f) : Color.black;
                RenderSettings.fogDensity = f == 4 ? 0.085f : red ? 0.06f : 0.055f;
                RenderSettings.ambientLight = red ? new Color(0.07f, 0.01f, 0.01f) : f == 4 ? new Color(0.012f, 0.012f, 0.018f) : new Color(0.02f, 0.021f, 0.028f);
                if (red) { sfx.StartLoop("droneRed", sfx.droneRed, 0.35f, null, 2f); sfx.StopLoop("drone", 2f); }
                else { sfx.StopLoop("droneRed", 2f); sfx.StartLoop("drone", sfx.drone, f == 4 ? 0.3f : 0.22f, null, 2f); }
            }

            // 5층(변한 뒤)에 처음 올라오면 자동 기록
            GameState gs = GameState.Current;
            if (f == 5 && gs.Flag("principal_done") && !gs.Flag("5f_autosaved") && !cutscene && !dead)
            {
                gs.SetFlag("5f_autosaved");
                SaveAt("5F_stair", "5층 계단");
                sfx.Play(sfx.saveChime, 0.4f, 0.8f);
                ui.Toast("[자동 기록]  5층 — 이곳에서 쓰러지면 5층 계단에서 다시 시작한다.", 4f);
            }
        }

        // 괴물이 추격을 시작하면 (처음 한 번만 안내)
        public void OnChaseStarted()
        {
            if (!GameState.Current.Flag("chase_hint"))
            {
                GameState.Current.SetFlag("chase_hint");
                ui.ShowBanner(new[] { "Shift" }, "도망쳐! 숨거나, 빈 캔을 던져 주의를 돌리자");
                StartCoroutine(HideBannerLater(5f));
            }
        }
    }
}
