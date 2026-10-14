using System.Collections.Generic;
using UnityEngine;

namespace BlackShade
{
    // 받은 3D 모델(소품)을 게임 안에 놓는 도우미.
    //
    // 모델은 메뉴 Tools > Black Shade > 소품 모델 정리하기 (또는 '게임 씬 만들기')를 실행하면
    // Assets/BlackShade/Resources/Props/ 에 프리팹으로 만들어지고, 여기서 이름으로 불러 써요.
    // 프리팹이 없으면 null 을 돌려주므로, 부르는 쪽은 원래의 임시 상자를 그대로 쓰면 돼요.
    //
    // 모델마다 원래 크기 단위가 제각각이라, 놓을 때 항상 '원하는 크기 상자'에 맞춰 줄이거나 늘려요.
    public static class PropModels
    {
        public enum Fit
        {
            Inside,   // 비율 유지, 상자 안에 쏙 들어가게
            Height,   // 비율 유지, 높이만 맞춤
            Stretch   // (예전 옵션) 지금은 Inside 와 같음 — 모델은 항상 비율을 지켜요
        }

        // 모델의 '앞'이 프리팹에서 어느 쪽을 보고 있는지 (도 단위, +Z 기준).
        // 앞이 뒤로 놓이면 여기 숫자만 0 ↔ 180 으로 바꾸면 돼요.
        static readonly Dictionary<string, float> YawOffset = new Dictionary<string, float>
        {
            { "Sofa", 0f },
            { "FoldingChair", 180f },
            { "Cabinet", 0f },
            { "Computer", 0f },
            { "Microphone", 0f },
            { "MusicStand", 180f },
            { "LightSwitch", 180f },
            { "Padlock", 0f },
            { "StatueHera", 0f },
            { "StatueAugustus", 0f },
            { "GrandPiano", 0f },        // 건반·의자 쪽이 앞
            { "UprightPiano", 0f },      // 건반 쪽이 앞
            { "WoodDoor", 0f },
            { "Cubicle", 0f },
            { "StageCurtain", 0f },
            { "ChocoBar", 0f },
            { "CorkBoard", 0f },
            { "DrinkCan", 0f },
            { "OfficeDesk", 0f },        // 서랍 손잡이 쪽(앉는 쪽)이 앞
            { "OnAirSign", 0f },         // 글씨가 읽히는 쪽이 앞
            { "ServerRack", 90f },       // 옆으로 빠지는 콘솔 쪽이 앞
            { "Skeleton", 0f },
            { "TapeDeck", 0f },
            { "VendingMachine", 0f },
            { "Badge", 0f },             // 그림이 있는 쪽이 앞
            // ---- 2026-10-14
            { "ClassDoorFrame", 90f }, { "ClassDoorLeaf", 90f }, { "HallDoor", 90f },
            { "LabTable", 0f }, { "LabFrontDesk", 0f }, { "LabStool", 0f }, { "SciChair", 0f },   // 의자: 앉는 쪽이 앞
            { "ArtTableSet", 0f },
            { "PcTeacherSet", -90f },    // 의자에 앉은 사람이 보는 쪽이 앞
            { "PcDesk", 0f },            // 앉는 쪽이 앞
            { "Lectern", 90f },          // 서랍(선생님) 쪽이 앞
            { "StoreShelf", 90f }, { "SoundPanel", 180f }, { "Toilet", 0f }, { "SteelLocker", 0f },
            { "KeyBox", 0f }, { "Locker", 90f }, { "Easel", 90f },   // 이젤: 그림 쪽이 앞
            { "PaperSheet", 0f }, { "Clipboard", 0f }, { "Flasks", 0f }, { "Medkit", 0f }, { "Pen", 0f }, { "FountainPen", 0f },
            { "ExitSign", 0f }, { "Hydrant", 0f }, { "BandageBox", 0f },
            { "RetroDesk", 90f }, { "RetroChair", -90f }, { "RetroFiling", 90f }, { "RetroCooler", 90f },
            // ---- 2026-10-14 (2)
            { "PlasticBench", 180f },    // 앉는 쪽이 앞
            { "MedShelf", 90f },         // 칸이 열린 쪽이 앞
            { "ReagentCabinet", 0f },    // 유리문 쪽이 앞
        };

        // 그림자를 만들지 않는 모델 (면이 아주 많아서 조명 그림자 계산이 무거운 것)
        static readonly HashSet<string> NoShadow = new HashSet<string> { "LabStool", "Hydrant", "Pen", "Flasks", "SoundPanel", "Toilet", "Medkit", "ReagentCabinet" };

        // ================================================================ ★ 직접 고치는 보정표 ★
        // (지금은 모델 원래 회전을 지켜서 놓기 때문에 대부분 0 그대로 두면 돼요)
        // 그래도 모델이 누워 있거나 기울어져 있으면 X / Z 를 90, -90, 180 중에서 바꿔 보세요.
        //   X: 앞뒤로 넘어짐 보정 (눕혀진 모델을 세울 때)
        //   Y: 좌우로 돌림 (앞뒤가 바뀌었으면 위 YawOffset 표에서 0 ↔ 180 이 더 쉬워요)
        //   Z: 옆으로 넘어짐 보정
        //   크기: 1 = 그대로, 1.2 = 20% 크게, 0.8 = 20% 작게
        // 고친 뒤: 저장 → 'Tools > Black Shade > 게임 씬 만들기' 다시 실행 (Decor 에 있는 것들은 이래야 바뀜)
        public struct Tweak
        {
            public Vector3 rot; public float scale;
            public Tweak(float x, float y, float z, float scale) { rot = new Vector3(x, y, z); this.scale = scale; }
        }
        static readonly Dictionary<string, Tweak> Tweaks = new Dictionary<string, Tweak>
        {
            //             X각도  Y각도  Z각도  크기
            { "Sofa", new Tweak(0f, 0f, 0f, 1f) },
            { "FoldingChair", new Tweak(0f, 0f, 0f, 1f) },
            { "Cabinet", new Tweak(0f, 0f, 0f, 1f) },
            { "Computer", new Tweak(0f, 0f, 0f, 1f) },
            { "Microphone", new Tweak(0f, 0f, 0f, 1f) },
            { "MusicStand", new Tweak(0f, 0f, 0f, 1f) },
            { "LightSwitch", new Tweak(0f, 0f, 0f, 1f) },
            { "Padlock", new Tweak(0f, 0f, 0f, 1f) },
            { "Key", new Tweak(0f, 0f, 0f, 1f) },
            { "Battery", new Tweak(0f, 0f, 0f, 1f) },
            { "Balloon", new Tweak(0f, 0f, 0f, 1f) },
            { "Torch", new Tweak(0f, 0f, 0f, 1f) },
            { "StatueHera", new Tweak(0f, 0f, 0f, 1f) },
            { "StatueAugustus", new Tweak(0f, 0f, 0f, 1f) },
            { "Wall_Panel", new Tweak(0f, 0f, 0f, 1f) },
            { "Wall_Pillar", new Tweak(0f, 0f, 0f, 1f) },
            { "Gate", new Tweak(0f, 0f, 0f, 1f) },
            { "GrandPiano", new Tweak(0f, 0f, 0f, 1f) },
            { "UprightPiano", new Tweak(0f, 0f, 0f, 1f) },
            { "WoodDoor", new Tweak(0f, 0f, 0f, 1f) },
            { "Cubicle", new Tweak(0f, 0f, 0f, 1f) },
            { "StageCurtain", new Tweak(0f, 0f, 0f, 1f) },
            { "ChocoBar", new Tweak(0f, 0f, 0f, 1f) },
            { "CorkBoard", new Tweak(0f, 0f, 0f, 1f) },
            { "DrinkCan", new Tweak(0f, 0f, 0f, 1f) },
            { "OfficeDesk", new Tweak(0f, 0f, 0f, 1f) },
            { "OnAirSign", new Tweak(0f, 0f, 0f, 1f) },
            { "ServerRack", new Tweak(0f, 0f, 0f, 1f) },
            { "Skeleton", new Tweak(0f, 0f, 0f, 1f) },
            { "TapeDeck", new Tweak(0f, 0f, 0f, 1f) },
            { "VendingMachine", new Tweak(0f, 0f, 0f, 1f) },
            { "Badge", new Tweak(0f, 0f, 0f, 1f) },
            { "PaperSheet", new Tweak(-90f, 0f, 0f, 1f) },   // 세워진 종이를 눕힘 (글씨가 위로)
            { "Clipboard", new Tweak(-90f, 0f, 0f, 1f) },    // 클립보드를 눕힘
            { "Pen", new Tweak(0f, 0f, -25.4f, 1f) },        // 비스듬히 기울어진 펜을 바로
            { "RetroClock", new Tweak(90f, 0f, 0f, 1f) },    // 바닥에 누운 시계를 벽에 걸리게 세움
        };

        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        public static GameObject Prefab(string key)
        {
            GameObject g;
            if (cache.TryGetValue(key, out g)) return g;
            g = Resources.Load<GameObject>("Props/" + key);
            cache[key] = g;
            return g;
        }

        public static void ClearCache() { cache.Clear(); }

        public static bool Has(string key) { return Prefab(key) != null; }

        // feet: 바닥 가운데, facing: 모델 앞이 볼 방향, size: (폭, 높이, 깊이) — facing 기준
        public static GameObject Place(string key, Transform parent, Vector3 feet, Vector3 facing, Vector3 size, Fit fit = Fit.Inside, string name = null)
        {
            GameObject prefab = Prefab(key);
            if (prefab == null) return null;
            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-6f) facing = Vector3.forward;
            facing.Normalize();

            // 겉 상자(wrapper) 안에 모델을 넣는다.
            //   모델 프리팹의 맨 위 오브젝트는 원래 '세워 두는 회전'(예: X -90)을 가지고 있는 경우가 많아서
            //   그걸 덮어쓰면 모델이 눕거나 크기가 엉뚱해져요. 그래서 그 회전은 그대로 두고,
            //   위치·방향·크기는 바깥 겉 상자에서만 바꿔요. (크기는 항상 비율 유지)
            GameObject wrap = new GameObject(name ?? key);
            Transform t = wrap.transform;
            GameObject g = Object.Instantiate(prefab, t, false);
            g.name = key;
            float yo = 0f;
            YawOffset.TryGetValue(key, out yo);
            Tweak tw;
            if (!Tweaks.TryGetValue(key, out tw)) tw = new Tweak(0f, 0f, 0f, 1f);
            if (tw.rot != Vector3.zero)
                g.transform.localRotation = Quaternion.Euler(tw.rot) * g.transform.localRotation;
            if (NoShadow.Contains(key))
                foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Quaternion baseRot = Quaternion.Euler(0f, yo, 0f);

            // 1) 원래 크기 재기 (앞이 +Z 를 보게 돌려 놓고)
            t.position = Vector3.zero;
            t.rotation = baseRot;
            t.localScale = Vector3.one;
            Bounds b;
            if (!WorldBounds(wrap, out b)) { if (parent != null) t.SetParent(parent, true); t.position = feet; return wrap; }
            Vector3 s0 = b.size;
            s0.x = Mathf.Max(s0.x, 1e-4f); s0.y = Mathf.Max(s0.y, 1e-4f); s0.z = Mathf.Max(s0.z, 1e-4f);

            // 2) 크기 맞추기 — 언제나 비율 유지 (Stretch 도 Inside 처럼)
            float kx = size.x > 0f ? size.x / s0.x : float.MaxValue;
            float ky = size.y > 0f ? size.y / s0.y : float.MaxValue;
            float kz = size.z > 0f ? size.z / s0.z : float.MaxValue;
            float u = fit == Fit.Height && ky != float.MaxValue ? ky : Mathf.Min(kx, Mathf.Min(ky, kz));
            if (u == float.MaxValue) u = 1f;
            t.localScale = Vector3.one * u * (tw.scale > 0f ? tw.scale : 1f);

            // 3) 방향 맞추고 바닥 가운데를 feet 에
            t.rotation = Quaternion.LookRotation(facing, Vector3.up) * baseRot;
            if (WorldBounds(wrap, out b))
                t.position += new Vector3(feet.x - b.center.x, feet.y - b.min.y, feet.z - b.center.z);
            else t.position = feet;
            if (parent != null) t.SetParent(parent, true);
            return wrap;
        }

        // 이미 놓은 모델을 제자리에서 더 돌린 뒤(눕히기·기울이기) 다시 바닥 가운데를 feet 에 맞춤.
        //   rot 은 월드 기준 회전 (예: Quaternion.AngleAxis(90, 옆방향) → 앞으로 눕힘)
        public static void Turn(GameObject wrap, Quaternion rot, Vector3 feet)
        {
            if (wrap == null) return;
            Transform t = wrap.transform;
            t.rotation = rot * t.rotation;
            Bounds b;
            if (WorldBounds(wrap, out b))
                t.position += new Vector3(feet.x - b.center.x, feet.y - b.min.y, feet.z - b.center.z);
        }

        // 임시 상자(Prop.Box)를 모델로 바꿔 보이게 한다. 상자의 충돌체/상호작용은 그대로 두고 모습만 숨김.
        public static GameObject Dress(GameObject box, string key, Fit fit = Fit.Inside, Vector3? facing = null)
        {
            if (box == null || !Has(key)) return null;
            Transform bt = box.transform;
            Vector3 size = bt.lossyScale;
            Vector3 feet = bt.position - Vector3.up * size.y * 0.5f;
            GameObject m = Place(key, bt.parent, feet, facing ?? bt.forward, size, fit, key + "_Model");
            if (m == null) return null;
            Renderer r = box.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            return m;
        }

        public static bool WorldBounds(GameObject g, out Bounds b)
        {
            b = new Bounds();
            bool any = false;
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any;
        }

        // 모델 전체 색을 살짝 바꾸기 (풍선 색 등) — 재질 복사본을 만들어 칠함
        public static void Tint(GameObject g, Color c)
        {
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            {
                Material[] ms = r.materials;
                foreach (Material m in ms) if (m.HasProperty("_Color")) m.color = c;
                r.materials = ms;
            }
        }

        // 공 모양(임시 풍선) 안에 받은 풍선 모델을 넣고 공은 숨김.
        // 공의 크기가 바뀌면(바람 넣기) 모델도 같이 커져요. 모델이 없으면 null (공 그대로).
        public static GameObject BalloonInside(GameObject sphere, Color c)
        {
            if (sphere == null || !Has("Balloon")) return null;
            Transform s = sphere.transform;
            Vector3 size = s.lossyScale;
            float h = size.y * 1.3f;   // 매듭까지 포함한 높이
            GameObject m = Place("Balloon", null, s.position - Vector3.up * size.y * 0.5f, Vector3.forward, new Vector3(0f, h, 0f), Fit.Height, "BalloonModel");
            if (m == null) return null;
            Bounds b;
            if (WorldBounds(m, out b)) m.transform.position += s.position - (b.center + Vector3.up * h * 0.1f);   // 몸통 가운데 = 공 가운데 (매듭은 아래로)
            m.transform.SetParent(s, true);
            Tint(m, c);
            Renderer r = sphere.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            return m;
        }

        // 풍선 하나 (실 포함): feet = 실이 묶인 바닥/책상 점, height = 풍선 가운데까지 높이
        public static GameObject BalloonOnString(Transform parent, Vector3 feet, float height, float size, Color c, string name = "Balloon")
        {
            GameObject g = new GameObject(name);
            if (parent != null) g.transform.SetParent(parent, false);
            Vector3 top = feet + Vector3.up * height;
            GameObject ball = Prop.Shape(PrimitiveType.Sphere, g.transform, "Ball", top, Quaternion.identity, new Vector3(size, size * 1.15f, size), c, false);
            BalloonInside(ball, c);
            float strLen = height - size * 0.6f;
            if (strLen > 0.05f)
                Prop.Shape(PrimitiveType.Cylinder, g.transform, "String", feet + Vector3.up * strLen * 0.5f, Quaternion.identity, new Vector3(0.004f, strLen * 0.5f, 0.004f), new Color(0.9f, 0.9f, 0.9f), false);
            return g;
        }

        // 모델에 맞는 상자 충돌체 하나 (괴물 길찾기 장애물 포함)
        public static void AddBoxCollider(GameObject g, bool navObstacle)
        {
            Bounds b;
            if (!WorldBounds(g, out b)) return;
            GameObject c = new GameObject("Collider");
            c.transform.SetParent(g.transform, false);
            c.transform.position = b.center;
            c.transform.rotation = Quaternion.identity;
            c.transform.localScale = Vector3.one;
            BoxCollider bc = c.AddComponent<BoxCollider>();
            bc.size = new Vector3(b.size.x / Mathf.Max(1e-4f, c.transform.lossyScale.x), b.size.y / Mathf.Max(1e-4f, c.transform.lossyScale.y), b.size.z / Mathf.Max(1e-4f, c.transform.lossyScale.z));
            if (navObstacle)
            {
                UnityEngine.AI.NavMeshObstacle ob = c.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                ob.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                ob.size = bc.size;
                ob.carving = true;
            }
        }
    }
}
