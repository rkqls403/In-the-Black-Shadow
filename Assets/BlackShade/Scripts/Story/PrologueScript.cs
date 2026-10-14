using UnityEngine;

namespace BlackShade
{
    // 프롤로그의 모든 대사.
    // 대사를 고치고 싶으면 이 파일의 글자만 바꾸면 돼요. (따옴표 안의 글자)
    // 이름을 바꾸려면 맨 위 Me / Minjae / Haeun / Dohyun 값을 바꾸세요.
    public static class PrologueScript
    {
        public const string Me = "나";
        public const string Minjae = "민재";   // 장난꾸러기, 분위기 메이커 (school_boy_8)
        public const string Haeun = "하은";    // 장난 좋아하고 학교 괴담을 잘 앎. 사실은 1985년 졸업식 사고로 죽은 졸업생 (여자 모델)
        public const string Dohyun = "도현";   // 조용하고 차분함, 의심 많음 (school_boy_10)
        public const string Unknown = "???";

        public static readonly Color MeColor = new Color(0.72f, 0.8f, 0.9f);
        public static readonly Color MinjaeColor = new Color(0.96f, 0.78f, 0.5f);
        public static readonly Color HaeunColor = new Color(0.98f, 0.62f, 0.7f);
        public static readonly Color DohyunColor = new Color(0.62f, 0.85f, 0.72f);
        public static readonly Color UnknownColor = new Color(0.85f, 0.3f, 0.28f);

        public struct Line
        {
            public string speaker;
            public string text;
            public bool whisper;   // 작게 (기울임)
            public float auto;     // >0 이면 그 시간 뒤 자동으로 넘어감
            public bool slow;      // 천천히 타이핑
        }

        static Line L(string s, string t) { return new Line { speaker = s, text = t, auto = -1f }; }
        static Line W(string s, string t) { return new Line { speaker = s, text = t, whisper = true, auto = -1f }; }
        static Line A(string s, string t, float auto) { return new Line { speaker = s, text = t, auto = auto }; }
        static Line Slow(string s, string t) { return new Line { speaker = s, text = t, slow = true, auto = -1f }; }

        // ------------------------------------------------------------ 컷씬: 졸다가 깨어남
        // 눈 감은 상태(검은 화면)에서 들리는 목소리
        public static readonly Line[] WakeDark =
        {
            new Line { speaker = Minjae, text = "…야.", whisper = true, auto = 1.6f },
            A(Minjae, "야. 일어나 봐.", 2.2f),
        };

        // 책상에 엎드린 채 눈을 뜸
        public static readonly Line[] WakeOnDesk =
        {
            L(Minjae, "와, 진짜 잤네. 석식 먹고 오자마자 엎어지더라."),
            L(Haeun, "걔 깼어? 빨리 오라 그래! 손 모자라!"),
        };

        // 몸을 일으켜 앉음
        public static readonly Line[] WakeSitting =
        {
            L(Minjae, "야자 종 친 지가 언젠데. 이제 일어났냐?"),
            L(Me, "…몇 시야?"),
            L(Minjae, "몰라, 일곱 시 다 됐나? 해 지기 전에 끝내자."),
            L(Dohyun, "우리가 3학년 1반 맡았잖아. 내일 졸업식 전까지 다 꾸며 놔야 돼."),
            L(Minjae, "빨리 좀 도와. 우리 아직 할 거 많아."),
        };

        // 자리에서 일어난 직후 (할 일 안내)
        public static readonly Line[] AfterStand =
        {
            L(Minjae, "롤링페이퍼는 맨 앞 책상 서랍에 넣어 놨고, 풍선은 뒤쪽 책상에 있어."),
            L(Minjae, "칠판은 네가 써. 우리 중에 글씨 제일 나은 게 너잖아."),
        };

        // ------------------------------------------------------------ 할 일 ① 롤링페이퍼
        public const string RollingPaperTitle = "3학년 1반 선배님들께";

        public static GameUI.Note[] RollingPaperNotes()
        {
            Color blue = new Color(0.16f, 0.27f, 0.6f);
            Color pink = new Color(0.82f, 0.3f, 0.45f);
            Color pencil = new Color(0.25f, 0.24f, 0.24f);
            Color green = new Color(0.15f, 0.5f, 0.3f);
            Color orange = new Color(0.85f, 0.45f, 0.15f);
            Color red = new Color(0.8f, 0.15f, 0.15f);
            return new[]
            {
                new GameUI.Note("선배님들 졸업 축하드려요!!\n대학 가서도 연락 주세요 ㅠㅠ\n- 민재", blue, new Vector2(-270, 150), 3f, 28),
                new GameUI.Note("급식 줄 새치기\n눈감아 주셔서 감사했습니다 (?)\n- 하은", pink, new Vector2(250, 160), -4f, 28),
                new GameUI.Note("덕분에 1년 동안 즐거웠습니다.\n졸업 축하드립니다.\n- 도현", pencil, new Vector2(-250, -40), -2f, 27),
                new GameUI.Note("내일 끝나고\n사진 같이 찍어요!", green, new Vector2(270, -20), 5f, 30),
                new GameUI.Note("선배 치킨 사 주세요 ㅎㅎ", orange, new Vector2(-220, -210), -6f, 28),
                new GameUI.Note("축 졸업 ♡", red, new Vector2(250, -200), 4f, 44),
            };
        }

        public static readonly Line[] AfterRollingPaper =
        {
            L(Dohyun, "다 봤으면 다시 서랍에 넣어 둬. 내일 선배들 오면 보게."),
        };

        // ------------------------------------------------------------ 할 일 ② 풍선
        public static readonly Line[] AfterBalloon =
        {
            L(Haeun, "오, 폐활량 뭐냐. 그거 뒤에 붙이자."),
            L(Haeun, "좋아. 이제 좀 졸업식 같네."),
        };

        // ------------------------------------------------------------ 할 일 ③ 칠판
        public const string BoardMain = "졸업 축하합니다!";
        public const string BoardSub = "3학년 1반 선배님들";
        public const string BoardSign = "- 2학년 일동 -";

        public static readonly Line[] AfterBoard =
        {
            L(Minjae, "야, 글씨 좀 예쁘게 써."),
            L(Me, "이 정도면 됐지 뭘."),
            L(Minjae, "'다'는 왜 기울어져 있냐 ㅋㅋ"),
        };

        // ------------------------------------------------------------ 해가 다 짐
        public static readonly Line[] AllDone =
        {
            L(Minjae, "오, 다 했다."),
            L(Dohyun, "…밖에 벌써 깜깜하네."),
            L(Minjae, "근데 하은이 어디 갔냐? 아까까지 뒤에 있었는데."),
            L(Dohyun, "화장실 간다던데."),
            L(Minjae, "아 춥다. 야, 뒤에 창문 좀 닫아 줘."),
        };

        public const string ObjectiveWindow = "창문 닫기";
        public const string PromptWindow = "창문 닫기";

        // ------------------------------------------------------------ 하은이 놀래킴
        public static readonly Line ScareCall = new Line { speaker = Unknown, text = "야.", auto = 0.7f };

        public static readonly Line[] AfterScare =
        {
            L(Haeun, "ㅋㅋㅋㅋㅋ 놀랐냐?"),
            L(Haeun, "표정 봐. 진짜 놀랐네."),
            L(Me, "야, 하지 마라."),
            L(Haeun, "아 왜~ 심심하잖아."),
            L(Minjae, "ㅋㅋㅋ 방금 소리 지를 뻔한 거 봤냐?"),
            L(Haeun, "근데 진짜 심심하다. 해도 다 졌는데."),
            L(Haeun, "우리 무서운 얘기나 할래?"),
            L(Minjae, "오, 콜. 다 이리 모여 봐."),
        };

        // ------------------------------------------------------------ 무서운 얘기
        public static readonly Line[] StoriesWarmup =
        {
            L(Minjae, "그럼 나부터."),
            L(Minjae, "우리 학교 밤에 돌아다니면 계단 숫자가 달라진다는 거 알아?"),
            L(Dohyun, "뭔 소리야."),
            L(Minjae, "진짜라니까. 4층에서 내려갔는데 3층이 안 나온대."),
            L(Haeun, "아 뭐야. 그런 거 말고 좀 제대로 된 거 해 봐."),
            L(Minjae, "그럼… 과학실 해골이 밤마다 자리 바뀐다는 거?"),
            L(Dohyun, "그건 초등학교 때도 들었어."),
            L(Haeun, "…그럼 내가 우리 학교에서 제일 오래된 얘기 해 줄까?"),
            L(Dohyun, "넌 그런 걸 어떻게 다 알아?"),
            W(Haeun, "나? 이 학교 오래 다녔거든."),
            L(Minjae, "뭐래. 너도 2학년이잖아 ㅋㅋ"),
        };

        // 하은이가 들려주는 이야기
        public static readonly Line[] OldestStory1 =
        {
            L(Haeun, "옛날에, 우리 학교 처음 생겼을 때 말이야."),
            L(Haeun, "첫 졸업식 날에 사고가 났대."),
            L(Haeun, "그날 졸업생들이 전부 강당에 모였는데…"),
            L(Haeun, "갑자기 강당 천장이 무너졌다는 거야."),
        };

        // (형광등이 잠깐 깜빡인 뒤)
        public static readonly Line[] OldestStory2 =
        {
            L(Haeun, "근데 이상한 게 뭔지 알아?"),
            L(Dohyun, "…뭔데?"),
            L(Haeun, "그날 강당에 있던 사람들은 한 명도 못 나왔대."),
            L(Minjae, "그래서?"),
            L(Haeun, "학생이랑 선생님까지… 거의 300명이 죽었다는 거야."),
            L(Minjae, "야, 300명은 좀 심한 거 아니냐?"),
            L(Dohyun, "그러니까 괴담이지."),
            Slow(Haeun, "근데 그 뒤로, 졸업식 전날 밤에 학교에 남아 있으면…"),
            Slow(Haeun, "선생님이 졸업식에 참석하지 않은 학생들을 데리러 온대."),
        };

        // ------------------------------------------------------------ 정전
        public static readonly Line[] Blackout =
        {
            L(Minjae, "…뭐야?"),
            L(Dohyun, "정전인가?"),
            L(Minjae, "야, 휴대폰 있어? 플래시 좀 켜 봐."),
            L(Dohyun, "…야. 하은이 어디 갔어?"),
            L(Minjae, "방금까지 여기 있었는데…?"),
        };

        public const string Broadcast1 = "졸업식이 시작되었습니다.";
        public const string Broadcast2 = "참석하지 않은 학생에게 벌점을 주기 위해,\n지금 선생님이 강당에서 출발합니다.";

        public static readonly Line AfterBroadcast = new Line { speaker = Minjae, text = "…방금 그거, 하은이가 한 얘기랑 똑같잖아.", whisper = true, auto = 3.0f };
        public static readonly Line WhoIsThere = new Line { speaker = Dohyun, text = "…하은아? 너야?", whisper = true, auto = 3.2f };

        // ------------------------------------------------------------ 튜토리얼 / 목표
        public const string ObjectiveTitle = "졸업식 준비 · 3학년 1반";
        public static readonly string[] Objectives =
        {
            "롤링페이퍼 확인하기 (맨 앞 책상 서랍)",
            "풍선 불어서 붙이기 (뒤쪽 책상)",
            "칠판에 축하 문구 쓰기",
        };

        public const string PromptPaper = "롤링페이퍼 꺼내 보기";
        public const string PromptBalloon = "풍선 불기";
        public const string PromptBoard = "분필로 칠판에 쓰기";

        public const string TutMove = "이동";
        public const string TutLook = "마우스로 주위 둘러보기";
        public const string TutInteract = "빛나는 물건을 바라보고 E 키로 상호작용";
        public const string TutHold = "E 키를 길게 누르면 풍선을 불 수 있어요";
    }
}
