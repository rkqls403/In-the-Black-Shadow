using UnityEngine;

namespace BlackShade
{
    // 학교 안 가구·천장 조명·소품 모델·담장을 한 번에 놓는 곳.
    // 'Tools > Black Shade > 게임 씬 만들기'가 이걸 미리 실행해서 씬에 'Decor' 오브젝트로 저장해요.
    //   → 유니티 편집 화면(Scene)에서도 보이고, 손으로 옮기거나 지울 수 있어요.
    // 씬에 Decor 가 없으면 (예: 씬을 다시 안 만들었을 때) 게임 시작할 때 코드로 만들어요.
    // ※ 씬을 다시 만들면 Decor 도 새로 만들어지므로, 손으로 옮긴 건 이 파일의 좌표를 고쳐야 유지돼요.
    public static class Decor
    {
        public const string RootName = "Decor";
        static readonly Color Wood = new Color(0.42f, 0.3f, 0.2f);
        static readonly Color DarkWood = new Color(0.25f, 0.16f, 0.1f);
        static readonly Color Steel = new Color(0.5f, 0.53f, 0.55f);
        static readonly Color White = new Color(0.85f, 0.85f, 0.83f);

        public static Transform Find()
        {
            GameObject g = GameObject.Find(RootName);
            return g != null ? g.transform : null;
        }

        // 씬에 구워져 있으면 그대로 쓰고, 없으면 지금 만든다
        public static Transform Ensure()
        {
            Transform t = Find();
            if (t != null) return t;
            GameObject g = new GameObject(RootName);
            Build(g.transform);
            return g.transform;
        }

        public static void Build(Transform root)
        {
            CeilingFixtures.Init();
            CeilingFixtures.BuildHalls(root);
            CeilingFixtures.BuildRooms(root);
            CeilingFixtures.DisposeTemplate();
            Perimeter.Build(root);
            Furnish(root);
        }

        static Vector3 G(int f, float x, float z, float h = 0f) { return MapSpace.GF(f, x, h, z); }
        static GameObject F(int f, string name, float x, float z, Vector3 size, Color c) { return Looks.Furniture(current, name, G(f, x, z), 0f, size, c); }
        static Transform current;

        static Vector3 E { get { return MapSpace.Dir(1, 0); } }
        static Vector3 Wd { get { return MapSpace.Dir(-1, 0); } }
        static Vector3 S { get { return MapSpace.Dir(0, 1); } }
        static Vector3 N { get { return MapSpace.Dir(0, -1); } }

        // 받은 모델 놓기 (없으면 아무것도 안 생김)
        static GameObject M(int f, string key, float x, float z, Vector3 facing, Vector3 size, PropModels.Fit fit, float h = 0f, string name = null)
        { return PropModels.Place(key, current, G(f, x, z, h), facing, size, fit, name ?? key); }
        static GameObject MH(int f, string key, float x, float z, Vector3 facing, float height, float h = 0f)
        { return M(f, key, x, z, facing, new Vector3(0f, height, 0f), PropModels.Fit.Height, h); }
        // 모델 + 충돌 상자 (col 은 facing 기준 폭·높이·깊이). 모델이 없으면 상자가 임시 모양으로 보임.
        static GameObject MC(int f, string key, float x, float z, Vector3 facing, Vector3 size, PropModels.Fit fit, Vector3 col, Color c)
        {
            GameObject m = M(f, key, x, z, facing, size, fit);
            GameObject box = Looks.Furniture(current, key + "_Col", G(f, x, z), Quaternion.LookRotation(facing).eulerAngles.y, col, c);
            if (m != null) box.GetComponent<Renderer>().enabled = false;
            return m;
        }

        static void Furnish(Transform root)
        {
            GameObject g = new GameObject("Furniture");
            g.transform.SetParent(root, false);
            current = g.transform;
            root = current;

            // ---------------------------------------------------------- 1층 행정실 (90년대 사무용품, 컴퓨터는 예전에 받은 모델)
            OfficeDesk(1, 53f, 8.5f, S, true);
            OfficeDesk(1, 57f, 8.5f, S, true);
            OfficeDesk(1, 53f, 10.2f, S, true);
            MC(1, "RetroFiling", 60.75f, 11.95f, N, new Vector3(0f, 1.0f, 0f), PropModels.Fit.Height, new Vector3(0.38f, 1.0f, 0.5f), Steel);
            MC(1, "RetroFiling", 61.15f, 11.95f, N, new Vector3(0f, 1.0f, 0f), PropModels.Fit.Height, new Vector3(0.38f, 1.0f, 0.5f), Steel);
            MC(1, "RetroPrinter", 63.3f, 11.75f, N, new Vector3(0f, 1.0f, 0f), PropModels.Fit.Height, new Vector3(0.86f, 1.0f, 0.78f), Steel);
            F(1, "Shelf", 67.35f, 7.5f, new Vector3(0.45f, 1.9f, 2.4f), Wood);
            MH(1, "RetroCooler", 48.0f, 11.85f, N, 1.0f);
            MH(1, "RetroPlant", 66.9f, 11.75f, N, 0.9f);
            MH(1, "RetroTrash", 58.1f, 9.4f, N, 0.36f);
            WallClock(1, 57.5f, MapSpace.CORR_END + 0.2f, S);
            MH(1, "RetroMug", 52.45f, 8.3f, S, 0.1f, 0.74f);
            MH(1, "RetroLamp", 56.45f, 8.3f, S, 0.45f, 0.74f);

            // ---------------------------------------------------------- 1층 교장실 / 도서실 / 상담실
            for (int i = 0; i < 4; i++) F(1, "Bookshelf", 87.3f, 14.5f + i * 3f, new Vector3(0.45f, 2.0f, 2.3f), Wood);
            F(1, "Table", 83.5f, 16.5f, new Vector3(1.8f, 0.75f, 0.9f), Wood);
            F(1, "Table", 83.5f, 21.5f, new Vector3(1.8f, 0.75f, 0.9f), Wood);
            F(1, "PrincipalDesk", 6f, 9f, new Vector3(2.1f, 0.88f, 1.35f), DarkWood);   // 받은 사무용 책상 모델 (비율 그대로, 약 2.06×1.29m, 높이 0.88m)
            F(1, "Sofa", 14f, 9.8f, new Vector3(2.2f, 0.75f, 0.8f), new Color(0.3f, 0.08f, 0.08f));
            F(1, "Bookcase", 0.55f, 8f, new Vector3(0.45f, 2.0f, 3.0f), DarkWood);
            F(1, "Table", 71.5f, 17f, new Vector3(1.2f, 0.72f, 0.8f), Wood);
            MC(1, "RetroFiling", 68.4f, 21.3f, E, new Vector3(0f, 1.0f, 0f), PropModels.Fit.Height, new Vector3(0.38f, 1.0f, 0.5f), Steel);
            MC(1, "RetroFiling", 68.4f, 21.7f, E, new Vector3(0f, 1.0f, 0f), PropModels.Fit.Height, new Vector3(0.38f, 1.0f, 0.5f), Steel);
            MH(1, "RetroPlant", 75.5f, 13.1f, Wd, 0.9f);

            // ---------------------------------------------------------- 2~4층 교무실 (90년대 사무용품)
            for (int f = 2; f <= 4; f++)
            {
                for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++)
                {
                    float x = 70.4f + i * 2.4f, z = 15.5f + j * 8f;
                    // 컴퓨터는 책상 오른쪽(동쪽) 뒤편 — 서류·열쇠는 왼쪽에 놓여요
                    OfficeDesk(f, x, z, S, true);
                }
                // 열쇠함 (서쪽 벽, 1·2학년 교무실만 — 열쇠가 걸려 있음)
                if (f == 2 || f == 3)
                {
                    if (M(f, "KeyBox", 68.1f + 0.2f, 16f, E, new Vector3(0.52f, 0f, 0f), PropModels.Fit.Inside, 0.95f) == null)
                        F(f, "KeyBox", 68.2f, 16f, new Vector3(0.1f, 0.5f, 0.6f), new Color(0.6f, 0.6f, 0.55f));
                }
                // 선생님 사물함 3칸 (서쪽 벽)
                for (int k = 0; k < 3; k++)
                    MC(f, "Locker", 68.1f + 0.29f, 23.95f + k * 0.82f, E, new Vector3(0f, 1.85f, 0f), PropModels.Fit.Height, new Vector3(0.8f, 1.85f, 0.56f), Steel);
                MC(f, "RetroFiling", 74.6f, 12.71f + 0.26f, S, new Vector3(0f, 1.0f, 0f), PropModels.Fit.Height, new Vector3(0.38f, 1.0f, 0.5f), Steel);
                MC(f, "RetroFiling", 75.0f, 12.71f + 0.26f, S, new Vector3(0f, 1.0f, 0f), PropModels.Fit.Height, new Vector3(0.38f, 1.0f, 0.5f), Steel);
                MC(f, "RetroPrinter", 75.45f, 14.3f, Wd, new Vector3(0f, 1.0f, 0f), PropModels.Fit.Height, new Vector3(0.86f, 1.0f, 0.78f), Steel);
                MH(f, "RetroCooler", 68.35f, 21.6f, E, 1.0f);
                MH(f, "RetroPlant", 75.45f, 26.95f, Wd, 0.9f);
                MH(f, "RetroFan", 74.6f, 17.6f, Wd, 1.2f);
                MH(f, "RetroTrash", 71.6f, 16.6f, N, 0.36f);
                WallClock(f, 72f, 12.71f, S);
                MH(f, "RetroLamp", 69.95f, 15.3f, S, 0.45f, 0.74f);
                if (f != 3) MH(f, "RetroMug", 72.4f, 15.78f, S, 0.1f, 0.74f);
                if (f != 2) M(f, "RetroBinder2", 72.25f, 23.3f, S, new Vector3(0.3f, 0f, 0f), PropModels.Fit.Inside, 0.74f);
                MH(f, "RetroPhone", 73.05f, 23.75f + 0.0f, S, 0.1f, 0.74f);
            }

            // ---------------------------------------------------------- 2층 과학실 시약장 / 3층 양호실
            // 시약장: 받은 유리문 약품장 모델 (서쪽 벽, 유리문이 교실 쪽). 높이 1.8m → 폭 약 0.97m, 깊이 0.48m (비율 그대로)
            MC(2, "ReagentCabinet", ReagentX, ReagentZ, E, new Vector3(0f, 1.8f, 0f), PropModels.Fit.Height, new Vector3(0.97f, 1.8f, 0.48f), new Color(0.75f, 0.78f, 0.75f));
            F(3, "Bed", 69.0f, 31f, new Vector3(0.95f, 0.55f, 2.0f), White);
            F(3, "Bed", 69.0f, 34.6f, new Vector3(0.95f, 0.55f, 2.0f), White);
            OfficeDesk(3, 73.5f, 40.5f, S, false);
            // 약장: 받은 책장 모델 (남쪽 벽, 칸이 방 쪽). 높이 2.0m → 폭 1.25m, 깊이 0.33m (비율 그대로)
            MC(3, "MedShelf", 71f, 43.46f - 0.2f, N, new Vector3(0f, 2.0f, 0f), PropModels.Fit.Height, new Vector3(1.25f, 2.0f, 0.34f), White);

            // ---------------------------------------------------------- 4층 방송실 / 창고
            F(4, "Console", 71.5f, 33f, new Vector3(2.6f, 0.9f, 0.8f), new Color(0.18f, 0.18f, 0.2f));
            // 방음판: 서쪽 벽 (창문 앞은 비움)
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 8; c++)
                    M(4, "SoundPanel", 68.1f + 0.03f, 28.4f + c * 0.6f, E, new Vector3(0.6f, 0f, 0f), PropModels.Fit.Inside, 1.0f + r * 0.6f, "SoundPanel");
            // 3학년 창고: 철제 선반 2개 (남쪽 벽) — 명찰 장식을 찾는 상자가 여기에
            MC(4, "StoreShelf", 69.3f, 12.2f - 0.27f, N, new Vector3(0f, 1.9f, 0f), PropModels.Fit.Height, new Vector3(1.32f, 1.9f, 0.54f), Steel);
            MC(4, "StoreShelf", 70.7f, 12.2f - 0.27f, N, new Vector3(0f, 1.9f, 0f), PropModels.Fit.Height, new Vector3(1.32f, 1.9f, 0.54f), Steel);
            F(4, "Boxes", 73.5f, 10f, new Vector3(1.0f, 0.6f, 0.8f), new Color(0.55f, 0.42f, 0.28f));

            // 2~4층 로비: 플라스틱 4인 벤치 2개씩 (자판기 옆 남쪽 벽, 앉는 쪽이 북쪽). 색은 PropModelSetup 의 BenchColor
            for (int f = 2; f <= 4; f++)
                foreach (float x in BenchX)
                    MC(f, "PlasticBench", x, BenchZ, N, new Vector3(0f, 0.87f, 0f), PropModels.Fit.Height, new Vector3(1.94f, 0.45f, 0.41f), Steel);
            SpecialRooms(root);
        }

        // 로비 벤치 자리 (MainDirector 가 벤치 위 쪽지 위치에 같이 씀)
        public static readonly float[] BenchX = { 24.0f, 26.3f };
        public const float BenchZ = 11.95f;
        // 과학실 시약장 자리 (서쪽 벽 안쪽 면 68.1 + 깊이 절반)
        public const float ReagentX = 68.1f + 0.27f, ReagentZ = 34.5f;

        // 사무용 책상 + (컴퓨터) + 의자. facing = 앉는 사람이 있는 쪽
        static void OfficeDesk(int f, float x, float z, Vector3 sit, bool pc)
        {
            MC(f, "RetroDesk", x, z, sit, new Vector3(0f, 0.74f, 0f), PropModels.Fit.Height, new Vector3(1.29f, 0.74f, 0.73f), Wood);
            Vector3 c = G(f, x, z);
            if (pc)
            {
                Vector3 p = c + sit * -0.12f + MapSpace.Dir(1, 0) * 0.38f;
                PropModels.Place("Computer", current, new Vector3(p.x, c.y + 0.74f, p.z), sit, new Vector3(0.5f, 0.42f, 0.32f), PropModels.Fit.Inside, "Computer");
            }
            Vector3 ch = c + sit * 0.68f;
            PropModels.Place("RetroChair", current, ch, -sit, new Vector3(0f, 1.0f, 0f), PropModels.Fit.Height, "Chair");
        }

        // 벽시계 (벽면 바로 앞, 높이 2.25m)
        static void WallClock(int f, float x, float z, Vector3 facing)
        {
            Vector3 p = G(f, x, z, 2.1f) + facing * 0.025f;
            PropModels.Place("RetroClock", current, p, facing, new Vector3(0.34f, 0.34f, 0f), PropModels.Fit.Inside, "Clock");
        }

        // ================================================================ 특수 교실 (직사각형: 짧은 벽 = 앞/뒤, 긴 벽 = 양옆)
        static readonly Color DeskTop = new Color(0.55f, 0.45f, 0.32f);
        static readonly Color LabTop = new Color(0.12f, 0.12f, 0.13f);
        static readonly Color PcGrey = new Color(0.6f, 0.6f, 0.62f);

        static void SpecialRooms(Transform root)
        {
            // ---------------------------------------------------------- 5층 컴퓨터실 (x 0~20.74) : 앞 = 서쪽 벽
            // 교탁: 받은 컴퓨터 책상 세트 (L자 책상 + 컴퓨터 + 의자), 선생님은 학생 쪽(동쪽)을 봄
            MC(5, "PcTeacherSet", 1.95f, 8.36f, E, new Vector3(0f, 1.05f, 0f), PropModels.Fit.Height, new Vector3(1.86f, 0.75f, 1.39f), Wood);
            foreach (float x in PcCols)
                foreach (float z in PcRows)
                {
                    // 학생 책상 (앉는 쪽 = 동쪽, 칠판을 봄) + 컴퓨터 1대 + 의자
                    MC(5, "PcDesk", x, z, E, new Vector3(1.3f, 0f, 0f), PropModels.Fit.Inside, new Vector3(1.3f, 0.74f, 0.56f), PcGrey);
                    PropModels.Place("Computer", root, G(5, x - 0.08f, z - 0.25f, 0.74f), E, new Vector3(0.5f, 0.42f, 0.32f), PropModels.Fit.Inside, "Computer");
                    PropModels.Place("RetroChair", root, G(5, x + 0.62f, z), Wd, new Vector3(0f, 1.0f, 0f), PropModels.Fit.Height, "Chair");
                }

            // ---------------------------------------------------------- 5층 미술실 (x 20.74~47.06) : 앞 = 서쪽 벽
            MC(5, "Lectern", 22.55f, 8.36f, Wd, new Vector3(0f, 0.85f, 0f), PropModels.Fit.Height, new Vector3(1.75f, 0.85f, 0.62f), Wood);
            foreach (float x in ArtCols)
                foreach (float z in ArtRows)
                    MC(5, "ArtTableSet", x, z, S, new Vector3(1.95f, 0f, 0f), PropModels.Fit.Inside, new Vector3(1.95f, 0.74f, 0.82f), DeskTop);   // 4인용 책상 + 의자
            // 석고 두상 2개: 앞쪽 양 구석 받침대 위에서 학생 쪽(동쪽)을 봄
            if (PropModels.Has("StatueHera") || PropModels.Has("StatueAugustus"))
            {
                F(5, "Pedestal", 21.3f, 5.3f, new Vector3(0.5f, 0.95f, 0.5f), White);
                F(5, "Pedestal", 21.3f, 11.4f, new Vector3(0.5f, 0.95f, 0.5f), White);
                PropModels.Place("StatueHera", root, G(5, 21.3f, 5.3f, 0.95f), E, new Vector3(0f, 0.42f, 0f), PropModels.Fit.Height, "Statue_Hera");
                PropModels.Place("StatueAugustus", root, G(5, 21.3f, 11.4f, 0.95f), E, new Vector3(0f, 0.55f, 0f), PropModels.Fit.Height, "Statue_Augustus");
            }
            // 이젤 3개: 교실 뒤쪽(동쪽)에서 그림이 교실 쪽(서쪽)을 봄. 그림 파일은 Resources/Paintings/easel1~3
            for (int i = 0; i < 3; i++) Easels.Create(root, G(5, 44.8f, 6.0f + i * 2.4f), Wd, i + 1);

            // ---------------------------------------------------------- 5층 음악실 (x 47.06~67.8) : 앞 = 서쪽 벽
            Pianos.Upright(root, G(5, PianoX, PianoZ), E);   // 작은 피아노, 건반은 교실 쪽
            foreach (float x in MusicCols)
                foreach (float z in MusicRows)
                    F(5, "MusicDesk", x, z, new Vector3(0.6f, 0.72f, 1.2f), DeskTop);
            PropModels.Place("MusicStand", root, G(5, 50.2f, 6.3f), Wd, new Vector3(0f, 1.15f, 0f), PropModels.Fit.Height, "MusicStand");
            PropModels.Place("MusicStand", root, G(5, 50.2f, 7.5f), Wd, new Vector3(0f, 1.15f, 0f), PropModels.Fit.Height, "MusicStand");

            // ---------------------------------------------------------- 2층 과학실 (z 27.51~43.62) : 앞 = 북쪽 벽
            // 앞쪽: ㄱ자 실험대(싱크대) — 긴 쪽이 학생을 향하고 선생님은 그 안쪽에 섬
            M(2, "LabFrontDesk", 72.0f, 29.35f, N, new Vector3(3.6f, 0f, 0f), PropModels.Fit.Inside);
            GameObject fc = Looks.Furniture(current, "LabFrontDesk_Col", G(2, 72.0f, 30.5f), 0f, new Vector3(3.6f, 0.95f, 0.82f), LabTop);
            if (PropModels.Has("LabFrontDesk")) fc.GetComponent<Renderer>().enabled = false;
            MH(2, "SciChair", 72.4f, 29.0f, S, 0.9f);
            // 학생 실험대 6개 (긴 쪽이 남북), 양옆에 의자
            foreach (float x in LabCols)
                foreach (float z in LabRows)
                {
                    MC(2, "LabTable", x, z, S, new Vector3(1.2f, 0f, 2.85f), PropModels.Fit.Inside, new Vector3(1.2f, 0.85f, 2.83f), LabTop);
                    MH(2, "LabStool", x - 0.85f, z, E, 0.54f);
                    MH(2, "LabStool", x + 0.85f, z, Wd, 0.54f);
                }
            // 인체 골격 모형: 앞쪽 오른쪽 구석에서 교실 쪽을 봄 (키 1.65m)
            PropModels.Place("Skeleton", root, G(2, 75.4f, 28.3f), MapSpace.Dir(-1, 1), new Vector3(0f, 1.65f, 0f), PropModels.Fit.Height, "Skeleton");

            // ---------------------------------------------------------- 1층 행정실 컴퓨터는 OfficeDesk 에서
            // ---------------------------------------------------------- 4층 방송실: 장비 랙 + 콘솔 위 녹음기
            PropModels.Place("ServerRack", root, G(4, 68.78f, 41.3f), E, new Vector3(0f, 1.9f, 0f), PropModels.Fit.Height, "ServerRack");
            PropModels.Place("TapeDeck", root, G(4, 72.2f, 33.0f, 0.9f), MapSpace.Dir(0, -1), new Vector3(0f, 0.3f, 0f), PropModels.Fit.Height, "TapeDeck");
        }

        // 책상 배치 (MainDirector 가 아이템 위치에 같이 씀)
        public static readonly float[] PcCols = { 4.4f, 6.8f, 9.2f, 11.6f, 14.0f, 16.4f };
        public static readonly float[] PcRows = { 5.6f, 7.0f, 9.7f, 11.1f };
        public static readonly float[] ArtCols = { 25.5f, 29.5f, 33.5f, 37.5f, 41.5f };
        public static readonly float[] ArtRows = { 6.4f, 10.3f };
        public static readonly float[] MusicCols = { 52.5f, 55.0f, 57.5f, 60.0f, 62.5f };
        public static readonly float[] MusicRows = { 5.7f, 7.1f, 9.6f, 11.0f };
        public static readonly float[] LabCols = { 70.3f, 73.8f };
        public static readonly float[] LabRows = { 32.65f, 36.45f, 40.25f };
        public const float PianoX = 47.52f, PianoZ = 9.6f;
    }
}
