using UnityEngine;

[CreateAssetMenu(fileName = "APIKeyConfig", menuName = "Config/APIKey")]
public class APIKeyConfig : ScriptableObject
{
    public string gcpApiKey; // 여기에 GCP 키를 담을 예정입니다.
}