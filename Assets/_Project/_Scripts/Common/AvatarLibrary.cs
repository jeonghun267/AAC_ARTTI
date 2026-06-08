using UnityEngine;

namespace Artti.Common
{
    // avatarId(이모지 hex) → Sprite 매핑. 빌더가 Resources/AvatarLibrary.asset로 생성.
    // 런타임에 동적 생성되는 프로필 카드가 아바타 이미지를 찾는 데 사용.
    public class AvatarLibrary : ScriptableObject
    {
        public Sprite[] sprites;

        public Sprite GetById(string id)
        {
            if (sprites == null || sprites.Length == 0) return null;
            if (!string.IsNullOrEmpty(id))
                foreach (var s in sprites)
                    if (s != null && s.name == id) return s;
            return sprites[0]; // 폴백: 첫 번째
        }

        public static AvatarLibrary Load()
        {
            return Resources.Load<AvatarLibrary>("AvatarLibrary");
        }
    }
}
