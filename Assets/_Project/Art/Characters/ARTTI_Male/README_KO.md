# 편의점 점원 — 남자 캐릭터 (대화 모드)

훈련 허브에서 **대화 모드**로 들어오면 이 남자 점원이 나옵니다. **정확도 모드**는 기존 Stationary v4 점원 그대로입니다.
대상 씬은 `Assets/_Project/Scenes/TrainingConvenienceScene.unity` 하나입니다.

## 원본

Artti_Male Unity 패키지 (Meshy 80k 모델 + Auto Rig 24본, 키 1.70m, 표정 키 10종).
프로젝트에 넣은 것:

- `Models/Artti_Male.fbx` — 원본 그대로 (SHA-256 `3dd30edc…`)
- `Animations/Artti_Male_Idle.fbx`, `Artti_Male_Wave.fbx` — 원본 `Idle.fbx`, `Wave.fbx` 그대로
- `Textures/Artti_Male_BaseColor.png`, `Artti_Male_MetallicSmoothness.png` — 원본 4096을 2048로 줄임 (Stationary v4와 같은 크기)

넣지 않은 것: GLB(glTFast가 중복 모델로 가져감), Walking/Running 동작, 원본 Metallic/Roughness 맵,
패키지 전용 스크립트(`ArttiFaceController` 등 — 기존 CharacterKit과 입 모양 키를 동시에 쓰게 됨), Blender 소스.

## 만드는 방법 (레시피는 .cs)

1. `Artti > Male Clerk > Build Prefab` — `ArttiMaleClerkSetup.cs`
   - 모델·동작 모두 **Generic**. 동작 FBX는 모델과 뼈 이름·길이가 같지만 기본 자세가 20~45도 달라,
     Humanoid로 옮기면 손 인사 중 반대쪽 팔꿈치가 꼬였다. Generic은 뼈 회전을 그대로 재생해 원본 미리보기와 같다.
   - 동작 FBX는 계층 보존(preserveHierarchy)을 켠다. 리그 노드 하나뿐이라 끄면 0.01배 스케일이 버려져 동작이 100배로 재생된다.
   - 차분한 Idle `Animations/Artti_Male_Idle_Calm.anim`: 원본 Idle은 두리번거린다(머리 방향 최대 134°, 머리 위치 20cm).
     첫 프레임(정면 자세)을 기준으로 몸 움직임은 10%만 남기고 머리는 매 프레임 정면을 유지하게 다시 굽는다
     → 머리 방향 0°, 머리 위치 1.7cm (v4 여자 점원 8.1°·0.7cm). 비율은 `ArttiMaleClerkSetup.CalmBodyMotion`.
   - 컨트롤러 `Artti_Male.controller`: Stationary v4와 같은 구조 (Speed, Wave 트리거, Locomotion(차분한 Idle) ↔ Wave)
   - 재질: URP Lit (BaseColor + Metallic/Smoothness), 입 안 메시는 어두운 단색
   - 컴포넌트: `ArttiFaceDriver`(몸 메시), `ArttiAudioLipSync`(v4의 한국어 프로필), `ArttiAnimationDriver`, `AudioSource`
2. `Artti > Integrate Male Clerk for Conversation Mode (TrainingConvenienceScene)` — `ArttiClerkSceneIntegration.cs`
   - `[ClerkStage]/ARTTI_Male`를 꺼진 상태로 추가, `[ClerkStage]`에 `ClerkCast`, `TrainingSceneRoot.clerkCast` 연결
   - 기존 v4 점원·`Clerk3D` 슬롯·UI 배치는 건드리지 않는다
   - 대시보드 빌더가 부르는 `Apply`에도 같은 단계가 들어 있어 재빌드해도 재현된다
3. `Artti > Verify Male Clerk (Conversation Mode)` — `ArttiMaleClerkCheck.cs`, 결과는 `Temp/ArttiMaleClerkCheck/`

## 실행 중 동작

- `TrainingSceneRoot.Awake`가 `ClerkCast.Activate(모드)`를 불러 모드에 맞는 점원 하나만 켜고,
  그 점원의 `ClerkView`·`AudioSource`로 NPC 발화, 립싱크, HUD 스피커 표시를 연결한다.
- 점원 카메라는 `ClerkCast`에 모드별로 저장된 값으로 옮긴다. 지금은 두 점원 모두 높이 1.40m·거리 2.20m·화각 40도.
  대기 자세 머리 꼭대기(머리카락 포함)가 v4 1.895m, 남자 1.884m로 거의 같아 같은 크기로 보인다.
- 대화 모드 점원 목소리는 남성 음성 `ko-KR-Standard-C` (`TrainingModeContext.ConversationVoice`).

## 알려진 점 / 남은 일

- 원본 Idle 동작은 Wave보다 엉덩이가 7cm 높고 발이 바닥에서 약 14cm 떠 있으며, 머리가 16cm 앞으로 숙여져 있다
  (Blender에서 원본 FBX로 측정). 차분한 Idle도 첫 프레임 자세를 그대로 쓰므로 같다. 점원 카메라에는 허벅지 아래가
  안 보여 뜬 발은 보이지 않지만, 인사를 시작할 때 몸이 살짝 뒤로·아래로 옮겨 가는 것처럼 보인다.
- 립싱크 프로필은 v4의 한국어 프로필을 같이 쓴다. 남성 음성으로 다시 보정하면 모음 구분이 더 정확해질 수 있다.
