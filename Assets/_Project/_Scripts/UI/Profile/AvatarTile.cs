using UnityEngine;

namespace Artti.UI
{
    // 프로필 등록 1단계(아바타 선택) 그리드의 한 칸. AvatarItem에 분류 탭용 분류와 미리보기 스프라이트를 더한다.
    public class AvatarTile : AvatarItem
    {
        [System.Flags]
        public enum Category { None = 0, Female = 1, Male = 2, Neutral = 4, Style = 8 }

        public Category categories;
        public Sprite sprite;
    }
}
