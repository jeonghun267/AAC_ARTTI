using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Artti.Editor
{
    public static class AndroidPlayerSettingsApplier
    {
        [MenuItem("Artti/Apply Android Player Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = "디지털융합제어과";
            PlayerSettings.productName = "아르띠";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.artti.aac");

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel30;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            // 가로(landscape) 고정 — 세로 회전 비허용 (PLAN v6: 폼팩터 가로 통일)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            Debug.Log("[AndroidPlayerSettings] 적용 완료");
            EditorUtility.DisplayDialog("Android Player Settings",
                "적용 완료\n\n" +
                "Company: 디지털융합제어과\n" +
                "Product: 아르띠\n" +
                "Bundle ID: com.artti.aac\n" +
                "Min SDK: 30 (Android 11)\n" +
                "Architecture: ARM64\n" +
                "Scripting: IL2CPP\n" +
                "Orientation: 가로(Landscape) 고정\n\n" +
                "⚠️ XR Plug-in Management에서 ARCore 활성화는 별도 (Project Settings → XR Plug-in)\n" +
                "⚠️ 마이크 권한은 첫 Microphone API 호출 시 자동 추가됨", "확인");
        }
    }
}
