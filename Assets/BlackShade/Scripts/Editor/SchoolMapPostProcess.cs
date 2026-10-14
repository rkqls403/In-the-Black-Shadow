#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// School_5F_Map.glb 를 유니티로 가져온 뒤 쓰는 후처리 도구.
// 사용법: Hierarchy에서 가져온 맵의 최상위 오브젝트(School_5F_Map)를 선택하고
//         메뉴 Tools > Black Shade > School Map > Add Colliders + Static 실행.
//
//  - 건물 껍데기(벽, 바닥, 천장, 창문 유리, 계단)와 교실/현관의 건축 부분: MeshCollider
//  - 책상, 의자, 사물함 같은 소품(Item, Items 그룹): BoxCollider (삼각형이 많아서 가벼운 상자로)
//  - 천장 조명(light 그룹): 충돌체 없음
//  - 전부 Static(배칭, 오클루전)으로 표시
public static class SchoolMapPostProcess
{
    [MenuItem("Tools/Black Shade/School Map/Add Colliders + Static (selected)")]
    static void Run()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null)
        {
            EditorUtility.DisplayDialog("School Map", "먼저 Hierarchy에서 맵의 최상위 오브젝트를 선택하세요.", "확인");
            return;
        }
        Process(root);
    }

    // 프롤로그 씬 만들기 도구에서도 이 함수를 불러 써요.
    public static void Process(GameObject root)
    {
        int meshCols = 0, boxCols = 0, skipped = 0;
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter mf = filters[i];
            if (mf.sharedMesh == null) continue;
            GameObject go = mf.gameObject;
            string path = GetPath(go.transform);

            EditorUtility.DisplayProgressBar("충돌체 추가 중", go.name, (float)i / filters.Length);

            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            if (path.Contains("/light/")) { skipped++; continue; }
            if (go.GetComponent<Collider>() != null) continue;

            if (path.Contains("/Item/") || path.Contains("/Items/"))
            {
                Undo.AddComponent<BoxCollider>(go);
                boxCols++;
            }
            else
            {
                MeshCollider mc = Undo.AddComponent<MeshCollider>(go);
                mc.sharedMesh = mf.sharedMesh;
                meshCols++;
            }
        }
        EditorUtility.ClearProgressBar();
        Debug.Log($"[SchoolMapPostProcess] MeshCollider {meshCols}개, BoxCollider {boxCols}개 추가, 조명 {skipped}개는 제외했어요.");
    }

    static string GetPath(Transform t)
    {
        string p = "/" + t.name + "/";
        while (t.parent != null)
        {
            t = t.parent;
            p = "/" + t.name + p;
        }
        return p;
    }
}
#endif
