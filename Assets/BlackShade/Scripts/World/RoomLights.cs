using System.Collections.Generic;
using UnityEngine;

namespace BlackShade
{
    // 방 하나의 형광등 + 벽 스위치.
    // 불이 켜진 방 안에서는 손전등 없이도 조사할 수 있어요.
    public class RoomLights : MonoBehaviour
    {
        public static readonly List<RoomLights> All = new List<RoomLights>();
        // 원래 밝은 곳 (1층 로비처럼 깜빡이는 불이 있는 곳): 맵 좌표 사각형 + 층
        public static readonly List<KeyValuePair<int, Rect>> litZones = new List<KeyValuePair<int, Rect>>();

        public RoomSpec room;
        public bool On { get; private set; }
        readonly List<Light> lights = new List<Light>();
        readonly List<Renderer> tubes = new List<Renderer>();
        Renderer led;
        Interactable sw;

        public static void Init()
        {
            LightCheck.roomLit = IsLitAt;
            litMats.Clear();
            mapShadowsFixed = false;
            FixMapShadows();
        }

        // 형광등 관: 켜지면 원래(빛나는) 재질, 꺼지면 회색 관
        static Material deadTube;
        static readonly Dictionary<Renderer, Material[]> litMats = new Dictionary<Renderer, Material[]>();
        public static void SetTubeOff(Renderer r) { SetTube(r, false); }
        static void SetTube(Renderer r, bool on)
        {
            if (r == null) return;
            Material[] orig;
            if (!litMats.TryGetValue(r, out orig)) { orig = r.sharedMaterials; litMats[r] = orig; }
            if (deadTube == null)
            {
                deadTube = new Material(Shader.Find("Standard"));
                deadTube.color = new Color(0.6f, 0.6f, 0.58f);
                deadTube.SetFloat("_Glossiness", 0.6f);
            }
            if (on) r.sharedMaterials = orig;
            else
            {
                Material[] d = new Material[Mathf.Max(1, orig.Length)];
                for (int i = 0; i < d.Length; i++) d[i] = deadTube;
                r.sharedMaterials = d;
            }
            r.enabled = true;
        }

        public static bool IsLitAt(Vector3 p)
        {
            int f = MapSpace.FloorOf(p);
            Vector2 g = MapSpace.ToGL(p);
            for (int i = 0; i < litZones.Count; i++)
                if (litZones[i].Key == f && litZones[i].Value.Contains(g)) return true;
            for (int i = 0; i < All.Count; i++)
            {
                RoomLights r = All[i];
                if (r.On && r.room.floor == f && r.room.Contains(g.x, g.y)) return true;
            }
            return false;
        }

        public static RoomLights For(RoomSpec room)
        {
            foreach (RoomLights r in All) if (r.room == room) return r;
            return null;
        }

        public static RoomLights Create(RoomSpec room, ModuleFrame mf)
        {
            GameObject go = new GameObject("Lights_" + room.id);
            RoomLights rl = go.AddComponent<RoomLights>();
            rl.room = room;
            All.Add(rl);

            List<Vector3> spots = new List<Vector3>();
            Vector3 switchPos, switchNormal;
            if (mf != null)
            {
                // 맵 교실의 천장 형광등 9개(3×3) 바로 아래마다 빛
                foreach (float u in ClassroomFrame.Layout.LightU)
                    foreach (float v in ClassroomFrame.Layout.LightV)
                        spots.Add(mf.P(u, v, LampH));
                switchPos = mf.P(2.35f, 0.16f, 1.3f);
                switchNormal = mf.ToWindow;
                foreach (Renderer r in mf.root.GetComponentsInChildren<Renderer>(true))
                    if (ClassroomFrame.PathOf(r.transform).Contains("/light/emissive/")) rl.tubes.Add(r);
            }
            else
            {
                // Decor 가 달아 둔 천장 형광등 모양(SpotsGL)과 같은 자리마다 빛
                foreach (Vector2 s in SpotsGL(room)) spots.Add(MapSpace.GF(room.floor, s.x, LampH, s.y));
                // 천장 형광등 모양(Decor 에 미리 달려 있음)의 관을 이 방 스위치에 연결
                Transform grp = CeilingFixtures.RoomGroup(room.id);
                if (grp != null)
                    foreach (Renderer r in grp.GetComponentsInChildren<Renderer>(true))
                        if (CeilingFixtures.IsTubeObject(r)) rl.tubes.Add(r);
                DoorSpec ds = room.doors.Count > 0 ? room.doors[0] : null;
                SwitchFor(room, ds, out switchPos, out switchNormal);
            }

            foreach (Vector3 s in spots)
            {
                GameObject l = new GameObject("Lamp");
                l.transform.SetParent(go.transform, false);
                l.transform.position = s;
                Light li = l.AddComponent<Light>();
                li.type = LightType.Point;
                li.range = mf != null ? 5f : 5.5f;
                li.intensity = mf != null ? 0.7f : 0.8f;
                li.color = new Color(1f, 0.96f, 0.9f);
                MakeRoomBound(li);
                li.enabled = false;
                rl.lights.Add(li);
            }

            // 스위치 (어둠 속에서도 찾을 수 있게 작은 빨간 불이 들어와 있음)
            Quaternion rot = Quaternion.LookRotation(switchNormal, Vector3.up);
            GameObject plate = Prop.Box(go.transform, "Switch", switchPos, rot, new Vector3(0.09f, 0.13f, 0.02f), new Color(0.9f, 0.9f, 0.86f), false);
            PropModels.Dress(plate, "LightSwitch", PropModels.Fit.Height);   // 모델이 있으면 모습만 교체 (LED 는 그대로)
            GameObject ledGo = Prop.Box(plate.transform, "Led", switchPos + switchNormal * (PropModels.Has("LightSwitch") ? 0.024f : 0.012f) + Vector3.up * 0.04f, rot, new Vector3(0.012f, 0.012f, 0.01f), Color.black, false);
            rl.led = ledGo.GetComponent<Renderer>();
            Prop.SetEmission(rl.led, new Color(1f, 0.15f, 0.1f) * 1.2f);

            rl.sw = plate.AddComponent<Interactable>();
            rl.sw.oneShot = false;
            rl.sw.range = 2f;
            rl.sw.dynamicPrompt = () => rl.On ? "불 끄기" : "불 켜기";
            rl.sw.onUse = () =>
            {
                AudioDirector.I.PlayAt(AudioDirector.I.click, switchPos, 0.6f, 0.8f);
                rl.Set(!rl.On);
            };
            rl.Set(false);
            return rl;
        }

        // 일반 방 천장 조명 자리 (맵 좌표) — 약 5m 간격 격자
        public static List<Vector2> SpotsGL(RoomSpec room)
        {
            List<Vector2> list = new List<Vector2>();
            float w = room.x1 - room.x0, d = room.z1 - room.z0;
            int nx = Mathf.Max(1, Mathf.RoundToInt(w / 5f)), nz = Mathf.Max(1, Mathf.RoundToInt(d / 5f));
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    list.Add(new Vector2(room.x0 + w * (i + 0.5f) / nx, room.z0 + d * (j + 0.5f) / nz));
            return list;
        }

        public const float LampH = 2.72f;   // 램프 높이 (형광등 바로 아래)

        // 벽·바닥에 막히도록 그림자를 켬 (방 불을 켜면 그 방만 밝아짐). 그림자는 가볍게(낮은 해상도).
        public static void MakeRoomBound(Light li)
        {
            li.shadows = LightShadows.Hard;
            li.shadowStrength = 1f;
            li.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low;
            li.shadowBias = 0.03f;
            li.shadowNormalBias = 0.3f;
            li.shadowNearPlane = 0.1f;
            li.renderMode = LightRenderMode.ForcePixel;
        }

        // 맵 벽이 양면으로 그림자를 만들게 (한쪽 면만 있는 벽도 빛을 막도록) — 한 번만
        static bool mapShadowsFixed;
        public static void FixMapShadows()
        {
            if (mapShadowsFixed) return;
            GameObject map = GameObject.Find("School_5F_Map");
            if (map == null) return;
            mapShadowsFixed = true;
            foreach (Renderer r in map.GetComponentsInChildren<Renderer>(true))
                if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On)
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        }

        // 일반 방: 첫 번째 문 옆 안쪽 벽에 스위치
        static void SwitchFor(RoomSpec room, DoorSpec ds, out Vector3 pos, out Vector3 normal)
        {
            float t = room.wallIn;
            if (ds == null)
            {
                pos = MapSpace.GF(room.floor, room.x0 + 0.5f, 1.3f, room.z0 + t + 0.02f);
                normal = MapSpace.Dir(0, 1);
                return;
            }
            float off = ds.center + ds.width * 0.5f + 0.3f;
            switch (ds.side)
            {
                case Side.N: pos = MapSpace.GF(room.floor, off, 1.3f, room.z0 + t + 0.012f); normal = MapSpace.Dir(0, 1); break;
                case Side.S: pos = MapSpace.GF(room.floor, off, 1.3f, room.z1 - t - 0.012f); normal = MapSpace.Dir(0, -1); break;
                case Side.W: pos = MapSpace.GF(room.floor, room.x0 + t + 0.012f, 1.3f, off); normal = MapSpace.Dir(1, 0); break;
                default: pos = MapSpace.GF(room.floor, room.x1 - t - 0.012f, 1.3f, off); normal = MapSpace.Dir(-1, 0); break;
            }
        }

        public void Set(bool on)
        {
            On = on;
            foreach (Light l in lights) if (l != null) l.enabled = on;
            foreach (Renderer r in tubes) SetTube(r, on);
            if (led != null) Prop.SetEmission(led, on ? new Color(0.2f, 1f, 0.3f) * 0.6f : new Color(1f, 0.15f, 0.1f) * 1.2f);
        }

        // 괴물이 지나가며 불을 깜빡이게 할 때
        public System.Collections.IEnumerator Flicker(int times)
        {
            bool was = On;
            for (int i = 0; i < times; i++)
            {
                Set(!was); yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
                Set(was); yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
            }
        }
    }

    // 손전등. [F] 로 켜고 끔. 켜져 있으면 배터리가 닳는다.
    public class Flashlight : MonoBehaviour
    {
        public static Flashlight I { get; private set; }
        public float secondsPerFullBattery = 480f;
        public bool On { get; private set; }
        Light spot;
        float flick;
        GameObject held;   // 손에 든 손전등 모델 (있을 때만)
        public static readonly Vector3 HeldPos = new Vector3(0.27f, -0.185f, 0.40f);   // 카메라 기준 (오른쪽, 아래, 앞) — 화면 오른쪽 아래 구석
        public static readonly Vector3 HeldRot = new Vector3(-3f, -9f, 0f);
        public const int HeldLayer = 29;                                               // 손에 든 물건 전용 레이어

        public static Flashlight Create(Camera cam)
        {
            GameObject go = new GameObject("Flashlight");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0.18f, -0.15f, 0.05f);
            Flashlight f = go.AddComponent<Flashlight>();
            f.spot = go.AddComponent<Light>();
            f.spot.type = LightType.Spot;
            f.spot.range = 11f;          // 너무 멀리까지 비추면 복도 전체가 밝아져서 줄임
            f.spot.spotAngle = 50f;
            f.spot.innerSpotAngle = 20f;
            f.spot.intensity = 1.6f;
            f.spot.color = new Color(1f, 0.95f, 0.85f);
            f.spot.shadows = LightShadows.Soft;
            // 빛이 많을 때 손전등이 '중요하지 않은 빛'으로 밀려나면 유니티가 물체 전체를 고르게 밝혀 버려서
            // (맵 전체가 갑자기 밝아지는 버그) 항상 제대로 비추게 고정
            f.spot.renderMode = LightRenderMode.ForcePixel;
            f.spot.shadowStrength = 0.9f;
            f.spot.enabled = false;
            // 손전등 모델: 화면 오른쪽 아래에 들고 있는 모습 (그림자는 안 만듦 → 불빛을 가리지 않음)
            //   위치를 바꾸고 싶으면 아래 HeldPos(오른쪽, 위아래, 앞뒤)와 HeldRot 을 고치세요.
            GameObject hold = new GameObject("HeldTorch");
            hold.transform.SetParent(cam.transform, false);
            hold.transform.localPosition = HeldPos;
            hold.transform.localRotation = Quaternion.Euler(HeldRot);
            GameObject tm = PropModels.Place("Torch", hold.transform, hold.transform.position, hold.transform.forward, new Vector3(0f, 0f, 0.2f), PropModels.Fit.Inside, "TorchModel");
            if (tm != null)
            {
                foreach (Renderer r in tm.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    r.gameObject.layer = HeldLayer;
                }
                EnsureHandFill(cam);
                f.held = hold;
                hold.SetActive(false);
            }
            else Object.Destroy(hold);
            I = f;
            LightCheck.flashlightOn = () => I != null && I.On && I.spot.enabled;
            return f;
        }

        // 손에 든 물건(손전등·캔)만 비추는 작은 불빛 — 어두운 곳에서도 살짝 보이게
        static Light handFill;
        public static void EnsureHandFill(Camera cam)
        {
            if (handFill != null || cam == null) return;
            GameObject fill = new GameObject("HandFill");
            fill.transform.SetParent(cam.transform, false);
            fill.transform.localPosition = new Vector3(0f, 0.05f, 0.1f);
            handFill = fill.AddComponent<Light>();
            handFill.type = LightType.Point;
            handFill.range = 1.0f;
            handFill.intensity = 0.6f;
            handFill.color = new Color(0.85f, 0.85f, 0.9f);
            handFill.cullingMask = 1 << HeldLayer;
            handFill.shadows = LightShadows.None;
            handFill.renderMode = LightRenderMode.ForcePixel;
        }

        public void Set(bool on)
        {
            if (on && GameState.Current.battery <= 0f) on = false;
            On = on;
            spot.enabled = on;
        }

        void Update()
        {
            GameState gs = GameState.Current;
            if (!UIState.ModalOpen && GameInput.FlashlightDown() && MainDirector.I != null && MainDirector.I.PlayerCanAct)
            {
                if (!gs.Has("flashlight")) { }
                else if (gs.battery <= 0f) { GameUI.I.Toast("배터리가 다 됐다."); AudioDirector.I.Play(AudioDirector.I.click, 0.4f, 0.7f); }
                else { Set(!On); AudioDirector.I.Play(AudioDirector.I.click, 0.5f); }
            }

            if (On)
            {
                gs.battery = Mathf.Max(0f, gs.battery - Time.deltaTime * 100f / secondsPerFullBattery);
                if (gs.battery <= 0f) { Set(false); GameUI.I.Toast("손전등이 꺼졌다. 배터리가 다 됐다."); }
                // 배터리가 얼마 안 남으면 깜빡임
                if (gs.battery < 15f)
                {
                    flick -= Time.deltaTime;
                    if (flick <= 0f) { flick = Random.Range(0.05f, 0.6f); spot.enabled = Random.value > 0.25f; }
                    spot.intensity = Mathf.Lerp(0.6f, 1.6f, gs.battery / 15f);
                }
                else { spot.enabled = true; spot.intensity = 1.6f; }
            }
            if (GameUI.I != null) GameUI.I.SetBattery(gs.battery / 100f, gs.Has("flashlight"), On);
            if (held != null)
            {
                bool show = gs.Has("flashlight") && MainDirector.I != null && MainDirector.I.PlayerCanAct && !HideSpot.PlayerHidden;
                if (held.activeSelf != show) held.SetActive(show);
            }
        }
    }
}
