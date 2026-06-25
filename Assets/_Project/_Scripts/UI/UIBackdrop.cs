using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 연한 파스텔 그라데이션 배경 + 느리게 떠오르는 미세 입자. 캔버스 맨 뒤에 런타임 생성.
    // 주의: MonoBehaviour는 클래스명과 동일한 파일이어야 안드로이드 빌드에서 MonoScript가
    // 외부 에셋으로 정상 직렬화된다. (한 파일에 여러 MonoBehaviour를 두면 씬에 가짜 MonoScript가
    // 임베드되어 device에서 "level corrupted / Position out of bounds" 크래시 발생.)
    public class UIBackdrop : MonoBehaviour
    {
        public int particleCount = 14;
        public bool gradient = true;                            // false면 입자만 (사진 배경 위에 얹을 때)
        public Color top = new Color(0.86f, 0.92f, 1f, 1f);     // 연한 하늘
        public Color bottom = new Color(1f, 0.97f, 0.92f, 1f);  // 크림

        private RectTransform[] _parts;
        private float[] _speed;
        private float _h = 1080f;

        private void Start()
        {
            var self = (RectTransform)transform;
            self.anchorMin = Vector2.zero; self.anchorMax = Vector2.one;
            self.offsetMin = Vector2.zero; self.offsetMax = Vector2.zero;
            _h = self.rect.height > 1f ? self.rect.height : 1080f;

            if (gradient) BuildGradient(self);
            BuildParticles(self);
        }

        private void BuildGradient(RectTransform parent)
        {
            var go = new GameObject("Gradient", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<RawImage>();
            img.raycastTarget = false;
            img.texture = MakeGradientTex();
        }

        private Texture2D MakeGradientTex()
        {
            const int n = 128;
            var tex = new Texture2D(2, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            {
                var c = Color.Lerp(bottom, top, y / (float)(n - 1));
                tex.SetPixel(0, y, c); tex.SetPixel(1, y, c);
            }
            tex.Apply();
            return tex;
        }

        private void BuildParticles(RectTransform parent)
        {
            var dot = MakeDotSprite();
            _parts = new RectTransform[particleCount];
            _speed = new float[particleCount];
            for (int i = 0; i < particleCount; i++)
            {
                var go = new GameObject("P" + i, typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                float size = 12f + (i % 4) * 7f;
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(Mathf.Repeat(i * 137.5f, 1900f) + 20f, Mathf.Repeat(i * 213f, _h));
                var img = go.AddComponent<Image>();
                img.sprite = dot; img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, 0.30f + 0.12f * (i % 3));
                _parts[i] = rt;
                _speed[i] = 8f + (i % 5) * 4f; // px/s
            }
        }

        private static Sprite MakeDotSprite()
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            float r = s * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        private void Update()
        {
            if (_parts == null) return;
            for (int i = 0; i < _parts.Length; i++)
            {
                var p = _parts[i];
                var pos = p.anchoredPosition;
                pos.y += _speed[i] * Time.deltaTime;
                if (pos.y > _h + 20f) pos.y = -20f;
                p.anchoredPosition = pos;
            }
        }
    }
}
