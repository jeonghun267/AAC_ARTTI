using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Artti.Editor
{
    public class CopyApiKeysOnBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        const string EnvFile = ".env";
        const string ResourcesFolder = "Assets/Resources";
        // Android의 StreamingAssets는 APK(jar) 내부라 File IO 불가 — Resources(TextAsset)로 포함
        const string TargetAssetPath = "Assets/Resources/api_keys.txt";
        const string LegacyStreamingPath = "Assets/StreamingAssets/api_keys.env";

        [MenuItem("Artti/Copy .env to Resources")]
        public static void CopyMenu()
        {
            if (CopyEnv())
                EditorUtility.DisplayDialog(".env 복사", "Resources/api_keys.txt로 복사 완료", "확인");
            else
                EditorUtility.DisplayDialog(".env 복사 실패", ".env 파일이 프로젝트 루트에 없습니다", "확인");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            CopyEnv();
        }

        static bool CopyEnv()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (projectRoot == null) return false;
            var envPath = Path.Combine(projectRoot, EnvFile);
            if (!File.Exists(envPath))
            {
                Debug.LogWarning($"[CopyApiKeysOnBuild] {envPath} not found — skipping. API calls in build will fail.");
                return false;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var targetPath = Path.Combine(projectRoot, TargetAssetPath);
            File.Copy(envPath, targetPath, true);
            AssetDatabase.ImportAsset(TargetAssetPath);

            // 구버전 StreamingAssets 사본 정리 (Android에서 못 읽는 경로)
            if (AssetDatabase.LoadAssetAtPath<Object>(LegacyStreamingPath) != null)
                AssetDatabase.DeleteAsset(LegacyStreamingPath);

            Debug.Log($"[CopyApiKeysOnBuild] .env → {TargetAssetPath} 복사 완료");
            return true;
        }
    }
}
