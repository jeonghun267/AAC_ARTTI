using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Artti.Training;

// 편의점 카운터 물건 자동 연결.
// CounterDisplay.items(카드 id -> 물건 오브젝트) 매핑과 TrainingSceneRoot.counter 참조를
// 손 드래그 없이 메뉴 한 번으로 꽂는다. 씬을 직접 편집하지 않고 열려 있는 씬에서 동작하므로 충돌 없음.
public static class CounterDisplayWirer
{
    // 매핑: 씬 오브젝트 이름 -> 카드 id
    private const string SnackObject = "PY_R_2_1_9";  // 과자
    private const string WaterObject = "D_2_3_3_0";   // 물

    [MenuItem("Artti/Wire Convenience Counter Items")]
    public static void Wire()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[CounterWirer] Play 모드에서는 연결이 저장되지 않습니다. Play를 끄고 실행하세요.");
            return;
        }

        var counter = Object.FindObjectOfType<CounterDisplay>(true);
        if (counter == null)
        {
            Debug.LogError("[CounterWirer] 씬에 CounterDisplay 컴포넌트가 없습니다. 먼저 아무 오브젝트에 CounterDisplay를 추가하세요.");
            return;
        }

        var snack = FindInActiveScene(SnackObject);
        var water = FindInActiveScene(WaterObject);
        if (snack == null) { Debug.LogError($"[CounterWirer] 오브젝트 '{SnackObject}'(과자)를 씬에서 못 찾음 — 이름 확인."); return; }
        if (water == null) { Debug.LogError($"[CounterWirer] 오브젝트 '{WaterObject}'(물)를 씬에서 못 찾음 — 이름 확인."); return; }

        // CounterDisplay.items 채우기
        var so = new SerializedObject(counter);
        var items = so.FindProperty("items");
        items.arraySize = 2;
        SetEntry(items, 0, "card_cvs_snack", snack);
        SetEntry(items, 1, "card_cvs_water", water);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(counter);

        // 물건의 상위(부모) 체인이 꺼져 있으면 켜고, 물건 본인은 꺼둔다(런타임에 CounterDisplay가 켬).
        // 부모가 비활성이면 물건을 켜도 화면에 안 보이므로 필수.
        ActivateAncestors(snack);
        ActivateAncestors(water);
        snack.SetActive(false);
        water.SetActive(false);
        EditorUtility.SetDirty(snack);
        EditorUtility.SetDirty(water);

        // TrainingSceneRoot.counter 연결
        var root = Object.FindObjectOfType<TrainingSceneRoot>(true);
        if (root != null)
        {
            var soRoot = new SerializedObject(root);
            var counterProp = soRoot.FindProperty("counter");
            counterProp.objectReferenceValue = counter;
            soRoot.ApplyModifiedProperties();
            EditorUtility.SetDirty(root);
        }
        else
        {
            Debug.LogWarning("[CounterWirer] TrainingSceneRoot를 못 찾아 counter 참조는 수동 연결 필요.");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[CounterWirer] 완료 — 과자={snack.name}, 물={water.name}, TrainingSceneRoot.counter={(root != null ? "연결됨" : "미연결")}. Ctrl+S로 씬 저장하세요.");
    }

    // 물건의 상위 체인을 따라 올라가며 비활성 부모를 모두 활성화 (물건 본인은 제외)
    private static void ActivateAncestors(GameObject go)
    {
        var t = go.transform.parent;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(true);
                EditorUtility.SetDirty(t.gameObject);
                Debug.Log($"[CounterWirer] 상위 '{t.name}' 활성화 (물건이 보이도록)");
            }
            t = t.parent;
        }
    }

    private static void SetEntry(SerializedProperty list, int index, string cardId, GameObject go)
    {
        var e = list.GetArrayElementAtIndex(index);
        e.FindPropertyRelative("cardId").stringValue = cardId;
        e.FindPropertyRelative("item").objectReferenceValue = go;
    }

    // 비활성 오브젝트까지 포함해 활성 씬에서 이름으로 탐색
    private static GameObject FindInActiveScene(string name)
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindRecursive(root.transform, name);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    private static Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var r = FindRecursive(child, name);
            if (r != null) return r;
        }
        return null;
    }
}
