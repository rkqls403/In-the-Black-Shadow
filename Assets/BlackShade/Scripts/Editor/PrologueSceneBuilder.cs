#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackShade.EditorTools
{
    // 메뉴 하나로 게임 씬을 만들어 주는 도구.
    //   Tools > Black Shade > 게임 씬 만들기
    //
    // 하는 일
    //   1. 새 씬을 만들고 School_5F_Map 을 배치
    //   2. 맵에 충돌체 추가 (SchoolMapPostProcess) + 교실 문짝은 움직일 수 있게 Static 해제
    //   3. 'Game' 오브젝트를 만들고 친구 모델 3개를 자동으로 연결
    //   4. 선생님 괴물이 다닐 길(NavMesh)을 굽기
    //   5. Assets/BlackShade/Scenes/BlackShade.unity 로 저장하고 빌드 설정에 등록
    public static class PrologueSceneBuilder
    {
        const string ScenePath = "Assets/BlackShade/Scenes/BlackShade.unity";
        const string NewMapPath = "Assets/BlackShade/Map/School_5F_Map.glb";
        const string FullMapPath = "Assets/BlackShade/Map/School_5F_Map_Full.glb";   // 예전 백업 맵 (문 구멍 맞추기 전) — 지워도 됨
        const float FullMapWidth = 80f;                                               // 이보다 좁으면 예전에 줄였던 맵

        [MenuItem("Tools/Black Shade/게임 씬 만들기", priority = 0)]
        static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Black Shade", "게임이 재생 중이에요.\n위쪽 ▶ 버튼을 눌러 재생을 멈춘 다음 다시 실행해 주세요.", "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // 받은 소품 모델을 먼저 프리팹으로 정리 (없으면 건너뜀 → 임시 상자로 대신)
            PropModelSetup.Run();

            // 새 맵(Assets/BlackShade/Map/)을 먼저 쓰고, 없으면 프로젝트 어딘가의 같은 이름 맵을 씀
            // 프로젝트 안의 맵 파일들 중 '원래 크기(가로 88m)' 맵을 골라 씀 (예전에 줄인 맵이 남아 있어도 무시)
            string mapPath; bool onlyShrunk;
            GameObject map = PickMap(out mapPath, out onlyShrunk);
            GameObject minjae = FindModel("school_boy_8");
            GameObject dohyun = FindModel("school_boy_10");
            GameObject haeun = FindModel("Haeun", "model");

            List<string> missing = new List<string>();
            if (map == null) missing.Add("School_5F_Map.glb (맵)");
            if (minjae == null) missing.Add("school_boy_8.glb (민재)");
            if (dohyun == null) missing.Add("school_boy_10.glb (도현)");
            if (haeun == null) missing.Add("Haeun.glb 또는 model.glb (하은)");
            if (missing.Count > 0)
            {
                bool go = EditorUtility.DisplayDialog("게임 씬 만들기",
                    "다음 파일을 프로젝트에서 못 찾았어요:\n\n- " + string.Join("\n- ", missing) +
                    "\n\nglTFast 패키지를 설치했는지, 파일을 Assets 폴더에 넣었는지 확인해 주세요.\n" +
                    "그래도 계속하면 없는 것은 임시 도형으로 대신해요. (맵이 없으면 본편은 제대로 동작하지 않아요)", "계속", "취소");
                if (!go) return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (Camera c in Object.FindObjectsOfType<Camera>()) Object.DestroyImmediate(c.gameObject);

            if (map != null)
            {
                GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(map);
                m.name = "School_5F_Map";
                m.transform.position = Vector3.zero;
                m.transform.rotation = Quaternion.identity;
                SchoolMapPostProcess.Process(m);
                PrepareForGame(m);

                // 가구·천장 조명·소품 모델·담장을 씬에 실제 오브젝트로 넣기 (편집 화면에서도 보임)
                MapSpace.Calibrate();
                PropModels.ClearCache();
                GameObject decor = new GameObject(Decor.RootName);
                Decor.Build(decor.transform);
            }

            GameObject g = new GameObject("Game");
            g.AddComponent<GameBootstrap>();
            PrologueDirector dir = g.AddComponent<PrologueDirector>();
            dir.minjaeModel = minjae;
            dir.dohyunModel = dohyun;
            dir.haeunModel = haeun;
            g.AddComponent<MainDirector>();

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuild(ScenePath);

            if (map != null) Bake();
            EditorSceneManager.SaveScene(scene, ScenePath);

            Selection.activeGameObject = g;
            EditorGUIUtility.PingObject(g);
            EditorUtility.DisplayDialog("게임 씬 만들기",
                "완료! " + ScenePath + " 에 저장했어요.\n\n쓴 맵: " + (mapPath ?? "(없음)") +
                (onlyShrunk ? "\n\n※ 주의: 이 맵은 예전에 줄였던 맵이에요! 가구·문 위치가 어긋나요.\n" + NewMapPath + " 파일을 넣고 다시 실행하세요." : "") +
                "\n\n위쪽 ▶ 재생 버튼을 누르면 타이틀 화면부터 시작해요.\n" +
                "(타이틀에서 F9: 본편부터 바로 / 프롤로그 중 F8: 무서운 얘기로 건너뛰기)", "확인");
        }

        // 교실 미닫이문(window+door+board)은 움직여야 하니 Static을 끄고,
        // 나머지는 괴물 길찾기에 쓰도록 Navigation Static을 켠다.
        static void PrepareForGame(GameObject root)
        {
#pragma warning disable 618
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string path = PathOf(t);
                if (path.Contains("/window+door+board/"))
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                    continue;
                }
                if (t.GetComponent<MeshFilter>() == null) continue;
                if (path.Contains("/light/")) continue;
                StaticEditorFlags f = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, f | StaticEditorFlags.NavigationStatic);
            }
#pragma warning restore 618
        }

        [MenuItem("Tools/Black Shade/길찾기(NavMesh) 다시 굽기", priority = 2)]
        static void Bake()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Black Shade", "게임이 재생 중이에요.\n위쪽 ▶ 버튼을 눌러 재생을 멈춘 다음 다시 실행해 주세요.", "확인");
                return;
            }
#pragma warning disable 618
            // 괴물 크기: 교실 미닫이문(약 70cm) 사이로 지나갈 수 있게 얇게
            SerializedObject so = new SerializedObject(UnityEditor.AI.NavMeshBuilder.navMeshSettingsObject);
            Set(so, "m_BuildSettings.agentRadius", 0.25f);
            Set(so, "m_BuildSettings.agentHeight", 1.8f);
            Set(so, "m_BuildSettings.agentClimb", 0.3f);
            Set(so, "m_BuildSettings.agentSlope", 50f);
            so.ApplyModifiedProperties();
            EditorUtility.DisplayProgressBar("길찾기", "선생님이 다닐 길을 굽는 중이에요. 맵이 커서 시간이 좀 걸려요…", 0.5f);
            try { UnityEditor.AI.NavMeshBuilder.BuildNavMesh(); }
            finally { EditorUtility.ClearProgressBar(); }
#pragma warning restore 618
            Debug.Log("[Black Shade] NavMesh 굽기 완료");
        }

        static void Set(SerializedObject so, string prop, float v)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p != null) p.floatValue = v;
            else Debug.LogWarning("[Black Shade] NavMesh 설정 '" + prop + "'을 찾지 못했어요. Window > AI > Navigation 에서 Agent Radius를 0.25로 맞춰 주세요.");
        }

        [MenuItem("Tools/Black Shade/게임 씬 열기", priority = 1)]
        static void Open()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Black Shade", "게임이 재생 중이에요.\n위쪽 ▶ 버튼을 눌러 재생을 멈춘 다음 다시 실행해 주세요.", "확인");
                return;
            }
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("게임 씬 열기", "아직 씬이 없어요. 먼저 '게임 씬 만들기'를 실행하세요.", "확인");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Black Shade/저장 기록 지우기", priority = 20)]
        static void ClearSave()
        {
            SaveSystem.Delete();
            EditorUtility.DisplayDialog("저장 기록", "저장된 기록을 지웠어요.", "확인");
        }

        static string PathOf(Transform t)
        {
            string p = "/" + t.name + "/";
            while (t.parent != null) { t = t.parent; p = "/" + t.name + p; }
            return p;
        }

        // 맵 고르기: School_5F_Map(최신, 문 구멍 맞춤) → School_5F_Map_Full(예전 백업) → 프로젝트 안의 다른 School_5F_Map* 순서로 보고,
        // 1층 껍데기(Shell_1F) 가로가 80m 넘는(= 원래 크기) 첫 번째 맵을 씀.
        static GameObject PickMap(out string usedPath, out bool onlyShrunk)
        {
            List<string> paths = new List<string>();
            foreach (string p in new[] { NewMapPath, FullMapPath }) if (File.Exists(p)) paths.Add(p);
            foreach (string guid in AssetDatabase.FindAssets("School_5F_Map"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string ext = Path.GetExtension(p).ToLowerInvariant();
                if ((ext == ".glb" || ext == ".gltf") && !paths.Contains(p)) paths.Add(p);
            }
            GameObject fallback = null; string fbPath = null;
            foreach (string p in paths)
            {
                GameObject a = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (a == null) continue;
                float w = MapWidth(a);
                Debug.Log("[게임 씬 만들기] 맵 후보 " + p + " : 가로 " + w.ToString("0.0") + "m");
                if (w > FullMapWidth) { usedPath = p; onlyShrunk = false; return a; }
                if (fallback == null) { fallback = a; fbPath = p; }
            }
            usedPath = fbPath; onlyShrunk = fallback != null;
            return fallback;
        }

        static float MapWidth(GameObject asset)
        {
            GameObject t = (GameObject)Object.Instantiate(asset);
            Transform shell = FindChild(t.transform, "Shell_1F");
            Bounds b = new Bounds(); bool any = false;
            foreach (Renderer r in (shell != null ? shell : t.transform).GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            Object.DestroyImmediate(t);
            return any ? b.size.x : 0f;
        }

        static Transform FindChild(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { Transform f = FindChild(c, name); if (f != null) return f; }
            return null;
        }

        static GameObject FindModel(params string[] names)
        {
            foreach (string n in names)
            {
                foreach (string guid in AssetDatabase.FindAssets(n))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                    if (ext != ".glb" && ext != ".gltf" && ext != ".fbx") continue;
                    if (!string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), n, System.StringComparison.OrdinalIgnoreCase)) continue;
                    GameObject g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (g != null) return g;
                }
            }
            return null;
        }

        static void AddToBuild(string path)
        {
            List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == path);
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
