using UnityEngine;
using System;

/// <summary>
/// Android AAR(mlkit_ocr)의 OcrBridge 클래스를 호출하는 C# 래퍼
/// 
/// 사용법:
///   OcrBridge.RecognizeFromPath(
///       imagePath,
///       onResult: (text) => Debug.Log("OCR 결과: " + text),
///       onError:  (msg)  => Debug.LogError("OCR 실패: " + msg)
///   );
/// </summary>
public static class OcrBridge
{
    // AAR 안의 OcrBridge object의 전체 패키지 경로
    // Kotlin object는 JVM에서 INSTANCE 싱글톤으로 노출됨
    private const string ANDROID_CLASS = "com.artee.mlkitocr.OcrBridge";

    /// <summary>
    /// 이미지 파일 경로를 받아 ML Kit OCR을 실행하고
    /// 결과를 콜백으로 전달
    /// </summary>
    public static void RecognizeFromPath(
        string imagePath,
        Action<string> onResult,
        Action<string> onError)
    {
        if (Application.platform != RuntimePlatform.Android)
        {
            onError?.Invoke("Android 환경에서만 동작합니다 (현재: " + Application.platform + ")");
            return;
        }

        try
        {
            // Kotlin object를 Java 측에서 정적으로 호출하기 위해 클래스 가져오기
            using (var bridgeClass = new AndroidJavaClass(ANDROID_CLASS))
            {
                // C#의 Action 콜백을 Java IOcrCallback으로 변환
                var proxy = new OcrCallbackProxy(onResult, onError);

                // recognizeFromPath(String imagePath, IOcrCallback callback) 호출
                bridgeClass.CallStatic(
                    "recognizeFromPath",
                    imagePath,
                    proxy
                );
            }
        }
        catch (Exception e)
        {
            onError?.Invoke("OcrBridge 호출 실패: " + e.Message);
        }
    }
}