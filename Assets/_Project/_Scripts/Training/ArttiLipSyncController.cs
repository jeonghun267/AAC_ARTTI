using UnityEngine;

namespace Artti.Training
{
    // 다섯 viseme(A/E/I/O/U) BlendShape의 런타임 독립 제어.
    // Animator(ARTTI_Clerk.controller)는 A/E/I/O/U 커브를 갖지 않으므로(빌드 시 제거) 이 컴포넌트가 단독으로 입 모양을 쓴다.
    // 쓰기는 LateUpdate에서 하므로 다른 애니메이션이 같은 shape를 만지더라도 이 값이 최종 적용된다.
    // 음성 인식/TTS 기반 자동 립싱크는 아직 붙이지 않는다. 외부 드라이버가 SetViseme/SetVisemes 로 0~100 값을 넣으면 된다.
    public enum ArttiViseme
    {
        A = 0,
        E = 1,
        I = 2,
        O = 3,
        U = 4,
    }

    public sealed class ArttiLipSyncController : MonoBehaviour
    {
        public const int VisemeCount = 5;
        private static readonly string[] ShapeNames = { "A", "E", "I", "O", "U" };

        [Tooltip("viseme BlendShape를 가진 SkinnedMeshRenderer들 (char1, Teeth_Lower, Teeth_Upper, Tongue). 비워 두면 Awake에서 자식에서 자동 수집")]
        [SerializeField] private SkinnedMeshRenderer[] targets;

        [Tooltip("목표값을 따라가는 속도(1/초). 0이면 즉시 적용. 18이면 약 0.1초 안에 90% 도달")]
        [SerializeField, Range(0f, 60f)] private float smoothing = 18f;

        private readonly float[] _target = new float[VisemeCount];
        private readonly float[] _current = new float[VisemeCount];
        private int[][] _indices;          // [renderer][viseme] -> blendShape index, 없으면 -1
        private bool _bound;

        public bool IsBound => _bound;
        public int TargetRendererCount => targets == null ? 0 : targets.Length;

        private void Awake()
        {
            Bind();
        }

        // 렌더러와 BlendShape 인덱스를 캐싱한다. Update 계열에서 GetComponent/문자열 검색을 하지 않기 위한 선행 작업.
        public void Bind()
        {
            if (targets == null || targets.Length == 0)
            {
                targets = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }
            _indices = new int[targets.Length][];
            for (int r = 0; r < targets.Length; r++)
            {
                _indices[r] = new int[VisemeCount];
                Mesh mesh = targets[r] == null ? null : targets[r].sharedMesh;
                for (int v = 0; v < VisemeCount; v++)
                {
                    _indices[r][v] = mesh == null ? -1 : mesh.GetBlendShapeIndex(ShapeNames[v]);
                }
            }
            _bound = true;
        }

        // 한 viseme의 목표 가중치(0~100)
        public void SetViseme(ArttiViseme viseme, float weight)
        {
            _target[(int)viseme] = Mathf.Clamp(weight, 0f, 100f);
        }

        // 다섯 viseme을 한 번에 (0~100)
        public void SetVisemes(float a, float e, float i, float o, float u)
        {
            _target[0] = Mathf.Clamp(a, 0f, 100f);
            _target[1] = Mathf.Clamp(e, 0f, 100f);
            _target[2] = Mathf.Clamp(i, 0f, 100f);
            _target[3] = Mathf.Clamp(o, 0f, 100f);
            _target[4] = Mathf.Clamp(u, 0f, 100f);
        }

        // 지정 viseme만 켜고 나머지는 0
        public void SetOnly(ArttiViseme viseme, float weight)
        {
            for (int v = 0; v < VisemeCount; v++)
            {
                _target[v] = 0f;
            }
            _target[(int)viseme] = Mathf.Clamp(weight, 0f, 100f);
        }

        public void Clear()
        {
            for (int v = 0; v < VisemeCount; v++)
            {
                _target[v] = 0f;
            }
        }

        public float GetTarget(ArttiViseme viseme)
        {
            return _target[(int)viseme];
        }

        public float GetCurrent(ArttiViseme viseme)
        {
            return _current[(int)viseme];
        }

        // 스무딩 없이 목표값을 즉시 메시에 쓴다 (에디터 검증, 컷 전환용)
        public void ApplyImmediate()
        {
            if (!_bound)
            {
                Bind();
            }
            for (int v = 0; v < VisemeCount; v++)
            {
                _current[v] = _target[v];
            }
            Write();
        }

        private void LateUpdate()
        {
            if (!_bound)
            {
                return;
            }
            if (smoothing <= 0f)
            {
                for (int v = 0; v < VisemeCount; v++)
                {
                    _current[v] = _target[v];
                }
            }
            else
            {
                float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
                for (int v = 0; v < VisemeCount; v++)
                {
                    _current[v] += (_target[v] - _current[v]) * k;
                }
            }
            Write();
        }

        private void Write()
        {
            for (int r = 0; r < targets.Length; r++)
            {
                SkinnedMeshRenderer smr = targets[r];
                if (smr == null)
                {
                    continue;
                }
                int[] idx = _indices[r];
                for (int v = 0; v < VisemeCount; v++)
                {
                    if (idx[v] >= 0)
                    {
                        smr.SetBlendShapeWeight(idx[v], _current[v]);
                    }
                }
            }
        }
    }
}
