# Blender → Unity 점원 리깅/애니메이션 트러블슈팅

ARTTI 편의점 점원(`ARTTI_Clerk`)을 Blender에서 리깅·애니메이션해서 Unity로 가져오며 겪은 문제와 해결을 정리한 문서. **버튼 순서까지 그대로 따라 할 수 있게** 적어둔다.

- Blender: **5.1**
- 모델 원본: Sketchfab 정적 FBX (`a53ab0c9961f4f1a8f075c8a6944f350.fbx`) — VRoid 아님
- 작업 .blend: `Art_Source/ARTTI_ConvenienceStore.blend`
- 내보내기 산출물: `Assets/_Project/Models/Clerk/ARTTI_Clerk.fbx`
- Unity: 6000.3.14f1, Generic 아님 → **Humanoid**로 최종 확정

---

## 0. 점원 모델의 특성 (작업 전 반드시 알 것)

| 항목 | 내용 |
|---|---|
| 부위 | 메쉬 10개(Body/Cloth/Ear/Eyebrow/Eyelid/Eyes/Hair/Hairbase/Hands/Head) + 명찰 4개 |
| 다리/발 | **없음** — 상반신 모델 (카운터 뒤에 서 있는 용도) |
| 본/웨이트 | 임포트 직후 **전무** → Unity가 "본 없음" 에러 |
| 셰이프키 | `Head_Skin-Face_0`에 5개(표정/립싱크용) |
| 스케일 | Sketchfab RootNode에 **0.003 스케일**이 박혀 있음 |

> 셰이프키가 있는 메쉬는 **절대 Join(Ctrl+J) 금지** — 합치면 셰이프키가 깨진다.

---

## 1. 뼈대(아마추어) 만들기 — 버튼 순서

목표: Hips부터 Head, 양팔, (Humanoid용) 더미 다리까지 **19본**.

### 1-1. 아마추어 추가
1. 상단 헤더 **Add ▸ Armature ▸ Single Bone** (또는 뷰포트에서 `Shift + A` ▸ Armature)
2. 뼈가 메쉬에 가려 안 보이면: 오른쪽 Properties 패널 ▸ **Object Data Properties(초록 막대기 아이콘)** ▸ **Viewport Display** ▸ **In Front** 체크
3. 같은 패널에서 **Axes** 체크하면 본 축 방향이 보여서 회전 디버깅에 도움

### 1-2. 척추 라인 만들기 (Edit Mode)
1. 아마추어 선택 후 `Tab` → **Edit Mode** 진입
2. 첫 본의 **뿌리(아래 구)** 를 골반 위치로 이동: 본 뿌리 클릭 → `G` → `Z`로 높이 맞춤 → 클릭 확정
3. 본 **끝(위 구)** 선택 → `E`(Extrude) → 위로 끌어 다음 본 생성 → 클릭. 이걸 반복:
   - **Hips → Spine → Chest → Neck → Head** 순서로 위로 5본
4. 측면(`Numpad 3`) 보면서 척추 곡률에 맞게 각 관절 위치 조정 (`G`로 이동)

> 캐릭터 정면 = **-Y**, 척추 = **+Z**. 좌표 헷갈리면 이 기준으로 확인.

### 1-3. 팔 만들기
1. **Chest 본의 끝**을 선택 → `E` → 마우스 오른쪽 클릭(이동 취소)하면 같은 위치에 본이 생김 → `G`로 어깨로 끌기 → **Shoulder**
2. Shoulder 끝에서 `E` 반복: **UpperArm → LowerArm → Hand**
3. 한쪽(예: 왼쪽 `.L`)만 만들고 반대쪽은 자동 미러:
   - 본 이름을 `Shoulder.L`, `UpperArm.L` 처럼 **`.L` 접미사**로 지정 (이름은 `N` 패널 ▸ Item ▸ Name, 또는 Bone Properties)
   - `.L` 본들 전체 선택 → 상단 **Armature ▸ Symmetrize** → 자동으로 `.R` 생성

### 1-4. 더미 다리뼈 (Humanoid 필수 — 2번 항목과 연결)
1. **Hips 본의 뿌리** 쪽에서 아래로 `E` 3번: **UpperLeg → LowerLeg → Foot** (L)
2. 무릎이 자연스럽게 굽도록 LowerLeg 시작점을 **살짝 -Y로** 이동 (`G` ▸ `Y`)
3. `.L` 다리 3본 선택 → **Armature ▸ Symmetrize** 로 `.R`
4. 이 다리뼈에는 **메쉬 웨이트를 주지 않는다**(미가중) → 카운터 밑에 숨어 안 보임

`Tab`으로 **Object Mode** 복귀.

---

## 2. 자동 웨이트(스키닝) — 버튼 순서

본이 메쉬를 따라 움직이게 묶는 단계.

1. **Object Mode**에서 메쉬들을 먼저 전부 선택 (Outliner에서 메쉬 클릭 → 나머지 `Ctrl + 클릭`)
2. **마지막에 아마추어를 `Shift + 클릭`** — 아마추어가 **활성(노란 테두리)** 이어야 함 (순서 중요!)
3. `Ctrl + P` → **Armature Deform ▸ With Automatic Weights**
4. 아무 본이나 Pose Mode(`Ctrl+Tab`)에서 `R`로 돌려보며 메쉬가 따라오는지 확인

> 겨드랑이/어깨 부근 웨이트가 깨지면 → **5번 트러블슈팅**의 "겨드랑이 Cloth 깨짐" 참고.

---

## 3. 본 이름 Unity 표준으로 리네임 (Humanoid 자동 매핑용)

커스텀 이름이면 Unity Humanoid가 매핑을 못 잡고 L/R 에러가 반복된다. **Unity 표준 이름으로 바꾸면 Create From This Model 시 자동 매핑이 초록색**이 된다.

표준 이름:

```
Hips, Spine, Chest, Neck, Head
LeftShoulder, LeftArm, LeftForeArm, LeftHand   (오른쪽은 Right…)
LeftUpLeg, LeftLeg, LeftFoot                   (오른쪽은 Right…)
```

순서:
1. Edit/Pose Mode ▸ 본 선택 ▸ **Bone Properties(뼈 아이콘)** ▸ 맨 위 Name 칸에서 변경
2. **버텍스 그룹도 같은 이름으로** 바꿔야 웨이트가 본을 따라감:
   - 메쉬 선택 ▸ **Object Data Properties(초록 삼각형)** ▸ **Vertex Groups** ▸ 해당 그룹 더블클릭 ▸ 본과 동일하게 리네임
3. 모든 메쉬에 대해 버텍스 그룹 리네임 반복

> 이름을 바꾸면 기존 Generic 시절 `@Idle/@Wave/@Nod/@Talk` 클립은 본 이름이 안 맞아 **폐기**된다. Humanoid에서는 Mixamo 클립으로 대체.

---

## 4. FBX 내보내기 — 버튼 순서 + 설정

1. **File ▸ Export ▸ FBX (.fbx)**
2. 오른쪽 옵션 패널에서:
   - **Path Mode: `Copy`** 선택 → 바로 옆 **상자 아이콘(Embed Textures) 클릭해서 활성**
     - (텍스처가 blend에 packed지만 경로가 Temp라, 이걸 안 하면 Unity에서 **얼굴/눈/귀 색이 날아감**)
   - **Limit to ▸ Selected Objects**: 점원만 내보낼 때 체크 (씬 전체 안 나가게)
   - **Object Types**: Armature + Mesh
   - **Armature ▸ Add Leaf Bones: 끄기**(Unity에서 불필요한 말단 본 방지)
3. 파일명/경로 = `Assets/_Project/Models/Clerk/ARTTI_Clerk.fbx`
4. **Export FBX**

> **클립이 안 잡히면 아마추어만 말고 메쉬까지 포함해서 내보낼 것.** Generic+CopyAvatar 조합에서 메쉬 없는 FBX는 클립이 누락된다.

---

## 5. 트러블슈팅 모음 (증상 → 원인 → 해결)

### Q1. Unity에서 "본이 없다 / 스킨이 없다" 에러
- **원인**: Sketchfab 모델은 정적 메쉬라 본·웨이트가 전무.
- **해결**: 위 1~2번대로 아마추어 심고 Automatic Weights.

### Q2. 트랜스폼 적용했더니 메쉬가 폭주/터짐
- **원인**: Sketchfab RootNode에 **0.003 스케일**이 박혀 있어, 부모연결 상태로 Apply하면 손상.
- **해결**: 메쉬를 **부모 해제(Alt+P ▸ Clear Parent and Keep Transform)** 후, 스크립트로 `mesh.transform(M, shape_keys=True)`로 직접 적용. 셰이프키 보존 위해 `shape_keys=True` 필수.

### Q3. 얼굴 표정/립싱크 셰이프키가 사라짐
- **원인**: 메쉬를 **Join(Ctrl+J)** 해서 합침.
- **해결**: `Head_Skin-Face_0`는 절대 합치지 말 것. 메쉬는 분리 상태 유지.

### Q4. Unity에서 점원 얼굴/눈/귀가 회색(텍스처 증발)
- **원인**: FBX 내보낼 때 Path Mode가 Copy가 아니거나 Embed Textures 미체크. baseColor.png들이 Temp 경로라 따라오지 않음.
- **해결**: 4번처럼 **Path Mode=Copy + Embed Textures**. Unity 쪽은 FBX Inspector ▸ **Materials 탭 ▸ Extract Textures / Extract Materials**.
- 참고: 몸·손(Skin-Body), 옷(`#1A56DB`/Brown), 머리, 눈썹은 텍스처 없이 Principled Base Color만 → Unity에서 색 정상.

### Q5. 명찰이 Unity에 안 나옴
- **원인**: 명찰 4개(`Clerk_NameTag/_Top/NameText/Uni_Logo`)가 `Clerk_Rig`가 아니라 `Sketchfab_model`에 붙어 있어, Selected Objects 내보내기에서 누락.
- **해결**: 명찰을 **Clerk_Rig 자식으로 재부모**(월드 위치 보존: `Ctrl+P ▸ Keep Transform`). 단 명찰은 **스킨 없는 정적 메쉬**로 내보내고, Unity에서 손배치 후 **Chest 본의 자식**으로 넣는다(스킨드 메쉬는 Transform 이동이 안 되므로).

### Q6. "반신이라 Humanoid가 안 된다"
- **진짜 원인**: 반신이라서가 아니라 **Unity Humanoid는 다리뼈가 필수**라서.
- **해결**: 1-4번처럼 **더미 다리뼈 6개 추가**(총 19본). 메쉬 미가중이라 안 보이지만 Humanoid 매핑은 통과.

### Q7. Humanoid Configure에서 본이 빨강/미매핑, L·R 에러 반복
- **원인**: 본 이름이 커스텀이라 자동 매핑 실패.
- **해결**: 3번처럼 **Unity 표준 본 이름 + 버텍스 그룹 리네임**. 그러면 자동 매핑이 초록.

### Q8. Mixamo 클립을 점원에 적용하니 스켈레톤 에러
- **원인**: Mixamo는 `mixamorig` 본이라 점원과 스켈레톤이 다름 → **Copy From Other Avatar 쓰면 안 됨**.
- **해결**: Humanoid 클립은 아바타 독립적(런타임 리타겟)이므로 **Avatar Definition = Create From This Model** 로 임포트. (점원 베이스 FBX도 Create From This Model)

### Q9. 인사 때 겨드랑이(유니폼 Cloth)가 깨짐 — Generic 시절 이슈
- **원인**: 팔을 높이 들면 자동웨이트 겨드랑이 변형이 깨지고 어색.
- **해결(당시)**: **낮은 인사**로 확정 — 위팔만 살짝 들고 팔뚝을 굽혀 손=얼굴 높이. 겨드랑이 Cloth 웨이트는 numpy로 수동 스무딩.
- 현재는 Humanoid + Mixamo Waving으로 대체됨.

---

## 6. Unity 임포트 최종 설정 (체크리스트)

점원 베이스 `ARTTI_Clerk.fbx`:
1. Inspector ▸ **Rig 탭**
   - Animation Type: **Humanoid**
   - Avatar Definition: **Create From This Model**
   - **Apply** ▸ **Configure…** 들어가서 본 매핑 전부 **초록**인지 확인
2. Inspector ▸ **Materials 탭** ▸ Extract Textures / Materials (Q4)

Mixamo 클립 FBX(Waving/Idle/Picking Up/Head Nod 등):
1. Rig 탭 ▸ Animation Type: **Humanoid** ▸ Avatar Definition: **Create From This Model**
2. Animation 탭 ▸ 필요시 **Loop Time** 체크(Idle/Talk 루프)
3. **Apply Root Motion 끄기** (Animator 컴포넌트에서) — 안 끄면 점원이 제자리를 벗어나 미끄러짐
   - Talk 모션은 Mixamo 기본엔 없음 → 별도 다운로드

---

## 7. Blender 5.1 주의 (스크립트 작업 시)

- **`action.fcurves`가 없다** — 5.1은 슬롯(slot) 구조로 바뀜. 파이썬으로 키프레임 다룰 때 5.0 이하 코드가 안 먹힘.
- 일부 웨이트/연산자는 `poll()` 실패 → 메쉬 웨이트는 numpy 직접 조작이 안전.
- 글로벌 축 회전이 필요하면 로컬 본축 대신 `pb.matrix = T @ R @ T⁻¹ @ M` 헬퍼 사용(본 로컬축이 불명확할 때).
- 방향 기준: 캐릭터 정면 **-Y**, 척추 **+Z**, `_R` 본은 **-X**(캐릭터 오른쪽).
