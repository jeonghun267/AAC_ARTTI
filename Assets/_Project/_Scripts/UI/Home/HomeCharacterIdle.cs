using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 메인 화면 캐릭터를 "살아있게" 만드는 ambient 모션.
    // 호흡(상하/스케일), 좌우 흔들림, 가끔 끄덕임, 그리고 눈 깜빡임(스프라이트 교체).
    // 진입 애니메이션(HomeIntroAnimator)이 끝난 뒤 enable 되도록 빌더가 enabled=false로 둠.
    [DisallowMultipleComponent]
    public class HomeCharacterIdle : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private RectTransform body;   // 움직일 캐릭터 RectTransform
        [SerializeField] private Image faceImage;      // 눈 깜빡임 스프라이트 교체 대상(= 캐릭터 이미지)
        [SerializeField] private Sprite eyesOpen;
        [SerializeField] private Sprite eyesClosed;    // null이면 깜빡임 비활성

        [Header("호흡")]
        [SerializeField] private float breathAmplitude = 6f;   // px 상하
        [SerializeField] private float breathSpeed = 0.5f;     // Hz
        [SerializeField] private float breathScale = 0.012f;   // 스케일 호흡량

        [Header("좌우 흔들림")]
        [SerializeField] private float swayAmplitude = 3f;
        [SerializeField] private float swaySpeed = 0.26f;

        [Header("끄덕임")]
        [SerializeField] private float nodAngle = 3.5f;
        [SerializeField] private float nodDuration = 0.6f;
        [SerializeField] private Vector2 nodInterval = new Vector2(4f, 8f);

        [Header("눈 깜빡임")]
        [SerializeField] private float blinkClosed = 0.11f;
        [SerializeField] private Vector2 blinkInterval = new Vector2(2.5f, 6f);

        private Vector2 _basePos;
        private Vector3 _baseScale;
        private float _phase;

        // 빌더에서 호출 (씬 저장 시 직렬화됨)
        public void Setup(RectTransform target, Image face, Sprite open, Sprite closed)
        {
            body = target;
            faceImage = face;
            eyesOpen = open;
            eyesClosed = closed;
        }

        private void OnEnable()
        {
            if (body == null) body = transform as RectTransform;
            _basePos = body.anchoredPosition;
            _baseScale = body.localScale;
            // 캐릭터마다 위상 다르게 → 동시에 같은 박자로 움직이지 않게
            _phase = (transform.GetSiblingIndex() + 1) * 1.7f;

            if (eyesClosed != null && faceImage != null) StartCoroutine(BlinkLoop());
            StartCoroutine(NodLoop());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (body != null)
            {
                body.anchoredPosition = _basePos;
                body.localScale = _baseScale;
                body.localRotation = Quaternion.identity;
            }
        }

        private void Update()
        {
            float t = (Time.time + _phase) * breathSpeed * Mathf.PI * 2f;
            float by = Mathf.Sin(t) * breathAmplitude;
            float tx = (Time.time + _phase) * swaySpeed * Mathf.PI * 2f;
            float bx = Mathf.Sin(tx) * swayAmplitude;
            body.anchoredPosition = _basePos + new Vector2(bx, by);

            float s = 1f + Mathf.Sin(t) * breathScale;
            body.localScale = new Vector3(_baseScale.x * s, _baseScale.y * s, _baseScale.z);
        }

        private IEnumerator BlinkLoop()
        {
            var open = eyesOpen != null ? eyesOpen : faceImage.sprite;
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(blinkInterval.x, blinkInterval.y));
                faceImage.sprite = eyesClosed;
                yield return new WaitForSeconds(blinkClosed);
                faceImage.sprite = open;
            }
        }

        private IEnumerator NodLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(nodInterval.x, nodInterval.y));
                float half = nodDuration * 0.5f;
                for (float e = 0f; e < nodDuration; e += Time.deltaTime)
                {
                    float k = e < half ? e / half : 1f - (e - half) / half;
                    k = Mathf.SmoothStep(0f, 1f, k);
                    body.localRotation = Quaternion.Euler(0f, 0f, -nodAngle * k);
                    yield return null;
                }
                body.localRotation = Quaternion.identity;
            }
        }
    }
}
