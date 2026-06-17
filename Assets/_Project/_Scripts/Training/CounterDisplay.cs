using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Artti.Training
{
    // 편의점 카운터 위 물건 표시.
    // 사용자가 요청한 물건(카드)을 점원이 집어든 뒤 카운터에 등장시킨다(요청할수록 쌓임).
    // 실제 물건 오브젝트는 씬에서 카운터 위 자리에 미리 배치(비활성)해 두고 cardId와 매핑한다.
    public class CounterDisplay : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string cardId;    // 예: card_cvs_water
            public GameObject item;  // 카운터 위에 놓일 물건(씬에 미리 배치, 비활성 상태)
        }

        [Tooltip("카드 id → 카운터 물건 매핑")]
        [SerializeField] private List<Entry> items = new List<Entry>();

        [Tooltip("점원이 집어드는 동안 기다렸다 카운터에 등장시키는 지연(초). Picking Up 클립 길이에 맞춤")]
        [SerializeField] private float revealDelaySeconds = 1.2f;

        private readonly Dictionary<string, GameObject> _map = new Dictionary<string, GameObject>();
        private bool _mapped;

        private void Awake()
        {
            EnsureMap();
            foreach (var go in _map.Values)
                if (go != null) go.SetActive(false); // 시작 시 카운터 비움
        }

        // 매핑을 지연 구성. CounterDisplay가 비활성 오브젝트에 붙어 Awake가 안 돌아도 동작하도록.
        private void EnsureMap()
        {
            if (_mapped) return;
            _mapped = true;
            foreach (var e in items)
            {
                if (e == null || e.item == null || string.IsNullOrEmpty(e.cardId)) continue;
                _map[e.cardId] = e.item;
            }
        }

        // 요청한 물건을 카운터에 등장(이미 올라와 있으면 무시). 점원 집기 애니 뒤 나타나도록 지연.
        public void PlaceItem(string cardId)
        {
            EnsureMap();
            if (string.IsNullOrEmpty(cardId)) return;
            if (!_map.TryGetValue(cardId, out var go) || go == null)
            {
                Debug.LogWarning($"[CounterDisplay] '{cardId}' 매핑 없음 (등록 {_map.Count}개) — Items의 cardId/Item 연결 확인.");
                return;
            }
            if (go.activeSelf) return;
            RevealAsync(go, this.GetCancellationTokenOnDestroy()).Forget();
        }

        // 세션 재시작 등에서 카운터 비우기
        public void ClearAll()
        {
            EnsureMap();
            foreach (var kv in _map)
                if (kv.Value != null) kv.Value.SetActive(false);
        }

        private async UniTaskVoid RevealAsync(GameObject go, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(revealDelaySeconds), cancellationToken: ct);
                if (go == null) return;
                go.SetActive(true);
                if (!go.activeInHierarchy)
                    Debug.LogWarning($"[CounterDisplay] '{go.name}' 활성화했지만 부모가 비활성이라 화면에 안 보임 — 상위 오브젝트 활성 상태 확인.");
            }
            catch (OperationCanceledException) { }
        }
    }
}
