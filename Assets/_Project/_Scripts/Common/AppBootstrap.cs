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

        private void Initialize()
        {
            ProfileManager = new ProfileManager();
            LogStore = new NullLogStore(); // Initial
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
