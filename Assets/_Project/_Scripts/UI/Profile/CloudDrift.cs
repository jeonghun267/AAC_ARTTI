using UnityEngine;

namespace Artti.UI
{
    // 프로필 만들기 배경: 구름이 글래스 패널 뒤에서 한 방향(좌 또는 우)으로
    // 천천히 흘러간다. 한쪽 끝으로 완전히 빠져나가면 반대쪽 끝에서 다시 들어와 무한 반복.
    // 빌더가 clouds 배열에 구름 RectTransform들을 연결한다. 흐르는 구간은
    // 부모(컨테이너) RectTransform의 폭을 기준으로 잡는다.
    [DisallowMultipleComponent]
    public class CloudDrift : MonoBehaviour
    {
        public enum Direction { Left = -1, Right = 1 }

        [SerializeField] private RectTransform[] clouds;

        [Header("흐름")]
        [SerializeField] private Direction direction = Direction.Right;
        [SerializeField] private float speed = 18f;     // px/sec
        [SerializeField] private float margin = 60f;     // 화면 밖으로 더 나가는 여유 px

        private float _halfWidth;

        private void Awake()
        {
            var rt = (RectTransform)transform;
            _halfWidth = rt.rect.width * 0.5f;
        }

        private void Update()
        {
            if (clouds == null) return;
            float dx = (int)direction * speed * Time.deltaTime;

            for (int i = 0; i < clouds.Length; i++)
            {
                var c = clouds[i];
                if (c == null) continue;

                var p = c.anchoredPosition;
                p.x += dx;

                float halfCloud = c.rect.width * 0.5f;
                float exit = _halfWidth + halfCloud + margin;

                // 진행 방향 끝으로 완전히 빠져나가면 반대쪽 바깥에서 재진입
                if (direction == Direction.Right && p.x > exit)
                    p.x = -exit;
                else if (direction == Direction.Left && p.x < -exit)
                    p.x = exit;

                c.anchoredPosition = p;
            }
        }
    }
}
