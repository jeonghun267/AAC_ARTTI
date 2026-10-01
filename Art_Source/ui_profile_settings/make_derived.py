# 프로필 설정 화면(ProfileSettingsScene)에 쓰는 파생 이미지를 디자이너 원본에서 만든다.
#   원본: Assets/_Project/Art/UI/Home/profile_T/67.png, 69.png / Assets/_Project/Art/UI/Profile_C/50.png, 51.png
#   결과: Assets/_Project/Art/UI/Home/profile_T/derived/
#     sec_profile.png, sec_training.png, row_blank.png  - 69(섹션 2개 + 행 바)를 나누고 섹션 안쪽 상자 테두리를 지움
#     dd_closed.png, dd_open.png                        - 67 드롭다운 칩 두 상태, '여자' 글자를 지움(글자는 Unity에서 TMP로)
#     nav_user_off.png, nav_settings_on.png             - 왼쪽 메뉴 상태를 바꾼 버전(50/51 틀과 글자를 맞바꿈)
#     icon_card_read.png, icon_speed.png, icon_voice.png - 훈련 설정 행 아이콘(원본에 없어 직접 그림)
# 원본을 바꾸면 이 스크립트를 다시 돌리고 Unity에서 Artti/Build ProfileSettingsScene Hierarchy를 실행한다.
# 필요: Python 3 + numpy + Pillow.   실행: python Art_Source/ui_profile_settings/make_derived.py
import os, sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
T = os.path.join(ROOT, 'Assets/_Project/Art/UI/Home/profile_T/').replace(os.sep, '/')
SRC = T
C = os.path.join(ROOT, 'Assets/_Project/Art/UI/Profile_C/').replace(os.sep, '/')
OUT = os.path.join(T, 'derived')
os.makedirs(OUT, exist_ok=True)

# ===== 공통 =====
def box_blur(a, r):
    # a: HxW or HxWxC float. separable box blur via cumsum, edge-clamped
    pad = [(r, r), (r, r)] + [(0, 0)] * (a.ndim - 2)
    p = np.pad(a, pad, mode='edge')
    c = np.cumsum(p, axis=0)
    c = np.concatenate([np.zeros_like(c[:1]), c], axis=0)
    p = (c[2 * r + 1:] - c[:-2 * r - 1]) / (2 * r + 1)
    c = np.cumsum(p, axis=1)
    c = np.concatenate([np.zeros_like(c[:, :1]), c], axis=1)
    return (c[:, 2 * r + 1:] - c[:, :-2 * r - 1]) / (2 * r + 1)

def fill_masked(rgba, mask, r=8, passes=3):
    """masked pixels(True)를 주변 픽셀의 부드러운 평균으로 채운다 (normalized convolution)."""
    img = rgba.astype(float)
    w = (~mask).astype(float)
    num = img * w[..., None]
    den = w.copy()
    for _ in range(passes):
        num = box_blur(num, r)
        den = box_blur(den, r)
    fill = num / np.maximum(den[..., None], 1e-6)
    out = img.copy()
    out[mask] = fill[mask]
    return out

def rounded_rect_outline(h, w, x0, y0, x1, y1, rad, half):
    """둥근 사각형 테두리선(두께 2*half) 마스크"""
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    cx = np.clip(xx, x0 + rad, x1 - rad)
    cy = np.clip(yy, y0 + rad, y1 - rad)
    # 모서리 영역은 원까지 거리, 나머지는 변까지 거리
    inside_x = (xx >= x0 + rad) & (xx <= x1 - rad)
    inside_y = (yy >= y0 + rad) & (yy <= y1 - rad)
    d_corner = np.hypot(xx - cx, yy - cy) - rad
    d_edge = np.where(inside_x & ~inside_y, np.minimum(np.abs(yy - y0), np.abs(yy - y1)),
             np.where(inside_y & ~inside_x, np.minimum(np.abs(xx - x0), np.abs(xx - x1)),
                      np.minimum(np.minimum(np.abs(yy - y0), np.abs(yy - y1)), np.minimum(np.abs(xx - x0), np.abs(xx - x1)))))
    d = np.where(inside_x | inside_y, d_edge, np.abs(d_corner))
    return d <= half

def save(arr, path):
    Image.fromarray(np.clip(arr + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(path)

def fill_rows(rgba, box, k=6, smooth=3):
    """box(x0,y0,x1,y1) 안을 줄마다 왼쪽/오른쪽 바깥 k px 평균색을 이어 채운다(넓은 글자 영역용). 위아래로 살짝 섞어 줄무늬를 없앤다."""
    out = rgba.astype(float).copy()
    x0, y0, x1, y1 = box
    t = (np.arange(x0, x1) - x0 + 0.5) / (x1 - x0)
    rows = []
    for y in range(y0, y1):
        l = out[y, x0 - k:x0].mean(axis=0)
        r = out[y, x1:x1 + k].mean(axis=0)
        rows.append(l[None, :] * (1 - t[:, None]) + r[None, :] * t[:, None])
    patch = np.stack(rows)
    if smooth > 0:
        pad = np.pad(patch, [(smooth, smooth), (0, 0), (0, 0)], mode='edge')
        c = np.cumsum(pad, axis=0); c = np.concatenate([np.zeros_like(c[:1]), c], axis=0)
        patch = (c[2 * smooth + 1:] - c[:-2 * smooth - 1]) / (2 * smooth + 1)
    out[y0:y1, x0:x1] = patch
    return out


# ===== 69: 섹션 / 행 바 =====
# 69(섹션 2개 + 행 바 1개가 한 장)을 조각으로 나누고, 섹션 안쪽 상자(inner box) 테두리를 지운다.
a = np.array(Image.open(SRC + '69.png').convert('RGBA')).astype(float)
pad = 8
parts = {
    'sec_profile':  dict(box=(36 - pad, 245 - pad, 1050 + pad, 657 + pad), inner=(75, 358, 1010, 610)),
    'sec_training': dict(box=(36 - pad, 732 - pad, 1050 + pad, 1105 + pad), inner=(75, 843, 1011, 1062)),
    'row_blank':    dict(box=(36 - pad, 1172 - pad, 1050 + pad, 1291 + pad), inner=None),
}
for name, p in parts.items():
    x0, y0, x1, y1 = p['box']
    crop = a[y0:y1, x0:x1].copy()
    if p['inner']:
        ix0, iy0, ix1, iy1 = p['inner']
        h, w = crop.shape[:2]
        m = rounded_rect_outline(h, w, ix0 - x0, iy0 - y0, ix1 - x0, iy1 - y0, 26, 5)   # 안쪽 상자 모서리 반지름 26, 지울 선 두께 ±5
        crop = fill_masked(crop, m, r=6, passes=3)
    save(crop, f'{OUT}/{name}.png')
    print(name, crop.shape)


# ===== 67 칩 / 50·51 메뉴 =====
# 67 드롭다운 칩의 '여자' 글자 지우기 + 왼쪽 메뉴(50/51) 상태 바꾼 버전 만들기

def load(p): return np.array(Image.open(p).convert('RGBA')).astype(float)

def rect_mask(shape, x0, y0, x1, y1):
    m = np.zeros(shape[:2], bool); m[y0:y1, x0:x1] = True; return m

# ---- 67: 칩 두 개(닫힘/열림)를 따로 자르고 글자만 지운다(꺾쇠는 남김) ----
a = load(T + '67.png')
for name, box, txt in [('dd_closed', (150, 340, 720, 680), (280, 462, 442, 562)),
                       ('dd_open',   (790, 340, 1420, 680), (952, 462, 1112, 562))]:
    clean = fill_rows(a, txt)
    x0, y0, x1, y1 = box
    save(clean[y0:y1, x0:x1], f'{OUT}/{name}.png')

# ---- 50/51: 틀(글자 지움)과 글자층(흰색 알파)을 분리 ----
def split(path, glyph_box):
    a = load(path)
    m = rect_mask(a.shape, *glyph_box)
    clean = fill_rows(a, glyph_box)
    mn_o = a[:, :, :3].min(axis=2); mn_c = clean[:, :, :3].min(axis=2)
    alpha = np.clip((mn_o - mn_c - 6) / np.maximum(250 - mn_c, 1), 0, 1) * m
    glyph = np.zeros_like(a); glyph[:, :, :3] = 255; glyph[:, :, 3] = alpha * 255
    return clean, glyph

f50, g50 = split(C + '50.png', (285, 435, 1150, 650))
f51, g51 = split(C + '51.png', (340, 330, 1345, 670))
B50 = (122, 340, 1328, 729); B51 = (113, 275, 1559, 726)

def bbox(alpha):
    ys, xs = np.where(alpha > 20); return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1

def paste_glyph(frame, frame_body, glyph, glyph_body, left_x):
    """glyph(원래 이미지의 body 높이 기준)를 frame body 높이에 맞춰 키우고, 왼쪽 끝을 left_x, 세로는 body 가운데에 놓는다"""
    s = (frame_body[3] - frame_body[1]) / (glyph_body[3] - glyph_body[1])
    gx0, gy0, gx1, gy1 = bbox(glyph[:, :, 3])
    crop = Image.fromarray(glyph[gy0:gy1, gx0:gx1].astype(np.uint8), 'RGBA')
    crop = crop.resize((max(1, round(crop.width * s)), max(1, round(crop.height * s))), Image.LANCZOS)
    # 원래 글자 묶음의 body 기준 세로 중심 비율을 유지
    rel_cy = ((gy0 + gy1) / 2 - glyph_body[1]) / (glyph_body[3] - glyph_body[1])
    cy = frame_body[1] + rel_cy * (frame_body[3] - frame_body[1])
    out = Image.fromarray(np.clip(frame, 0, 255).astype(np.uint8), 'RGBA')
    layer = Image.new('RGBA', out.size, (0, 0, 0, 0))
    layer.paste(crop, (int(round(left_x)), int(round(cy - crop.height / 2))))
    return Image.alpha_composite(out, layer)

# 프로필 설정(켜짐) = 51 틀 + 50 글자. 글자 시작 = 51 아이콘 자리
on = paste_glyph(f51, B51, g50, B50, bbox(g51[:, :, 3])[0])
on.save(f'{OUT}/nav_settings_on.png')

# 사용자 선택(꺼짐) = 50 틀을 어둡게 + 51 글자. 글자 시작 = 50 아이콘 자리
dim = f50.copy()
dim[:, :, :3] *= 0.62
dim[:, :, 3] = np.where(dim[:, :, 3] < 250, dim[:, :, 3] * 0.55, dim[:, :, 3])
off = paste_glyph(dim, B50, g51, B51, bbox(g50[:, :, 3])[0])
off.save(f'{OUT}/nav_user_off.png')
print('glyph50', bbox(g50[:, :, 3]), 'glyph51', bbox(g51[:, :, 3]))


# ===== 아이콘 =====
# 훈련 설정 행 아이콘 3개(카드 읽어주기 / 말 빠르기 / 읽어주는 목소리). 71~74 행 아이콘처럼 흰 선 + 옅은 파란 빛.
N = 1024; F = 256; W = int(N * 0.068)          # 그리는 크기 / 결과 크기 / 선 굵기
def P(x, y): return (x * N, y * N)
def line(d, pts, w=W, fill=255):
    pts = [P(*p) for p in pts]
    d.line(pts, fill=fill, width=w)
    for p in (pts[0], pts[-1]): d.ellipse([p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2], fill=fill)   # 둥근 끝
def arc(d, cx, cy, r, a0, a1):
    h = W / N / 2
    d.arc([P(cx - r - h, cy - r - h), P(cx + r + h, cy + r + h)], start=a0, end=a1, fill=255, width=W)
    for a in (a0, a1):
        t = math.radians(a); circle(d, cx + r * math.cos(t), cy + r * math.sin(t), h)
def circle(d, cx, cy, r, fill=255):
    d.ellipse([P(cx - r, cy - r), P(cx + r, cy + r)], fill=fill)

def card_read(d):
    # 말풍선 카드 + 글줄 두 개 + 왼쪽 아래 꼬리 + 오른쪽 아래 소리 점
    d.rounded_rectangle([P(0.12, 0.16), P(0.84, 0.70)], radius=0.11 * N, outline=255, width=W)
    d.rectangle([P(0.28, 0.66), P(0.44, 0.74)], fill=0)                       # 꼬리 자리 비우기
    line(d, [(0.28, 0.70), (0.25, 0.86), (0.44, 0.70)])
    line(d, [(0.30, 0.37), (0.66, 0.37)])
    line(d, [(0.30, 0.52), (0.50, 0.52)])
    circle(d, 0.80, 0.72, 0.19, fill=0)                                        # 점 둘레 틈
    circle(d, 0.80, 0.72, 0.12)

def speed(d):
    cx, cy, r = 0.5, 0.56, 0.36
    arc(d, cx, cy, r, 140, 400)
    for a in (190, 230, 270, 310, 350):   # 눈금
        t = math.radians(a)
        line(d, [(cx + (r - 0.11) * math.cos(t), cy + (r - 0.11) * math.sin(t)),
                 (cx + (r - 0.17) * math.cos(t), cy + (r - 0.17) * math.sin(t))], w=int(W * 0.8))
    t = math.radians(330)
    line(d, [(cx, cy), (cx + 0.22 * math.cos(t), cy + 0.22 * math.sin(t))])   # 바늘
    circle(d, cx, cy, 0.065)

def voice(d):
    d.rounded_rectangle([P(0.37, 0.08), P(0.63, 0.58)], radius=0.13 * N, outline=255, width=W)
    cx, cy, r = 0.5, 0.40, 0.25
    arc(d, cx, cy, r, 0, 180)
    line(d, [(0.5, cy + r), (0.5, 0.86)])
    line(d, [(0.35, 0.88), (0.65, 0.88)])

for name, fn in [('icon_card_read', card_read), ('icon_speed', speed), ('icon_voice', voice)]:
    m = Image.new('L', (N, N), 0)
    fn(ImageDraw.Draw(m))
    m = m.resize((F, F), Image.LANCZOS)
    glow = m.filter(ImageFilter.GaussianBlur(5)).point(lambda v: int(v * 0.55))
    out = Image.new('RGBA', (F, F), (120, 170, 255, 0))
    out.putalpha(glow)
    white = Image.new('RGBA', (F, F), (255, 255, 255, 0)); white.putalpha(m)
    out = Image.alpha_composite(out, white)
    out.save(f'{OUT}/{name}.png')
