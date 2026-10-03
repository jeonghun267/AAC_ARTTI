# AR 현장모드 결과 화면(HJ 92~96 시안)에서 쓰는 파생 이미지를 만든다.
#   결과: Assets/_Project/Art/UI/Home/HJ/derived/
#     93_board.png   93(세 패널이 그려진 배경)의 가장자리를 투명하게 녹인 것.
#                    변경점: 인식 결과 카드 아래 세 줄 오른쪽의 '>'는 지운다(세 줄은 누를 수 없는 글이라). CHEVRON_ROWS 참고.
#                    화면이 3:2보다 넓으면(폰 20:9) 보드를 줄여 위아래(제목/버튼)가 안 잘리게 하고,
#                    남는 좌우는 92(패널 없는 같은 배경)가 채운다. 이음매가 안 보이게 좌우는 넓게, 위아래는 조금만 녹인다.
#     back_ring.png  뒤로 가기 버튼: LED 원 테두리(프로필 등록 LED 테두리와 같은 심/빛) + 흰 화살표.
#   실행: python Art_Source/ui_arfield/make_derived.py      필요: Python 3 + numpy + Pillow
import os
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
HJ = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'UI', 'Home', 'HJ')
OUT = os.path.join(HJ, 'derived')
os.makedirs(OUT, exist_ok=True)

# ===== 93_board.png =====
FEATHER_X = 120   # 좌우 녹이는 폭(px, 93 기준 1536 폭)
FEATHER_Y = 24    # 위아래 녹이는 폭
im = np.array(Image.open(os.path.join(HJ, '93.png')).convert('RGBA')).astype(float)
H, W = im.shape[:2]

# 변경점: 세 줄의 '>'(x 1457~1466, 줄마다 높이 약 19) 지우기. 빛 번짐까지 덮는 칸을 줄마다 칸 왼쪽/오른쪽 바로 바깥 색으로
#   가로로 이어 칠한다(어두운 판 위라 자국이 남지 않고, 지나가는 가로선은 그대로 이어진다).
CHEVRON_X = (1449, 1474)
CHEVRON_ROWS = [(641, 671), (692, 722), (743, 774)]
cx0, cx1 = CHEVRON_X
for cy0, cy1 in CHEVRON_ROWS:
    left, right = im[cy0:cy1 + 1, cx0 - 1], im[cy0:cy1 + 1, cx1 + 1]
    t = ((np.arange(cx0, cx1 + 1) - (cx0 - 1)) / (cx1 + 2 - cx0))[None, :, None]
    im[cy0:cy1 + 1, cx0:cx1 + 1] = left[:, None, :] * (1 - t) + right[:, None, :] * t
x = np.arange(W)
y = np.arange(H)
ax = np.clip(np.minimum(x, W - 1 - x) / FEATHER_X, 0, 1)
ay = np.clip(np.minimum(y, H - 1 - y) / FEATHER_Y, 0, 1)
smooth = lambda t: t * t * (3 - 2 * t)
im[:, :, 3] = 255 * smooth(ay)[:, None] * smooth(ax)[None, :]
Image.fromarray(np.clip(im + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, '93_board.png'))

# ===== back_ring.png =====
N = 256
CORE_R = 100          # 하얀 심 반지름(px)
CORE_HALF = 4.0
GLOW_OUT, GLOW_IN = 11.0, 7.0
CORE_RGB = np.array([232, 248, 255], float)
GLOW_RGB = np.array([58, 104, 255], float)
FILL_RGBA = (10, 22, 70, 0.45)   # 원 안쪽 어두운 유리

c = (np.arange(N) + 0.5) - N / 2
X, Y = np.meshgrid(c, c)
d = np.hypot(X, Y) - CORE_R
core = np.clip((CORE_HALF + 0.6 - np.abs(d)) / 1.2, 0, 1)
dist = np.maximum(np.abs(d) - CORE_HALF, 0)
glow = 0.95 * np.exp(-0.5 * (dist / np.where(d > 0, GLOW_OUT, GLOW_IN)) ** 2)
fill = np.clip(-d / 1.0, 0, 1) * FILL_RGBA[3]

# 아래에서부터 안쪽 유리 -> 빛 -> 심 순서로 덮는다 (straight alpha)
def over(src_rgb, src_a, dst_rgb, dst_a):
    a = src_a + dst_a * (1 - src_a)
    rgb = (src_rgb * src_a[..., None] + dst_rgb * (dst_a * (1 - src_a))[..., None]) / np.maximum(a, 1e-6)[..., None]
    return rgb, a

rgb = np.zeros((N, N, 3)) + np.array(FILL_RGBA[:3], float)
a = fill
rgb, a = over(np.broadcast_to(GLOW_RGB, (N, N, 3)), glow, rgb, a)
rgb, a = over(np.broadcast_to(CORE_RGB, (N, N, 3)), core, rgb, a)

# 흰 화살표(←): 4배로 그려 줄여서 매끄럽게
S = 4
arrow = Image.new('L', (N * S, N * S), 0)
dr = ImageDraw.Draw(arrow)
w = int(13 * S)
cx, cy = N * S / 2, N * S / 2
left, right, head = cx - 44 * S, cx + 46 * S, 40 * S
dr.line([(right, cy), (left + 4 * S, cy)], fill=255, width=w)
dr.line([(left + head, cy - head), (left, cy), (left + head, cy + head)], fill=255, width=w, joint='curve')
for px, py in ((right, cy), (left + head, cy - head), (left + head, cy + head), (left, cy)):
    r = w / 2
    dr.ellipse([px - r, py - r, px + r, py + r], fill=255)
arr = np.array(arrow.resize((N, N), Image.LANCZOS)).astype(float) / 255
rgb, a = over(np.broadcast_to(np.array([245, 250, 255], float), (N, N, 3)), arr, rgb, a)

Image.fromarray(np.clip(np.dstack([rgb, a * 255]) + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'back_ring.png'))
print('-> %s (93_board.png, back_ring.png)' % os.path.relpath(OUT, ROOT))
