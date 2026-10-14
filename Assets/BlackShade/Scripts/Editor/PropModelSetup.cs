#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlackShade
{
    // 받은 소품 모델(Assets/BlackShade/Models/...)을 게임에서 바로 쓸 수 있게 정리하는 도구.
    //   - 모델 가져오기 설정 (애니메이션/카메라 끄기, 재질은 우리가 직접)
    //   - 텍스처로 재질 만들기 (노멀맵 설정 포함)
    //   - 필요한 부분만 남겨 Assets/BlackShade/Resources/Props/<이름>.prefab 으로 저장
    // 메뉴: Tools > Black Shade > 소품 모델 정리하기  ('게임 씬 만들기'도 이걸 먼저 실행해요)
    // 유니티를 열었을 때 모델은 있는데 프리팹이 아직 없으면 자동으로 한 번 정리
    [InitializeOnLoad]
    static class PropModelAutoSetup
    {
        static PropModelAutoSetup() { EditorApplication.delayCall += Check; }
        static void Check()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AssetDatabase.IsValidFolder("Assets/BlackShade/Models")) return;
            bool need = false;
            // (모델 폴더, 만들어질 프리팹) — 폴더는 있는데 프리팹이 없으면 새로 받은 모델
            string[,] probe = { { "Torch", "Torch" }, { "VendingMachine", "VendingMachine" }, { "ClassDoor", "ClassDoorLeaf" }, { "RetroOffice", "RetroDesk" }, { "BandageBox", "BandageBox" }, { "PlasticBench", "PlasticBench" }, { "MedShelf", "MedShelf" }, { "ReagentCabinet", "ReagentCabinet" } };
            for (int i = 0; i < probe.GetLength(0); i++)
                if (Directory.Exists("Assets/BlackShade/Models/" + probe[i, 0]) && !File.Exists("Assets/BlackShade/Resources/Props/" + probe[i, 1] + ".prefab")) need = true;
            if (!need) return;
            Debug.Log("[PropModelSetup] 새 소품 모델을 발견해서 프리팹으로 정리해요.");
            PropModelSetup.Run();
        }
    }

    public static class PropModelSetup
    {
        const string ModelRoot = "Assets/BlackShade/Models/";
        const string PrefabDir = "Assets/BlackShade/Resources/Props";

        class Rule
        {
            public string match = "";       // 오브젝트 이름에 들어 있으면 (| 로 여러 개), 빈 칸이면 나머지 전부
            public string tex, normal;      // 폴더 기준 파일 이름
            public Color color = Color.white;
            public float metallic, smooth = 0.3f;
            public bool emit;               // 그림이 스스로 빛남 (비상구 유도등)
        }

        class Entry
        {
            public string key, folder, file;
            public string[] keep;           // 이 이름의 오브젝트만 남김 (없으면 전부)
            public Rule[] rules;            // null 이면 모델의 원래 재질 그대로 (glb)
            public bool lit;                // glb 재질을 빛을 받는 Standard 재질로 바꿈 (빛을 안 받는 재질이라 어둠 속에서 혼자 밝게 보일 때)
            public bool native;             // fbx 안에 든 재질 색을 그대로 씀 (재질이 여러 개인 fbx)
        }

        static Rule R(string match, string tex, string normal, float metallic = 0f, float smooth = 0.3f, Color? c = null)
        { return new Rule { match = match, tex = tex, normal = normal, metallic = metallic, smooth = smooth, color = c ?? Color.white }; }
        static Rule C(string match, Color c, float metallic = 0f, float smooth = 0.3f)
        { return new Rule { match = match, color = c, metallic = metallic, smooth = smooth }; }
        static Rule RE(string match, string tex, float smooth = 0.5f)
        { return new Rule { match = match, tex = tex, smooth = smooth, color = Color.white, emit = true }; }

        // 90년대 사무용품 묶음에서 쓰는 것만 골라 낸 파일 (옛날 컴퓨터는 빼고, 예전에 받은 컴퓨터를 씀)
        static Entry Retro(string key, params string[] parts) { return new Entry { key = key, folder = "RetroOffice", file = "RetroOffice.glb", keep = parts }; }

        static readonly Entry[] Entries =
        {
            new Entry { key = "Sofa", folder = "Sofa", file = "Sofa.fbx", rules = new[] { R("", "Sofa_BaseColor.jpg", "Sofa_Normal.png", 0f, 0.35f) } },
            new Entry { key = "FoldingChair", folder = "FoldingChair", file = "FoldingChair.fbx", rules = new[] { R("", "FoldingChair_BaseColor.jpg", "FoldingChair_Normal.png", 0.6f, 0.35f) } },
            new Entry { key = "Cabinet", folder = "Cabinet", file = "Cabinet.fbx", keep = new[] { "shkaf", "door" }, rules = new[] { R("", "Cabinet_BaseColor.jpg", "Cabinet_Normal.png", 0.5f, 0.3f) } },
            new Entry { key = "Computer", folder = "Computer", file = "Computer.fbx", rules = new[] {
                R("Box015", "Computer_Screen.jpg", null, 0f, 0.85f, new Color(0.45f, 0.45f, 0.45f)),
                C("Cylinder00", new Color(0.2f, 0.45f, 1f), 0f, 0.6f),
                C("Box018|Box016|Box005|Box003|Box002|Box017", new Color(0.72f, 0.72f, 0.74f), 0.8f, 0.55f),
                C("", new Color(0.07f, 0.07f, 0.08f), 0f, 0.45f) } },
            new Entry { key = "Microphone", folder = "Microphone", file = "Microphone.fbx", rules = new[] {
                C("wire", new Color(0.03f, 0.03f, 0.03f), 0f, 0.4f),
                C("microphone_filter|handler_for_filter", new Color(0.18f, 0.18f, 0.19f), 0.7f, 0.5f),
                C("", new Color(0.12f, 0.12f, 0.13f), 0.8f, 0.55f) } },
            new Entry { key = "MusicStand", folder = "MusicStand", file = "MusicStand.fbx", rules = new[] { R("", "MusicStand_BaseColor.jpg", null, 0.5f, 0.35f, new Color(0.7f, 0.7f, 0.7f)) } },
            new Entry { key = "LightSwitch", folder = "LightSwitch", file = "LightSwitch.fbx", rules = new[] {
                C("Lever", new Color(0.95f, 0.95f, 0.93f), 0f, 0.5f), C("", new Color(0.88f, 0.88f, 0.84f), 0f, 0.4f) } },
            new Entry { key = "Key", folder = "Key", file = "Key.fbx", rules = new[] {
                R("SM_KeyTag", "KeyTag_BaseColor.jpg", "KeyTag_Normal.png", 0f, 0.35f),
                R("SM_Ring", "Ring_BaseColor.jpg", "Ring_Normal.png", 0.9f, 0.6f),
                R("", "Key_BaseColor.jpg", "Key_Normal.png", 0.9f, 0.6f) } },
            new Entry { key = "Padlock", folder = "Padlock", file = "Padlock.fbx", rules = new[] {
                R("Padlock1", "Padlock_BaseColor.jpg", "Padlock_Normal.png", 0.8f, 0.5f),
                R("", "MetalPieces_BaseColor.jpg", "MetalPieces_Normal.png", 0.8f, 0.55f) } },
            new Entry { key = "Battery", folder = "Battery", file = "Battery.fbx", rules = new[] { R("", "Battery_BaseColor.png", null, 0.3f, 0.6f) } },
            new Entry { key = "Balloon", folder = "Balloon", file = "Balloon.fbx", rules = new[] { R("", "Balloon_BaseColor.jpg", "Balloon_Normal.png", 0f, 0.7f) } },
            new Entry { key = "Wall_Panel", folder = "Wall", file = "Wall.fbx", keep = new[] { "01_022", "011" }, rules = new[] {
                R("011", "WallFence_BaseColor.jpg", "WallFence_Normal.png", 0.6f, 0.3f),
                R("", "WallBase_BaseColor.jpg", "WallBase_Normal.png", 0f, 0.2f) } },
            new Entry { key = "Wall_Pillar", folder = "Wall", file = "Wall.fbx", keep = new[] { "02_022" }, rules = new[] { R("", "WallBase_BaseColor.jpg", "WallBase_Normal.png", 0f, 0.2f) } },
            new Entry { key = "Gate", folder = "Wall", file = "Wall.fbx", keep = new[] { "door" }, rules = new[] { R("", "Gate_BaseColor.jpg", "Gate_Normal.png", 0.7f, 0.35f) } },
            new Entry { key = "Torch", folder = "Torch", file = "Torch.fbx", rules = new[] { R("", "Torch_BaseColor.jpg", null, 0.1f, 0.45f) } },
            new Entry { key = "StatueHera", folder = "StatueHera", file = "StatueHera.glb" },
            new Entry { key = "GrandPiano", folder = "GrandPiano", file = "GrandPiano.glb" },
            new Entry { key = "UprightPiano", folder = "UprightPiano", file = "UprightPiano.glb" },
            new Entry { key = "WoodDoor", folder = "WoodDoor", file = "WoodDoor.glb" },
            new Entry { key = "Cubicle", folder = "Cubicle", file = "Cubicle.glb" },
            new Entry { key = "ChocoBar", folder = "ChocoBar", file = "ChocoBar.glb" },
            new Entry { key = "CorkBoard", folder = "CorkBoard", file = "CorkBoard.glb" },
            new Entry { key = "DrinkCan", folder = "DrinkCan", file = "DrinkCan.glb" },
            new Entry { key = "OfficeDesk", folder = "OfficeDesk", file = "OfficeDesk.glb" },
            new Entry { key = "OnAirSign", folder = "OnAirSign", file = "OnAirSign.glb" },
            new Entry { key = "ServerRack", folder = "ServerRack", file = "ServerRack.glb" },
            new Entry { key = "Skeleton", folder = "Skeleton", file = "Skeleton.glb" },
            new Entry { key = "TapeDeck", folder = "TapeDeck", file = "TapeDeck.glb" },
            new Entry { key = "VendingMachine", folder = "VendingMachine", file = "VendingMachine.glb" },
            new Entry { key = "StatueAugustus", folder = "StatueAugustus", file = "StatueAugustus.glb" },
            new Entry { key = "Badge", folder = "Badge", file = "Badge.glb" },             // 철쭉 명찰 장식 (받은 그림으로 만든 모델)
            // ---- 2026-10-14 받은 모델
            new Entry { key = "ClassDoorFrame", folder = "ClassDoor", file = "ClassDoor.fbx", keep = new[] { "Cube.007" }, rules = new[] { C("", new Color(0.3f, 0.31f, 0.33f), 0.5f, 0.4f) } },
            new Entry { key = "ClassDoorLeaf", folder = "ClassDoor", file = "ClassDoor.fbx", keep = new[] { "Cube.002", "Cube.003", "Cube.004", "Cube.005", "Cube.006", "Cube.013" }, rules = new[] {
                C("Cube.003|Cube.004|Cube.005|Cube.006", new Color(0.7f, 0.85f, 0.9f, 0.28f), 0f, 0.9f),
                C("Cube.013", new Color(0.75f, 0.75f, 0.76f), 0.9f, 0.6f),
                C("", new Color(0.46f, 0.32f, 0.2f), 0f, 0.35f) } },
            new Entry { key = "HallDoor", folder = "HallDoor", file = "HallDoor.fbx", rules = new[] {
                R("02", "HallFrame_BaseColor.png", null, 0.4f, 0.4f),
                R("", "HallDoor_BaseColor.png", "HallDoor_Normal.png", 0.2f, 0.45f) } },
            new Entry { key = "LabTable", folder = "LabTable", file = "LabTable.glb" },
            new Entry { key = "LabFrontDesk", folder = "LabFrontDesk", file = "LabFrontDesk.glb" },
            new Entry { key = "LabStool", folder = "LabStool", file = "LabStool.fbx", rules = new[] {
                C("Object06|Object07|Object08|Object09", new Color(0.05f, 0.05f, 0.05f), 0f, 0.4f),
                C("Object0|Cylinder05", new Color(0.72f, 0.72f, 0.74f), 0.9f, 0.7f),
                C("", new Color(0.07f, 0.07f, 0.08f), 0f, 0.45f) } },
            new Entry { key = "SciChair", folder = "SciChair", file = "SciChair.glb" },
            new Entry { key = "ArtTableSet", folder = "ArtTableSet", file = "ArtTableSet.glb" },
            new Entry { key = "PcTeacherSet", folder = "PcTeacherSet", file = "PcTeacherSet.glb" },
            new Entry { key = "PcDesk", folder = "PcDesk", file = "PcDesk.glb" },
            new Entry { key = "Lectern", folder = "Lectern", file = "Lectern.glb" },
            new Entry { key = "StoreShelf", folder = "StoreShelf", file = "StoreShelf.glb" },
            new Entry { key = "SoundPanel", folder = "SoundPanel", file = "SoundPanel.glb" },
            new Entry { key = "Toilet", folder = "Toilet", file = "Toilet.glb" },
            new Entry { key = "SteelLocker", folder = "SteelLocker", file = "SteelLocker.glb" },
            new Entry { key = "KeyBox", folder = "KeyBox", file = "KeyBox.fbx", rules = new[] {
                C("pCylinder|polySurface470|polySurface471", new Color(0.25f, 0.25f, 0.26f), 0.8f, 0.6f),
                R("", "KeyBox_BaseColor.jpg", null, 0.1f, 0.35f) } },
            new Entry { key = "Locker", folder = "Locker", file = "Locker.fbx", rules = new[] { R("", "Locker_BaseColor.jpg", "Locker_Normal.png", 0.5f, 0.35f) } },
            new Entry { key = "Easel", folder = "Easel", file = "Easel.fbx", native = true },
            new Entry { key = "PaperSheet", folder = "PaperSheet", file = "PaperSheet.glb" },
            new Entry { key = "Clipboard", folder = "Clipboard", file = "Clipboard.fbx", rules = new[] {
                R("Plane.005", "ClipPaper_BaseColor.jpg", null, 0f, 0.2f),
                R("", "Clipboard_BaseColor.jpg", null, 0.1f, 0.4f) } },
            new Entry { key = "Flasks", folder = "Flasks", file = "Flasks.fbx", rules = new[] {
                R("big", "FlaskBig_BaseColor.png", null, 0f, 0.9f, new Color(0.85f, 0.95f, 1f, 0.5f)),
                R("mid", "FlaskMid_BaseColor.png", null, 0f, 0.9f, new Color(0.85f, 1f, 0.9f, 0.5f)),
                R("", "FlaskSmall_BaseColor.png", null, 0f, 0.9f, new Color(0.95f, 0.95f, 1f, 0.5f)) } },
            new Entry { key = "Medkit", folder = "Medkit", file = "Medkit.glb" },
            new Entry { key = "Pen", folder = "Pen", file = "Pen.glb" },
            new Entry { key = "FountainPen", folder = "FountainPen", file = "FountainPen.fbx", rules = new[] { R("", "FountainPen_BaseColor.jpg", "FountainPen_Normal.png", 0.8f, 0.6f) } },
            new Entry { key = "ExitSign", folder = "ExitSign", file = "ExitSign.fbx", rules = new[] { RE("", "ExitSign_BaseColor.jpg", 0.5f) } },
            new Entry { key = "Hydrant", folder = "Hydrant", file = "Hydrant.fbx", native = true },
            new Entry { key = "BandageBox", folder = "BandageBox", file = "BandageBox.glb" },
            Retro("RetroDesk", "Desk.004", "Top_Drawer.003", "Bottom_Drawer.003"),
            Retro("RetroChair", "Chair 1.001"),
            Retro("RetroFiling", "Filing Cainet Base.015", "Filing Cainet Base.006", "Filing Cainet Base.007", "Filing Cainet Base.008"),
            Retro("RetroLamp", "White Lamp.002"),
            Retro("RetroCooler", "Cooler.003"),
            Retro("RetroBottle", "Bottle.002"),
            Retro("RetroBinder", "Binder.001"),
            Retro("RetroBinder2", "Binder 2.004"),
            Retro("RetroPapers", "paper.024"),
            Retro("RetroFileBox", "File Box 1.005", "Files.004", "File Box 1.001"),
            Retro("RetroTrash", "Trash can.003"),
            Retro("RetroPrinter", "Printer Base.003", "Printer Top.003"),
            Retro("RetroPhone", "Phone.002"),
            Retro("RetroMug", "Mug 1.001"),
            Retro("RetroMug2", "Mug 3.001"),
            Retro("RetroPenHolder", "Pen Holder.001"),
            Retro("RetroClock", "Clock.001", "Hour Arm.001", "Minute Arm.001", "Center Piece.001"),
            Retro("RetroPlant", "Pot.002"),
            Retro("RetroFan", "Fan Blades", "Fan Body", "Fan Head"),
            // ---- 2026-10-14 (2)
            new Entry { key = "PlasticBench", folder = "PlasticBench", file = "PlasticBench.fbx", rules = new[] {
                R("", "PlasticBench_BaseColor_" + BenchColor + ".jpg", "PlasticBench_Normal.png", 0f, 0.45f) } },   // 로비 벤치
            new Entry { key = "MedShelf", folder = "MedShelf", file = "MedShelf.glb" },                 // 양호실 약장 (받은 책장)
            new Entry { key = "ReagentCabinet", folder = "ReagentCabinet", file = "ReagentCabinet.glb" },   // 과학실 시약장 (유리문)
        };

        // ★ 로비 벤치 색 — 아래 중 하나로 바꾸고 'Tools > Black Shade > 소품 모델 정리하기' → '게임 씬 만들기' 다시 실행
        //   "Blue"(파랑) "Green"(원래 초록) "Orange"(주황) "Red"(빨강) "Yellow"(노랑) "Grey"(회색) "Navy"(남색) "White"(흰색)
        public const string BenchColor = "Blue";

        [MenuItem("Tools/Black Shade/소품 모델 정리하기", priority = 3)]
        static void Menu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Black Shade", "게임이 재생 중이에요.\n위쪽 ▶ 버튼을 눌러 재생을 멈춘 다음 다시 실행해 주세요.", "확인");
                return;
            }
            string report = Run();
            EditorUtility.DisplayDialog("소품 모델 정리하기", report, "확인");
        }

        // 결과 요약 문자열을 돌려줌
        public static string Run()
        {
            EnsureFolder(PrefabDir);
            List<string> ok = new List<string>(), missing = new List<string>();
            try
            {
                for (int i = 0; i < Entries.Length; i++)
                {
                    Entry e = Entries[i];
                    EditorUtility.DisplayProgressBar("소품 모델 정리하기", e.key, (float)i / Entries.Length);
                    if (Build(e)) ok.Add(e.key); else missing.Add(e.folder + "/" + e.file);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string s = "준비된 소품 " + ok.Count + "개: " + string.Join(", ", ok);
            if (missing.Count > 0) s += "\n\n못 찾은 파일 (임시 상자로 대신해요):\n- " + string.Join("\n- ", missing) + "\n\n(" + ModelRoot + " 안에 있어야 해요)";
            Debug.Log("[PropModelSetup] " + s);
            return s;
        }

        static bool Build(Entry e)
        {
            string path = ModelRoot + e.folder + "/" + e.file;
            if (!File.Exists(path)) path = FindModelFile(ModelRoot + e.folder, Path.GetExtension(e.file));   // 원본 이름 그대로 넣었을 때
            if (path == null || !File.Exists(path)) return false;

            // 1) 가져오기 설정
            ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi != null)
            {
                bool dirty = false;
                ModelImporterMaterialImportMode want = e.native ? ModelImporterMaterialImportMode.ImportViaMaterialDescription : ModelImporterMaterialImportMode.None;
                if (mi.materialImportMode != want) { mi.materialImportMode = want; dirty = true; }
                if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
                if (mi.animationType != ModelImporterAnimationType.None) { mi.animationType = ModelImporterAnimationType.None; dirty = true; }
                if (mi.importCameras) { mi.importCameras = false; dirty = true; }
                if (mi.importLights) { mi.importLights = false; dirty = true; }
                if (dirty) mi.SaveAndReimport();
            }
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return false;

            // 2) 재질
            List<Material> mats = new List<Material>();
            if (e.rules != null)
                for (int i = 0; i < e.rules.Length; i++) mats.Add(MakeMaterial(e, e.rules[i], i));

            // 3) 복사본을 만들어 다듬기
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (PrefabUtility.IsPartOfPrefabInstance(inst))
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.transform.position = Vector3.zero;
            GameObject root = inst;
            if (e.keep != null && e.keep.Length > 0)
            {
                root = new GameObject(e.key);
                foreach (string n in e.keep)
                {
                    Transform t = FindDeep(inst.transform, n);
                    if (t != null) t.SetParent(root.transform, true);
                    else Debug.LogWarning("[PropModelSetup] " + e.key + ": '" + n + "' 부분을 못 찾음");
                }
                Object.DestroyImmediate(inst);
            }
            root.name = e.key;
            foreach (Animator a in root.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (Animation a in root.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(a);
            if (e.rules == null && (e.lit || e.native)) ToLitMaterials(e, root);
            foreach (Camera c in root.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
            foreach (Light l in root.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            if (e.rules != null)
            {
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material m = Pick(e, mats, r.gameObject.name);
                    Material[] arr = r.sharedMaterials;
                    for (int i = 0; i < arr.Length; i++) arr[i] = m;
                    if (arr.Length == 0) arr = new[] { m };
                    r.sharedMaterials = arr;
                }
            }
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + e.key + ".prefab");
            Object.DestroyImmediate(root);
            return true;
        }

        // glb 의 재질(빛을 안 받는 것 등)을 Standard 재질로 바꿔 저장
        static void ToLitMaterials(Entry e, GameObject root)
        {
            string dir = ModelRoot + e.folder + "/Materials";
            EnsureFolder(dir);
            Dictionary<Material, Material> made = new Dictionary<Material, Material>();
            int n = 0;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] arr = r.sharedMaterials;
                for (int i = 0; i < arr.Length; i++)
                {
                    Material src = arr[i];
                    if (src == null) continue;
                    Material m;
                    if (!made.TryGetValue(src, out m))
                    {
                        string path = dir + "/" + e.key + "_lit" + (n++) + ".mat";
                        m = AssetDatabase.LoadAssetAtPath<Material>(path);
                        bool isNew = m == null;
                        if (isNew) m = new Material(Shader.Find("Standard"));
                        m.shader = Shader.Find("Standard");
                        Texture t = src.HasProperty("baseColorTexture") ? src.GetTexture("baseColorTexture") : (src.HasProperty("_MainTex") ? src.mainTexture : null);
                        Color c = src.HasProperty("baseColorFactor") ? src.GetColor("baseColorFactor") : (src.HasProperty("_Color") ? src.color : Color.white);
                        m.mainTexture = t;
                        m.color = c;
                        m.SetFloat("_Metallic", 0f);
                        m.SetFloat("_Glossiness", 0.15f);
                        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
                        made[src] = m;
                    }
                    arr[i] = m;
                }
                r.sharedMaterials = arr;
            }
        }

        static Material Pick(Entry e, List<Material> mats, string objName)
        {
            for (int i = 0; i < e.rules.Length; i++)
            {
                string m = e.rules[i].match;
                if (string.IsNullOrEmpty(m)) return mats[i];
                foreach (string part in m.Split('|'))
                    if (part.Length > 0 && objName.Contains(part)) return mats[i];
            }
            return mats[mats.Count - 1];
        }

        static Material MakeMaterial(Entry e, Rule r, int index)
        {
            string dir = ModelRoot + e.folder + "/Materials";
            EnsureFolder(dir);
            string matPath = dir + "/" + e.key + "_" + index + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool isNew = m == null;
            if (isNew) m = new Material(Shader.Find("Standard"));
            m.shader = Shader.Find("Standard");
            m.color = r.color;
            m.SetFloat("_Metallic", r.metallic);
            m.SetFloat("_Glossiness", r.smooth);
            m.mainTexture = string.IsNullOrEmpty(r.tex) ? null : LoadTex(TexPath(e, r.tex, false), false);
            Texture2D n = string.IsNullOrEmpty(r.normal) ? null : LoadTex(TexPath(e, r.normal, true), true);
            m.SetTexture("_BumpMap", n);
            if (n != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            if (r.color.a < 0.99f)
            {
                // 유리처럼 비치는 재질 (Fade)
                m.SetFloat("_Mode", 2f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
            }
            else
            {
                m.SetFloat("_Mode", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                m.SetInt("_ZWrite", 1);
                m.DisableKeyword("_ALPHABLEND_ON");
                m.renderQueue = -1;
            }
            if (r.emit)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", m.mainTexture);
                m.SetColor("_EmissionColor", Color.white * 0.9f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else m.DisableKeyword("_EMISSION");
            if (isNew) AssetDatabase.CreateAsset(m, matPath); else EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------------------------------------------------------- 원본 파일 그대로 넣었을 때 찾기
        // 폴더 이름만 맞으면, 안에 든 모델(.fbx/.glb)과 텍스처를 이름으로 짐작해서 찾아요 (하위 폴더 포함).
        static readonly string[] ModelExt = { ".fbx", ".glb", ".gltf", ".obj" };
        static readonly string[] ImageExt = { ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".exr" };
        static readonly string[] NormalWords = { "normal", "_nor", "nrm" };
        static readonly string[] OtherMapWords = { "rough", "metallic", "metalness", "height", "displace", "disp", "_ao", "ao_", "ambient", "occlusion", "alpha", "opacity", "spec", "gloss", "emiss", "mask", "internal_ground" };
        static readonly string[] BaseWords = { "basecolor", "base_color", "albedo", "diffuse", "diff", "color", "col" };

        static string FindModelFile(string dir, string preferExt)
        {
            if (!Directory.Exists(dir)) return null;
            string best = null;
            foreach (string f in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (System.Array.IndexOf(ModelExt, ext) < 0) continue;
                string p = f.Replace('\\', '/');
                if (ext == preferExt.ToLowerInvariant()) return p;
                if (best == null) best = p;
            }
            return best;
        }

        static string TexPath(Entry e, string file, bool normal)
        {
            string exact = ModelRoot + e.folder + "/" + file;
            if (File.Exists(exact)) return exact;
            string dir = ModelRoot + e.folder;
            if (!Directory.Exists(dir)) return exact;

            // 재질이 여러 개인 모델은 파일 이름 앞부분(Key, Ring, KeyTag ...)으로 구분
            List<string> tokens = new List<string>();
            foreach (Rule r in e.rules)
                foreach (string t in new[] { r.tex, r.normal })
                    if (!string.IsNullOrEmpty(t)) { string k = t.Split('_')[0].ToLowerInvariant(); if (!tokens.Contains(k)) tokens.Add(k); }
            string token = tokens.Count > 1 ? file.Split('_')[0].ToLowerInvariant() : null;

            string best = null; int bestScore = -1;
            foreach (string f in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                if (System.Array.IndexOf(ImageExt, Path.GetExtension(f).ToLowerInvariant()) < 0) continue;
                string n = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                if (f.Replace('\\', '/').Contains("/Materials/")) continue;
                if (token != null)
                {
                    if (!n.Contains(token)) continue;
                    bool other = false;   // 예: Key 를 찾는데 KeyTag 파일이면 건너뜀
                    foreach (string t in tokens) if (t != token && t.Contains(token) && n.Contains(t)) other = true;
                    if (other) continue;
                }
                bool isNormal = Has(n, NormalWords);
                int score;
                if (normal)
                {
                    if (!isNormal) continue;
                    score = 1 + (n.Contains("opengl") || n.EndsWith("_gl") || n.Contains("_gl_") ? 2 : 0) - (n.Contains("directx") || n.Contains("_dx") ? 1 : 0);
                }
                else
                {
                    if (isNormal || Has(n, OtherMapWords)) continue;
                    score = 1 + (Has(n, BaseWords) ? 2 : 0);
                }
                if (score > bestScore) { bestScore = score; best = f.Replace('\\', '/'); }
            }
            return best ?? exact;
        }

        static bool Has(string s, string[] words)
        {
            foreach (string w in words) if (s.Contains(w)) return true;
            return false;
        }

        static Texture2D LoadTex(string path, bool normal)
        {
            if (!File.Exists(path)) { Debug.LogWarning("[PropModelSetup] 텍스처 없음: " + path); return null; }
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                TextureImporterType want = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (ti.textureType != want || ti.maxTextureSize > 1024)
                {
                    ti.textureType = want;
                    ti.maxTextureSize = Mathf.Min(ti.maxTextureSize, 1024);
                    ti.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                Transform f = FindDeep(c, name);
                if (f != null) return f;
            }
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
