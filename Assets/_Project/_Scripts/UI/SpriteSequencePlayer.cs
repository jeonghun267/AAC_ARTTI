using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 한 장의 Image 위에서 Sprite 배열을 프레임 단위로 재생하는 범용 플레이어.
    // 캐릭터 모션(walk/idle/talk/full_motion)을 동일한 컴포넌트로 처리한다.
    // 비즈니스 로직 없음 - 순수 표시. 상태 전환은 ReportCharacterDirector가 담당.
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public class SpriteSequencePlayer : MonoBehaviour
    {
        private Image _image;
        private IReadOnlyList<Sprite> _frames;
        private float _frameInterval;     // 1 / fps
        private float _timer;
        private int _index;
        private bool _loop;
        private bool _playing;
        private Action _onComplete;

        public bool IsPlaying => _playing;

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        // frames를 fps로 재생. loop=false면 마지막 프레임 도달 후 onComplete 1회 호출하고 정지.
        public void Play(IReadOnlyList<Sprite> frames, float fps, bool loop, Action onComplete = null)
        {
            if (frames == null || frames.Count == 0)
            {
                _playing = false;
                onComplete?.Invoke();
                return;
            }

            _frames = frames;
            _frameInterval = fps > 0f ? 1f / fps : 0.1f;
            _loop = loop;
            _onComplete = onComplete;
            _timer = 0f;
            _index = 0;
            _playing = true;
            ApplyFrame(0);
        }

        public void Stop()
        {
            _playing = false;
        }

        // 자동재생 대신 외부(거리 동기화 walk 등)에서 프레임을 직접 지정. 음수/초과 index는 순환.
        public void ShowFrameExternal(IReadOnlyList<Sprite> frames, int index)
        {
            _playing = false;
            if (frames == null || frames.Count == 0) return;
            _frames = frames;
            _index = ((index % frames.Count) + frames.Count) % frames.Count;
            ApplyFrame(_index);
        }

        private void Update()
        {
            if (!_playing || _frames == null) return;

            _timer += Time.deltaTime;
            if (_timer < _frameInterval) return;

            // 누적 시간이 여러 프레임을 넘겼을 수 있으니 한 번에 따라잡는다.
            while (_timer >= _frameInterval)
            {
                _timer -= _frameInterval;
                _index++;

                if (_index >= _frames.Count)
                {
                    if (_loop)
                    {
                        _index = 0;
                    }
                    else
                    {
                        _index = _frames.Count - 1;
                        ApplyFrame(_index);
                        _playing = false;
                        var cb = _onComplete;
                        _onComplete = null;
                        cb?.Invoke();
                        return;
                    }
                }
            }

            ApplyFrame(_index);
        }

        private void ApplyFrame(int i)
        {
            if (_image != null && _frames != null && i >= 0 && i < _frames.Count)
                _image.sprite = _frames[i];
        }
    }
}
