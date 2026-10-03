using UnityEngine;
using Artti.AAC.Logging;
using Artti.Common;
using Cysharp.Threading.Tasks;

namespace Artti.Common
{
    public class AppBootstrap : MonoBehaviour
    {
        public static AppBootstrap Instance { get; private set; }

        public ProfileManager ProfileManager { get; private set; }
        public ILogStore LogStore { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        // 변경점: 스플래시에서 바로 메인으로 가면 프로필 고르기 씬(여기 AppBootstrap이 놓여 있다)을 거치지 않아
        //   AppBootstrap이 없었고, 훈련 기록이 아무 데도 저장되지 않아 리포트·완료 화면 결과가 비었다.
        //   앱이 켜질 때 스스로 만든다(프로필 고르기 씬에 놓인 것은 Awake에서 중복으로 지워진다).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;   // Reload Domain을 끈 에디터용

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureOnStart()
        {
            if (Instance == null) new GameObject("[AppBootstrap]").AddComponent<AppBootstrap>();
        }

        private void Initialize()
        {
            ProfileManager = new ProfileManager();
            // 변경점: 마지막으로 쓴 프로필을 다시 골라 기록 저장소를 연다(앱을 다시 켜도 그 프로필로 기록된다).
            //   프로필이 하나도 없으면 anonymous 기록에 남긴다(프로필을 만들면 그 프로필 기록으로 바뀐다).
            var last = ProfileManager.LastUsedProfile();
            if (last != null) SwitchProfile(last.id);
            else LogStore = new JsonAppendLogStore(null);
            Debug.Log($"[AppBootstrap] 훈련 기록 저장: {(last != null ? last.nickname : "프로필 없음(anonymous)")} -> {(LogStore as JsonAppendLogStore)?.FilePath}");
        }

        public void SwitchProfile(string profileId)
        {
            ProfileManager.SetActiveProfile(profileId);
            
            // Dispose old log store
            if (LogStore != null)
            {
                var oldStore = LogStore;
                DisposeLogStore(oldStore).Forget();
            }

            // Initialize new log store for the profile
            LogStore = new JsonAppendLogStore(profileId);
        }

        private async UniTaskVoid DisposeLogStore(ILogStore store)
        {
            await store.DisposeAsync();
        }
    }
}
