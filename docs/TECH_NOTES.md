# 아르띠(Artti) 기술 정리

씬마다 어떤 기술을 썼고, 그래픽과 NPC를 어떻게 처리했고, 어떤 문제를 어떻게 해결했는지 정리한 문서입니다.

- 기준 시점: 2026-09-30, `main` 7d452f5 (PR #79 머지 직후)
- 범위
  - 포함: 훈련모드(편의점 대화 훈련), 3D 점원 NPC, 화면(Splash, 사용자 선택·등록, 메인, 말하기 훈련, 레포트, 상세기록), 공통 기반
  - 제외: AR현장모드(ARField/ARStore/OCR). 다른 팀원이 담당한 부분이라 넣지 않았습니다.
  - 제외: 지금 코드에서 쓰지 않는 기술. 예: Convai, VRoid 점원과 glTFast, Ready Player Me, 예전 점원 v03~v10, 삭제된 약국·음식점 훈련 씬
- 커밋 해시는 `git show <해시>`로 원문을 볼 수 있습니다.

---

## 0. 한눈에 보기

### 기술 스택 (현재 코드에서 실제로 쓰는 것)

| 분류 | 기술 | 어디에 |
|---|---|---|
| 엔진 | Unity 6000.3 (6.3 LTS), Android(Min SDK 30, ARM64, IL2CPP) | 전체 |
| 렌더링 | URP 17.3, URP Lit, Camera → RenderTexture | 점원 NPC, 메인 화면(SMAA) |
| UI | uGUI 2.0 + TextMeshPro, NotoSansKR-Medium SDF(동적 아틀라스) | 모든 화면 |
| 비동기 | UniTask 2.5.10 + CancellationToken | STT/TTS/LLM, 화면 연출 |
| JSON | Newtonsoft.Json 3.2.2(LLM 도구 인자, 설정 JSON), JsonUtility(로그, 프로필, TTS) | 대화 매니저, 기록 |
| LLM | Google Gemini 2.5 Flash, `generateContent` + function calling | 대화 매니저, 레포트 문구 |
| 음성 합성 | Google Cloud Text-to-Speech (LINEAR16 24kHz) | 점원 목소리 |
| 음성 인식 | LAN STT 서버(Whisper 계열, 명료도 %) + Android SpeechRecognizer 폴백 | 사용자 발화 |
| 립싱크 | uLipSync 3.1.5 (MFCC 음소 추정, 한국어 프로필) | 점원 입 모양 |
| 3D 제작 | Meshy(모델 생성, Auto Rig 24본), Blender(보정·셰이프 키·내보내기) | 점원 NPC |
| 에디터 자동화 | 씬 빌더(`[MenuItem]` + `SerializedObject`), ModelImporter/AnimatorController API | 씬·캐릭터 재현 |
| AI 보조 개발 | Claude Code + mcp-unity(Claude ↔ Unity 에디터) | 개발 전반 |
| 기타 | NativeGallery(레포트 이미지 저장), Unity Test Framework(EditMode 테스트 8개) | 레포트, 로직 검증 |

### 씬별 핵심

| 씬 | 핵심 기술 |
|---|---|
| SplashScene | 시간 함수 하나로 계산하는 6초 연출(UniTask), 페이드·암전 |
| ProfileSelectScene | 배경 꽉 채움(CoverImageFitter) + Stage 따라가기, 끌어서 넘기는 카드 줄, '동의한다' 입력 삭제 팝업 |
| ProfileCreateScene | 3단계를 한 씬에서 전환, 아바타 원형 Mask, 입력 검증 |
| MainScene | 합본 이미지 위에 글자·버튼 얹기, 진입 연출, 화면 모서리 앵커 |
| TrainingHubScene | 디자이너 이미지 본체 기준 배치, 카드 전체를 버튼으로 |
| TrainingConvenienceScene | Gemini 대화 매니저, 서버 STT + 명료도, Cloud TTS, 정확도/대화 모드, 3D 점원 |
| ReportScene | JSONL 로그 집계, XP·레벨, Gemini 응원 문구 병렬 생성 |
| RecordDetailScene | 도넛 차트(Filled Radial), 단계별 발화 정확도와 이유 표시 |

---

## 1. 공통 기반

### 1-1. 씬 빌더: 씬을 C# 코드로 다시 만든다

**왜**: `.unity` 파일(YAML)은 여러 명이 같이 고치면 머지 충돌이 나고, 손으로 배치한 값이 사라지기 쉽습니다.

**방식**
- `Assets/_Project/Editor/*SceneBuilder.cs`가 원본이고 `.unity`는 그 결과물입니다. 메뉴 `Artti > Build XxxScene Hierarchy`로 실행합니다.
- 모든 빌더의 실행 순서
  1. 씬 열기 (`SceneBuilderUtils.OpenScene`)
  2. 루트 오브젝트 전부 삭제 (`ClearRootObjects`)
  3. 코드대로 다시 생성
  4. View의 SerializeField 연결 (`SerializedObject.FindProperty`)
  5. 꺼진 패널까지 잠시 켜서 LayoutGroup 계산 (`ForceRebuildCanvasLayouts`)
  6. 저장
- 버튼 이벤트는 `UnityEventTools.AddPersistentListener`로 영구 리스너로 등록합니다.
- `Artti > Build All Scenes`(`AllScenesBuilder.cs`)는 6개 씬을 한 번에 만들고 Build Settings에 10개 씬을 등록합니다(0번 = Splash).

**손배치 씬 보존**
- 팀원이 Game 뷰에서 손으로 배치한 씬은 빌더를 다시 돌리면 배치가 사라집니다. 그래서 두 가지 방법을 씁니다.
  - "변경분만 적용" 메뉴: `Artti/Fix/ProfileSelect - Apply Updates (keep hand placement)`, `Artti/Fix/ProfileCreate - Apply Done Avatar Circle` 등. 기존 자리는 두고 필요한 부분만 다시 만듭니다. 프리팹은 `PrefabUtility.LoadPrefabContents`로 일부만 교체합니다.
  - 손배치 값을 빌더 코드로 옮겨 적기(ece872b). `RecordDetailLayoutDumper.cs`가 RectTransform 값을 콘솔에 덤프해 줍니다.

### 1-2. 디자이너 이미지를 UI로 옮기는 방법

**좌표 환산**
- 시안은 1672x941 크기입니다. 이것을 1920x1080 "Stage"로 옮기면서 좌표에 `1920/1672 ≈ 1.1483`을 곱합니다.
- Stage 좌표는 좌상단이 원점이고, 아래로 갈수록 y가 음수입니다.

**"본체(body)" 기준 배치**
- 문제: 버튼 PNG에는 글로우 때문에 투명 여백이 큽니다. 이미지 크기로 배치하면 버튼끼리 줄이 안 맞고, 이웃 버튼의 투명 여백이 클릭을 가로챕니다.
- 방법: 알파가 기준(200 또는 250)을 넘는 픽셀의 bounding box를 Python(PIL)으로 실측해서 `Rect` 상수로 적어 둡니다. 예: `BluePillBody = Rect.MinMaxRect(259, 135, 1912, 559)`.
- `MakeImageButton`의 구조
  - 루트: 본체 크기의 투명 Image. 이것이 클릭 영역입니다.
  - 자식 `Visual`: 여백까지 포함한 이미지 전체. 본체 중심과 이미지 중심의 차이만큼 옮겨 놓습니다.
- 결과: 글로우 폭이 달라도 본체끼리 정렬되고, 클릭 영역이 겹치지 않습니다.

**글자가 그려진(baked) 이미지 다루기**
- 이미지에 이미 그려진 글자는 바꿀 수 없어서, 필요하면 위를 덮습니다.
  - 삭제 팝업 입력칸에 그려진 '동의한다': 이미지에서 코드로 만든 패치로 덮습니다(2-2 참고).
  - 레포트 카드의 "/ XP" 글자, 추천 카드의 음식 그림: 카드 색 패치나 아이콘으로 덮습니다.
- 배경에 뒤로 버튼이 그려져 있으면(말하기 훈련 화면 56) 그 위에 투명 버튼을 얹습니다.

**임포트 설정 자동화**
- 각 빌더의 `LoadSprite`가 텍스처를 Sprite(Single), 알파 투명, 밉맵 끔, maxTextureSize 2048로 맞춥니다. 기본값 1024면 큰 합본 이미지가 흐려지기 때문입니다.
- Auto Refresh를 끈 환경이라, 아직 임포트되지 않은 파일은 먼저 임포트하고, 그래도 Sprite가 없으면 강제로 다시 임포트합니다(5f5af09).

### 1-3. 기기 화면비 대응

**대상 해상도**
- 주 대상: Galaxy Tab S7 FE 2560x1600 (16:10)
- 추가 점검: 20:9 폰(2400x1080), 16:9
- 팀원은 Unity Game 뷰 1920x1440(4:3)에서 손배치를 했습니다. 이 차이 때문에 에디터에서는 멀쩡한데 태블릿에서 잘리는 문제가 여러 번 있었습니다.

**CanvasScaler: Expand**
- 대부분의 캔버스는 `ScaleWithScreenSize + Expand`입니다.
- 캔버스가 기준 해상도보다 작아지지 않으므로 Stage가 잘리지 않습니다.

**`CoverImageFitter`** (`_Scripts/UI/CoverImageFitter.cs`, 06a4500)
- 배경을 빈 띠 없이 꽉 채우는(cover) 컴포넌트입니다.
- 가로세로 비율을 최대 6%(`maxStretch = 1.06`)까지 바꾸는 것을 허용해 잘리는 양을 줄입니다.
  - 16:10 태블릿: 좌우가 106씩 잘리던 것을 46씩으로 줄였습니다.
  - 20:9 폰: 위아래가 잘립니다. 기준점(`focus`)을 위쪽(0.70~0.72)에 둬서 로고와 뒤로 버튼이 있는 위쪽을 덜 자릅니다.
- **follower**: Stage를 배경의 자식으로 두고, 배경이 늘어난 비율만큼 Stage도 `localScale`로 늘립니다. 그래서 그림 위의 UI가 어떤 화면비에서도 그림과 같은 자리에 옵니다.
- 구현 세부
  - `[ExecuteAlways]`
  - `DrivenRectTransformTracker`로 자기 RectTransform을 잠급니다.
  - `OnRectTransformDimensionsChange` 때마다 다시 계산합니다.
  - `Start`에서 한 번 더 계산합니다. 씬 로드 직후에는 캔버스 크기가 0일 수 있기 때문입니다.
- 사용처: ProfileSelect, TrainingHub. 나머지 배경은 `AspectRatioFitter.EnvelopeParent`를 씁니다.

**화면 모서리 앵커**
- 우상단 문구나 닫기처럼 화면 끝에 붙어야 하는 요소는 Stage가 아니라 캔버스 모서리 `(1,1)`에 붙였습니다.
- 이유: 폰에서 배경 위쪽이 잘리면 Stage 기준 요소가 화면 밖으로 나갑니다.

**레이아웃 검증 도구** (`Editor/DeviceLayoutCapture.cs`)
- 메뉴 `Artti/Debug/Capture Scenes - Tab S7 FE (2560x1600)`, `- Phone 20x9`, `- 16x9`
- 씬마다 CanvasScaler 공식을 기기 해상도로 직접 계산해 적용하고, Overlay 캔버스를 잠시 Camera 모드로 바꿔 RenderTexture에 그립니다.
- 결과 PNG는 `Temp/LayoutCaptures`에 저장되고 씬은 저장하지 않습니다.
- 평소에는 꺼져 있는 상태(프로필 완료 단계, 삭제 팝업의 입력 중/안내, 카드 0/1/3장)도 따로 찍습니다.

### 1-4. 코드로 만든 스프라이트, 폰트, 효과

**코드로 생성한 스프라이트**
- `CardGlow.png` (`SceneBuilderUtils.EnsureGlowSprite`)
  - 둥근 사각형 SDF에 알파 falloff를 준 256px 흰 텍스처입니다. 9-slice border 124.
  - Image 색으로 틴트해서 후광과 그림자에 모두 씁니다.
- `Circle256.png` (`EnsureCircleSprite`, 3b4f16a)
  - 가장자리 1px만 안티에일리어싱한 원입니다.
  - 원형 Mask와 누름 표시에 씁니다. 내장 `Knob` 스프라이트는 해상도가 낮아서 크게 쓰면 Mask 경계가 각집니다.

**RoundedRect 알약 트릭**
- `RoundedRect.png`는 96px, border 44인 에셋입니다.
- `pixelsPerUnitMultiplier = 88 / 높이`로 두면 높이에 딱 맞는 반원 끝 알약이 됩니다.
- 코드로 그린 패널, 입력칸, 진행 바, 안내 바가 모두 이 스프라이트를 Sliced/Filled로 씁니다.

**폰트**
- `Assets/Fonts/NotoSansKR-Medium SDF.asset` 하나로 통일했습니다(Dynamic, 1024x1024, Multi Atlas).

**버튼 반응**
- `HomeButtonGlow` (`_Scripts/UI/Home/`)
  - 올리면 1.03배, 누르면 0.985배로 Visual 크기만 바꿉니다. 클릭 영역은 그대로입니다.
  - glow Graphic을 넣으면 알파도 보간합니다(말하기 훈련 뒤로 버튼은 평소 0, 누르는 동안 0.3).
  - 참조는 미리 캐싱하고 Update에서는 보간만 합니다.

**연출 유틸**
- `UIFx`(UniTask 기반): `SlideFadeIn`, `PopIn`, `CountUp`, `Typewriter`, `PunchScale`. 모두 CancellationToken을 받습니다.

### 1-5. 앱 공통 코드 (`_Scripts/Common/`)

| 클래스 | 역할 |
|---|---|
| `AppBootstrap` | `DontDestroyOnLoad` 싱글톤. `ProfileManager`와 로그 저장소를 가집니다. 프로필을 바꾸면 이전 로그 저장소를 `DisposeAsync`로 정리하고 새로 만듭니다. 다른 화면은 `AppBootstrap.Instance?.…`로 접근해 씬을 단독으로 실행해도 동작합니다. |
| `ProfileManager` | 프로필 목록을 JsonUtility로 `persistentDataPath/profiles.json`에 저장합니다. 필드: id(GUID), 닉네임, 아바타, 생년월일(0 = 미입력), 소개, 마지막 사용 시각 |
| `JsonAppendLogStore` (`AAC/Logging/`) | 학습 로그를 `persistentDataPath/aac_logs/{profileId}.jsonl`에 한 줄씩 덧붙입니다. 큐에 모았다가 스레드풀에서 파일에 씁니다. |
| `AvatarLibrary` | ScriptableObject. 아바타 id(스프라이트 이름)로 Sprite를 찾습니다. `Resources.Load`로 읽습니다. |
| `TrainingModeContext` | 씬 사이에 훈련 모드(정확도/대화)를 넘기는 정적 변수. 모드별 TTS 목소리도 여기서 정합니다. |
| `SceneBackButton` | 지정한 씬으로 돌아갑니다. |
| `ClearCameraGuard` | 카메라가 없는 씬에 화면만 지우는 카메라를 붙입니다(1-7 표 참고). |
| `ApiKeyLoader` | API 키를 읽습니다(1-6 참고). |

### 1-6. API 키 관리와 Android 빌드 설정

**키 로딩** (`Common/ApiKeyLoader.cs`)
- 에디터: 프로젝트 루트의 `.env`를 읽습니다.
- 빌드: `Resources/api_keys.txt`(TextAsset)를 읽습니다.
- 파서는 BOM, `export KEY=`, 따옴표, 줄 끝 `#` 주석을 처리합니다(0c26310).
- 키 이름: `GEMINI_API_KEY`, `GOOGLE_TTS_API_KEY`, `STT_SERVER_URL`, `STT_SERVER_API_KEY`, `STT_SERVER_MODEL` 등. 값 없이 이름만 적은 `.env.example`이 있습니다(bc2863b).

**빌드 때 키 복사** (`Editor/CopyApiKeysOnBuild.cs`)
- `IPreprocessBuildWithReport`로 빌드 직전에 `.env`를 `Assets/Resources/api_keys.txt`로 복사합니다.
- 이 파일은 `.gitignore`의 `*api_key*` 패턴으로 git에서 빠집니다.

**Android 설정** (`Editor/AndroidPlayerSettingsApplier.cs`, 메뉴 `Artti/Apply Android Player Settings`)
- 번들 `com.artti.aac`, 제품명 "아르띠", Min SDK 30, ARM64만, IL2CPP, 가로 고정
- 그래픽 API: Vulkan → GLES3
- `insecureHttpOption: 2`: LAN STT 서버의 `http://` 접속을 허용합니다(0c26310).
- 권한: `Plugins/Android/recordaudio.androidlib`가 RECORD_AUDIO, INTERNET 권한과 Android 11 패키지 가시성용 `<queries>`(음성 인식, TTS 서비스)를 추가합니다.

### 1-7. 개발 방식

- **AI 보조 개발**
  - Claude Code로 작업하고, mcp-unity로 Claude가 Unity 에디터를 직접 조작합니다(메뉴 실행, 콘솔 로그 확인, 레이아웃 캡처 등).
  - CLAUDE.md에 에이전트 규칙이 있습니다. 예: 빌더 실행 전 사용자 확인, Play 모드 여부와 미커밋 손배치 확인.
- **브랜치와 PR**
  - 작업 브랜치 `jeonghun`, AI 세션 브랜치 `claude/*`, 디자이너 시안 에셋을 먼저 올리는 `wip/*`.
  - 모든 작업은 PR로 `main`에 머지합니다. 스쿼시가 아닌 머지 커밋 방식입니다.

### 1-8. 공통 문제 → 원인 → 해결

| 문제 | 원인 | 해결 |
|---|---|---|
| 씬 전환 잔상: 기기에서 메인 화면 위에 이전 프로필 화면이 보임 | 메인을 뺀 씬에는 카메라가 없음. 카메라가 없으면 매 프레임 화면을 지우지 않아 이전 씬의 마지막 프레임이 남음 | `ClearCameraGuard`: `sceneLoaded` 때 카메라가 없으면 화면만 지우는 카메라를 붙임(cullingMask 0, depth -100, HDR·MSAA 끔). 손배치 씬은 빌더로 다시 만들 수 없어서 씬 파일이 아니라 런타임에서 처리 (ac2bd4c) |
| 태블릿(16:10)에서 버튼이 잘림 | 손배치를 1920x1440(4:3) Game 뷰에서 함 | 우상단 버튼을 화면 모서리에 앵커, 캡처 도구로 기기 해상도 검증 (ac2bd4c, ece872b) |
| API 키가 저장소에 올라감 | `.gitignore`가 루트 경로만 막아 하위 폴더의 키 에셋이 추적됨. 크래시 복구 씬 `_Recovery/*.unity`에 키가 직렬화됨 | 파일 삭제, `**/` 패턴으로 수정, `_Recovery` 무시, 키를 `.env`로 이동 (7e7dfea), `.env.example` (bc2863b) |
| 기기에서 키 파일을 못 읽음 | Android의 StreamingAssets는 APK 내부라 일반 파일 입출력으로 읽을 수 없음 | `Resources/api_keys.txt`(TextAsset)로 이동 (fef5af7) |
| 한글이 네모(tofu)로 보임 | TMP 폴백 설정과 글리프 테이블이 비어 있었음 | TMP Essential Resources 다시 임포트, 폰트를 NotoSansKR-Medium 하나로 정리 (78c7fbe) |
| `.unity` 머지 충돌과 손배치 유실 | 여러 명이 같은 씬 YAML을 수정 | 씬 빌더 규칙, "변경분만 적용" 메뉴, 손배치 값을 빌더로 이식 |
| Play를 다시 눌러도 이전 값이 남음 | Enter Play Mode Options로 Domain Reload를 꺼 둠. 정적 변수가 초기화되지 않음 | `ApiKeyLoader`는 `SubsystemRegistration` 시점에 리셋, `ClearCameraGuard`는 구독 해제 후 다시 구독 |
| LAN STT 서버(http)에 접속이 안 됨 | Unity가 http 연결을 기본으로 막음 | `insecureHttpOption` 0 → 2. 막힌 경우를 감지하면 설정을 고치라고 안내하고 서버 STT를 끔 (0c26310) |
| 상세기록 씬(RecordDetail)으로 이동이 안 됨 | Build Settings 등록 목록에 빠져 있었음 | `AllScenesBuilder` 등록 목록에 추가 (ac2bd4c) |

---

## 2. 씬별 정리

### 2-1. SplashScene

**화면 구성**: 42 배경, 43 ARTTI 로고, 44 문구 "더 나은 소통의 시작", 로딩 점 3개(47 꺼짐 위에 46 켜짐), 45 "불러오는 중...", 전체를 덮는 검은 페이드 막

**기술** (`SplashSceneBuilder.cs`, `SplashSceneView.cs`, d70dc02)
- UniTask 루프 하나에서 `Apply(t)`가 시간 t로 모든 요소의 상태를 계산합니다. 요소마다 코루틴을 따로 돌리지 않아 타이밍이 어긋나지 않습니다.
- 전체 6초(Inspector에서 조정)
  - 0~0.6초: 페이드 인
  - 0.2~1.2초: 로고가 0.92배에서 1배로 커짐(smoothstep)
  - 0.9초: 문구, 1.4초: 로딩 표시. 점은 0.4초마다 하나씩 켜짐
  - 마지막 0.8초: 화면 암전과 음악 볼륨을 같이 줄임
- 끝나면 MainScene으로 이동합니다.
- 음악: `Assets/_Project/Audio/splash_bgm.(mp3|wav|ogg)`가 있으면 빌더가 AudioSource에 연결합니다. 지금은 파일이 없어 무음입니다.

**그래픽 처리**
- 시안에 에셋을 겹쳐 맞춘 좌표를 1920 폭으로 환산했습니다.
- 켜진 점(46)과 꺼진 점(47)은 본체 지름에 맞춰 크기를 정했습니다.
- 47은 밝은 회색이라 남색 틴트로 눌렀습니다.

### 2-2. ProfileSelectScene (사용자 선택)

**화면 구성**
- 48 배경(점원 캐릭터, 제목, 안내 패널이 그려져 있음)
- 왼쪽 메뉴: 51 사용자 선택(현재 화면), 50 프로필 설정
- 가운데 가로 카드 줄: 49 틀, 한 화면 3장, 맨 왼쪽이 "새 사용자"
- 17 다음, 우상단 "당신의 일상에, ARTTI"와 닫기
- 삭제 확인 팝업: 52 판, 53 취소, 54 삭제, 55 닫기

**기술** (`ProfileSelectSceneBuilder.cs`, `ProfileSelectView.cs`, `ProfileCardView.cs`; 63b87fe, 06a4500, 47859f2)
- 카드 줄: `RectMask2D` 뷰포트 + `HorizontalLayoutGroup` + `ContentSizeFitter` + `ScrollRect`(Elastic, 관성)로 끌어서 넘깁니다.
- 카드가 3장보다 적으면 가운데 정렬하고 드래그를 끕니다. 넘치면 왼쪽부터 보여 줍니다.
- 카드 선택: 테두리 빛, 체크(33), 삭제 칩(54)이 켜지고 1.08배로 커집니다.
- 다음: `AppBootstrap.SwitchProfile` → MainScene
- 삭제: 입력칸에 '동의한다'를 정확히 입력해야 `ProfileManager.DeleteProfile`이 실행됩니다.

**그래픽 처리**
- 배경은 CoverImageFitter(최대 6%, focus 0.72)이고 Stage가 follower로 따라갑니다.
- 삭제 팝업 입력칸
  - 52에 입력칸과 '동의한다' 안내 글자가 이미 그려져 있습니다. 그 위에 투명한 `TMP_InputField`만 얹었습니다.
  - 입력 중이거나 틀렸다는 안내를 띄울 때는 박힌 글자를 가려야 합니다. 그래서 52에서 코드로 패치 이미지(`52_field_clean.png`)를 만듭니다(`EnsureFieldPatch`).
  - 패치 생성: 줄마다 글자 좌우 6px의 평균색을 구하고 그 사이를 선형 보간으로 채웁니다.
  - 압축하면 블록이 보여서 무압축으로 임포트합니다.

**문제 → 원인 → 해결**
- 태블릿에서 가볍게 탭해도 드래그로 잡혀 카드가 선택되지 않음
  - 원인: 고해상도 화면에서 기본 드래그 임계값 10px가 너무 작음
  - 해결: `pixelDragThreshold`를 약 2mm(`dpi × 0.08`)로 올림 (06a4500)
- 16:10에서 배경 가장자리 글자가 잘리고, 폰에서는 우상단 UI가 화면 밖으로 나감
  - 원인: Envelope 방식은 많이 자르고, Stage 기준 배치는 폰에서 위가 잘림
  - 해결: CoverImageFitter + follower Stage, 우상단은 화면 모서리에 앵커 (06a4500)
- 카드를 지운 직후 줄 폭 계산에 지운 카드가 남음
  - 원인: `Destroy`는 프레임 끝에 실행됨
  - 해결: 지우기 전에 `SetActive(false)`를 먼저 호출 (47859f2)

### 2-3. ProfileCreateScene (1 아바타 선택 → 2 프로필 설정 → 3 완료)

**구성**
- 세 단계를 한 씬에 겹쳐 두고 켜고 끄며 전환합니다.
- 세 단계 모두 같은 틀(`BuildFrame`)을 씁니다: 10 배경, 코드로 그린 패널, "ARTTI" 로고, 단계 표시, 닫기(18).

**1단계: 아바타 선택** (`ProfileAvatarStepBuilder.cs`, `ProfileAvatarStepView.cs`, 2bfef9b)
- 분류 탭 5종(11~15). 비트 플래그 `Category`로 걸러 냅니다.
- 5x2 `GridLayoutGroup`에 아바타 9종(19~27), 큰 미리보기 카드, 랜덤 선택(16), 다음(17)
- 아바타 목록은 `Resources/AvatarLibrary.asset`에 등록됩니다.

**2단계: 프로필 설정** (`ProfileCreateSceneBuilder.cs`, `ProfileCreateView.cs`, e6987e0, 1ae437d)
- 이름(필수, 12자), 생년월일 연/월/일 `TMP_Dropdown`(0번 = 입력 안 함), 한마디 소개(60자, 여러 줄)
- 입력칸 배경은 코드로 그리고, 입력 위젯 자체는 투명합니다.
- 이름이 비어 있으면 안내 팝업을 띄웁니다.

**3단계: 완료** (`ProfileDoneStepBuilder.cs`, 48ccf53, 725cc7d)
- 35 프로필 카드(원 안에 아바타와 이름), 41 생년월일 줄(달력 칸 이미지 위에 날짜 글자, 입력한 만큼만 표시), 40 시작하기(MainScene)
- 편집 배지를 누르면 1단계로 돌아가고, 다시 저장하면 새로 만들지 않고 같은 프로필을 고칩니다.

**그래픽 처리**
- 단계 표시(ea48916): 지난 단계는 체크(33), 현재 단계는 번호 원(30~32), 남은 단계는 알파 0.4. 원마다 글로우 여백이 달라 본체 지름을 50으로 맞췄습니다.
- 아바타 카드: 카드 높이의 90%로 키우고 카드 모양 Mask로 잘라 어깨가 밖으로 나가지 않게 했습니다.
- 편집 배지: 카드 이미지에 그려진 배지가 아바타에 가려져서, 같은 카드 이미지를 배지 크기 원형 Mask로 한 번 더 얹었습니다.

**문제 → 원인 → 해결**
- 완료 화면에서 원형 아바타의 머리 위가 평평하게, 아래는 계단처럼 잘림
  - 원인 1: 내장 `Knob` 스프라이트가 저해상도라 크게 늘리면 Mask 경계가 각짐
  - 원인 2: 아바타가 원보다 컸음
  - 해결: Mask를 Circle256으로 바꾸고 아바타를 원 지름의 0.92배로 줄임. 손배치 씬이라 Fix 메뉴로 적용 (3b4f16a)
- 이미지가 흰색으로 나옴
  - 원인: `.meta`가 guid만 있는 최소 형식이라 Sprite로 불러오지 못함
  - 해결: meta 교체, `LoadSprite`에 "미임포트면 임포트 → 그래도 없으면 강제 재임포트" 단계 추가 (5f5af09)

### 2-4. MainScene (모드 선택)

**화면 구성**: 2 배경, 1 합본(좌측 패널·훈련 카드·현장 카드·캐릭터 둘이 한 장), 3 타이틀, 4 프로필 보기, 5 훈련 시작하기, 6 도움 시작하기, 우상단 7 종료하기 / 8 리포트 보기 / 9 설정

**기술** (`MainSceneBuilder.cs`, `MainSceneView.cs`, `HomeMenu.cs`, `HomeIntroAnimator.cs`; ac731fb, ece872b)
- 이 씬만 Screen Space-Camera + 전용 URP UI 카메라(SMAA)를 씁니다.
- `HomeIntroAnimator`: 요소마다 지연을 두고 오프셋 이동 + 페이드(EaseOutCubic)로 들어옵니다.
- 이동: 훈련 시작하기 → TrainingHubScene, 프로필 보기 → ProfileSelectScene, 리포트 보기 → ReportScene

**그래픽 처리**
- 합본 이미지는 조각별로 움직일 수 없어서, 그 위에 TMP 글자와 투명 버튼을 얹었습니다.
- 글자 묶음은 카드 기울기에 맞춰 회전했습니다(-4°, 2.5° 등).
- 좌표는 손배치 결과를 빌더로 옮긴 값입니다(ece872b).

**문제 → 원인 → 해결**
- 태블릿에서 우상단 버튼이 화면 밖으로 잘림
  - 원인: 1920x1440 Game 뷰에서 Stage 바깥(위쪽 +87)에 손배치함
  - 해결: 버튼을 캔버스 우상단 앵커로 옮기고 위 여백 24로 고정 (ece872b, ac2bd4c)

### 2-5. TrainingHubScene (말하기 훈련 모드 선택)

**화면 구성**: 56 배경(뒤로 버튼과 왼쪽 문구가 그려져 있음), 57 제목, 58 정확도 모드 카드 + 60 연습하기, 59 대화 모드 카드 + 61 연습하기, 코드로 그린 안내 바 + 전구 아이콘(`tip_bulb.png`)

**기술** (`TrainingHubSceneBuilder.cs`, `TrainingHubView.cs`, cbc6d4d)
- 모드마다 Button 하나. 투명 클릭 영역 두 개(`CardHit`, `PillHit`)가 같은 Button으로 이어져, 카드 어디를 눌러도 시작합니다.
- 누르면 `TrainingModeContext.Mode`에 모드를 저장하고 TrainingConvenienceScene으로 갑니다.
- 뒤로 버튼: 배경에 그려진 원 위에 투명 버튼을 얹고, 누르는 동안 Circle256 빛을 알파 0.3으로 띄웁니다.

**그래픽 처리**
- 배경은 CoverImageFitter(focus 0.70) + follower Stage
- 카드 배치 조건
  - 56의 왼쪽 문구(Stage x 약 505까지)를 가리면 안 됩니다.
  - 태블릿에서 좌우가 약 44씩 잘려도 대화 모드 카드 오른쪽 끝이 남아야 합니다.
  - 그래서 카드를 0.52배로 줄였습니다.
- 최종 카드 그림은 체크리스트가 카드 아래까지 차 있어서, 연습하기 버튼을 카드 안이 아닌 카드 아래에 놓았습니다.
- 뒤로 버튼(Stage y 54~)이 폰에서 잘리지 않도록 focus를 정하고, 안내 바는 폰에서 보이는 범위(Stage y 49~965) 안에 넣었습니다.
- 배치 전에 Python으로 16:9, 16:10, 20:9 합성 미리보기를 만들어 확인하고, 빌드 후 캡처 도구로 다시 확인했습니다.

### 2-6. TrainingConvenienceScene (편의점 대화 훈련)

앱의 핵심 씬입니다. 정확도 모드와 대화 모드가 같은 씬을 씁니다. 3D 점원은 3장에서 따로 정리합니다.

#### 전체 흐름

- 대화는 두 가지를 섞어서 진행합니다.
  - 규칙 기반: 고정 문장 버튼과 상품 버튼은 앱이 직접 처리합니다(LLM 호출 없음, 빠르고 안정적).
  - LLM 기반: 규칙에 없는 발화만 Gemini 2.5 Flash의 function calling으로 처리합니다.
- 한 턴의 흐름(`RunGuidedVoiceTurn`)
  1. 화면에서 강조된 추천 문장을 명료도 비교 기준(목표 문장)으로 STT에 넘깁니다.
  2. 점원 음성이 끝날 때까지 기다린 뒤 녹음합니다.
  3. 인식 결과가 비었으면 다시 말하게 합니다.
  4. 정확도 모드는 명료도 관문을 거치고, 대화 모드는 발화를 화면 문장으로 해석합니다.
  5. 고정 문장이면 규칙 정책으로, 아니면 Gemini로 다음 턴을 만듭니다.
  6. 단계가 바뀌면 상단 스테퍼와 문장 세트를 갱신하고, 마지막 단계면 완료 화면을 띄웁니다.

#### 대화 매니저 (`_Scripts/Training/`)

**Gemini 호출** (`GeminiDialogueService.cs`)
- REST `v1beta/models/gemini-2.5-flash:generateContent`, `tool_config.mode = ANY`(항상 도구를 호출하게 강제)
- 호출 방식 3가지
  - `RequestNextTurnAsync`: 도구 호출 결과를 `DialogueTurn`으로 받습니다(대화 턴).
  - `RequestFreeTalkAsync`: 평문 응답
  - `RequestPhraseMatchAsync`: 후보 문장 번호 하나만 받습니다(대화 모드 AI 판단).
- 도구 8종(`Resources/AAC/dialogue_tools.json`): `present_cards`, `mark_objective_complete`, `transition_to_objective`, `enter_subflow`, `return_from_subflow`, `request_clarification`, `express_understanding`, `force_complete_scenario`
- 도구 인자와 설정 JSON은 Newtonsoft.Json으로 파싱합니다.

**프롬프트** (`SystemPromptProvider.cs`)
- 시스템 프롬프트 순서: 공통 규칙 → 편의점 점원 페르소나 → few-shot 예시 4개 → 사용 가능한 카드 목록
- 사용자 프롬프트: 최근 대화 6줄, 현재 단계, 턴 수(최대 12), 시도 횟수, 힌트 단계, 채워진 슬롯, 진행 중인 서브플로

**대화 상태 머신** (`DialogueManager.cs`)
- 단계 순서: `greeting → select_items → extras → checkout → farewell`
- LLM이 제안한 다음 단계를 그대로 믿지 않고 검증합니다.
  - 없는 단계(환각): 무시하고 순서상 다음 단계로
  - 이미 지난 단계(역행): 거부
  - 먼 단계로 건너뛰기: 바로 다음 단계로 제한
- `TurnCount`(12턴 상한)와 `AttemptCount`(실패한 턴만)를 따로 셉니다.
- `ScaffoldedRetryPolicy`: 실패 횟수에 따라 힌트를 0/1/2단계로 올리고, 4회면 시나리오를 마무리합니다.

**고정 문장 규칙** (`ConvenienceQuickPhrasePolicy.cs`)
- "계산할게요", "봉투 주세요" 등 9종은 LLM 없이 현재 단계와 장바구니 상태만 보고 응답을 만듭니다.
- 발화를 후보 문장에 맞추는 편집 거리 함수도 여기 있습니다.

**추천 카드 고르기** (`ResolveCardPool`)
1. LLM이 고른 카드 중 현재 단계에 맞는 것만 받습니다.
2. 룰북(`ConvenienceCardRuleBook`): 점원이 실제로 한 말에서 키워드를 찾아 카드를 고릅니다.
3. 남은 칸은 단계 태그로 채웁니다.
- 점원이 예/아니요 질문을 했으면 네·아니요 카드를 맨 앞에 둡니다.

**실패 대비** (`FallbackResponsePicker`)
- LLM 호출 실패, STT 빈 결과, 턴 상한 도달 때 쓸 대사를 `fallback_responses.json`에서 고릅니다.

#### 정확도 모드와 대화 모드

| 항목 | 정확도 모드 | 대화 모드 |
|---|---|---|
| 목적 | 또렷하고 정확하게 말하기 | 실제 상황처럼 자유롭게 대화하기 |
| 점원 | 여자 점원 ARTTI_Stationary_v4 | 남자 점원 ARTTI_Male |
| TTS 목소리 | ko-KR-Standard-A | ko-KR-Standard-C (남성) |
| 발화 처리 | 인식된 문장을 그대로 사용 | 규칙 → AI 판단 → 추천 문장 순으로 해석 |
| 명료도 관문 | 적용 (60% 미만이면 다시 말하기) | 적용 안 함 |
| 레포트 | 정답률·완료율 집계 | 정답률은 "--", 모드별 명료도 평균만 따로 집계 |

**정확도 모드: 명료도 관문** (`ClarityGatePolicy.cs`, 6c0e045)
- 목표 문장을 말했는데 서버가 준 명료도가 60% 미만이면, 같은 단계에서 다시 말하게 합니다.
- 발달장애 사용자가 한 단계에 갇히지 않도록, 같은 단계에서 2번 미달이면 그다음 시도는 통과시킵니다.
- 명료도가 없는 발화(기기 STT, 다른 정답 문장)는 막지 않습니다.
- 두 번째 재시도부터는 "천천히, 또박또박 한 번만 더 말씀해 주세요."처럼 더 구체적으로 안내합니다.
- 기준(60)과 횟수(2)는 `TrainingSceneRoot`의 Inspector에서 바꿀 수 있고, 단계가 바뀌면 횟수를 0으로 되돌립니다.

**대화 모드: 어눌한 발화 해석** (`ResolveConversationUtteranceAsync`, 02aa9b1)
1. 편집 거리가 후보 문장 글자 수의 절반 이하인 후보가 있으면 그 문장으로 봅니다.
2. 애매하면 Gemini(`RequestPhraseMatchAsync`)가 뜻을 보고 고릅니다. 최대 3초(`CancelAfterSlim`).
3. 둘 다 못 고르면 추천 문장을 말한 것으로 봅니다.
- 실제로 들린 문장과 명료도는 해석과 별개로 레포트에 남습니다.

**레포트의 모드 구분** (4a8275d)
- 세션, 기록, 상세에 모드를 붙였습니다. 모드 기록이 없는 예전 세션은 정확도 모드로 봅니다.

#### 음성 인식 (STT, `_Scripts/Common/Speech/`)

**구성**
- `FailoverSttService`: 서버 STT를 먼저 쓰고, 안 되면 기기 인식을 씁니다.
  - Android: `AndroidNativeSttService`(Android SpeechRecognizer를 `AndroidJavaProxy`로 감쌈)
  - 에디터: `EditorMockSttService`
- `ServerSttService`: 상태는 Online, Offline(30초 뒤 다시 확인), disabled(이번 실행 동안 끔) 셋입니다.

**녹음** (`MicrophoneUtteranceRecorder.cs`)
- Unity `Microphone`으로 16kHz 녹음 → 자체 VAD로 발화 구간 판정 → WAV 인코딩 → multipart POST
- VAD: 20ms 단위 RMS. 8초 동안 말이 없으면 NoSpeech, 말이 끝나고 2초 조용하면 종료. 기침처럼 짧은 소리만 들렸으면 3.5초까지 더 기다립니다.
- 배경 소음 수준(floor)과 최대 레벨(peak)을 따라가며 기준을 조정합니다.

**서버 API 자동 탐색** (`SttServerProtocol.cs`)
- 서버(`http://192.168.0.167:8000`, 같은 Wi-Fi)의 API 형식을 몰라서, 클라이언트가 찾아가게 만들었습니다.
  - API 문서 위치 후보 8곳(`/openapi.json` 등)을 차례로 봅니다.
  - 문서가 없으면 흔한 경로 11개 × 파일 필드 이름 5개를 시도합니다.
  - 응답에서 문장 키 후보와 명료도(`clarity_percent`, `clarity`, `light`, `target`)를 파싱합니다.
- UnityEngine을 쓰지 않는 순수 로직이라 에디터 없이 테스트할 수 있습니다(`SttServerProtocolClarityTests`).
- 서버 주소는 씬의 Inspector 값, 비어 있으면 `.env`의 `STT_SERVER_URL`을 씁니다.

#### 음성 합성 (TTS)

- `CloudTtsService`: Google Cloud Text-to-Speech REST `v1/text:synthesize`, LINEAR16 24kHz → `AudioClip.Create` → 점원 AudioSource
- 요청 타임아웃 10초. `SetVoice`로 모드별 목소리를 바꿉니다.
- 점원 대사와 카드 읽어주기는 AudioSource를 분리했습니다. 그래서 점원이 사용자 문구를 따라 입을 움직이지 않습니다.

#### 화면 (HUD·대시보드)

- 빌더: 메뉴 `Artti/Build TrainingConvenienceScene Hierarchy` → `ConvenienceTrainingDashboardBuilder.Build()`
  - 기준 해상도 1536x1024, Expand
- 구성
  - 배경, 3D 점원(RawImage), 미션 스테퍼 5단계, 점원 말풍선(다시 듣기)
  - 대화 힌트 5개: 단계별로 문장 세트가 바뀌고, 추천 문장은 굵은 파랑
  - 말하기 연습 카드, 추천 상품 12개(4개 카테고리), 음성 버튼
  - 도움말, 일시정지, 완료(컨페티) 모달
- View(`ConvenienceDashboardView`, `ConvenienceHudView`, `TrainingUIView`)는 이벤트를 내보내고 화면만 바꿉니다. 흐름은 `TrainingSceneRoot`가 가집니다.

#### 기록 (레포트용 로그)

- `EventLogger`가 이벤트(`AACEvent`)를 만들고 `JsonAppendLogStore`가 JSONL로 씁니다.
  - `ScenarioEntered`: `mode:accuracy` 또는 `mode:conversation`
  - `CardSelected`: `{id}:{문장}|stt:{인식 결과}|clarity:{%}:{신호등}:{목표 문장}`
- 완료하면 `SessionEnded` 후 즉시 저장합니다. 완료하지 않고 나가면 `OnDestroy`에서 `SessionAbandoned`를 남깁니다.

#### 비동기·구조 패턴

- 비동기는 모두 UniTask입니다. `UnityWebRequest.WithCancellation`, `UniTaskCompletionSource`(Android 콜백 대기), `CancelAfterSlim`(타임아웃), `SwitchToThreadPool`(파일 쓰기)
- 씬 단위 `CancellationTokenSource`를 두고 `OnDestroy`에서 취소합니다. TTS 전용 토큰으로 아직 도착하지 않은 음성까지 취소합니다.
- 로직은 MonoBehaviour가 아닌 일반 클래스로 분리했습니다: `DialogueManager`, 각종 Policy, `SttServerProtocol`, `GeminiDialogueService`, `EventLogger`
- EditMode 테스트 8개(`Assets/_Project/Editor/Tests/`): `DialogueManagerTests`, `ClarityGatePolicyTests`, `ConvenienceQuickPhrasePolicyTests`, `SttServerProtocolClarityTests`, `ClarityExplainerTests`, `ReportClarityTests`, `ReportModeTests`, `ConvenienceHudViewTests`

#### 문제 → 원인 → 해결

- **점원 대답이 한 턴씩 밀림** (94a5609)
  - 원인: `mark_objective_complete` 도구에 `npc_speech` 인자가 없어서, 단계가 넘어가는 턴에 점원이 말을 안 함
  - 해결: 이 도구에 `npc_speech`, `card_ids`를 필수로 추가. 도구가 말을 안 주면 고정 멘트로 채움
- **LLM이 준 인자가 버려짐** (94a5609)
  - 원인: 대사와 카드만 받고 `objective_id`, `slots_filled` 등은 버림
  - 해결: 응답 전체를 담는 `DialogueTurn`을 만들고, 단계 진행 판정을 `DialogueManager`로 옮김
- **잘하고 있는데 힌트가 쏟아짐** (94a5609)
  - 원인: 카드를 누를 때마다 시도 횟수를 올림
  - 해결: 턴 수와 실패 횟수를 분리
- **결제 단계에 "물 주세요" 카드가 뜸** (94a5609)
  - 원인: LLM이 고른 카드를 검증 없이 씀
  - 해결: 단계 적합성 필터, 룰북은 점원이 실제로 한 말로 매칭, 봉투를 묻고 결제하도록 단계 순서 수정
- **천천히 말하면 중간에 잘림** (ba02417)
  - 원인: Android 음성 인식의 침묵 기준이 너무 짧음
  - 해결: 침묵 3000ms, 최소 입력 5000ms 옵션 추가
- **점원 목소리가 사용자 발화로 녹음됨** (0c26310)
  - 원인: 서버 STT는 Unity 마이크로 직접 녹음하는데, 점원 TTS가 아직 재생 중
  - 해결: 요청 중인 TTS 수와 재생 상태를 보고 음성이 끝난 뒤 250ms 더 기다렸다가 녹음. 듣는 동안은 다시 듣기 버튼을 막음
- **서버 STT의 API를 모름** (67ced75, 0c26310)
  - 해결: API 문서나 흔한 경로에서 엔드포인트를 자동으로 찾음. 실패 종류(404/400/5xx/인증/리다이렉트)에 따라 다시 찾기, 잠시 쉬기, 끄기로 나눔
- **말하지 않았는데 문장이 인식됨** (67ced75)
  - 원인: 무음을 보내면 Whisper 계열 모델이 "시청해주셔서 감사합니다" 같은 문장을 지어냄
  - 해결: 무음이면 서버를 부르지 않음(NoSpeech), 알려진 환각 문장은 걸러냄
- **명료도를 주는 엔드포인트를 못 고름** (1429b14)
  - 원인: 필수 `target` 필드를 "채울 수 없는 필드"로 보고 감점함
  - 해결: 목표 문장 필드로 인식해 발화마다 채우고, 이런 엔드포인트에 가점
- **다른 정답을 말했는데 명료도가 낮게 기록됨** (07c7cf5, 97badeb)
  - 원인: 무조건 목표 문장과 비교함. 빈 발화 턴이 이전 턴의 값을 가져가기도 함
  - 해결: `IsAttemptAtTarget`으로 다른 후보에 더 가까우면 명료도를 버림, 빈 발화 턴 처리 수정
- **명료도 기록이 왜 없는지 알 수 없음** (d00dde0)
  - 해결: 음성 입력인지 버튼 입력인지 구분해 상세기록에 원인을 표시
- **정확도 모드 관문 규칙 충돌** (4f00956 → 7c09515 → 6c0e045)
  - 처음(4f00956): 관문 실패를 `DialogueManager.AttemptCount`로 셈. 이 카운터는 4회면 시나리오를 강제 종료하므로 관문 한도(3회)와 충돌
  - revert(7c09515): "정확도 모드는 대화 힌트 카드로 넘길 수 있어 관문이 필요 없음"으로 일단 제거
  - 다시 추가(6c0e045): 별도 클래스 `ClarityGatePolicy`로 분리하고 실패 횟수를 따로 셈
- **씬을 나갈 때 MissingReferenceException** (d3c6078)
  - 원인: `?.`는 이미 파괴된 Unity 오브젝트를 걸러내지 못함(Unity 오브젝트의 가짜 null)
  - 해결: `!= null`(Unity의 비교 연산자)로 확인
- **훈련 씬 정리** (4591856)
  - 약국·음식점 훈련 씬을 빼고 편의점 하나로 집중

### 2-7. ReportScene (연습 리포트)

**화면 구성**
- 캐릭터 카드(이름, 레벨, XP 바, 응원 문구)
- 스탯 3장(연습한 시나리오, 총 연습 시간, 평균 정답률)과 "오늘 +n" 변화 칩
- 최근 연습 기록 3행(모드명, 상태, 정답률, 날짜), AI 피드백, 이번 주 목표(n/5), 추천 다음 연습
- "전체 보기" 오버레이(세션 카드 목록)

**데이터** (`_Scripts/Report/ReportDataService.cs`)
- 프로필별 JSONL 로그를 한 줄씩 JsonUtility로 읽어 집계합니다.
- XP = 기록 포인트 합(완료 30 + 목표 수 × 10 − 재시도 × 5), 레벨 = 1 + XP / 300
- 정답률은 정확도 모드 세션만 계산합니다. 대화 모드는 해석에 실패해도 진행되므로 정답률로 쓸 수 없어 "--"로 표시합니다.
- 응원, AI 피드백, 주간 미션 문구는 Gemini로 `UniTask.WhenAll`을 써서 동시에 만듭니다. 실패하면 기본 문구를 유지합니다.

**그래픽 처리** (`ReportSceneBuilder.cs`)
- 카드는 디자이너가 만든 PNG입니다. 값은 카드 이미지 안의 비율 좌표(`Slot(fx, fy, fw, fh)`)에 TMP 글자로 얹습니다.
- 길이가 바뀌는 글자(LLM 문구, 닉네임)는 AutoSize로 칸에 맞춥니다.
- 진행 바(XP, 주간 목표): RoundedRect 트랙 위에 `Image.Type.Filled` Horizontal
- 이미지 저장: `WaitForEndOfFrame` 코루틴 → `ScreenCapture` → NativeGallery. 프레임이 다 그려진 뒤 찍어야 해서 UniTask 대신 코루틴을 씁니다.

**문제 → 원인 → 해결**
- 태블릿에서 뒤로/공유/이미지 저장 버튼이 안 보이고 아래가 잘림
  - 원인: 손배치를 4:3(1920x1440) 기준으로 함
  - 해결: 씬의 기준 해상도를 1920x1440 + Expand로 변경 (ac2bd4c)

### 2-8. RecordDetailScene (세션 상세 기록)

**화면 구성**
- 전체 성공률 도넛과 범례(완전 성공 / 부분 성공 / 도움 필요), 발화 정확도 평균
- 스탯 4장(시나리오·모드, 상태, 날짜, 걸린 시간)
- AI 리드백(잘한 점 / 연습하면 좋아요 / 다음 목표)
- 6단계 타임라인 카드: 목표, 사용자 발화, 정확도 % 배지, 평가, 다시 듣기(TTS)

**데이터**
- 단계 평가는 재시도 횟수로 정합니다: 0회 = 잘함, 1회 = 부분, 2회 이상 = 도움 필요
- 성공률 = (완전 + 0.5 × 부분) / 단계 수
- 발화 정확도
  - 서버 STT가 준 명료도 %와 신호등 색을 그대로 씁니다. 앱이 따로 점수를 매기지 않습니다.
  - 평균 문구나 단계 카드를 누르면 팝업(`ClarityExplainer`)이 단계별 목표 문장, 들린 문장, 이유를 보여 줍니다. 대화 모드는 "대화에 쓴 문장"도 같이 보여 줍니다.
  - 채점이 없는 단계는 이유를 구분합니다: 버튼 입력이라 채점 대상이 아님 / 음성이었지만 서버 응답 없음
- AI 피드백: 규칙 기반 문구를 먼저 보여 주고, Gemini(`ReportFeedbackService`) 결과가 오면 타자기 효과로 바꿉니다.

**그래픽 처리** (`RecordDetailSceneBuilder.cs`, `RecordDetailView.cs`)
- 도넛: `DonutRing.png`를 회색 배경 링과 `Filled Radial360`(위에서 시계 방향) 채움 링 두 겹으로 씁니다. `UIFx.CountUp` 1.2초 동안 채움과 퍼센트 글자를 같이 올립니다.
- 카드는 CardGlow 그림자 + RoundedRect 배경으로 코드로 그렸습니다.
- 런타임 효과: 파스텔 배경(`UIBackdrop`), 연결선 위 흐르는 입자, 메달 반짝임, 카드 탭 펀치, 섹션별 순차 `SlideFadeIn`

**문제 → 원인 → 해결**
- 16:10 태블릿에서 위에 붙인 요소와 가운데 블록이 7쌍 겹침
  - 원인: 캔버스 높이(1200)가 손배치 기준 높이보다 짧음
  - 해결: 기준 해상도를 1920x1440 + Expand로 올림 (bc8dcae)
- Android 빌드에서 "level corrupted / Position out of bounds" 크래시
  - 원인: 한 파일에 MonoBehaviour가 여러 개 있으면 씬에 잘못된 스크립트 참조가 들어감
  - 해결: `UIBackdrop`, `UICountUp` 등을 각자 파일로 분리. 한 파일에 남은 효과(`UISparkle` 등)는 씬에 넣지 않고 런타임 `AddComponent`로만 붙임. `Artti/Fix - Reserialize ReportScene`으로 씬 재직렬화

---

## 3. 3D 점원 NPC

### 3-1. 개요

- 편의점 훈련 씬에는 점원이 두 명 있고, `ClerkCast`가 훈련 모드에 맞는 한 명만 켭니다.
  - 정확도 모드: 여자 점원 `ARTTI_Stationary_v4` (`Art/Characters/ARTTI_Stationary_v4/`)
  - 대화 모드: 남자 점원 `ARTTI_Male` (`Art/Characters/ARTTI_Male/`, 89ba9f5)
- 점원은 2D 화면 안에 3D로 보입니다. 전용 카메라가 투명 배경으로 RenderTexture에 그리고, 대시보드의 RawImage `Clerk3D`가 그 텍스처를 보여 줍니다.
- 동작: 대기(Idle) 반복, 손 인사(Wave) 1회
- 표정: 자동 눈 깜빡임, TTS 음성 기반 립싱크(uLipSync + 한국어 프로필)

### 3-2. 제작 파이프라인

**① 모델 생성: Meshy**
- 80k 폴리곤 모델, Auto Rig 24본. 남자 점원은 키 1.70m, 표정 키 10종 패키지로 받았습니다.
- 두 점원 모두 뼈 이름 체계가 같습니다: Hips, Spine02, Spine01, Spine, neck, Head, 좌우 Shoulder/Arm/ForeArm/Hand, 좌우 UpLeg/Leg/Foot/ToeBase 등

**② 보정·내보내기: Blender**
- 여자 점원은 Blender 5.1, 남자 점원은 Blender 4.5 LTS로 FBX를 내보냈습니다.
- 입 안 메시(윗니, 아랫니, 혀, 구강)를 따로 둡니다.
- 모든 스킨 메시에 셰이프 키 10종: A, E, I, O, U, JawOpen, Blink_L, Blink_R, Smile, MouthClose. 검증 코드가 "메시마다 10개"를 확인합니다.

**③ Unity 임포트**
- 여자 점원: Humanoid. 모델에서 아바타를 만들고(Spine02→Spine, Spine01→Chest, Spine→UpperChest), Idle/Wave는 그 아바타를 복사해 씁니다. Idle 1~121프레임 루프, Wave 1~124프레임 1회
- 남자 점원: 레시피 코드 `Editor/ArttiMaleClerkSetup.cs`(메뉴 `Artti/Male Clerk/Build Prefab`)가 임포트 설정을 코드로 합니다.
  - 모델: Generic, 내장 재질 사용 안 함
  - 동작 FBX: Generic + CopyFromOther + `preserveHierarchy = true`
  - 필수 뼈 22개가 있는지 검사하고, 다시 실행해도 GUID가 유지됩니다.

**④ 머티리얼: URP Lit**
- 여자 점원(`Editor/ArttiMaterialSetup.cs`): BaseMap, Normal, MetallicSmoothness. 이·혀·구강은 단색(smoothness 0.15)
- 남자 점원: BaseColor + MetallicSmoothness. 텍스처를 4096에서 2048로 줄였습니다.
- 몸 재질은 단면(Front) 렌더링입니다.

**⑤ 애니메이터**
- 두 컨트롤러 구조가 같습니다.
  - 파라미터: Speed(float), Wave(trigger)
  - Any State → Wave: 트리거, 0.25초 전이. Wave → Idle: exitTime 0.94, 0.3초
  - root motion 끔, 화면 밖에서도 애니메이션 갱신(AlwaysAnimate)

**⑥ 립싱크 프로필**
- `ARTTI_Stationary_v4/Artti_KoreanVoice.asset`(uLipSync Profile)
  - 음소 7개: A/E/I/O/U/M/S
  - 16kHz, MFCC 12, mel 30채널, 비교 방식 L2Norm
  - 음소별 녹음(`Audio/A~S.wav`)으로 캘리브레이션했습니다.
- 남자 점원도 같은 프로필을 씁니다.

**⑦ 씬 통합** (`Editor/ArttiClerkSceneIntegration.cs`)
- `Apply`(메뉴 `Artti/Integrate Stationary v4 Clerk`, 대시보드 빌더에서도 호출)
  - 기존 무대와 `Clerk3D`를 지우고 다시 만듭니다. 몇 번 실행해도 결과가 같습니다.
  - 예전 2D 점원 이미지와 같은 자리에 RawImage를 놓고, 2D 이미지는 끈 상태로 남겨 되돌릴 수 있게 했습니다.
  - `TrainingSceneRoot`의 점원 View, 점원 AudioSource, HUD 스피커 채널을 연결합니다.
- `ApplyConversationClerk`(메뉴 `Artti/Integrate Male Clerk for Conversation Mode`): 남자 점원을 꺼진 상태로 추가하고 `ClerkCast`에 연결합니다.
- 검증 메뉴: `ArttiStationaryIntegrationCheck.cs`, `ArttiMaleClerkCheck.cs`. 결과는 `Temp/` 아래 리포트와 PNG

### 3-3. 그래픽 처리

- **RenderTexture** `Artti_ClerkRT`: 880x1320, MSAA 4x, sRGB, depth 24. 화면 칸(440x660)의 2배 해상도로 그려서 선명하게 보입니다.
- **점원 카메라**
  - 위치 (0, 1.40, 2.20), 바라보는 점 (0, 1.40, 0), FOV 40
  - SolidColor (0,0,0,0) 투명 배경, HDR 끔, 포스트프로세싱·그림자 끔
  - 키 1.9m 모델의 머리와 손 인사가 모두 들어오고 기존 440x660 칸을 유지하는 값입니다.
  - 모드별 카메라 값은 `ClerkCast`에 따로 저장됩니다(지금은 두 모드 값이 같음).
- **격리**: 무대를 월드 x=50에 둬서 레이어를 추가하지 않고도 다른 카메라 시야 밖에 있게 했습니다.
- **조명**(두 라이트 모두 그림자 없음)
  - 키 라이트: Directional, 회전 (38°, 205°), 세기 1.15, 따뜻한 색
  - 필 라이트: 회전 (15°, 140°), 세기 0.45, 차가운 색

### 3-4. 런타임 구조

| 클래스 | 경로 | 역할 |
|---|---|---|
| `ClerkCast` | `_Scripts/Training/ClerkCast.cs` | 모드별로 점원 하나만 켜고 카메라 위치·FOV를 적용 |
| `ClerkView` | `_Scripts/Training/ClerkView.cs` | Animator 트리거를 HashSet에 캐싱. 인사 트리거가 없으면 Wave로 대체, 없는 트리거는 무시 |
| `ArttiFaceDriver` | `_Scripts/CharacterKit/ArttiFaceDriver.cs` | 셰이프 인덱스 캐싱, LateUpdate에서 지수 스무딩(18/s), 입 모양 합이 100을 넘지 않게 정규화. 눈 깜빡임 0.22초, 2.8~5.2초 랜덤 간격 |
| `ArttiAudioLipSync` | `_Scripts/CharacterKit/ArttiAudioLipSync.cs` | 점원 AudioSource에 uLipSync를 붙이고, 음소 비율 × 음량으로 A/E/I/O/U를, M 음소로 MouthClose를 움직임. 무음·정지·0.2초 콜백 없음이면 입을 닫음 |
| `ArttiAnimationDriver`, `ArttiDemoControls` | `_Scripts/CharacterKit/` | 데모 씬(`Artti_CharacterDemo.unity`)용 동작 제어와 IMGUI 버튼 |

- 흐름: 점원 대사 → `CloudTtsService` → 점원 AudioSource → uLipSync(MFCC) → `ArttiAudioLipSync` → `ArttiFaceDriver`(LateUpdate에서 블렌드셰이프) → 점원 카메라 → `Artti_ClerkRT` → `Clerk3D`
- 음량 → 입 벌림: `√clamp01((rms − 0.003) / (0.045 − 0.003)) × 100`

### 3-5. 문제 → 원인 → 해결

- **남자 점원 동작이 100배 크기로 재생됨** (89ba9f5)
  - 원인: 동작 FBX에는 리그 노드 하나(0.01 스케일 + 축 회전)만 있음. 계층을 보존하지 않으면 Unity가 이 노드를 루트로 합치면서 스케일을 버리고 뼈 경로도 어긋남
  - 해결: `preserveHierarchy = true`
- **Humanoid로 옮기면 반대쪽 팔꿈치가 꼬임** (89ba9f5)
  - 원인: 동작 FBX의 기본 자세가 모델과 20~45° 다름
  - 해결: 뼈 이름과 길이가 같으므로 Generic으로 임포트해 뼈 회전을 그대로 재생
- **남자 점원이 대기 중 두리번거림** (89ba9f5)
  - 원인: 원본 Idle이 고개를 최대 134° 돌리고 20cm 움직임
  - 해결: 차분한 Idle(`Artti_Male_Idle_Calm.anim`)을 코드로 베이크. 몸 움직임은 0프레임 기준 10%만 남기고 머리는 0프레임 방향으로 고정. 결과 0°, 1.7cm(여자 점원은 8.1°, 0.7cm)
  - 몸 움직임 25%에서는 머리가 4.4cm 흔들려 10%로 낮춤
  - 모든 프레임을 키로 쓰면 파일이 10MB라, 3프레임 간격 키 + SmoothTangents로 줄임
- **남자 점원 FBX에 4096 텍스처가 내장됨**
  - 해결: 내장 재질을 쓰지 않고 2048 텍스처로 URP 재질을 직접 만듦
- **패키지에 들어 있던 표정 스크립트가 같은 입 셰이프를 동시에 씀**
  - 해결: 그 스크립트는 가져오지 않고 우리 CharacterKit(`ArttiFaceDriver`)만 입 셰이프를 쓰게 함. 다른 컴포넌트가 있으면 검증이 실패함 (d6f5fba)
- **점원 입이 카드 읽어주기 음성에도 움직일 위험** (d6f5fba)
  - 해결: 점원 AudioSource와 카드 AudioSource를 분리. 검증 코드가 분리를 확인
- **인사 트리거가 컨트롤러에 없음** (d6f5fba)
  - 해결: `ClerkView`가 트리거를 캐싱해 두고 없으면 Wave로 대체
- **점원이 화면 칸에 다 안 들어옴** (d6f5fba)
  - 해결: 카메라를 1.20m / 1.55m / FOV 34에서 1.40m / 2.20m / FOV 40으로 변경
  - 검증: 손 인사 중에도 상체가 화면 안(x 0.01~0.99, y 0.98 이하)에 있는지, 발이 5mm 이상 미끄러지지 않는지 확인
- **대화 모드 목소리가 여성** (89ba9f5)
  - 해결: `ko-KR-Standard-B`에서 `ko-KR-Standard-C`(남성)로 변경
- **에디터에서 애니메이션 포즈 캡처가 안 바뀜**
  - 해결: `BakeMesh`로 현재 포즈를 구운 임시 메시를 만들고 `RenderPipeline.SubmitRenderRequest`로 RenderTexture에 그려 PNG로 저장

---

## 4. 남은 이슈와 정리할 것

**훈련 씬**
- 기획서(PLAN.md)는 Gemini streaming을 전제로 하지만, 코드는 `generateContent`(한 번에 받기)를 씁니다. 첫 문장부터 TTS를 시작하는 최적화는 아직 없습니다.
- 대화 턴 Gemini 호출(`RequestNextTurnAsync`)에는 요청 타임아웃이 없습니다. 3초 제한은 대화 모드 AI 판단에만 있습니다.
- 단계 순서와 턴 상한이 `TrainingSceneRoot`에 하드코딩돼 있습니다.
- `TrainingSceneRoot`가 1400줄이 넘고 카드 풀 결정, 프롬프트 조립 같은 로직이 남아 있습니다. "MonoBehaviour는 view만" 규칙과 완전히 맞지는 않습니다.
- STT 서버의 실제 API는 코드와 테스트가 가정한 형식입니다. 기기와 서버 PC가 같은 Wi-Fi에 있어야 하고, 서버 IP가 빌더 상수와 씬 두 곳에 있습니다.

**보안**
- Gemini·TTS 키를 URL 쿼리(`?key=`)로 보내고, 키 파일이 APK의 Resources에 평문으로 들어갑니다. 앱을 풀면 키를 꺼낼 수 있습니다.
- 7e7dfea 이전 커밋 이력에는 키가 남아 있으므로 키 재발급 여부를 확인해야 합니다.

**NPC**
- 두 컨트롤러 모두 Nod/HandOver/Smile 트리거가 없어서, 대화 중 끄덕임과 건네기 동작은 실행되지 않습니다(인사만 재생).
- 남자 원본 Idle은 Wave보다 엉덩이가 7cm 높고 발이 약 14cm 떠 있어, 인사를 시작할 때 몸이 뒤·아래로 옮겨 가는 것처럼 보입니다.
- 립싱크 프로필은 여자 음성으로 보정한 값이라, 남자 목소리로 다시 보정해야 합니다.
- JawOpen, Smile 셰이프와 S 음소는 아직 쓰지 않습니다.

**화면**
- ReportScene은 씬 파일만 1920x1440으로 고쳤고 `ReportSceneBuilder`는 아직 1920x1080입니다. 빌더를 다시 돌리면 태블릿 수정이 되돌아갑니다.
- `Build All Scenes`는 손배치가 있는 Main/ProfileSelect/ProfileCreate 빌더도 호출하므로 실행하면 손배치를 덮어씁니다.
- 앱을 새로 켜면 활성 프로필이 저장돼 있지 않아, 사용자 선택을 거치기 전까지 레포트가 비어 보입니다(의도 확인 필요).
- 스플래시 음악 파일(`splash_bgm`)이 저장소에 없습니다.

**저장소 정리 후보** (지금 코드에서 쓰지 않음)
- 패키지: Ready Player Me, glTFast, Visual Scripting, Timeline, Multiplayer Center, collab-proxy. Input System도 코드 참조가 없음
- 예전 점원 에셋과 도구: `Art/Characters/ARTTI_Clerk*`(v03~v10), `Models/Clerk/`, `ArttiLipSyncController.cs`, `ArttiClerk*.cs`, `ClerkCharacterSetup.cs`, `ClerkAnimationSetup.cs`, `CounterDisplayWirer.cs`, `docs/BLENDER_TROUBLESHOOTING.md`
- 예전 화면 효과: `CardHoverEffect`, `SparkleField`, `HomeParallax` 등 씬에서 참조하지 않는 스크립트
- 반복되는 커밋 노이즈
  - 폰트 아틀라스: `NotoSansKR-Medium SDF`가 동적 아틀라스라 새 글자가 쓰일 때마다 에셋이 바뀝니다(수정 커밋 26개).
  - ProjectSettings: Android 빌드 때 XR/ARCore `preloadedAssets`가 들어갔다 빠졌다 합니다.

---

## 5. 타임라인 (2026)

AR현장모드와 다른 팀원 작업은 빼고, 이 문서 범위의 주요 작업만 적었습니다.

| 시기 | 주요 작업 |
|---|---|
| 5/21 ~ 5/28 | Unity 프로젝트 시작(b1dd3d3), 기기 음성 인식과 카드 풀 모드(b674a3e) |
| 6/7 ~ 6/12 | 한글 tofu 수정(78c7fbe), 스플래시·프로필 생성/선택(c87b0cf), 프로필 삭제 모달(86fe0d5), 메인·레포트 씬(b00ed66), 키 파일을 Resources로 이동(fef5af7) |
| 6/16 ~ 6/25 | 편의점 대시보드, AAC 카드 데이터, 레포트 애니메이션(35d37f5, 3fe8982, 1036518) |
| 8/25 ~ 9/2 | LLM 대화 루프 재구성과 대화하기(94a5609), 대시보드 UI·Gemini 연결(dd7c978), 완료 화면(68174d9) |
| 9/21 ~ 9/22 | 3D 점원 NPC 파이프라인(bc8dcae), 캐릭터 완성(d6f5fba) |
| 9/28 | 서버 STT(67ced75, 0c26310), 발화 정확도·명료도 표시(07c7cf5, 97badeb, d00dde0, 1429b14), API 키 제거(7e7dfea) |
| 9/29 | 훈련을 편의점 하나로 정리(4591856), 정확도/대화 모드 카드(d0ad482), 대화 모드 해석(02aa9b1), 레포트 모드 구분(4a8275d), 메인 다크 네온(ac731fb) |
| 9/30 | 프로필 등록 1~3단계, 새 스플래시(d70dc02), 사용자 선택 개편(63b87fe, 06a4500), 태블릿 맞춤·잔상 수정(ac2bd4c), 대화 모드 남자 점원(89ba9f5), 명료도 관문(6c0e045), 말하기 훈련 화면(cbc6d4d) |
