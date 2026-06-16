# 편의점 씬 3D 전환 — 돌아와서 할 일 (Claude가 코드 다 짜둠)

교수님 피드백("메인씬 다음 캐릭터 등장이 AR/3D 같지 않다") 대응. 평면 사진 위 RT 합성 →
**메인 카메라가 진짜 3D 편의점 무대(환경 + 점원 + 데스크/포스기)를 직접 렌더**하는 구조로 바꿈.
AAC 카드 UI는 그대로 2D HUD(Screen Space Overlay).

## Claude가 이미 해둔 것 (코드/에셋)
- `Assets/_Project/Models/Clerk.glb` — VRoid 점원(Clerk.vrm을 .glb로 복사, glTFast 임포트됨)
- `Packages/manifest.json` — `com.unity.cloud.gltfast` 직접 의존성으로 고정
- `Assets/_Project/Editor/ClerkCharacterSetup.cs` — Clerk을 Humanoid 아바타로 변환 + Idle 붙인 프리팹 생성 (신규)
- `Assets/_Project/Editor/ConvenienceTrainingSceneBuilder.cs` — 3D 무대 렌더 구조로 개조 (배경사진/RT 제거, 메인카메라+환경+점원+데스크/포스기+VRoid 립싱크)

## 네가 Unity에서 실행할 순서
1. Unity 포커스 → **Ctrl+R** (스크립트 컴파일). Console에 빨간 에러 없는지 확인.
2. 메뉴 **Artti > Setup Clerk Character (Humanoid + Idle)** 실행
   - Console에 `[ClerkSetup] 완료` 뜨면 OK → `Clerk_Rigged.prefab` 생성됨
   - `Clerk_Rigged`를 빈 씬에 드래그 → **Play** → T포즈 안 풀리고 자연스럽게 서 있으면 성공
   - (실패: "아바타 생성 실패" 로그 → 본 매핑 문제. Claude에게 Console 로그 전달)
3. 메뉴 **Artti > Build TrainingConvenienceScene Hierarchy** 실행
   - Console `[ConvenienceTrainingSceneBuilder] 완료` 확인
   - Play 해서 확인

## Play 후 점검 + 조정 포인트 (블라인드 추정값이라 십중팔구 위치 조정 필요)
`ConvenienceTrainingSceneBuilder.cs` 상단 배치 상수를 보고 Unity에서 조정 후 그 값으로 갱신·재빌드:
- `ClerkEuler` — 점원이 등 보이면 Y를 0 또는 180으로 뒤집기
- `CamPos`/`CamEuler` — 점원 얼굴이 화면 중앙 오게
- `EnvPos`/`EnvScale` — Tripo 환경 스케일/위치 (단일 메시라 안 맞을 수 있음)
- `CounterPos`/`PosPos` — 데스크·포스기 위치
- 캐릭터가 너무 어둡다 → `[Directional Light]` 각도/세기 조정
- 립싱크 입 크기 → Clerk 얼굴 메시(Face)의 `uLipSync Blend Shape` 컴포넌트 `Max Blend Shape Value`.
  glTFast가 VRoid 모프를 ~18배 과장 임포트해서 **적정값이 3** (실측 2026-06-15, 빌더 코드에 기본 적용됨).
  Smoothness 0.1. 말 사이 입 계속 열리면 `Max Volume` -1.5→-1.0.

## 아직 안 한 것 (네가 와서 같이 / 런타임 테스트 필요)
- **대화 "상황 리드" 고도화**: 편의점은 현재 풀 모드(STT-only, 고정 대사 시퀀스)라 점원이
  상황을 능동적으로 못 끌어줌. 진짜 리드는 Gemini function-calling 경로(현재 `GeminiDialogueService`
  응답 파싱이 TODO mock) + DialogueManager의 subflow/objective 적용을 구현해야 함.
  → API 키 + 런타임 테스트 필요해서 Claude 혼자 블라인드로 안 함. 돌아오면 같이.
- ARTTI 유니폼(파랑/갈색 + 로고) 정밀 적용 — 현재 점원은 VRoid 기본 드레스. 폴리싱 단계.
- 죽은 RPM SDK 제거(`com.readyplayerme.core`) — glTFast 직접 고정해서 안전하게 제거 가능. 선택.
