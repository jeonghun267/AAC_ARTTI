using UnityEditor;
using UnityEngine;

namespace Artti.Editor
{
    public static class AllScenesBuilder
    {
        [MenuItem("Artti/Build All Scenes")]
        public static void BuildAll()
        {
            RegisterBuildSettings();

            if (!EditorUtility.DisplayDialog(
                "Build All Scenes",
                "7개 씬을 모두 자동 빌드합니다. 기존 GameObject가 삭제될 수 있습니다. 계속하시겠습니까?",
                "빌드", "취소"))
            {
                return;
            }

            try
            {
                SplashSceneBuilder.Build();
                ProfileCreateSceneBuilder.Build();
                ProfileSelectSceneBuilder.Build();
                MainSceneBuilder.Build();
                TrainingHubSceneBuilder.Build();
                TrainingSceneBuilder.Build(ScenePaths.TrainingPharmacy,   "pharmacy");
                ConvenienceTrainingSceneBuilder.Build();
                TrainingSceneBuilder.Build(ScenePaths.TrainingRestaurant,  "restaurant");
                // ARFieldScene은 손배치 씬(커밋이 원본) — 일괄 빌드에서 제외. 필요 시 개별 메뉴(확인 다이얼로그)로만

                Debug.Log("[AllScenesBuilder] 9개 씬 빌드 + Build Settings 등록 완료");
                EditorUtility.DisplayDialog("완료", "9개 씬 빌드 완료\nBuild Settings 등록됨 (0=Splash)", "확인");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AllScenesBuilder] 실패: {e}");
                EditorUtility.DisplayDialog("에러", $"빌드 중 에러:\n{e.Message}\n자세한 내용은 Console 확인", "확인");
            }
        }

        [MenuItem("Artti/Register Build Settings Scenes")]
        public static void RegisterBuildSettingsMenu() => RegisterBuildSettings();

        static void RegisterBuildSettings()
        {
            var scenes = new[]
            {
                ScenePaths.Splash,
                ScenePaths.ProfileCreate,
                ScenePaths.ProfileSelect,
                ScenePaths.Main,
                ScenePaths.TrainingHub,
                ScenePaths.TrainingPharmacy,
                ScenePaths.TrainingConvenience,
                ScenePaths.TrainingRestaurant,
                ScenePaths.ARField,
                ScenePaths.Report,
            };
            var list = new EditorBuildSettingsScene[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
                list[i] = new EditorBuildSettingsScene(scenes[i], true);
            EditorBuildSettings.scenes = list;
            Debug.Log("[AllScenesBuilder] Build Settings 등록 완료 (0=Splash)");
        }
    }
}
