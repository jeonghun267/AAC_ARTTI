using UnityEngine;
using UnityEngine.SceneManagement;

namespace Artti.Common
{
    // 카메라가 하나도 없는 씬(Screen Space Overlay UI만 있는 씬)에 화면 지우기 카메라를 런타임에 붙인다.
    // 카메라가 없으면 안드로이드 기기에서 매 프레임 화면이 지워지지 않아, UI가 덮지 않은 곳에
    // 이전 씬의 마지막 프레임이 그대로 남는다(메인 위에 프로필 화면이 떠 보이던 원인).
    // 손배치가 있는 씬은 빌더를 다시 돌릴 수 없으므로 씬 파일 대신 여기서 막는다.
    public static class ClearCameraGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // Reload Domain을 끈 에디터에서는 정적 구독이 플레이 사이에 남으므로 중복 구독을 막는다.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Camera.allCamerasCount > 0) return;

            var go = new GameObject("[ClearCamera]");
            SceneManager.MoveGameObjectToScene(go, scene);   // 씬과 함께 사라지게
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;   // 아무것도 그리지 않고 화면만 지운다
            cam.orthographic = true;
            cam.depth = -100f;
            // 안드로이드 품질(Balanced)은 HDR이 켜져 있어, 그대로 두면 전체 해상도 HDR 중간 버퍼 + 블릿이 매 프레임 생긴다.
            cam.allowHDR = false;
            cam.allowMSAA = false;
        }
    }
}
