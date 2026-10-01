# AR 현장모드 첫 화면(사진 추가하기)의 발판(110.png) 윗면에서 도는 빛 고리의 무늬 띠를 만든다.
#   결과: Assets/_Project/Art/UI/Home/ARField/derived/
#     orbit_strip_outer.png  바깥 고리 띠: 끊긴 빛 호(긴 것 2, 짧은 것 2) + 꼬리 달린 빛 점 2개
#     orbit_strip_inner.png  안쪽 고리 띠: 얇은 빛 선 + 눈금 + 작은 빛 점 2개
#     orbit_strip_comets.png 아래층 빛 테두리를 따라 도는 빛 점 3개(긴 꼬리, 선 없음). 그려진 테두리 위를 빛이 달리는 것처럼 보인다
#   띠 한 장(가로 1024 x 세로 64) = 고리 한 바퀴. 가로(u)가 둘레 방향, 세로(v)가 고리 굵기 방향(0 안쪽, 1 바깥쪽).
#   UIOrbitRing이 발판 윗면 타원을 따라 이 띠를 감고 u를 밀어 무늬가 타원 위를 도는 것처럼 보이게 한다.
#   가로는 이음매 없이 이어진다(u 0과 1이 같은 자리). 무늬는 u가 줄어드는 쪽으로 움직이므로 빛 점의 꼬리는 u가 큰 쪽에 단다.
#   실행: python Art_Source/ui_arfield/make_home_stage.py      필요: Python 3 + numpy + Pillow
import os
import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'UI', 'Home', 'ARField', 'derived')
os.makedirs(OUT, exist_ok=True)

W, H = 1024, 64
CORE_RGB = np.array([236, 250, 255], float)   # 흰 심
GLOW_RGB = np.array([70, 150, 255], float)    # 파란 빛 번짐(110 발판 빛과 같은 계열)

U = ((np.arange(W) + 0.5) / W)[None, :]
V = ((np.arange(H) + 0.5) / H)[:, None]


def smooth(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3 - 2 * t)


def wrap(d):
    """u 거리(이음매 너머까지 가장 가까운 쪽)."""
    return (d + 0.5) % 1.0 - 0.5


def band(core_sigma, glow_sigma):
    """굵기 방향 모양: 가운데 흰 심 + 넓은 빛."""
    d = V - 0.5
    return np.exp(-(d / core_sigma) ** 2), np.exp(-(d / glow_sigma) ** 2)


def dash(start, length, fade):
    a = (U - start) % 1.0
    edge = np.minimum(a, length - a)
    return np.where(a <= length, smooth(edge / fade), 0.0)


def comet(head, tail):
    """머리(head)는 밝은 점, 꼬리는 u가 큰 쪽으로 tail만큼 흐려진다."""
    d = (U - head) % 1.0
    trail = np.where(d <= tail, np.clip(1.0 - d / tail, 0.0, 1.0) ** 1.8, 0.0)
    spot = np.exp(-(wrap(U - head) / 0.006) ** 2)
    return trail, spot


def save(core, glow, name):
    core = np.clip(core, 0, 1)
    glow = np.clip(glow, 0, 1)
    alpha = np.clip(core + glow * 0.8, 0, 1)
    w = np.where(alpha > 0, core / np.maximum(alpha, 1e-6), 0)[..., None]
    rgb = CORE_RGB * w + GLOW_RGB * (1 - w)
    out = np.dstack([rgb, alpha * 255])
    Image.fromarray(np.clip(out + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, name))


# ===== 바깥 고리 =====
core_v, glow_v = band(0.09, 0.30)
dashes = sum(dash(s, n, 0.025) for s, n in ((0.0, 0.16), (0.37, 0.07), (0.5, 0.16), (0.87, 0.07)))   # 긴 호 2 + 짧은 호 2
t1, s1 = comet(0.20, 0.13)                                                # 빛 점 2개(마주 보게, 호 사이 빈 칸에)
t2, s2 = comet(0.70, 0.13)
trail = t1 + t2
spot = s1 + s2
outer_core = core_v * np.clip(dashes * 0.7 + trail, 0, 1) + spot * np.exp(-((V - 0.5) / 0.22) ** 2)
outer_glow = glow_v * np.clip(dashes * 0.5 + trail * 0.9 + spot * 1.5, 0, 1)
save(outer_core, outer_glow, 'orbit_strip_outer.png')

# ===== 안쪽 고리 =====
core_v, glow_v = band(0.06, 0.20)
inner_core = core_v * 0.55
inner_glow = glow_v * 0.40
ticks = np.zeros((H, W))
for k in range(48):                                                       # 7.5도마다 눈금, 6개마다 긴 눈금
    u0 = k / 48.0
    width = np.exp(-(wrap(U - u0) / 0.0016) ** 2)
    reach = 0.42 if k % 6 == 0 else 0.30
    ticks = np.maximum(ticks, width * smooth((reach - np.abs(V - 0.5)) / 0.04) * (1.0 if k % 6 == 0 else 0.7))
inner_core = inner_core + ticks * 0.75
for u0 in (0.10, 0.60):                                                   # 작은 빛 점 2개
    sp = np.exp(-(wrap(U - u0) / 0.005) ** 2)
    inner_core = inner_core + sp * np.exp(-((V - 0.5) / 0.18) ** 2)
    inner_glow = inner_glow + sp * glow_v
save(inner_core, inner_glow, 'orbit_strip_inner.png')

# ===== 아래층 빛 점 =====
core_v, glow_v = band(0.10, 0.32)
trail = np.zeros((H, W))
spot = np.zeros((H, W))
for head in (0.0, 1.0 / 3.0, 2.0 / 3.0):
    t, sp = comet(head, 0.16)
    trail += t
    spot += sp
comet_core = core_v * np.clip(trail, 0, 1) + spot * np.exp(-((V - 0.5) / 0.24) ** 2)
comet_glow = glow_v * np.clip(trail * 0.9 + spot * 1.6, 0, 1)
save(comet_core, comet_glow, 'orbit_strip_comets.png')

print('ok ->', OUT)
