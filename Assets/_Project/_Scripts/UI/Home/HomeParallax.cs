using System.Collections.Generic;
using UnityEngine;

namespace Artti.UI
{
    // 선택 기능. 깊이별 레이어를 입력(에디터=마우스, 안드로이드=기울기)에 따라
    // 서로 다른 크기로 미세 이동시켜 패럴랙스 깊이감을 준다.
    // 기본은 비활성(빌더가 enabled=false). 필요할 때 인스펙터에서 켠다.
    public class HomeParallax : MonoBehaviour
    {
        [System.Serializable]
        public struct Layer
        {
            public RectTransform target;
            public float maxOffset; // px
        }

        [SerializeField] private List<Layer> layers = new List<Layer>();
        [SerializeField] private float smooth = 6f;
        [SerializeField] private bool useTiltOnDevice = true;

        private Vector2[] _base;
        private Vector2 _tilt;

        public void AddLayer(RectTransform target, float maxOffset)
        {
            layers.Add(new Layer { target = target, maxOffset = maxOffset });
        }

        private void OnEnable()
        {
            _base = new Vector2[layers.Count];
            for (int i = 0; i < layers.Count; i++)
                if (layers[i].target != null) _base[i] = layers[i].target.anchoredPosition;
        }

        private void OnDisable()
        {
            if (_base == null) return;
            for (int i = 0; i < layers.Count && i < _base.Length; i++)
                if (layers[i].target != null) layers[i].target.anchoredPosition = _base[i];
        }

        private void Update()
        {
            Vector2 target;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (useTiltOnDevice)
            {
                var a = Input.acceleration;
                target = new Vector2(Mathf.Clamp(a.x, -1f, 1f), Mathf.Clamp(a.y + 0.5f, -1f, 1f));
            }
            else target = ScreenTilt();
#else
            target = ScreenTilt();
#endif
            _tilt = Vector2.Lerp(_tilt, target, smooth * Time.deltaTime);
            for (int i = 0; i < layers.Count; i++)
            {
                var l = layers[i];
                if (l.target == null) continue;
                l.target.anchoredPosition = _base[i] + _tilt * l.maxOffset;
            }
        }

        private static Vector2 ScreenTilt()
        {
            Vector2 m = Input.mousePosition;
            return new Vector2((m.x / Screen.width) * 2f - 1f, (m.y / Screen.height) * 2f - 1f);
        }
    }
}
