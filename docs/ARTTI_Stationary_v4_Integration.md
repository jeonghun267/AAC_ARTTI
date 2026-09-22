# 편의점 점원 — Stationary v4 연결

대상 씬은 `Assets/_Project/Scenes/TrainingConvenienceScene.unity`입니다. AR 매장 배치용 `ARStoreScene`은 변경하지 않았습니다.

## 씬에서 사용하는 에셋

- 프리팹: `Assets/_Project/Art/Characters/ARTTI_Stationary_v4/ARTTI_Character.prefab`
- 런타임 스크립트: `Assets/_Project/_Scripts/CharacterKit/`
- 씬의 캐릭터: `[ClerkStage]/ARTTI_Stationary_v4`
- UI 표시: 기존 `Clerk3D` RawImage → `Artti_ClerkRT` → `ClerkCamera`

기존 UI 슬롯과 화면 배치를 유지했습니다. 점원 전용 카메라만 새 모델과 손 인사에 맞춰 높이 1.4m, 거리 2.2m, 화각 40도로 조정했습니다. 원래 정지 점원 이미지 `Clerk`는 비활성 상태로 남겨 두었습니다.

## 동작과 음성 연결

- 기본 상태는 Idle입니다. 기존 `ClerkView.PlayGreeting()`은 Greeting 트리거가 없는 v4에서 Wave를 호출하며, 손 인사가 끝나면 Idle로 돌아갑니다.
- `TrainingSceneRoot.npcSpeechSource`는 새 캐릭터의 AudioSource를 가리킵니다. NPC 대사와 재청취는 이 채널을 사용합니다.
- `ArttiAudioLipSync`가 같은 채널을 분석하고 `ArttiFaceDriver`가 입 모양을 적용합니다. 기존 수동 `ArttiLipSyncController`는 이 캐릭터에 붙이지 않았습니다.
- 카드 읽어주기는 원래 TrainingSceneRoot의 AudioSource를 사용하므로 점원이 사용자 문구를 따라 입을 움직이지 않습니다.
- HUD의 스피커 상태는 NPC 채널을 표시합니다. 듣기 전환·중지·씬 종료 처리는 두 채널을 함께 정리합니다.

v4의 모델·UV·텍스처·입 모양은 그대로 가져왔고, 재질은 프로젝트 URP에 맞췄습니다. 새 캐릭터에는 Idle/Wave만 포함합니다.

## 재현 및 검증

점원 연결 레시피는 `ArttiClerkSceneIntegration.cs`에 반영했습니다. `ConvenienceTrainingDashboardBuilder.cs`도 같은 레시피와 NPC 오디오 연결을 사용합니다. 이번 적용에서는 전체 대시보드를 다시 만들지 않고 점원 무대만 교체했습니다.

- `Artti > Integrate Stationary v4 Clerk (TrainingConvenienceScene)`: 점원 연결만 다시 적용.
- `Artti > Verify Stationary v4 Scene Integration`: 실제 씬 연결, 인사 전환, 발 고정, URP 및 대시보드 캡처 확인.
- `Artti > Verify Stationary v4 Local Speech Runtime`: 실제 NPC 채널에 로컬 한국어 WAV를 재생해 립싱크·동시 손 인사·무음 복귀 확인.

검증 기본 출력은 `Temp/ArttiStationaryIntegrationCheck`입니다. `ARTTI_INTEGRATION_QA_DIR` 환경변수로 다른 폴더를 지정할 수 있습니다. 검증용 런타임 컴포넌트는 에디터 전용이며 실제 씬에는 저장하지 않습니다.

이번 음성 검증은 실제 연결된 NPC AudioSource와 로컬 WAV를 사용했습니다. Google TTS 및 Gemini 통신은 호출하지 않았습니다. 실제 클라우드 음성은 프로젝트의 기존 키·네트워크 설정을 사용합니다.

기존 ReportScene·보고서 코드·폰트의 작업 중 변경사항은 보존했습니다.
