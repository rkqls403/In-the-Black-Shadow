using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using S = BlackShade.PrologueScript;
using LY = BlackShade.ClassroomFrame.Layout;

namespace BlackShade
{
    // 프롤로그 전체 진행을 담당하는 감독.
    // 씬에 이 컴포넌트 하나만 있으면 교실 찾기, 조명, UI, 플레이어, 친구들, 소품을 전부 코드로 준비해요.
    //
    // 진행 순서
    //   1. 컷씬: 책상에 엎드려 졸다가 깸 → 둘러봄 → 자리에서 일어남
    //   2. 튜토리얼: 이동/시점/상호작용 안내 + 할 일 3개 (롤링페이퍼, 풍선, 칠판). 할 때마다 해가 짐
    //   3. 하은이 사라짐 → 창문 닫기 → 뒤에서 "야." (놀래키기)
    //   4. 모여서 무서운 얘기 → 정전 → 교내 방송 → 쿵, 쿵 → 복도의 그림자 → 암전
    public class PrologueDirector : MonoBehaviour
    {
        [Header("친구 모델 (glb를 끌어다 놓기, 비우면 임시 캡슐)")]
        public GameObject minjaeModel;   // school_boy_8
        public GameObject dohyunModel;   // school_boy_10
        public GameObject haeunModel;    // model.glb (여자)

        [Header("(선택) Mixamo로 리깅했다면 Idle 애니메이터 컨트롤러")]
        public RuntimeAnimatorController minjaeAnimator;
        public RuntimeAnimatorController dohyunAnimator;
        public RuntimeAnimatorController haeunAnimator;

        [Header("모델 키(m) / 모델이 뒤돌아 있으면 180")]
        public float minjaeHeight = 1.76f;
        public float dohyunHeight = 1.73f;
        public float haeunHeight = 1.62f;
        public float minjaeYawOffset = 0f;
        public float dohyunYawOffset = 0f;
        public float haeunYawOffset = 0f;

        [Header("주인공 자리 (교실 좌표: 몇 번째 줄, 몇 번째 분단)")]
        public int seatRow = 2;   // 0 = 맨 앞
        public int seatCol = 0;   // 0 = 복도 쪽

        [Header("개발용")]
        [Tooltip("켜면 바로 '무서운 얘기' 부분부터 시작. 플레이 중 F8을 눌러도 돼요.")]
        public bool startAtStories = false;
        [Tooltip("프롤로그가 끝나고 불러올 씬 이름 (빌드 설정에 등록돼 있어야 함). 비우면 암전 상태로 멈춤.")]
        public string nextSceneName = "";

        ClassroomFrame frame;
        LightingDirector lighting;
        GameUI ui;
        AudioDirector sfx;
        FirstPersonController player;
        PlayerInteractor interactor;
        NPC minjae, dohyun, haeun;
        readonly Dictionary<string, NPC> bySpeaker = new Dictionary<string, NPC>();

        BoardWriter board;
        GameObject balloonOnDesk;
        readonly List<GameObject> wallBalloons = new List<GameObject>();
        GameObject shadowFigure;
        Interactable paperIt, balloonIt, boardIt, windowIt;

        int tasksDone;
        bool taskRunning;
        bool skipping;
        Coroutine fovTween;

        // ================================================================ 준비
        public bool Playing { get; private set; }
        System.Action onFinished;
        bool setupDone;

        // 부트스트랩이 만든 플레이어를 같이 씀
        public void UsePlayer(FirstPersonController p, PlayerInteractor i) { player = p; interactor = i; }

        void Setup()
        {
            if (setupDone) return;
            setupDone = true;
            frame = Get<ClassroomFrame>();
            frame.Build();
            sfx = Get<AudioDirector>();
            ui = Get<GameUI>();
            lighting = Get<LightingDirector>();
            lighting.Setup(frame);

            ui.SetSpeakerColor(S.Me, S.MeColor);
            ui.SetSpeakerColor(S.Minjae, S.MinjaeColor);
            ui.SetSpeakerColor(S.Haeun, S.HaeunColor);
            ui.SetSpeakerColor(S.Dohyun, S.DohyunColor);
            ui.SetSpeakerColor(S.Unknown, S.UnknownColor);

            if (player == null) SpawnPlayer();
            SpawnFriends();
            SpawnProps();
        }

        // 프롤로그 시작 (끝나면 onDone 호출 → 본편)
        public void Begin(System.Action onDone)
        {
            onFinished = onDone;
            Setup();
            Playing = true;
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 12); // 형광등 9개 + 해/달 + 비상등
            sfx.StartLoop("room", sfx.roomTone, 0.25f, null, 0.01f);
            sfx.StartLoop("hum", sfx.fluorescentHum, 0.10f, lighting.RoomCenterCeiling(), 0.01f);
            if (startAtStories) StartCoroutine(SkipToStories());
            else StartCoroutine(Run());
        }

        // 저장된 기록에서 시작할 때: 3학년 1반은 '다 꾸며진 상태'로만 만들어 둔다
        public void SetupForMain()
        {
            Setup();
            Playing = false;
            tasksDone = 3;
            paperIt.available = balloonIt.available = boardIt.available = windowIt.available = false;
            balloonOnDesk.SetActive(false);
            foreach (GameObject b in wallBalloons) b.SetActive(true);
            board.ShowFinal(S.BoardMain, S.BoardSub, S.BoardSign);
            board.SetBrightness(0.6f);
            HideFriends();
        }

        // 개발자 모드용
        public void DevSkipToStories()
        {
            if (!Playing || skipping) return;
            StopAllCoroutines();
            StartCoroutine(SkipToStories());
        }

        public void HideFriends()
        {
            foreach (NPC n in bySpeaker.Values) n.SetVisible(false);
            shadowFigure.SetActive(false);
        }

        T Get<T>() where T : Component
        {
            T c = GetComponent<T>();
            return c != null ? c : gameObject.AddComponent<T>();
        }

        void Update()
        {
            if (Playing && GameInput.DebugSkipDown() && !skipping && !UIState.ModalOpen)
            {
                StopAllCoroutines();
                StartCoroutine(SkipToStories());
            }
        }

        // ---------------------------------------------------------------- 교실 좌표 도우미
        Vector3 P(float u, float v, float h = 0f) { return frame.P(u, v, h); }
        float YawTo(float fromU, float fromV, float toU, float toV) { return frame.Yaw(toU - fromU, toV - fromV); }
        // 월드 위치 → 교실 좌표 (u, v)
        Vector2 UV(Vector3 world)
        {
            Vector3 d = world - frame.Origin;
            return new Vector2(Vector3.Dot(d, frame.Forward), Vector3.Dot(d, frame.ToWindow));
        }
        float SeatU { get { return LY.DeskRowU[seatRow]; } }
        float SeatV { get { return LY.DeskColV[seatCol]; } }

        void SpawnPlayer()
        {
            GameObject go = new GameObject("Player");
            go.AddComponent<CharacterController>();
            player = go.AddComponent<FirstPersonController>();
            interactor = go.AddComponent<PlayerInteractor>();
            player.SetMode(FirstPersonController.Mode.Cutscene);
        }

        void SpawnFriends()
        {
            minjae = NPC.Create(minjaeModel, S.Minjae, minjaeHeight, minjaeYawOffset, new Color(0.25f, 0.3f, 0.5f), minjaeAnimator);
            dohyun = NPC.Create(dohyunModel, S.Dohyun, dohyunHeight, dohyunYawOffset, new Color(0.3f, 0.32f, 0.45f), dohyunAnimator);
            haeun = NPC.Create(haeunModel, S.Haeun, haeunHeight, haeunYawOffset, new Color(0.25f, 0.27f, 0.25f), haeunAnimator);
            bySpeaker[S.Minjae] = minjae;
            bySpeaker[S.Dohyun] = dohyun;
            bySpeaker[S.Haeun] = haeun;
            PlaceFriendsForWork();
        }

        // 꾸미기 하는 동안의 자리
        void PlaceFriendsForWork()
        {
            // 민재: 주인공 자리 옆 (복도 쪽 빈 공간)
            minjae.Place(P(SeatU - 0.3f, 1.15f), YawTo(SeatU - 0.3f, 1.15f, SeatU + 0.3f, SeatV));
            // 하은: 뒤쪽 벽에 풍선 붙이는 중
            haeun.Place(P(9.25f, 5.0f), frame.Yaw(1f, 0f));
            // 도현: 앞쪽 창가에서 창문에 종이 붙이는 중
            dohyun.Place(P(1.9f, 7.45f), frame.Yaw(-0.3f, 1f));
            minjae.lookTarget = haeun.lookTarget = dohyun.lookTarget = null;
        }

        void SpawnProps()
        {
            Quaternion facing = Quaternion.LookRotation(frame.Forward);
            Transform root = new GameObject("PrologueProps").transform;

            // ① 롤링페이퍼: 맨 앞 줄, 복도 쪽 책상 서랍에서 살짝 삐져나온 종이 뭉치
            float pu = LY.DeskRowU[0], pv = LY.DeskColV[0];
            GameObject paper = Prop.Box(root, "RollingPaper", P(pu - 0.12f, pv, LY.DeskTopH + 0.03f), facing * Quaternion.Euler(0, 12, 0), new Vector3(0.32f, 0.05f, 0.24f), new Color(0.97f, 0.95f, 0.88f), false);
            Prop.Box(paper.transform, "Ribbon", paper.transform.position + Vector3.up * 0.027f, paper.transform.rotation, new Vector3(0.33f, 0.01f, 0.04f), new Color(0.9f, 0.35f, 0.45f), false);
            paperIt = MakeInteractable(paper, S.PromptPaper, 0f, new[] { paper.GetComponent<Renderer>() });
            paperIt.onUse = () => StartCoroutine(TaskPaper());

            // ② 풍선: 맨 뒤 줄, 창가 쪽 책상 위 (바람 빠진 풍선 + 풍선 봉지)
            float bu = LY.DeskRowU[4], bv = LY.DeskColV[3];
            Vector3 deskTop = P(bu - 0.15f, bv, LY.DeskTopH + 0.02f);
            Prop.Box(root, "BalloonBag", deskTop + frame.ToWindow * 0.18f + Vector3.up * 0.03f, facing, new Vector3(0.18f, 0.06f, 0.24f), new Color(0.85f, 0.85f, 0.95f), false);
            balloonOnDesk = Prop.Shape(PrimitiveType.Sphere, root, "Balloon", deskTop - frame.ToWindow * 0.1f + Vector3.up * 0.05f, Quaternion.identity, new Vector3(0.08f, 0.06f, 0.08f), new Color(0.95f, 0.75f, 0.2f), false);
            GameObject deskModel = PropModels.BalloonInside(balloonOnDesk, new Color(0.95f, 0.75f, 0.2f));   // 받은 풍선 모델
            Renderer[] deskGlow = deskModel != null ? deskModel.GetComponentsInChildren<Renderer>() : new[] { balloonOnDesk.GetComponent<Renderer>() };
            balloonIt = MakeInteractable(balloonOnDesk, S.PromptBalloon, 2.2f, deskGlow);
            balloonIt.onHoldProgress = BalloonProgress;
            balloonIt.onUse = () => StartCoroutine(TaskBalloon());

            // 뒷벽에 이미 붙어 있는 풍선 몇 개 (하은이 붙인 것)
            Color[] cols = { new Color(0.95f, 0.35f, 0.45f), new Color(0.35f, 0.6f, 0.95f), new Color(0.98f, 0.95f, 0.95f), new Color(0.6f, 0.4f, 0.9f), new Color(0.98f, 0.6f, 0.75f) };
            float[] vs = { 3.6f, 4.0f, 6.0f, 6.4f, 5.0f, 2.2f };
            float[] hs = { 2.35f, 2.15f, 2.4f, 2.2f, 2.55f, 2.4f };
            for (int i = 0; i < vs.Length; i++)
            {
                GameObject b = Prop.Shape(PrimitiveType.Sphere, root, "WallBalloon" + i, P(LY.BackWallU - 0.2f, vs[i], hs[i]), Quaternion.identity, new Vector3(0.3f, 0.36f, 0.3f), cols[i % cols.Length], false);
                PropModels.BalloonInside(b, cols[i % cols.Length]);
                b.SetActive(i < 2);
                wallBalloons.Add(b);
            }

            // 칠판 양옆 구석에 풍선 다발 (실로 묶여 둥실)
            for (int side = 0; side < 2; side++)
            {
                float bvv = side == 0 ? LY.BoardVMin - 0.35f : LY.BoardVMax + 0.35f;
                for (int i = 0; i < 3; i++)
                    PropModels.BalloonOnString(root, P(LY.BoardU + 0.35f, bvv + (i - 1) * 0.12f, 0f), 1.75f + i * 0.18f, 0.28f, cols[(i + side * 2) % cols.Length], "CornerBalloon");
            }

            // ③ 칠판: 분필 + 글씨 쓸 캔버스
            Vector3 boardCenter = P(LY.BoardU, (LY.BoardVMin + LY.BoardVMax) * 0.5f, (LY.BoardHMin + LY.BoardHMax) * 0.5f);
            board = BoardWriter.Create(boardCenter, -frame.Forward, LY.BoardVMax - LY.BoardVMin, LY.BoardHMax - LY.BoardHMin);
            GameObject chalk = Prop.Shape(PrimitiveType.Cylinder, root, "Chalk", P(LY.BoardU + 0.1f, (LY.BoardVMin + LY.BoardVMax) * 0.5f + 0.4f, LY.BoardHMin - 0.01f),
                                          Quaternion.LookRotation(frame.ToWindow) * Quaternion.Euler(90, 0, 0), new Vector3(0.012f, 0.04f, 0.012f), Color.white, false);
            boardIt = MakeInteractable(chalk, S.PromptBoard, 0f, new[] { chalk.GetComponent<Renderer>() });
            boardIt.focus = new GameObject("BoardFocus").transform;
            boardIt.focus.position = boardCenter;
            boardIt.range = 3.2f;
            boardIt.onUse = () => StartCoroutine(TaskBoard());

            // ④ 창문 (해가 다 진 뒤에만)
            GameObject win = new GameObject("BackWindow");
            win.transform.SetParent(root, true);
            win.transform.position = P(8.4f, LY.WindowV - 0.05f, 1.55f);
            windowIt = win.AddComponent<Interactable>();
            windowIt.prompt = S.PromptWindow;
            windowIt.range = 2.2f;
            windowIt.available = false;
            windowIt.onUse = () => StartCoroutine(Scare());

            // 복도를 지나갈 그림자 (마지막 장면용)
            shadowFigure = new GameObject("CorridorShadow");
            Prop.Shape(PrimitiveType.Capsule, shadowFigure.transform, "Body", Vector3.up * 1.15f, Quaternion.identity, new Vector3(0.62f, 1.15f, 0.45f), new Color(0.01f, 0.01f, 0.012f), false);
            Prop.Shape(PrimitiveType.Sphere, shadowFigure.transform, "Head", Vector3.up * 2.35f, Quaternion.identity, new Vector3(0.3f, 0.36f, 0.3f), new Color(0.01f, 0.01f, 0.012f), false);
            shadowFigure.SetActive(false);
        }

        Interactable MakeInteractable(GameObject go, string prompt, float hold, Renderer[] glow)
        {
            Interactable it = go.AddComponent<Interactable>();
            it.prompt = prompt;
            it.holdSeconds = hold;
            it.glowRenderers = glow;
            it.available = false; // 튜토리얼이 시작되면 켬
            return it;
        }

        // ================================================================ 대사 도우미
        IEnumerator Talk(S.Line[] lines, bool cameraFollows = false)
        {
            foreach (S.Line l in lines) yield return TalkOne(l, cameraFollows);
            ui.HideDialogue();
            ClearSpeaking();
        }

        IEnumerator TalkOne(S.Line l, bool cameraFollows = false)
        {
            ClearSpeaking();
            NPC who;
            if (bySpeaker.TryGetValue(l.speaker, out who) && who.gameObject.activeInHierarchy)
            {
                who.speaking = true;
                who.lookTarget = player.Head;
                // 다른 친구들은 말하는 사람을 봄
                foreach (NPC n in bySpeaker.Values)
                    if (n != who && n.gameObject.activeInHierarchy) n.lookTarget = who.transform;
                if (cameraFollows) StartCoroutine(player.LookAtPoint(who.HeadPoint - Vector3.up * 0.08f, 0.7f));
            }
            else if (l.speaker == S.Me)
            {
                foreach (NPC n in bySpeaker.Values) n.lookTarget = player.Head;
            }
            float cps = ui.charsPerSecond;
            if (l.slow) ui.charsPerSecond = 13f;
            yield return ui.Say(l.speaker, l.text, l.auto, l.whisper);
            ui.charsPerSecond = cps;
        }

        void ClearSpeaking()
        {
            foreach (NPC n in bySpeaker.Values) n.speaking = false;
        }

        void FriendsLookAtPlayer()
        {
            foreach (NPC n in bySpeaker.Values) n.lookTarget = player.Head;
        }

        void FriendsBackToWork()
        {
            foreach (NPC n in bySpeaker.Values) n.lookTarget = null;
        }

        // ================================================================ 전체 흐름
        IEnumerator Run()
        {
            yield return WakeUpCutscene();
            yield return Tutorial();
            // 할 일 3개가 끝나면 Tutorial() 안에서 AfterAllTasks()로 이어져요.
        }

        // ---------------------------------------------------------------- 1. 졸다가 깨는 컷씬
        IEnumerator WakeUpCutscene()
        {
            ui.SetFade(1f);
            ui.SetEyes(0f);
            ui.SetLetterbox(true);
            ui.SetCrosshair(false);
            interactor.Busy = true;
            lighting.Apply(0f);

            // 책상에 엎드린 자세: 팔 위에 얼굴을 옆으로 대고 창문 쪽을 보고 있음
            float chairU = SeatU + 0.32f;
            Vector3 headOnDesk = P(SeatU + 0.05f, SeatV, 0f);
            player.Teleport(headOnDesk, frame.Yaw(0.15f, 1f));
            player.eyeHeight = LY.DeskTopH + 0.14f;
            player.pitch = 4f;
            player.roll = -72f;
            player.fov = 58f;

            yield return new WaitForSeconds(1.2f);
            foreach (S.Line l in S.WakeDark) yield return TalkOne(l);
            ui.HideDialogue();

            // 눈을 반쯤 떴다가 다시 감기
            ui.SetFade(0f);
            yield return ui.Eyes(0.25f, 1.4f);
            yield return new WaitForSeconds(0.4f);
            yield return ui.Eyes(0.05f, 0.5f);
            yield return new WaitForSeconds(0.5f);
            yield return ui.Eyes(0.55f, 1.0f);
            yield return ui.Blink(0.7f);
            yield return ui.Eyes(1f, 0.8f);

            // 엎드린 채로 친구들 목소리
            yield return Talk(S.WakeOnDesk);
            yield return ui.Blink();

            // 몸을 일으켜 의자에 앉음
            StartCoroutine(player.TweenFov(65f, 1.6f));
            yield return player.TweenView(P(chairU, SeatV), frame.Yaw(-1f, 0f), 6f, 0f, 1.18f, 1.8f);
            yield return ui.Blink(0.8f);

            // 주위를 둘러봄: 창가의 도현 → 뒤의 하은 → 옆의 민재
            yield return player.LookAtPoint(dohyun.HeadPoint, 1.4f);
            yield return new WaitForSeconds(0.5f);
            yield return player.LookAtPoint(haeun.HeadPoint, 1.6f);
            yield return new WaitForSeconds(0.4f);
            minjae.lookTarget = player.Head;
            yield return player.LookAtPoint(minjae.HeadPoint, 1.1f);

            yield return Talk(S.WakeSitting, true);

            // 자리에서 일어남: 의자를 뒤로 빼고 복도 쪽 통로로 한 걸음
            sfx.PlayAt(sfx.thud, P(chairU, SeatV, 0.3f), 0.18f, 2.6f); // 의자 끄는 소리 대용
            Vector3 standSpot = P(SeatU + 0.55f, 1.45f);
            yield return player.TweenView(standSpot, YawTo(SeatU + 0.55f, 1.45f, SeatU - 0.3f, 1.15f), 2f, 0f, player.standEyeHeight, 1.5f);

            yield return Talk(S.AfterStand, true);
            FriendsBackToWork();

            // 조작 시작
            StartCoroutine(ui.Letterbox(false, 0.8f));
            ui.SetCrosshair(true);
            player.SetMode(FirstPersonController.Mode.Free);
            interactor.Busy = false;
        }

        // ---------------------------------------------------------------- 2. 튜토리얼 + 할 일
        IEnumerator Tutorial()
        {
            float moved0 = player.MovedDistance;
            ui.ShowBanner(new[] { "W", "A", "S", "D" }, S.TutMove + "     [Shift] 달리기");
            float t = 0f;
            while (player.MovedDistance - moved0 < 2.5f && t < 12f) { t += Time.deltaTime; yield return null; }

            float look0 = player.LookedDegrees;
            ui.ShowBanner(new[] { "마우스" }, S.TutLook);
            t = 0f;
            while (player.LookedDegrees - look0 < 140f && t < 10f) { t += Time.deltaTime; yield return null; }

            ui.SetObjective(S.ObjectiveTitle, S.Objectives);
            paperIt.available = balloonIt.available = boardIt.available = true;
            ui.ShowBanner(new[] { "E" }, S.TutInteract);

            bool holdShown = false;
            while (tasksDone < 3)
            {
                // 풍선 앞에 처음 섰을 때만 '길게 누르기' 안내
                if (!holdShown && balloonIt.available && Vector3.Distance(player.Cam.transform.position, balloonIt.FocusPoint) < 2f)
                {
                    holdShown = true;
                    ui.ShowBanner(new[] { "E" }, S.TutHold);
                    StartCoroutine(HideBannerLater(5f));
                }
                yield return null;
            }
            while (taskRunning) yield return null;
            ui.HideBanner();
            yield return AfterAllTasks();
        }

        IEnumerator HideBannerLater(float s)
        {
            yield return new WaitForSeconds(s);
            ui.HideBanner();
        }

        // 할 일 하나가 끝날 때마다 해가 조금씩 짐
        void CompleteTask(int index)
        {
            ui.CheckObjective(index);
            tasksDone++;
            lighting.TweenTo(Mathf.Min(3f, tasksDone), 7f);
            if (tasksDone == 2) sfx.StartLoop("wind", sfx.wind, 0.18f, P(8f, 9f, 1.6f), 4f);
        }

        IEnumerator BeginTask()
        {
            taskRunning = true;
            interactor.Busy = true;
            ui.HideBanner();
            yield return null;
        }

        IEnumerator EndTask()
        {
            FriendsBackToWork();
            player.SetMode(FirstPersonController.Mode.Free);
            interactor.Busy = false;
            taskRunning = false;
            yield return null;
        }

        // ① 롤링페이퍼
        IEnumerator TaskPaper()
        {
            yield return BeginTask();
            player.SetMode(FirstPersonController.Mode.LookOnly);
            sfx.Play(sfx.paper, 0.8f);
            yield return ui.ShowRollingPaper(S.RollingPaperTitle, S.RollingPaperNotes());
            sfx.Play(sfx.paper, 0.6f, 0.9f);
            yield return Talk(S.AfterRollingPaper);
            CompleteTask(0);
            yield return EndTask();
        }

        // ② 풍선 (E 길게 누르는 동안 커짐)
        bool inflating;
        void BalloonProgress(float p)
        {
            if (balloonOnDesk == null) return;
            float s = Mathf.Lerp(0.08f, 0.32f, p);
            balloonOnDesk.transform.localScale = new Vector3(s, s * (p > 0.05f ? 1.15f : 0.75f), s);
            bool should = p > 0.001f && p < 0.999f;
            if (should && !inflating) sfx.StartLoop("inflate", sfx.inflate, 0.55f, null, 0.05f);
            if (!should && inflating) sfx.StopLoop("inflate", 0.1f);
            inflating = should;
        }

        IEnumerator TaskBalloon()
        {
            yield return BeginTask();
            sfx.StopLoop("inflate", 0.1f);
            inflating = false;
            player.SetMode(FirstPersonController.Mode.LookOnly);
            sfx.Play(sfx.pop, 0.7f);

            // 다 분 풍선이 뒷벽으로 둥실
            GameObject target = wallBalloons[2];
            Vector3 from = balloonOnDesk.transform.position, to = target.transform.position;
            Vector3 s0 = balloonOnDesk.transform.localScale;
            float t = 0f;
            while (t < 1.4f)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / 1.4f);
                balloonOnDesk.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.4f;
                balloonOnDesk.transform.localScale = Vector3.Lerp(s0, target.transform.localScale, k);
                yield return null;
            }
            balloonOnDesk.SetActive(false);
            for (int i = 2; i < wallBalloons.Count; i++)
            {
                wallBalloons[i].SetActive(true);
                sfx.PlayAt(sfx.pop, wallBalloons[i].transform.position, 0.5f, Random.Range(0.9f, 1.2f));
                yield return new WaitForSeconds(0.18f);
            }
            yield return Talk(S.AfterBalloon);
            CompleteTask(1);
            yield return EndTask();
        }

        // ③ 칠판
        IEnumerator TaskBoard()
        {
            yield return BeginTask();
            player.SetMode(FirstPersonController.Mode.LookOnly);
            ui.SetCrosshair(false);
            yield return player.LookAtPoint(boardIt.FocusPoint + Vector3.up * 0.1f, 0.8f);
            sfx.StartLoop("chalk", sfx.chalk, 0.6f, null, 0.05f);
            yield return board.Write(S.BoardMain, S.BoardSub, S.BoardSign);
            sfx.StopLoop("chalk", 0.15f);
            yield return new WaitForSeconds(0.4f);
            minjae.lookTarget = player.Head;
            yield return Talk(S.AfterBoard);
            ui.SetCrosshair(true);
            CompleteTask(2);
            yield return EndTask();
        }

        // ---------------------------------------------------------------- 3. 해가 다 짐 → 놀래키기
        IEnumerator AfterAllTasks()
        {
            yield return new WaitForSeconds(1.5f);
            ui.HideObjective();

            // 하은이는 화면에 안 보일 때 슬쩍 사라짐
            interactor.Busy = true;
            player.SetMode(FirstPersonController.Mode.LookOnly);
            player.lookOnlyYawRange = 120f;
            yield return TalkOne(S.AllDone[0]);
            yield return TalkOne(S.AllDone[1]);
            float wait = 0f;
            while (haeun.IsSeenBy(player.Cam) && wait < 3f) { wait += Time.deltaTime; yield return null; }
            haeun.SetVisible(false);
            for (int i = 2; i < S.AllDone.Length; i++) yield return TalkOne(S.AllDone[i]);
            ui.HideDialogue();
            ClearSpeaking();
            FriendsBackToWork();

            ui.SetObjective(S.ObjectiveTitle, new[] { S.ObjectiveWindow });
            windowIt.available = true;
            player.SetMode(FirstPersonController.Mode.Free);
            interactor.Busy = false;
        }

        IEnumerator Scare()
        {
            interactor.Busy = true;
            ui.CheckObjective(0);
            player.SetMode(FirstPersonController.Mode.LookOnly);
            player.lookOnlyYawRange = 25f;

            // 창문을 닫음 → 바람 소리가 끊김
            yield return player.LookAtPoint(windowIt.FocusPoint, 0.6f);
            sfx.PlayAt(sfx.thud, windowIt.FocusPoint, 0.35f, 2.2f);
            sfx.StopLoop("wind", 0.15f);
            ui.HideObjective();
            sfx.StopLoop("hum", 0.6f); // 갑자기 조용해짐
            yield return new WaitForSeconds(2.2f);

            // 바로 뒤에 하은이가 서 있음
            Vector3 back = player.transform.position - Quaternion.Euler(0f, player.yaw, 0f) * Vector3.forward * 0.62f;
            back.y = player.transform.position.y;
            haeun.Place(back, player.yaw);
            haeun.SetVisible(true);
            haeun.lookTarget = player.Head;

            yield return TalkOne(S.ScareCall);
            ui.HideDialogue();
            sfx.Play(sfx.sting, 0.7f);
            player.Shake(0.04f, 0.5f);
            ui.SetCrosshair(false);
            StartCoroutine(player.TweenFov(52f, 0.25f));
            float y0 = player.yaw;
            float t = 0f;
            while (t < 0.32f)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / 0.32f);
                player.yaw = y0 + 180f * k;
                player.pitch = Mathf.Lerp(player.pitch, 8f, k);
                yield return null;
            }
            player.lookOnlyYawRange = 40f;
            yield return player.LookAtPoint(haeun.HeadPoint, 0.2f);
            yield return new WaitForSeconds(0.6f);
            sfx.StartLoop("hum", sfx.fluorescentHum, 0.10f, lighting.RoomCenterCeiling(), 1.5f);

            // 민재, 도현이 다가와서 웃음
            Vector2 me = UV(player.transform.position);
            float cu = Mathf.Clamp(me.x, 2.5f, 9.3f);
            minjae.Place(P(Mathf.Clamp(cu - 1.1f, 2f, 9.4f), 6.75f), 0f);
            dohyun.Place(P(Mathf.Clamp(cu + 1.0f, 2f, 9.5f), 6.7f), 0f);
            minjae.FaceInstant(player.transform.position);
            dohyun.FaceInstant(player.transform.position);
            FriendsLookAtPlayer();
            StartCoroutine(player.TweenFov(65f, 1.5f));
            yield return Talk(S.AfterScare, true);
            yield return GatherForStories();
        }

        // ---------------------------------------------------------------- 4. 무서운 얘기 → 정전
        IEnumerator GatherForStories()
        {
            yield return ui.Fade(1f, 0.7f);
            PlaceForStories();
            yield return new WaitForSeconds(0.6f);
            yield return ui.Fade(0f, 0.9f);
            yield return Stories();
        }

        // 창가 쪽 빈 공간에 둘러섬
        void PlaceForStories()
        {
            haeun.SetVisible(true);
            float pu = 6.7f, pv = 7.35f;
            player.SetMode(FirstPersonController.Mode.Cutscene);
            player.Teleport(P(pu, pv), frame.Yaw(-1f, 0f));
            player.eyeHeight = player.standEyeHeight;
            player.pitch = 4f; player.roll = 0f; player.fov = 65f;
            player.SetMode(FirstPersonController.Mode.LookOnly);
            player.lookOnlyYawRange = 110f;
            dohyun.Place(P(5.35f, 7.2f), YawTo(5.35f, 7.2f, pu, pv));
            minjae.Place(P(5.8f, 6.45f), YawTo(5.8f, 6.45f, pu, pv));
            haeun.Place(P(5.75f, 7.95f), YawTo(5.75f, 7.95f, pu, pv));
            FriendsLookAtPlayer();
            ui.SetCrosshair(false);
            interactor.Busy = true;
        }

        IEnumerator Stories()
        {
            yield return Talk(S.StoriesWarmup, true);

            // 도현이 쪽으로 서서히 집중
            fovTween = StartCoroutine(player.TweenFov(58f, 14f));
            yield return Talk(S.OldestStory1, true);
            yield return lighting.Flicker(3);
            yield return new WaitForSeconds(0.8f);

            for (int i = 0; i < S.OldestStory2.Length; i++)
            {
                if (i == S.OldestStory2.Length - 2) sfx.StopLoop("room", 2f);
                yield return TalkOne(S.OldestStory2[i], true);
            }
            ClearSpeaking();

            // 말이 끝나는 순간 정전
            ui.HideDialogue();
            sfx.Play(sfx.powerCut, 0.9f);
            sfx.StopLoop("hum", 0.05f);
            lighting.Blackout();
            board.SetBrightness(0.18f);
            haeun.SetVisible(false);   // 불이 꺼진 사이 하은이가 사라짐
            if (fovTween != null) StopCoroutine(fovTween);
            player.fov = 65f;
            yield return new WaitForSeconds(2.2f);

            sfx.StartLoop("room", sfx.roomTone, 0.12f, null, 1f);
            yield return Talk(S.Blackout, true);
            yield return Broadcast();
        }

        IEnumerator Broadcast()
        {
            foreach (NPC n in bySpeaker.Values) n.lookTarget = null;
            Vector3 speaker = P(0.1f, LY.Width * 0.5f, 2.7f); // 칠판 위 스피커
            foreach (NPC n in bySpeaker.Values) n.FaceInstant(speaker);

            sfx.PlayAt(sfx.staticNoise, speaker, 0.8f);
            yield return new WaitForSeconds(1.0f);
            StartCoroutine(player.LookAtPoint(speaker, 1.6f));
            sfx.PlayAt(sfx.chimeUp, speaker, 0.7f, 0.94f);
            yield return new WaitForSeconds(3.0f);

            yield return ui.Broadcast(S.Broadcast1, 3.0f);
            yield return ui.HideBroadcast(0.3f);
            sfx.PlayAt(sfx.staticNoise, speaker, 0.5f, 1.1f);
            yield return new WaitForSeconds(0.6f);
            yield return ui.Broadcast(S.Broadcast2, 4.5f);
            yield return ui.HideBroadcast(0.6f);
            sfx.PlayAt(sfx.staticNoise, speaker, 0.6f, 0.9f);
            yield return new WaitForSeconds(2.0f);

            // 아무도 말을 못 함
            yield return TalkOne(S.AfterBroadcast);
            ui.HideDialogue();
            yield return new WaitForSeconds(1.2f);

            // 쿵. 쿵.
            Vector3 corridor = P(LY.FrontDoorU - 3f, -2.5f, 3.2f);
            Vector3 door = P(LY.FrontDoorU, 0f, 1.6f);
            sfx.PlayAt(sfx.thud, corridor, 1f);
            player.Shake(0.015f, 0.4f);
            ui.ShowCaption("쿵.", 1.2f);
            foreach (NPC n in bySpeaker.Values) n.FaceInstant(door);
            yield return player.LookAtPoint(door, 1.4f);
            yield return new WaitForSeconds(1.6f);
            sfx.PlayAt(sfx.thud, corridor + frame.Forward * 3f, 1f, 0.95f);
            player.Shake(0.02f, 0.4f);
            ui.ShowCaption("쿵.", 1.2f);
            yield return lighting.EmergencyOn(1.2f);
            yield return new WaitForSeconds(0.8f);

            // 복도 끝에서 무언가의 그림자가 천천히 지나감
            yield return ShadowPass();

            yield return TalkOne(S.WhoIsThere);
            ui.HideDialogue();
            yield return new WaitForSeconds(1.5f);

            // 치직— 암전
            sfx.Play(sfx.staticNoise, 0.9f, 0.8f);
            StartCoroutine(ui.Eyes(0f, 1.2f));
            yield return ui.Fade(1f, 1.2f);
            sfx.StopLoop("room", 1.5f);
            yield return EndPrologue();
        }

        IEnumerator ShadowPass()
        {
            shadowFigure.SetActive(true);
            float v = -1.4f; // 복도 가운데
            Vector3 a = P(LY.BackDoorU + 3f, v), b = P(LY.FrontDoorU - 4f, v);
            shadowFigure.transform.rotation = Quaternion.LookRotation(-frame.Forward);
            float dur = 7.5f, t = 0f;
            float nextStep = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                shadowFigure.transform.position = Vector3.Lerp(a, b, t / dur) + Vector3.up * Mathf.Abs(Mathf.Sin(t * 2.1f)) * 0.03f;
                nextStep -= Time.deltaTime;
                if (nextStep <= 0f)
                {
                    nextStep = 1.45f;
                    sfx.PlayAt(sfx.RandomFootstep(), shadowFigure.transform.position, 0.9f, 0.55f);
                }
                yield return null;
            }
            shadowFigure.SetActive(false);
        }

        IEnumerator EndPrologue()
        {
            yield return new WaitForSeconds(2f);
            Playing = false;
            HideFriends();
            sfx.StopAllLoops(0.5f);
            if (onFinished != null) onFinished();
            else if (!string.IsNullOrEmpty(nextSceneName) && Application.CanStreamedLevelBeLoaded(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
        }

        // ================================================================ 개발용: 무서운 얘기부터
        IEnumerator SkipToStories()
        {
            skipping = true;
            ui.HideDialogue();
            ui.HideBanner();
            ui.HideObjective();
            ui.SetLetterbox(false);
            ui.SetEyes(1f);
            ui.SetFade(1f);
            tasksDone = 3;
            taskRunning = false;
            paperIt.available = balloonIt.available = boardIt.available = windowIt.available = false;
            balloonOnDesk.SetActive(false);
            foreach (GameObject b in wallBalloons) b.SetActive(true);
            board.StopAllCoroutines();
            yield return null;
            StartCoroutine(board.Write(S.BoardMain, S.BoardSub, S.BoardSign, 9999f));
            lighting.Apply(3f);
            sfx.StopLoop("chalk", 0.05f);
            sfx.StopLoop("inflate", 0.05f);
            sfx.StopLoop("wind", 0.05f);
            sfx.StartLoop("hum", sfx.fluorescentHum, 0.10f, lighting.RoomCenterCeiling(), 0.1f);
            PlaceForStories();
            yield return ui.Fade(0f, 0.6f);
            skipping = false;
            yield return Stories();
        }
    }
}
