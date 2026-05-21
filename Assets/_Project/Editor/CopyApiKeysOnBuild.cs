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
        const string StreamingFolder = "Assets/StreamingAssets";
        const string TargetAssetPath = "Assets/StreamingAssets/api_keys.env";

        [MenuItem("Artti/Copy .env to StreamingAssets")]
        public static void CopyMenu()
        {
            if (CopyEnv())
                EditorUtility.DisplayDialog(".env 복사", "StreamingAssets/api_keys.env로 복사 완료", "확인");
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

            if (!AssetDatabase.IsValidFolder(StreamingFolder))
                AssetDatabase.CreateFolder("Assets", "StreamingAssets");

            var targetPath = Path.Combine(projectRoot, TargetAssetPath);
            File.Copy(envPath, targetPath, true);
            AssetDatabase.ImportAsset(TargetAssetPath);
            Debug.Log($"[CopyApiKeysOnBuild] .env → {TargetAssetPath} 복사 완료");
            return true;
        }
    }
}
