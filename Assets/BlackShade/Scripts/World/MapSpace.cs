using System.Collections.Generic;
using UnityEngine;

namespace BlackShade
{
    // 맵 좌표 ↔ 유니티 좌표 변환.
    // build_school.py 의 좌표(m)를 그대로 쓰면, 맵을 유니티로 가져올 때 축이 뒤집히거나 위치가 옮겨져도
    // 맵 안의 표지(Room_4F_3학년 1반 등) 두 개로 자동 보정해요.
    public static class MapSpace
    {
        // ---- 맵(School_5F_Map.glb)과 같은 치수
        public const float CW = 10.37f, CD = 8.31f, LW = 26.32f, HS = 3.505f;
        public const float NORTH_T = 0.6f, CORR_END = 4.2f, WING_END = 12.51f;
        public const float L = 67.8f, COL0 = 67.8f, COL1 = 76.11f, VC0 = 76.11f, VC1 = 79.71f, RC0 = 79.71f, E = 88.02f, BOT = 43.62f;
        public static readonly float[] XS = { 0f, 10.37f, 20.74f, 47.06f, 57.43f, 67.8f };
        public static readonly float[] VR = { 12.51f, 22.88f, 33.25f, 43.62f };
        // 5층 특별실 3개 (컴퓨터실 / 미술실 / 음악실) 경계
        public static readonly float[] X5 = { 0f, 20.74f, 47.06f, 67.8f };
        // 계단: 중앙계단(북쪽 탑), 계단B(북동 모서리)
        public const float SN0 = 16.74f, SN1 = 20.74f;
        public const float SB0 = 84.02f, SB1 = 88.02f, SB_Z0 = 0.6f, SB_Z1 = 5.6f;
        // 1층 현관 유리문 줄 (z)
        public const float FACADE_Z = 9.95f;

        public static float sx = -1f, sz = 1f, ox, oy, oz;
        public static bool Calibrated { get; private set; }

        public static bool Calibrate()
        {
            Transform a = Find("Room_4F_3학년 1반");
            Transform b = Find("Room_4F_3학년 5반");
            if (a == null || b == null)
            {
                Debug.LogWarning("[MapSpace] 맵 표지(Room_4F_...)를 못 찾았어요. 맵이 씬에 있는지 확인하세요. 기본값(glTFast 기준)으로 계산합니다.");
                Calibrated = false;
                return false;
            }
            // 맵 좌표
            Vector3 ga = new Vector3(5.185f, 10.515f, 8.355f);
            Vector3 gb = new Vector3(83.865f, 10.515f, 17.695f);
            Vector3 ua = a.position, ub = b.position;
            float kx = (ub.x - ua.x) / (gb.x - ga.x);
            float kz = (ub.z - ua.z) / (gb.z - ga.z);
            // 축이 바뀐 경우 (예: 다른 임포터) 대비
            if (Mathf.Abs(kx) < 0.5f || Mathf.Abs(kz) < 0.5f)
            {
                Debug.LogWarning("[MapSpace] 맵이 회전되어 있는 것 같아요. 맵 오브젝트의 Rotation을 (0,0,0)으로 맞춰 주세요.");
            }
            sx = Mathf.Sign(kx); sz = Mathf.Sign(kz);
            ox = ua.x - sx * ga.x;
            oz = ua.z - sz * ga.z;
            oy = ua.y - ga.y;
            Calibrated = true;
            Debug.Log("[MapSpace] 보정 완료  x' = " + sx + "·x + " + ox.ToString("0.00") + ",  z' = " + sz + "·z + " + oz.ToString("0.00"));
            return true;
        }

        static Transform Find(string name)
        {
            foreach (Transform t in Object.FindObjectsOfType<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // 맵 좌표 → 월드
        public static Vector3 G(float gx, float gy, float gz) { return new Vector3(sx * gx + ox, gy + oy, sz * gz + oz); }
        // 층 기준: f층 바닥에서 h 높이
        public static Vector3 GF(int f, float gx, float h, float gz) { return G(gx, (f - 1) * HS + h, gz); }
        // 맵 방향 → 월드 방향
        public static Vector3 Dir(float dx, float dz) { return new Vector3(sx * dx, 0f, sz * dz).normalized; }
        public static float YawOf(float dx, float dz) { Vector3 d = Dir(dx, dz); return Quaternion.LookRotation(d).eulerAngles.y; }

        // 월드 → 맵 (x, z)
        public static Vector2 ToGL(Vector3 w) { return new Vector2((w.x - ox) / sx, (w.z - oz) / sz); }
        public static float FloorY(int f) { return (f - 1) * HS + oy; }
        public static int FloorOf(Vector3 w) { return Mathf.Clamp(Mathf.FloorToInt((w.y - oy + 1.0f) / HS) + 1, 1, 5); }
    }

    // ==================================================================== 방/문 데이터
    public enum DoorLock { Open, Locked, Key, Code, Special }
    public enum Side { N, E, S, W }

    public class DoorSpec
    {
        public string id;
        public Side side;
        public float center;     // N/S 벽이면 x, E/W 벽이면 z
        public float width = 1.2f;
        public DoorLock lockType = DoorLock.Open;
        public string key;
        public string code;
        public string lockedMsg;
    }

    public class RoomSpec
    {
        public string id;          // 예: "2F_1-5", "1F_admin"
        public int floor;
        public string name;        // 화면에 보이는 이름
        public string mapNode;     // 교실 모듈 이름 (Class_1학년5반) - 없으면 일반 방
        public float x0, z0, x1, z1;
        public bool hideSpot = true;   // 청소함 배치
        public bool toilet;
        public bool lights = true;
        public float wallIn = 0.2f;    // 문이 있는 안쪽 벽 두께
        public readonly List<DoorSpec> doors = new List<DoorSpec>();

        public bool IsModule { get { return !string.IsNullOrEmpty(mapNode); } }
        public bool Contains(float gx, float gz) { return gx >= x0 && gx <= x1 && gz >= z0 && gz <= z1; }
        public Vector2 Center { get { return new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f); } }
    }

    public static class MapData
    {
        static List<RoomSpec> rooms;
        public static List<RoomSpec> Rooms { get { if (rooms == null) Build(); return rooms; } }

        public static RoomSpec Get(string id)
        {
            foreach (RoomSpec r in Rooms) if (r.id == id) return r;
            return null;
        }

        public static RoomSpec RoomAt(Vector3 world)
        {
            int f = MapSpace.FloorOf(world);
            Vector2 g = MapSpace.ToGL(world);
            foreach (RoomSpec r in Rooms) if (r.floor == f && r.Contains(g.x, g.y)) return r;
            return null;
        }

        // 맵 위에 그릴 복도/로비 (지도 화면용)
        public static List<RoomSpec> Halls(int f)
        {
            List<RoomSpec> h = new List<RoomSpec>();
            float E = MapSpace.E;
            if (f == 1)
            {
                h.Add(R("hall", f, "복도", 0, 0, MapSpace.XS[2], MapSpace.CORR_END));
                h.Add(R("hall", f, "복도", MapSpace.XS[3], 0, E, MapSpace.CORR_END));
                h.Add(R("hall", f, "현관", MapSpace.XS[2], 0, MapSpace.XS[3], MapSpace.FACADE_Z));
            }
            else h.Add(R("hall", f, "복도", 0, 0, E, MapSpace.CORR_END));
            h.Add(R("hall", f, "", MapSpace.VC0, MapSpace.CORR_END, E, MapSpace.WING_END));
            if (f != 5) h.Add(R("hall", f, "", MapSpace.VC0, MapSpace.WING_END, MapSpace.VC1, MapSpace.BOT));
            if (f >= 2 && f <= 4) h.Add(R("hall", f, "로비", MapSpace.XS[2], MapSpace.CORR_END, MapSpace.XS[3], MapSpace.WING_END));
            h.Add(R("hall", f, "계단", MapSpace.SN0, -4.5f, MapSpace.SN1, 0f));
            return h;
        }

        static RoomSpec R(string id, int f, string name, float x0, float z0, float x1, float z1)
        {
            return new RoomSpec { id = id, floor = f, name = name, x0 = x0, z0 = z0, x1 = x1, z1 = z1 };
        }

        static DoorSpec D(string id, Side s, float c, float w = WoodW, DoorLock l = DoorLock.Open, string key = null, string msg = null)
        {
            return new DoorSpec { id = id, side = s, center = c, width = w, lockType = l, key = key, lockedMsg = msg };
        }

        const string LockedClass = "문이 잠겨 있다.";
        // 문 폭 = 맵 벽 구멍 폭 (문 모델 크기에 맞춰 벽을 뚫어 둠)
        public const float WoodW = 1.02f, StaffW = 2.04f, ClassW = 1.10f, HallW = 3.0f;

        static void Build()
        {
            rooms = new List<RoomSpec>();
            float[] XS = MapSpace.XS, VR = MapSpace.VR;
            float CE = MapSpace.CORR_END, WE = MapSpace.WING_END, C0 = MapSpace.COL0, C1 = MapSpace.COL1, RC = MapSpace.RC0, E = MapSpace.E, BOT = MapSpace.BOT;

            // ------------------------------------------------ 층마다 공통: 화장실, 창고
            for (int f = 1; f <= 5; f++)
            {
                RoomSpec wc = R(f + "F_wc", f, f + "층 화장실", C0, CE, C1, 9.2f);
                wc.toilet = true; wc.hideSpot = false;
                wc.doors.Add(D(f + "F_wc", Side.E, (CE + 9.2f) * 0.5f));
                rooms.Add(wc);

                RoomSpec st = R(f + "F_store", f, f == 5 ? "체육창고" : (f == 4 ? "3학년 창고" : "창고"), C0, 9.2f, C1, WE);
                st.hideSpot = false;
                if (f == 4) st.doors.Add(D("4F_store", Side.E, (9.2f + WE) * 0.5f, WoodW, DoorLock.Key, "key_storage", "창고 문이 잠겨 있다."));
                else st.doors.Add(D(f + "F_store", Side.E, (9.2f + WE) * 0.5f, WoodW, DoorLock.Locked, null, "창고 문이 잠겨 있다."));
                rooms.Add(st);
            }

            // ------------------------------------------------ 1층
            RoomSpec principal = R("1F_principal", 1, "교장실", 0, CE, XS[2], WE);
            principal.doors.Add(D("1F_principal", Side.N, 16.0f, WoodW, DoorLock.Key, "key_principal", "교장실 문이 굳게 잠겨 있다."));
            rooms.Add(principal);

            RoomSpec admin = R("1F_admin", 1, "행정실", XS[3], CE, L(), WE);
            admin.doors.Add(D("1F_admin", Side.N, 51.5f, WoodW, DoorLock.Key, "key_admin", "행정실 문이 잠겨 있다. 열쇠가 근처 어딘가에 있지 않을까."));
            rooms.Add(admin);

            RoomSpec counsel = R("1F_counsel", 1, "상담실", C0, WE, C1, 26.51f);
            counsel.doors.Add(D("1F_counsel", Side.E, (WE + 26.51f) * 0.5f, WoodW, DoorLock.Key, "key_counsel", "상담실 문이 잠겨 있다."));
            rooms.Add(counsel);

            RoomSpec special = R("1F_special", 1, "특수학급", C0, 26.51f, C1, BOT);
            special.doors.Add(D("1F_special", Side.E, (26.51f + BOT) * 0.5f, WoodW, DoorLock.Locked, null, "특수학급 문이 잠겨 있다. 열릴 것 같지 않다."));
            special.hideSpot = false; special.lights = false;
            rooms.Add(special);

            RoomSpec library = R("1F_library", 1, "도서실", RC, WE, E, 26.51f);
            library.doors.Add(D("1F_library", Side.W, (WE + 26.51f) * 0.5f - 1.5f));
            rooms.Add(library);

            RoomSpec archive = R("1F_archive", 1, "자료실", RC, 26.51f, E, 35.51f);
            archive.doors.Add(D("1F_archive", Side.W, (26.51f + 35.51f) * 0.5f - 1.5f, WoodW, DoorLock.Locked, null, "자료실 문이 잠겨 있다."));
            archive.hideSpot = false; archive.lights = false;
            rooms.Add(archive);

            RoomSpec wcE = R("1F_wcE", 1, "1층 화장실 (동)", RC, 35.51f, E, BOT);
            wcE.toilet = true; wcE.hideSpot = false;
            wcE.doors.Add(D("1F_wcE", Side.W, (35.51f + BOT) * 0.5f));
            rooms.Add(wcE);

            // ------------------------------------------------ 2~4층 교실
            for (int f = 2; f <= 4; f++)
            {
                int g = f - 1;
                // 선생님을 피해 숨을 수 있도록 2~4층 교실은 모두 열려 있음 (청소함·불 스위치도 전부 있음)
                // 잠그고 싶은 반이 있으면 그 자리를 false 로 (1반 ~ 7반 순서)
                bool[] open = { true, true, true, true, true, true, true };

                for (int i = 0; i < 7; i++)
                {
                    int n = i + 1;
                    float x0, z0, x1, z1;
                    if (i < 2) { x0 = XS[i]; x1 = XS[i + 1]; z0 = CE; z1 = WE; }
                    else if (i < 4) { x0 = XS[i + 1]; x1 = XS[i + 2]; z0 = CE; z1 = WE; }
                    else { x0 = RC; x1 = E; z0 = VR[i - 4]; z1 = VR[i - 3]; }
                    RoomSpec c = R(f + "F_" + g + "-" + n, f, g + "학년 " + n + "반", x0, z0, x1, z1);
                    c.mapNode = "Class_" + g + "학년" + n + "반";
                    c.hideSpot = open[i];
                    c.lights = open[i];
                    // 교실 문은 맵에 이미 있는 미닫이문을 써요 (앞문/뒷문). id 뒤에 _F / _B
                    DoorLock l = open[i] ? DoorLock.Open : DoorLock.Locked;
                    c.doors.Add(new DoorSpec { id = c.id + "_F", lockType = l, lockedMsg = LockedClass });
                    c.doors.Add(new DoorSpec { id = c.id + "_B", lockType = l, lockedMsg = LockedClass });
                    rooms.Add(c);
                }

                RoomSpec staff = R(f + "F_staff", f, g + "학년 교무실", C0, WE, C1, 27.51f);
                DoorSpec sd = D(f + "F_staff", Side.E, (WE + 27.51f) * 0.5f, StaffW);
                if (g == 2) { sd.lockType = DoorLock.Key; sd.key = "key_staff2"; sd.lockedMsg = "2학년 교무실 문이 잠겨 있다."; }
                if (g == 3) { sd.lockType = DoorLock.Code; sd.code = "0215"; sd.lockedMsg = "교무실 문에 네 자리 번호 자물쇠가 걸려 있다."; }
                staff.doors.Add(sd);
                rooms.Add(staff);

                RoomSpec sp;
                if (f == 2)
                {
                    sp = R("2F_science", 2, "과학실", C0, 27.51f, C1, BOT);
                    sp.doors.Add(D("2F_science", Side.E, (27.51f + BOT) * 0.5f, ClassW, DoorLock.Key, "key_science", "과학실 문이 잠겨 있다."));
                }
                else if (f == 3)
                {
                    sp = R("3F_nurse", 3, "양호실", C0, 27.51f, C1, BOT);
                    sp.doors.Add(D("3F_nurse", Side.E, (27.51f + BOT) * 0.5f, WoodW, DoorLock.Key, "key_nurse", "양호실 문이 잠겨 있다."));
                }
                else
                {
                    sp = R("4F_broadcast", 4, "방송실", C0, 27.51f, C1, BOT);
                    sp.doors.Add(D("4F_broadcast", Side.E, (27.51f + BOT) * 0.5f));
                }
                rooms.Add(sp);
            }

            // ------------------------------------------------ 5층
            // 문 자리는 맵의 문 구멍과 같아야 해요
            float[] X5 = MapSpace.X5;
            RoomSpec comp = R("5F_computer", 5, "컴퓨터실", X5[0], CE, X5[1], WE);
            comp.doors.Add(D("5F_computer_a", Side.N, 3.0f, ClassW)); comp.doors.Add(D("5F_computer_b", Side.N, 17.7f, ClassW));
            rooms.Add(comp);
            RoomSpec art = R("5F_art", 5, "미술실", X5[1], CE, X5[2], WE);
            art.doors.Add(D("5F_art_a", Side.N, 24.5f, ClassW)); art.doors.Add(D("5F_art_b", Side.N, 43.3f, ClassW));
            rooms.Add(art);
            RoomSpec music = R("5F_music", 5, "음악실", X5[2], CE, X5[3], WE);
            music.doors.Add(D("5F_music_a", Side.N, 50.5f, ClassW)); music.doors.Add(D("5F_music_b", Side.N, 64.5f, ClassW));
            rooms.Add(music);
            RoomSpec hall = R("5F_hall", 5, "강당", C0, WE, E, BOT);
            hall.wallIn = 0.3f; hall.hideSpot = false; hall.lights = false;
            hall.doors.Add(D("5F_hall", Side.N, 82.0f, HallW, DoorLock.Special));
            rooms.Add(hall);
        }

        static float L() { return MapSpace.L; }
    }
}
