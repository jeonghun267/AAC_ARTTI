using System.Collections.Generic;
using Artti.AAC;

namespace Artti.Training
{
    // Gemini function calling 한 턴의 응답 전체.
    // 기존 (DialogueTool, string npcText, string[] args) 튜플을 대체한다.
    // 튜플 시절에는 objective_id / slots_filled / scaffold_level / subflow_id가 파싱된 뒤 버려져
    // DialogueManager의 슬롯·서브플로·진행 판정이 전부 죽은 코드로 남아 있었다.
    public class DialogueTurn
    {
        // 호출된 도구. dialogue_tools.json의 function 이름과 1:1.
        public DialogueTool Tool;

        // 이번 턴 점원이 실제로 말할 대사. 모든 도구가 채우는 것이 원칙이나
        // LLM이 빈 값을 낼 수 있어 호출부에 침묵 방어선이 필요하다.
        public string NpcSpeech;

        // present_cards의 card_ids, enter_subflow/request_clarification의 suggested_cards.
        public string[] CardIds;

        // mark_objective_complete / transition_to_objective가 지정한 목표 단계.
        // 앱이 ResolveNextObjective로 검증한 뒤 반영한다(하이브리드 진행 제어).
        public string ObjectiveId;

        // enter_subflow
        public string SubflowId;
        public string PendingTopic;

        // force_complete_scenario
        public string Reason;

        // present_cards의 scaffold_level. LLM이 안 보내면 null → 앱 정책값만 사용.
        public int? ScaffoldLevel;

        // mark_objective_complete / return_from_subflow의 slots_filled.
        // 값 타입이 string[]·bool·string으로 섞여 있어 문자열로 평탄화해 보관한다
        // (프롬프트에 되먹이는 용도라 원본 타입이 필요 없음).
        public Dictionary<string, string> SlotsFilled;

        public bool HasSpeech => !string.IsNullOrWhiteSpace(NpcSpeech);
    }
}
