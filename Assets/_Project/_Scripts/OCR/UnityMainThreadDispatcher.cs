using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 백그라운드 스레드에서 메인 스레드로 작업을 옮기는 싱글톤 디스패처
/// 
/// 사용법:
///   UnityMainThreadDispatcher.Instance.Enqueue(() => {
///       myText.text = "갱신된 내용";  // 메인 스레드에서 실행됨
///   });
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private static readonly Queue<Action> _queue = new Queue<Action>();
    private static readonly object _lock = new object();

    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                // 씬에 없으면 자동 생성
                var go = new GameObject("UnityMainThreadDispatcher");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    /// <summary>
    /// 메인 스레드에서 실행할 작업을 큐에 추가
    /// </summary>
    public void Enqueue(Action action)
    {
        if (action == null) return;
        lock (_lock)
        {
            _queue.Enqueue(action);
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // 매 프레임 큐에 쌓인 작업을 메인 스레드에서 실행
        lock (_lock)
        {
            while (_queue.Count > 0)
            {
                try
                {
                    _queue.Dequeue()?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError("MainThreadDispatcher 작업 실패: " + e);
                }
            }
        }
    }
}