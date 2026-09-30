using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HelpTextInputController : MonoBehaviour
{
    [SerializeField] private TMP_InputField messageInputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private TTSManager ttsManager;

    private void Awake()
    {
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(SendMessageToTTS);
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

        if (messageInputField != null)
        {
            messageInputField.onSubmit.RemoveListener(HandleSubmit);
        }
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
    }

    private void HandleSubmit(string _)
    {
        SendMessageToTTS();
    }
}