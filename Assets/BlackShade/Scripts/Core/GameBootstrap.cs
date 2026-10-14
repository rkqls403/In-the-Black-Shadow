using UnityEngine;

namespace BlackShade
{
    // 게임의 시작점. 씬의 'Game' 오브젝트에 붙어 있어요.
    //   타이틀 → (새로 시작) 프롤로그 → 본편
    //          → (이어하기) 마지막 기록에서 본편
    // 죽거나 배드 엔딩 뒤 '다시 하기'는 씬을 다시 불러와 마지막 기록에서 시작해요.
    [DefaultExecutionOrder(-50)]
    public class GameBootstrap : MonoBehaviour
    {
        PrologueDirector prologue;
        MainDirector main;
        FirstPersonController player;
        PlayerInteractor interactor;

        T Get<T>() where T : Component
        {
            T c = GetComponent<T>();
            return c != null ? c : gameObject.AddComponent<T>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            UIState.Reset();
            Get<AudioDirector>();
            Get<GameUI>();
            Get<ModalUI>();
            Get<MenuUI>();
            prologue = Get<PrologueDirector>();
            main = Get<MainDirector>();
            Get<FinaleDirector>();
            Get<DevMode>();
        }

        void Start()
        {
            MapSpace.Calibrate();

            GameObject go = new GameObject("Player");
            go.AddComponent<CharacterController>();
            player = go.AddComponent<FirstPersonController>();
            interactor = go.AddComponent<PlayerInteractor>();
            go.AddComponent<CanThrower>();
            player.SetMode(FirstPersonController.Mode.Cutscene);
            // 처음엔 맵 밖(위)에 숨겨 둠
            player.Teleport(MapSpace.GF(4, 5f, 20f, 8f), 0f);
            prologue.UsePlayer(player, interactor);
            main.UsePlayer(player, interactor);

            GameUI.I.SetFade(1f);

            if (GameSession.loadOnStart)
            {
                GameSession.loadOnStart = false;
                SaveData d = SaveSystem.Load();
                prologue.SetupForMain();
                if (d != null) main.LoadFromSave(d); else main.StartNew();
            }
            else if (GameSession.startMainDirectly)
            {
                GameSession.startMainDirectly = false;
                prologue.SetupForMain();
                main.StartNew();
            }
            else ShowTitle();
        }

        void ShowTitle()
        {
            SaveData d = SaveSystem.Load();
            string info = d != null ? d.placeName + "  ·  " + d.savedAt : "";
            ModalUI.I.Title(d != null, info, NewGame, Continue, () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        void NewGame()
        {
            if (GameSession.startMainDirectly)
            {
                GameSession.startMainDirectly = false;
                prologue.SetupForMain();
                main.StartNew();
                return;
            }
            SaveSystem.Delete();
            prologue.Begin(() =>
            {
                main.StartAfterPrologue();
            });
        }

        void Continue()
        {
            SaveData d = SaveSystem.Load();
            prologue.SetupForMain();
            if (d != null) main.LoadFromSave(d); else main.StartNew();
        }

        void Update()
        {
            // [Esc] 일시정지
            if (GameInput.EscapeDown() && !UIState.ModalOpen && !UIState.JustClosed)
            {
                bool inGame = prologue.Playing || (main.Running && main.AllowPause);
                if (inGame) ModalUI.I.Pause(main.ToTitle);
            }
        }
    }
}
