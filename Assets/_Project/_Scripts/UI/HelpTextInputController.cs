using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HelpTextInputController : MonoBehaviour
{
    [SerializeField] private TMP_InputField messageInputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private TTSManager ttsManager;
    // 변경점: 입력창 오른쪽 지우기(X) 버튼
    [SerializeField] private Button clearButton;

    // 변경점: 문장을 읽은 뒤 알린다(도움 화면 채팅창이 보낸 문장 목록에 남긴다)
    public event Action<string> Spoken;

    public TMP_InputField InputField => messageInputField;

    private void Awake()
    {
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(SendMessageToTTS);
        }

        if (clearButton != null)
        {
            clearButton.onClick.AddListener(ClearMessage);
        }

        if (messageInputField != null)
        {
            messageInputField.onSubmit.AddListener(HandleSubmit);
        }
    }

    private void OnDestroy()
    {
        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(SendMessageToTTS);
        }

        if (clearButton != null)
        {
            clearButton.onClick.RemoveListener(ClearMessage);
        }

        if (messageInputField != null)
        {
            messageInputField.onSubmit.RemoveListener(HandleSubmit);
        }
    }

    // 변경점: 입력한 문장을 지우고 바로 다시 입력할 수 있게 한다
    public void ClearMessage()
    {
        if (messageInputField == null)
        {
            return;
        }

        messageInputField.text = string.Empty;
        messageInputField.ActivateInputField();
    }

    public void SendMessageToTTS()
    {
        if (messageInputField == null || ttsManager == null)
        {
            return;
        }

        string message = messageInputField.text.Trim();
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        ttsManager.Speak(message);
        messageInputField.text = string.Empty;
        messageInputField.ActivateInputField();
        Spoken?.Invoke(message);
    }

    private void HandleSubmit(string _)
    {
        SendMessageToTTS();
    }
}