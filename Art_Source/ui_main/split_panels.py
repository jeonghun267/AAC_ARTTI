# 메인 화면 패널 합본(Assets/_Project/Art/UI/Home/main/1.png)을 왼쪽 글래스와 오른쪽 카드(훈련/현장 + 두 캐릭터) 두 장으로 나눈다.
#   결과: main/1_left.png, main/1_cards.png (원본과 같은 크기라 같은 자리에 겹쳐 놓으면 원본과 똑같다)
#   Unity에서 왼쪽 글래스만 왼쪽으로 옮겨 카드와의 틈을 벌린다(MainSceneBuilder).
# 자르는 선: 줄마다 왼쪽 글래스(알파>200) 끝과 카드 시작 사이 틈에서 가장 투명한 x. 틈이 없는 줄(맨 위/아래)은 이어 붙인다.
# 잘린 빛이 딱 끊겨 보이지 않게 자르는 선 양쪽 FEATHER px는 빛을 서서히 0으로 줄인다.
# 1.png를 바꾸면 이 스크립트를 다시 돌린다.   필요: Python 3 + numpy + Pillow
#   실행: python Art_Source/ui_main/split_panels.py
import os
import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
MAIN = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'UI', 'Home', 'main')
SEARCH_X0, SEARCH_X1 = 330, 560   # 틈을 찾는 가로 범위(px). 왼쪽 글래스 오른쪽 끝 ~ 카드 왼쪽 끝을 포함
FEATHER = 4

im = np.array(Image.open(os.path.join(MAIN, '1.png')).convert('RGBA')).astype(float)
alpha = im[:, :, 3]
H, W = alpha.shape

cut = np.full(H, -1.0)
for y in range(H):
    seg = alpha[y, SEARCH_X0:SEARCH_X1]
    xs = np.where(seg > 200)[0]
    for i in range(1, len(xs)):
        if xs[i] - xs[i - 1] > 3:
            gap = seg[xs[i - 1] + 1:xs[i]]
            low = np.where(gap <= gap.min() + 6)[0]   # 가장 투명한 구간의 가운데 (한쪽 테두리에 붙지 않게)
            cut[y] = SEARCH_X0 + xs[i - 1] + 1 + float(low.mean())
            break
rows = np.where(cut >= 0)[0]
if len(rows) == 0:
    raise SystemExit('틈을 찾지 못함: SEARCH_X0/X1을 확인')
cut = np.interp(np.arange(H), rows, cut[rows])

# 줄마다 자르는 선에서 양쪽 패널(알파>60)까지 거리보다 넓게 지우지 않는다(틈이 좁은 곳에서 테두리 빛을 깎지 않게)
feather_l = np.full(H, float(FEATHER))
feather_r = np.full(H, float(FEATHER))
for y in range(H):
    c = int(round(cut[y]))
    row = alpha[y]
    lx = np.where(row[max(0, c - FEATHER - 1):c] > 60)[0]
    rx = np.where(row[c + 1:c + FEATHER + 2] > 60)[0]
    if len(lx): feather_l[y] = max(1.0, (FEATHER + 1) - lx.max() - 1)
    if len(rx): feather_r[y] = max(1.0, float(rx.min()))

d = np.arange(W)[None, :] - cut[:, None]
left = im.copy()
right = im.copy()
left[:, :, 3] *= np.clip(-d / feather_l[:, None] + 0.5, 0, 1)
right[:, :, 3] *= np.clip(d / feather_r[:, None] + 0.5, 0, 1)

for name, arr in (('1_left.png', left), ('1_cards.png', right)):
    Image.fromarray(np.clip(arr + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(MAIN, name))
print('cut x %d~%d, rows with gap %d/%d -> 1_left.png, 1_cards.png' % (cut.min(), cut.max(), len(rows), H))
