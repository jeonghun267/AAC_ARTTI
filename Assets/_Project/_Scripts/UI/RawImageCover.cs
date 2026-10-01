using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // RawImage에 들어온 사진을 칸을 꽉 채우게(가운데 기준으로 넘치는 쪽을 잘라) 보여 준다. 사진 비율이 칸과 달라도 찌그러지지 않는다.
    // 사진(texture)을 넣는 코드는 건드리지 않고, 사진이나 칸 크기가 바뀐 프레임에만 uvRect를 다시 맞춘다.
    [RequireComponent(typeof(RawImage))]
    public class RawImageCover : MonoBehaviour
    {
        private RawImage _image;
        private RectTransform _rt;
        private Texture _lastTexture;
        private Vector2 _lastSize;

        private void Awake() => Init();

        private void Init()
        {
            if (_image != null) return;
            _image = GetComponent<RawImage>();
            _rt = (RectTransform)transform;
        }

        private void LateUpdate()
        {
            if (ReferenceEquals(_image.texture, _lastTexture) && _rt.rect.size == _lastSize) return;
            Apply();
        }

        // 지금 사진과 칸 크기로 바로 맞춘다(에디터 캡처처럼 LateUpdate가 돌지 않을 때도 부를 수 있다)
        public void Apply()
        {
            Init();
            var tex = _image.texture;
            var size = _rt.rect.size;
            _lastTexture = tex;
            _lastSize = size;

            if (tex == null || tex.height <= 0 || size.x <= 0f || size.y <= 0f)
            {
                _image.uvRect = new Rect(0f, 0f, 1f, 1f);
                return;
            }
            float texAspect = tex.width / (float)tex.height;
            float boxAspect = size.x / size.y;
            if (texAspect > boxAspect)
            {
                float w = boxAspect / texAspect;
                _image.uvRect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            else
            {
                float h = texAspect / boxAspect;
                _image.uvRect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
            }
        }
    }
}
