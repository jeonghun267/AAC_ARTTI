using UnityEditor;
using UnityEngine;

namespace Artti.Editor
{
    // ReportScene이 안드로이드 빌드에서 "level9 is corrupted! [Position out of bounds!]"로
    // 크래시할 때 사용. .unity YAML이 현재 스크립트의 필드 레이아웃과 어긋난(stale) 상태를
    // 현재 타입트리 기준으로 강제 재직렬화해 바이너리 빌드와 일치시킨다. 손배치는 보존된다.
    public static class ReportSceneFix
    {
        const string ScenePath = "Assets/_Project/Scenes/ReportScene.unity";

        [MenuItem("Artti/Fix - Reserialize ReportScene")]
        public static void ReserializeReportScene()
        {
            AssetDatabase.ForceReserializeAssets(
                new[] { ScenePath },
                ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ReportSceneFix] Reserialized: {ScenePath} (현재 스크립트 레이아웃으로 재기록). 이제 다시 빌드하세요.");
        }
    }
}
