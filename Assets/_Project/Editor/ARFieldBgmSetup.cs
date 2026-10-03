using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Artti.ARField;

namespace Artti.Editor
{
    // AR 현장모드(ARFieldScene) 배경음 연결. ARFieldScene은 손배치 씬이라 다시 만들지 않고 필요한 것만 더한다.
    //   [ARFieldBgm] = AudioSource + ARFieldBgmPlayer(클립 Audio/ar_field_bgm.mp3, 볼륨 2/3)
    //   HomePanel / ResultPanel = BgmZone(켜져 있는 동안 배경음이 들림)
    //   변경점: ResultPanel '설명 듣기'(Listencard) 버튼 클릭에 ARFieldBgmPlayer.PauseForSpeech를 더한다(읽는 동안 멈춤, 기존 TTS 연결은 그대로)
    // 여러 번 실행해도 같은 결과가 되게(이미 있는 것은 이름/타입으로 다시 찾는다) 만들었다.
    public static class ARFieldBgmSetup
    {
        const string ClipPath = "Assets/_Project/Audio/ar_field_bgm.mp3";
        static readonly string[] ZonePanels = { "HomePanel", "ResultPanel" };
        const string ListenButtonName = "Listencard";

        [MenuItem("Artti/Fix/ARField - BGM on Home + Result Panels")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ARFieldBgmSetup] Play 모드에서는 실행하지 않는다."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePaths.ARField)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePaths.ARField, OpenSceneMode.Single);
            }

            var clip = LoadClip();
            if (clip == null) { Debug.LogError($"[ARFieldBgmSetup] 배경음 파일 없음: {ClipPath}"); return; }

            var player = Object.FindFirstObjectByType<ARFieldBgmPlayer>(FindObjectsInactive.Include);
            if (player == null)
            {
                var go = new GameObject("[ARFieldBgm]");
                SceneManagerMoveToScene(go, scene);
                go.AddComponent<AudioSource>();
                player = go.AddComponent<ARFieldBgmPlayer>();
            }
            var source = player.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;   // 2D(화면 어디서나 같은 크기)
            source.clip = clip;
            var tts = Object.FindFirstObjectByType<TTSManager>(FindObjectsInactive.Include);
            if (tts == null) Debug.LogWarning("[ARFieldBgmSetup] TTSManager 없음 - 설명 듣기 동안 멈춤이 바로 풀린다");
            var pso = new SerializedObject(player);
            pso.FindProperty("clip").objectReferenceValue = clip;
            pso.FindProperty("tts").objectReferenceValue = tts;
            pso.ApplyModifiedPropertiesWithoutUndo();

            int zones = 0;
            foreach (var name in ZonePanels)
            {
                var panel = FindPanel(scene, name);
                if (panel == null) { Debug.LogError($"[ARFieldBgmSetup] Canvas/{name} 없음"); continue; }
                var zone = panel.GetComponent<BgmZone>() ?? panel.gameObject.AddComponent<BgmZone>();
                var zso = new SerializedObject(zone);
                zso.FindProperty("player").objectReferenceValue = player;
                zso.ApplyModifiedPropertiesWithoutUndo();
                zones++;
            }

            bool listen = WireListenButton(scene, player);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ARFieldBgmSetup] 완료 - 배경음 연결, 들리는 패널 {zones}개({string.Join(", ", ZonePanels)})" +
                      (listen ? ", 설명 듣기 동안 멈춤" : ""));
        }

        // '설명 듣기' 버튼 클릭에 PauseForSpeech를 하나만 더한다(여러 번 실행해도 중복 없음)
        static bool WireListenButton(UnityEngine.SceneManagement.Scene scene, ARFieldBgmPlayer player)
        {
            var result = FindPanel(scene, "ResultPanel");
            Button button = null;
            if (result != null)
                foreach (var b in result.GetComponentsInChildren<Button>(true))
                    if (b.name == ListenButtonName) { button = b; break; }
            if (button == null) { Debug.LogError($"[ARFieldBgmSetup] ResultPanel 아래 {ListenButtonName} 버튼 없음"); return false; }
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentTarget(i) == player &&
                    button.onClick.GetPersistentMethodName(i) == nameof(ARFieldBgmPlayer.PauseForSpeech))
                    return true;
            UnityEventTools.AddPersistentListener(button.onClick, player.PauseForSpeech);
            EditorUtility.SetDirty(button);
            return true;
        }

        // 6분이 넘는 곡이라 통째로 메모리에 풀지 않고 재생하면서 읽는다(Streaming). 압축은 플랫폼 기본(Vorbis).
        static AudioClip LoadClip()
        {
            if (AssetImporter.GetAtPath(ClipPath) == null)
                AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(ClipPath) is AudioImporter ai)
            {
                var settings = ai.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.Streaming)
                {
                    settings.loadType = AudioClipLoadType.Streaming;
                    ai.defaultSampleSettings = settings;
                    ai.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        }

        static void SceneManagerMoveToScene(GameObject go, UnityEngine.SceneManagement.Scene scene)
        {
            if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        }

        static Transform FindPanel(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = root.transform.Find(name);
                if (t != null) return t;
            }
            return null;
        }
    }
}
