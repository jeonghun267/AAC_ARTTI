using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 아바타 선택 그리드의 한 칸. 빌더가 avatarId/sprite/selectedMark를 채운다.
    public class AvatarItem : MonoBehaviour
    {
        public string avatarId;
        public Button button;
        public GameObject selectedMark;
    }
}
