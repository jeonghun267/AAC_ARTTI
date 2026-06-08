# 아르띠 (Artti)

**발달장애인 지역사회 소통 지원을 위한 AI · AAC 통합 플랫폼**

훈련모드 + AR현장모드 듀얼 모드 Unity 앱.

| 구분 | 내용 |
|---|---|
| 프로젝트명 | 아르띠 (Artti) |
| 엔진 | Unity 6.3 LTS (6000.3.14f1) · URP 17.3.0 · AR Foundation 6.3.4 |
| 폼팩터 | 가로(landscape) 고정 |
| 팀 구성 | 팀장 김연영 · 팀원 방승훈 · 팀원 오정훈 |

자세한 기획은 [`docs/PLAN.md`](docs/PLAN.md) 참고.

---

## 🛠 트러블슈팅 (Troubleshooting)

### 한글이 전부 네모(□, tofu)로만 보일 때

TextMeshPro에서 한글 글자가 전부 빈 네모로 표시되는 현상.

**증상**
- 머티리얼·글리프·폰트 에셋이 "정상"으로 보이는데도 한글만 네모.
- 폰트를 다시 지정하거나 **Font Asset의 Clear Dynamic Data / Reset 을 눌러도 안 고쳐짐** (오히려 악화될 수 있음).

**원인**
- TMP 폴백(fallback) 폰트와 기본 리소스 셋업이 비어 있거나, SDF 폰트 에셋에 구워진 **글리프 테이블(Glyph/Character Table)이 비워진** 상태.
- "Font Asset 클리어"는 이 글리프 테이블을 통째로 날리는 동작이라, 클리어할수록 더 깨진다.

**해결 (둘 중 하나)**
1. **TMP Essential Resources 재import** ← 가장 빠른 해결
   - Unity 메뉴: `Window ▸ TextMeshPro ▸ Import TMP Essential Resources`
   - 폴백/기본 폰트 셋업이 복원되면서 한글이 정상 표시됨.
2. **git에서 폰트 에셋 복구**
   ```bash
   git restore "Assets/Fonts/"
   git restore "Assets/TextMesh Pro/"
   ```
   - 커밋 시점에 글리프가 구워져 있던 폰트 에셋으로 되돌린다.

**⚠️ 하지 말 것 (재발 방지)**
- **KoreanFontBaker / 폰트 베이커를 1024 NotoSans 등에 일괄 실행하지 말 것.** 프로젝트 전체 한글이 네모로 깨진 전례가 있음. (Dynamic + Bold 조합도 깨짐)
- 한글이 안 나온다고 **Font Asset Clear / Reset 를 반복하지 말 것.** 구워진 글리프가 사라져 상황이 악화됨. 위 1·2번으로 복구할 것.
