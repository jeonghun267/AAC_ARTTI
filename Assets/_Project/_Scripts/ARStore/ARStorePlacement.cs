using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Artti.ARStore
{
    // 탭한 바닥 평면에 편의점 매장을 월드 고정(world-anchored) 배치한다.
    // 매장은 카메라 자식이 아니라 현실 좌표에 박혀 있어, 폰을 좌우로 돌리면 매장 양끝이 차례로 보인다.
    // MonoBehaviour는 입력 수집 + 인스턴스 배치만 담당(CLAUDE.md). 배치 후 평면 추적을 끈다.
    public class ARStorePlacement : MonoBehaviour
    {
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private GameObject storePrefab;     // ARTTI_Store(+ 점원) — 미터 단위
        [SerializeField] private GameObject guideRoot;        // "바닥을 탭하세요" 안내 패널

        [Header("스케일")]
        [SerializeField] private float lifeSizeScale  = 1f;   // 실물 크기(걸어 들어가는 느낌). Blender가 미터로 모델링됐다는 전제
        [SerializeField] private float miniatureScale = 0.05f; // 책상 위 모형

        private GameObject _instance;
        private bool _isMiniature;
        private static readonly List<ARRaycastHit> _hits = new List<ARRaycastHit>();

        public bool IsPlaced => _instance != null;

        private void Update()
        {
            if (raycastManager == null || storePrefab == null) return;
            if (!TryGetTap(out Vector2 screenPos, out int pointerId)) return;
            if (IsOverUI(pointerId)) return; // 버튼 위 탭은 배치로 처리하지 않음

            if (!raycastManager.Raycast(screenPos, _hits, TrackableType.PlaneWithinPolygon)) return;

            Pose pose = _hits[0].pose;
            if (_instance == null)
            {
                _instance = Instantiate(storePrefab, pose.position, pose.rotation);
                ApplyScale();
                if (guideRoot != null) guideRoot.SetActive(false);
                SetPlaneDetection(false); // 한번 놓으면 평면 추적/시각화 정지 → 월드에 고정
            }
            else
            {
                // 이미 배치된 경우 탭한 위치로 이동(재배치)
                _instance.transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
        }

        // 버튼(onClick)에서 호출 — 실물 ↔ 미니어처 토글
        public void ToggleScale()
        {
            _isMiniature = !_isMiniature;
            ApplyScale();
        }

        // 버튼(onClick)에서 호출 — 다시 놓기 (Unity 매직메서드 Reset과 겹치지 않게 별도 이름)
        public void ResetPlacement()
        {
            if (_instance != null) Destroy(_instance);
            _instance = null;
            _isMiniature = false;
            if (guideRoot != null) guideRoot.SetActive(true);
            SetPlaneDetection(true);
        }

        private void ApplyScale()
        {
            if (_instance == null) return;
            float s = _isMiniature ? miniatureScale : lifeSizeScale;
            _instance.transform.localScale = new Vector3(s, s, s);
        }

        private void SetPlaneDetection(bool on)
        {
            if (planeManager == null) return;
            planeManager.enabled = on;
            foreach (var plane in planeManager.trackables)
                plane.gameObject.SetActive(on);
        }

        // 터치(기기) 우선, 없으면 마우스(에디터 Play 테스트). activeInputHandler=Both라 레거시 Input 사용 가능.
        private bool TryGetTap(out Vector2 pos, out int pointerId)
        {
            pos = default;
            pointerId = -1;
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase != TouchPhase.Began) return false;
                pos = t.position;
                pointerId = t.fingerId;
                return true;
            }
            if (Input.GetMouseButtonDown(0))
            {
                pos = Input.mousePosition;
                return true;
            }
            return false;
        }

        private static bool IsOverUI(int pointerId)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            return pointerId < 0 ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(pointerId);
        }
    }
}
