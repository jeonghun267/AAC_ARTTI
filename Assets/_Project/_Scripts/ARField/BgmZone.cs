using UnityEngine;

namespace Artti.ARField
{
    // 이 패널이 켜져 있는 동안 배경음(ARFieldBgmPlayer)이 들린다. 패널을 켜고 끄는 코드는 그대로 두고 붙이기만 하면 된다.
    [DisallowMultipleComponent]
    public sealed class BgmZone : MonoBehaviour
    {
        [SerializeField] private ARFieldBgmPlayer player;

        private void OnEnable()
        {
            if (player != null) player.EnterZone();
        }

        private void OnDisable()
        {
            if (player != null) player.ExitZone();
        }
    }
}
