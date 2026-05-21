using UnityEngine;
using System;

/// <summary>
/// Java 측 IOcrCallback 인터페이스를 C#으로 구현한 프록시
/// 
/// AndroidJavaProxy를 상속하면 Java 코드가 이 객체를 자기 인터페이스의
/// 구현체로 인식하고, 메서드 호출 시 C# 메서드가 실행됨
/// 
/// 주의: ML Kit는 백그라운드 스레드에서 콜백을 호출합니다.
///       Unity API는 메인 스레드에서만 호출해야 하므로
///       UnityMainThreadDispatcher를 통해 메인 스레드로 마샬링합니다.
/// </summary>
public class OcrCallbackProxy : AndroidJavaProxy
{
    private readonly Action<string> _onResult;
    private readonly Action<string> _onError;

    public OcrCallbackProxy(Action<string> onResult, Action<string> onError)
        : base("com.artee.mlkitocr.IOcrCallback")
    {
        _onResult = onResult;
        _onError = onError;
    }

    /// <summary>
    /// Java IOcrCallback.onResult(String text) 구현
    /// 메서드 이름은 Java 인터페이스의 시그니처와 정확히 일치해야 함
    /// </summary>
    public void onResult(string text)
    {
        // ML Kit는 메인 스레드가 아닌 곳에서 호출하므로 디스패처 사용
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            _onResult?.Invoke(text ?? "");
        });
    }

    /// <summary>
    /// Java IOcrCallback.onError(String message) 구현
    /// </summary>
    public void onError(string message)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            _onError?.Invoke(message ?? "Unknown error");
        });
    }
}