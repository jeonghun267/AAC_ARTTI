using UnityEditor;
using UnityEngine;

namespace Artti.Editor
{
    // 현장모드 읽기(TTSManager) 확인 메뉴. Play 모드에서 ARFieldScene을 켠 뒤 쓴다(카드를 누르지 않고 읽기 경로만 확인).
    //   Console: "[TTSManager] Google 음성 재생 (목소리, 길이)" / "늦게 도착한 이전 문장 버림"
    public static class TtsDebugMenu
    {
        [MenuItem("Artti/Debug/TTS - Speak Test (Play mode)")]
        static void SpeakTest()
        {
            var tts = Find();
            if (tts != null) tts.Speak("안녕하세요, 아르띠입니다.");
        }

        // 두 문장을 연달아: 첫 문장은 버려지고 두 번째만 재생되어야 한다
        [MenuItem("Artti/Debug/TTS - Stale Test (Play mode)")]
        static void StaleTest()
        {
            var tts = Find();
            if (tts == null) return;
            tts.Speak("첫 번째 문장은 들리면 안 돼요.");
            tts.Speak("두 번째 문장만 들려요.");
        }

        // 자동 확인(에디터 도구)에서 Play 모드를 켜고 끌 때 쓴다
        [MenuItem("Artti/Debug/TTS - Enter Play Mode")]
        static void EnterPlay() => EditorApplication.EnterPlaymode();

        [MenuItem("Artti/Debug/TTS - Exit Play Mode")]
        static void ExitPlay() => EditorApplication.ExitPlaymode();

        static TTSManager Find()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[TtsDebugMenu] Play 모드에서만 쓴다"); return null; }
            var tts = Object.FindAnyObjectByType<TTSManager>(FindObjectsInactive.Include);
            if (tts == null) Debug.LogWarning("[TtsDebugMenu] TTSManager 없음 - ARFieldScene에서 실행");
            return tts;
        }
    }
}
