# 아바타 선택 칸의 "선택됨" 테두리(LED 느낌) 스프라이트를 만든다.
#   결과: Assets/_Project/Art/UI/Home/profiles/derived/led_tile_frame.png (9-slice, 가운데는 비어 있음)
# 모양은 프로필 등록 완료 카드(profiles/35.png)의 원 테두리를 따랐다:
#   하얀 심(35 기준 지름의 약 2%) + 양쪽으로 번지는 파란 빛(바깥이 안쪽보다 조금 더 넓게).
# 칸(Bg)은 RoundedRect.png(모서리 반지름 44)를 1:1로 9-slice 하므로 같은 반지름의 둥근 사각형을 따라 그린다.
# 단위: Unity Stage 단위(칸 200x200 기준). 2배 해상도로 그려 Image.pixelsPerUnitMultiplier = 2로 쓴다.
#   Unity 쪽(ProfileAvatarStepBuilder.LedFrame*)과 값이 맞아야 한다: MARGIN(칸 밖으로 넘치는 폭), PX(배율).
#   실행: python Art_Source/ui_profiles/make_led_frame.py      필요: Python 3 + numpy + Pillow
import os
import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'UI', 'Home', 'profiles', 'derived', 'led_tile_frame.png')

PX = 2                 # 1 단위 = 2 px
MARGIN = 18            # 칸 밖으로 그림이 넘치는 폭(단위). 빛 번짐이 이 안에서 0이 된다
RADIUS = 44            # 칸 모서리 반지름(단위)
CORE_OFFSET = 3        # 하얀 심 가운데가 칸 가장자리에서 바깥으로 떨어진 거리
CORE_HALF = 1.6        # 하얀 심 두께의 절반
GLOW_OUT = 5.0         # 바깥 빛 번짐(가우시안 sigma)
GLOW_IN = 3.2          # 안쪽 빛 번짐
CORE_RGB = (232, 248, 255)
GLOW_RGB = (58, 104, 255)
GLOW_ALPHA = 0.95

# 9-slice 테두리 = MARGIN + RADIUS + 여유 4. 가운데 2단위는 늘어나는 부분(비어 있음)
BORDER = MARGIN + RADIUS + 4
SIZE = 2 * BORDER + 2
inner = SIZE - 2 * MARGIN                       # 그림 속 가상의 칸 크기
half = inner / 2.0 + CORE_OFFSET                # 하얀 심이 지나는 둥근 사각형의 반 크기
r = RADIUS + CORE_OFFSET

n = SIZE * PX
c = (np.arange(n) + 0.5) / PX - SIZE / 2.0      # 픽셀 가운데의 좌표(단위, 그림 가운데 0)
x, y = np.meshgrid(c, c)
qx = np.abs(x) - (half - r)
qy = np.abs(y) - (half - r)
d = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r   # 심 경로까지 거리(바깥 +)

aa = 0.6 / PX
core = np.clip((CORE_HALF + aa - np.abs(d)) / (2 * aa), 0, 1)
dist = np.maximum(np.abs(d) - CORE_HALF, 0)
sigma = np.where(d > 0, GLOW_OUT, GLOW_IN)
glow = GLOW_ALPHA * np.exp(-0.5 * (dist / sigma) ** 2)

# 심을 빛 위에 덮는다 (straight alpha)
a = core + glow * (1 - core)
rgb = np.zeros((n, n, 3))
for i in range(3):
    rgb[:, :, i] = (CORE_RGB[i] * core + GLOW_RGB[i] * glow * (1 - core)) / np.maximum(a, 1e-6)
# 그림 끝(MARGIN 바깥 끝)에서 빛이 남아 잘려 보이지 않게 0으로 맞춘다
edge = np.minimum.reduce([x + SIZE / 2.0, SIZE / 2.0 - x, y + SIZE / 2.0, SIZE / 2.0 - y])
a *= np.clip(edge / 2.0, 0, 1)

img = np.dstack([rgb, a * 255])
os.makedirs(os.path.dirname(OUT), exist_ok=True)
Image.fromarray(np.clip(img + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(OUT)
print('%s  %dx%d px, 9-slice border %d px (= %d units x %d)' % (os.path.relpath(OUT, ROOT), n, n, BORDER * PX, BORDER, PX))
