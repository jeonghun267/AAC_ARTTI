using System;
using UnityEngine;
using Artti.Common;

namespace Artti.Training
{
    // 훈련 모드별 점원 배역. 편의점 씬 하나를 정확도 모드와 대화 모드가 같이 쓰므로
    // 시작할 때 모드에 맞는 점원 하나만 켜고, 점원 카메라를 그 점원의 키에 맞춘다.
    //   정확도 모드: Stationary v4 점원 / 대화 모드: 남자 점원(ARTTI_Male)
    // 대화 모드 점원이 비어 있으면 정확도 모드 점원을 그대로 쓴다.
    // [ClerkStage]에 붙고, TrainingSceneRoot.Awake가 Activate를 호출해 뷰와 음성 채널을 받아 간다.
    public class ClerkCast : MonoBehaviour
    {
        [Serializable]
        public class Actor
        {
            public GameObject root;
            public ClerkView view;
            public AudioSource speech;
            [Tooltip("[ClerkStage] 기준 점원 카메라 위치")]
            public Vector3 cameraLocalPosition = new Vector3(0f, 1.40f, 2.20f);
            [Tooltip("[ClerkStage] 기준 카메라가 바라보는 점")]
            public Vector3 cameraLookLocal = new Vector3(0f, 1.40f, 0f);
            public float fieldOfView = 40f;
        }

        [SerializeField] private Camera clerkCamera;
        [SerializeField] private Actor accuracyClerk = new Actor();
        [SerializeField] private Actor conversationClerk = new Actor();

        // 변경점(2026-10-06): 이 모드에서 실제로 나오는 점원이 남자 점원인가(목소리를 점원 성별에 맞추려고)
        public bool UsesMaleClerk(TrainingMode mode) =>
            mode == TrainingMode.Conversation && conversationClerk.root != null;

        public Actor Activate(TrainingMode mode)
        {
            bool useConversation = mode == TrainingMode.Conversation && conversationClerk.root != null;
            Actor selected = useConversation ? conversationClerk : accuracyClerk;
            Actor other = useConversation ? accuracyClerk : conversationClerk;

            if (other.root != null && other.root != selected.root) other.root.SetActive(false);
            if (selected.root != null) selected.root.SetActive(true);

            if (clerkCamera != null)
            {
                clerkCamera.transform.localPosition = selected.cameraLocalPosition;
                clerkCamera.transform.LookAt(transform.TransformPoint(selected.cameraLookLocal));
                clerkCamera.fieldOfView = selected.fieldOfView;
            }
            return selected;
        }
    }
}
