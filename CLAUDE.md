# 아르띠 (Artti) — 발달장애인 지역사회 소통 지원 AI·AAC 통합 플랫폼

## 프로젝트 개요

캡스톤 디자인 프로젝트. 발달장애인의 지역사회 소통 참여를 지원하는 듀얼 모드 Unity 앱.

- 훈련모드: 약국·편의점·음식점 시나리오에서 LLM(Gemini Flash) 대화 매니저 기반 발화 훈련
- AR현장모드: 카메라로 간판 인식 후 OCR 기반 AAC 카드 표시 (오프라인 동작)

## 참조 문서

- `docs/PLAN.md` — 개발기획서 v4 (정본). 작업 지시 시 기획서 절 번호로 참조 가능 (예: "기획서 5.5절 기반")
- 충돌 시 우선순위: `PLAN.md` > `CLAUDE.md`

## 환경

- Unity: 6000.3.14f1 (Unity 6.3 LTS)
- 타겟: Android 11 이상 (Min SDK 30, ARM64, Scripting Backend IL2CPP)
- 렌더 파이프라인: URP 17.3.0 (URP_COMPATIBILITY_MODE)
- UI: uGUI 2.0.0 (TextMeshPro 통합)
- AR: AR Foundation 6.3.4 + ARCore XR Plugin 6.3.4
- 비동기: UniTask (Cysharp.Threading.Tasks)
- LLM: Google Gemini 2.5 Flash (function calling, streaming)
- OCR: Google ML Kit (온디바이스)
- 작업 보조: mcp-unity (Claude ↔ Unity Editor 연동)

## 폴더 컨벤션

- 모든 자체 코드/자산: `Assets/_Project/` 아래
- 스크립트: `Assets/_Project/_Scripts/` 아래 기능별 분류 (AAC, Training, ARField, Common 등)
- 데이터: `Assets/_Project/_Data/` 아래 (AAC ScriptableObject, 시나리오 JSON 등)
- 프리팹: `Assets/_Project/Prefabs/`
- 씬: `Assets/_Project/Scenes/`

## 코딩 규칙 (반드시 지킬 것)

### 아키텍처
- MonoBehaviour는 view 갱신과 입력 처리만 담당. 비즈니스 로직, 데이터 모델, 외부 API 연동은 분리.
- Update() 안에서 GetComponent, FindObjectOfType, LINQ 체인, 문자열 연결 금지. 참조는 Awake/Start에서 캐싱.

### 비동기
- 표준 Task 사용 금지. UniTask 사용.
- 모든 async 작업은 CancellationToken을 받고 명시적으로 취소 처리.
- Coroutine은 렌더 루프와 강결합된 경우만 (WaitForEndOfFrame 등).

### 메모리/성능
- JSON 파싱:
  - LLM function calling 응답·도구 인자: Newtonsoft.Json 3.2.2 (mcp-unity 의존성으로 프로젝트에 이미 포함)
  - 일반·작은 페이로드: JsonUtility
  - 큰 페이로드 (필요 시): Utf8Json / MemoryPack 검토
- 자주 생성/소멸되는 오브젝트는 ObjectPool 사용.
- 성능 주장은 측정 지표 기반 (Profiler ms, allocation KB, frame time).

## 코드 작성 규칙

- 부분 코드만 출력. 변경 지점은 주석으로 명시.
- 전체 파일은 사용자가 명시적으로 요청하거나, 새 클래스 도입, using/namespace 변경 시에만.
- 코드, 인라인 주석, 코드 설명에 이모지 사용 금지.

## 작업 흐름

- 작업 시작 전 사용자가 git commit으로 깨끗한 상태 확보.
- 큰 변경은 작업 단위로 쪼개기. "전체 시나리오 시스템" 같은 명령보다 "약국 시나리오의 greeting objective 처리"처럼 작게.
- Unity 자동 컴파일 충돌 방지: Auto Refresh와 Reload Domain은 사용자가 미리 끔. mcp-unity 도구 호출 직전 사용자가 수동 Refresh.

## Scene Builder

### 개념
- `Assets/_Project/Editor/*SceneBuilder.cs`: Unity 씬을 자동 생성하는 에디터 스크립트.
- Unity 메뉴 `Artti > Build XxxScene Hierarchy`로 실행. 전체 일괄 빌드는 `Artti > Build All Scenes`.
- 빌더 실행 시: 기존 씬 GameObject 제거 → .cs 레시피대로 재생성 → 자동 저장.
- 목적: `.unity` 파일의 git 머지 충돌 방지, 협업 시 씬 재현성 확보.
- 약국·편의점·음식점 3개 훈련 씬은 `TrainingSceneBuilder.cs` 한 파일로 처리 (시나리오 ID 파라미터로 분기).

### 진실의 원본
- 빌더 `.cs` 파일이 원본. `.unity`는 빌더가 `.cs`로부터 자동 생성하는 산출물.
- 씬 구조와 와이어링은 빌더 `.cs`에 반영되어야 영구 보존됨.

### 빌더 실행 OK
- 빈 씬을 처음 만들 때.
- 빌더 `.cs` 코드를 수정한 후 반영해야 할 때.
- 변경 성격이 씬 구조 변경(GameObject 추가/삭제, SerializeField 와이어링)일 때.

### 빌더 실행 NO
- 씬에 미커밋 손배치 GameObject가 있는 상태 — 덮어쓰기로 영구 손실.
- Unity Play 모드 실행 중 — InvalidOperationException.
- 변경 성격이 로직만 수정(스크립트 내부 메서드/분기)일 때 — 빌더 불필요.

### 특이사항
- TrainingPharmacyScene 빌더는 실행 시 확인 다이얼로그 표시. 빌더 코드 수정 반영 목적이 아니면 [취소] 선택.

### 손배치 → 빌더 코드 이식
- Unity Inspector의 값(Position, Anchor, Size, 컴포넌트 설정 등)을 빌더 `.cs`로 옮겨 적기.
- 단순 git commit은 임시 보존 수단. 다른 팀원이 빌더를 한 번 실행하면 덮어쓰기 발생.

### AI 에이전트 지침
- 빌더 메뉴 실행은 반드시 사용자 사전 확인 후 진행. 임의 실행 금지.
- 빌더 실행 전 다음을 사용자에게 확인:
  - Unity가 Play 모드가 아닌가
  - 씬에 미커밋 손배치 변경이 있는가
- 빌더 `.cs` 수정 후에는 사용자에게 "Unity 메뉴에서 빌더 실행 필요" 명시적 안내.
- 실행 결과는 Console의 `[XxxSceneBuilder] 완료` 로그로 확인 후 보고.
- 손배치 → 빌더 코드 이식 작업 시 Inspector 값을 사용자에게 요청한 뒤 처리.

## 팀

- 팀장: 김연영 (훈련모드·대화 매니저 담당)
- 팀원: 방승훈 (UI·AAC DB·시각 리소스 담당)
- 팀원: 오정훈 (AR현장모드 담당)
