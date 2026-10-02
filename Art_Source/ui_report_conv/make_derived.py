# 대화 모드 리포트(ConversationReportScene)와 정확도 모드 리포트(AccuracyReportScene, 같은 REPORT 조각)에 쓰는 파생 이미지를 만든다.
#   원본: Assets/_Project/Art/UI/Home/REPORT/*.png (디자이너 조각)
#   결과: Assets/_Project/Art/UI/Home/REPORT/derived/
#     icon_ring.png                       - 스탯 카드 아이콘 바탕(어두운 유리 원 + 하늘색 테두리 빛)
#     icon_clock/chat/sparkle/flag.png    - 스탯 카드 아이콘(흰 선 + 파란 빛). 시안에 있지만 조각이 없어 직접 그림
#     icon_back.png                       - 왼쪽 위 뒤로 가기(원 + 화살표)
#     icon_bulb.png, icon_check.png       - AI 코멘트 제목 전구 / 줄마다 체크
#     icon_bars.png, icon_chat_fill.png   - 14.png / 15.png에서 아이콘만 떼어 낸 것(이번 주 변화 / 대화 기록 제목)
#     icon_row0~3.png                     - 15~18.png(주요 대화 분석 항목)에서 아이콘만. 글자 그림은 작게 줄이면 흐려져 이름은 TMP 흰 글자로
#     arrow_up.png, arrow_down.png        - 변화 칩 화살표
#     chip.png                            - 변화 칩 테두리(알약 모양, 9-slice)
#     bubble_npc.png, bubble_user.png     - 대화 기록 말풍선(점원 = 어두운 유리 + 왼쪽 꼬리, 나 = 밝은 파랑). 9-slice
#     fill_blue/violet/cyan.png           - 주요 대화 분석 막대 채움(그라데이션 알약, 9-slice)
#     dot.png                             - 빛나는 점(대화 기록 줄, 범례, 그래프 점). 색은 Unity에서 입힌다
#     badge_blank.png                     - 20.png('우수')에서 글자를 지운 알약(등급 글자는 TMP)
#     radar_grid.png                      - 21.png에서 축 이름(이해/자연스러움/속도/표현력/지속력)을 지운 격자(이름은 TMP)
#   정확도 모드 리포트(시안에 있지만 조각이 없는 것):
#     icon_target/aim/repeat/warning/gauge/wave/diamond/link/play/stop.png - 흰 선 아이콘(메뉴·제목·카드·항목·재생·멈춤)
#     icon_alert.png                      - 어려웠던 발음 목록 제목(파란 원 + 흰 느낌표)
#     nav_normal.png, nav_active.png      - 10.png / 11.png(왼쪽 메뉴)에서 아이콘·글자를 지운 판. 아이콘과 이름은 Unity에서 얹는다
#     btn_retry_blank.png                 - 13.png('다시 대화하기')에서 글자만 지운 버튼(화살표는 남김, 글자는 TMP)
#     row_plate.png, tag_pink.png         - 어려웠던 발음 한 줄 판 / 분홍 태그(9-slice)
#   두 리포트 공통:
#     tab_bar.png                         - 기간 탭 막대(알약 모양, 선택 표시 1.png와 같은 곡률, 9-slice)
#   대화 모드 '전체 보기' 창:
#     bubble_npc_top.png, icon_close.png  - 꼬리가 위쪽에 있는 점원 말풍선(여러 줄로 늘어나도 꼬리가 안 늘어남, 9-slice) / 닫기 X
# 디자이너가 진짜 그림을 주면 같은 이름으로 바꿔 넣고 Unity 메뉴 Artti/Build ConversationReportScene Hierarchy를 다시 실행한다.
# 필요: Python 3 + numpy + Pillow.   실행: python Art_Source/ui_report_conv/make_derived.py
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
SRC = os.path.join(ROOT, 'Assets/_Project/Art/UI/Home/REPORT/').replace(os.sep, '/')
OUT = os.path.join(SRC, 'derived')
os.makedirs(OUT, exist_ok=True)

GLOW = (80, 165, 255)
CYAN = (70, 225, 255)


def save(img, name):
    img.save(os.path.join(OUT, name))
    print('saved', name, img.size[0], 'x', img.size[1])


def glow_compose(mask, fill, glow_layers):
    """mask(L) 글자/선 + 바깥 빛 여러 겹 -> RGBA. fill = (r,g,b) 또는 세로 그라데이션 (위, 아래)."""
    w, h = mask.size
    m = np.asarray(mask).astype(np.float64) / 255
    rgb = np.zeros((h, w, 3))
    a = np.zeros((h, w))
    for color, radius, strength in glow_layers:
        g = np.asarray(mask.filter(ImageFilter.GaussianBlur(radius))).astype(np.float64) / 255
        g = np.clip(g * strength, 0, 1)
        c = np.array(color, dtype=np.float64)
        na = g + a * (1 - g)
        rgb = (c * g[..., None] + rgb * a[..., None] * (1 - g[..., None])) / np.maximum(na, 1e-6)[..., None]
        a = na
    if isinstance(fill[0], (tuple, list)):
        t = np.linspace(0, 1, h)[:, None, None]
        f = np.array(fill[0], dtype=np.float64) * (1 - t) + np.array(fill[1], dtype=np.float64) * t
    else:
        f = np.array(fill, dtype=np.float64)
    na = m + a * (1 - m)
    rgb = (f * m[..., None] + rgb * a[..., None] * (1 - m[..., None])) / np.maximum(na, 1e-6)[..., None]
    return Image.fromarray(np.dstack([np.clip(rgb, 0, 255), na * 255]).astype(np.uint8), 'RGBA')


def line_icon(name, draw_fn, size=256, stroke=14, color=(255, 255, 255)):
    mask = Image.new('L', (size, size), 0)
    draw_fn(ImageDraw.Draw(mask), size, stroke)
    save(glow_compose(mask, color, [(GLOW, 14, 1.4), (GLOW, 5, 1.2)]), name)


# ===== 아이콘 =====
def clock(d, s, w):
    c, r = s / 2, s * 0.30
    d.ellipse([c - r, c - r, c + r, c + r], outline=255, width=w)
    d.line([(c, c), (c, c - r * 0.62)], fill=255, width=w)
    d.line([(c, c), (c + r * 0.45, c + r * 0.12)], fill=255, width=w)
    d.ellipse([c - w * 0.7, c - w * 0.7, c + w * 0.7, c + w * 0.7], fill=255)


def chat(d, s, w):
    x0, y0, x1, y1 = s * 0.20, s * 0.24, s * 0.80, s * 0.66
    d.rounded_rectangle([x0, y0, x1, y1], radius=s * 0.16, outline=255, width=w)
    d.polygon([(s * 0.32, y1 - 4), (s * 0.27, s * 0.80), (s * 0.45, y1 - 4)], fill=255)
    for i in range(3):
        cx = s * (0.38 + 0.12 * i)
        d.ellipse([cx - w * 0.75, s * 0.45 - w * 0.75, cx + w * 0.75, s * 0.45 + w * 0.75], fill=255)


def star4(d, cx, cy, r, w, fill=False):
    k = 0.28
    pts = []
    for i in range(8):
        ang = np.pi / 2 * (i // 2) + (np.pi / 4 if i % 2 else 0)
        rr = r if i % 2 == 0 else r * k
        pts.append((cx + rr * np.cos(ang - np.pi / 2), cy + rr * np.sin(ang - np.pi / 2)))
    if fill:
        d.polygon(pts, fill=255)
    else:
        d.line(pts + [pts[0]], fill=255, width=w, joint='curve')


def sparkle(d, s, w):
    star4(d, s * 0.47, s * 0.52, s * 0.30, w)
    star4(d, s * 0.74, s * 0.27, s * 0.08, w, fill=True)


def flag(d, s, w):
    x = s * 0.30
    d.line([(x, s * 0.20), (x, s * 0.82)], fill=255, width=w)
    pts = [(x, s * 0.22), (s * 0.72, s * 0.22), (s * 0.60, s * 0.36), (s * 0.72, s * 0.50), (x, s * 0.50)]
    d.line(pts + [pts[0]], fill=255, width=w, joint='curve')


def back(d, s, w):
    c, r = s / 2, s * 0.40
    d.ellipse([c - r, c - r, c + r, c + r], outline=255, width=max(4, w // 2))
    d.line([(s * 0.33, c), (s * 0.68, c)], fill=255, width=w)
    d.line([(s * 0.33, c), (s * 0.47, c - s * 0.14)], fill=255, width=w)
    d.line([(s * 0.33, c), (s * 0.47, c + s * 0.14)], fill=255, width=w)


def bulb(d, s, w):
    c = s / 2
    d.ellipse([c - s * 0.22, s * 0.14, c + s * 0.22, s * 0.58], fill=255)
    d.rectangle([c - s * 0.11, s * 0.52, c + s * 0.11, s * 0.68], fill=255)
    d.rounded_rectangle([c - s * 0.12, s * 0.70, c + s * 0.12, s * 0.78], radius=s * 0.03, fill=255)
    d.rounded_rectangle([c - s * 0.08, s * 0.80, c + s * 0.08, s * 0.86], radius=s * 0.03, fill=255)


def check(d, s, w):
    c, r = s / 2, s * 0.38
    d.ellipse([c - r, c - r, c + r, c + r], outline=255, width=max(6, w * 2 // 3))
    d.line([(s * 0.33, s * 0.52), (s * 0.45, s * 0.64), (s * 0.68, s * 0.38)], fill=255, width=w, joint='curve')


def arrow(d, s, w, up=True):
    c = s / 2
    top, bot = s * 0.18, s * 0.82
    if not up:
        top, bot = bot, top
    d.line([(c, bot), (c, top)], fill=255, width=w)
    sign = 1 if up else -1
    d.line([(c, top), (c - s * 0.24, top + sign * s * 0.24)], fill=255, width=w)
    d.line([(c, top), (c + s * 0.24, top + sign * s * 0.24)], fill=255, width=w)


def icon_ring():
    s = 256
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    a = np.zeros((s, s, 4))
    yy, xx = np.mgrid[0:s, 0:s]
    d = np.hypot(xx - s / 2 + 0.5, yy - s / 2 + 0.5) / (s * 0.40)
    inside = np.clip((1.0 - d) * 60, 0, 1)                    # 원 안(가장자리 부드럽게)
    rim = np.exp(-((d - 1.0) / 0.035) ** 2)                    # 테두리 선
    halo = np.exp(-((d - 1.0) / 0.12) ** 2) * (d > 1.0)        # 바깥 빛
    fill = np.array([12, 30, 78], dtype=np.float64) * (1 - 0.35 * d[..., None]) + np.array([20, 60, 140]) * 0.35 * d[..., None]
    rgb = fill * inside[..., None]
    alpha = inside * 0.78
    for col, k in ((np.array([120, 200, 255]), rim), (np.array([70, 150, 255]), halo * 0.6)):
        na = k + alpha * (1 - k)
        rgb = (col * k[..., None] + rgb * alpha[..., None] * (1 - k[..., None])) / np.maximum(na, 1e-6)[..., None]
        alpha = na
    a[..., :3] = rgb
    a[..., 3] = alpha * 255
    save(Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA'), 'icon_ring.png')


def crop_icon(src, box, pad, name):
    im = Image.open(SRC + src).convert('RGBA')
    x0, y0, x1, y1 = box
    save(im.crop((x0 - pad, y0 - pad, x1 + pad, y1 + pad)), name)


# ===== 9-slice 판 =====
def rounded_mask(w, h, r, inset=0):
    m = Image.new('L', (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle([inset, inset, w - 1 - inset, h - 1 - inset], radius=r, fill=255)
    return m


def panel(name, w, h, r, fill_top, fill_bot, fill_alpha, stroke, stroke_w, glow_r, glow_a, tail=None, glow=GLOW):
    pad = glow_r * 2
    W, H = w + 2 * pad, h + 2 * pad
    body = Image.new('L', (W, H), 0)
    bd = ImageDraw.Draw(body)
    bd.rounded_rectangle([pad, pad, pad + w - 1, pad + h - 1], radius=r, fill=255)
    if tail:   # 왼쪽 꼬리 (세로 가운데)
        tx, ty, tw, th = tail
        bd.polygon([(pad + 2, pad + ty - th), (pad - tw, pad + ty), (pad + 2, pad + ty + th)], fill=255)
    edge = body.filter(ImageFilter.FIND_EDGES).filter(ImageFilter.MaxFilter(stroke_w | 1))
    b = np.asarray(body).astype(np.float64) / 255
    e = np.asarray(edge).astype(np.float64) / 255 * b
    g = np.asarray(body.filter(ImageFilter.GaussianBlur(glow_r))).astype(np.float64) / 255 * (1 - b)
    t = np.linspace(0, 1, H)[:, None, None]
    fill = np.array(fill_top, dtype=np.float64) * (1 - t) + np.array(fill_bot, dtype=np.float64) * t
    rgb = np.broadcast_to(fill, (H, W, 3)).copy()
    alpha = b * fill_alpha
    sc = np.array(stroke[:3], dtype=np.float64)
    ea = e * stroke[3]
    rgb = (sc * ea[..., None] + rgb * alpha[..., None] * (1 - ea[..., None])) / np.maximum(ea + alpha * (1 - ea), 1e-6)[..., None]
    alpha = ea + alpha * (1 - ea)
    ga = np.clip(g * glow_a * 2.2, 0, 1)
    gc = np.array(glow, dtype=np.float64)
    na = alpha + ga * (1 - alpha)
    rgb = (rgb * alpha[..., None] + gc * ga[..., None] * (1 - alpha[..., None])) / np.maximum(na, 1e-6)[..., None]
    out = np.dstack([np.clip(rgb, 0, 255), np.clip(na, 0, 1) * 255]).astype(np.uint8)
    save(Image.fromarray(out, 'RGBA'), name)
    print(f'  {name}: body ({pad},{pad})-({pad + w},{pad + h}) pad {pad}')


def bar_fill(name, c0, c1):
    w, h = 512, 72
    m = rounded_mask(w, h, h // 2)
    x = np.linspace(0, 1, w)[None, :, None]
    rgb = np.array(c0, dtype=np.float64) * (1 - x) + np.array(c1, dtype=np.float64) * x
    rgb = np.broadcast_to(rgb, (h, w, 3)).copy()
    y = np.linspace(0, 1, h)[:, None, None]
    rgb = rgb + 40 * np.exp(-((y - 0.28) / 0.18) ** 2)       # 위쪽 윤기
    a = np.asarray(m).astype(np.float64)
    save(Image.fromarray(np.dstack([np.clip(rgb, 0, 255), a]).astype(np.uint8), 'RGBA'), name)


def dot():
    s = 64
    yy, xx = np.mgrid[0:s, 0:s]
    d = np.hypot(xx - s / 2 + 0.5, yy - s / 2 + 0.5) / (s / 2)
    core = np.clip((0.42 - d) * 30, 0, 1)
    halo = np.clip(np.exp(-((d - 0.3) / 0.28) ** 2) * (d > 0.3) * 0.55, 0, 1)
    a = np.clip(core + halo * (1 - core), 0, 1)
    out = np.dstack([np.full((s, s, 3), 255.0), a * 255]).astype(np.uint8)
    save(Image.fromarray(out, 'RGBA'), 'dot.png')


# ===== '우수' 글자 지우기 =====
def badge_blank():
    a = np.asarray(Image.open(SRC + '20.png').convert('RGBA')).astype(np.float64)
    ys, xs = np.where(a[..., 3] >= 250)
    # 알약 안쪽(테두리 빛 안)만 다시 메운다: 가운데 줄의 밝은 테두리 사이
    x0, x1 = 560, 1214
    y0, y1 = 352, 534
    out = a.copy()
    region = out[y0:y1, x0:x1]
    top = a[y0 - 2, x0:x1, :3]
    bot = a[y1 + 1, x0:x1, :3]
    left = a[y0:y1, x0 - 2, :3]
    right = a[y0:y1, x1 + 1, :3]
    h, w = y1 - y0, x1 - x0
    u = (np.arange(w) + 0.5)[None, :, None] / w
    v = (np.arange(h) + 0.5)[:, None, None] / h
    fill = ((1 - v) * top[None] + v * bot[None] + (1 - u) * left[:, None] + u * right[:, None]
            - ((1 - u) * (1 - v) * top[0] + u * (1 - v) * top[-1] + (1 - u) * v * bot[0] + u * v * bot[-1]))
    region[..., :3] = fill
    save(Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), 'RGBA'), 'badge_blank.png')


# ===== 레이더 격자에서 축 이름 지우기 =====
# 21.png(1254px, 투명 바탕): 이름 상자 안을 투명하게 하되, 꼭짓점 점(빛 포함) 둘레 원은 남긴다.
#   격자 꼭짓점: 위(621,307) 오른쪽(950,553) 오른쪽 아래(821,912) 왼쪽 아래(416,913) 왼쪽(289,553)
RADAR_VERTICES = [(621, 307), (950, 553), (821, 912), (416, 913), (289, 553)]
RADAR_LABEL_BOXES = [(520, 150, 720, 292), (955, 465, 1254, 598), (815, 915, 1012, 1040), (188, 915, 445, 1046), (20, 468, 285, 600)]


def radar_grid():
    a = np.asarray(Image.open(SRC + '21.png').convert('RGBA')).copy()
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]]
    keep = np.zeros(a.shape[:2], dtype=bool)
    for vx, vy in RADAR_VERTICES:
        keep |= (xx - vx) ** 2 + (yy - vy) ** 2 <= 27 ** 2
    for x0, y0, x1, y1 in RADAR_LABEL_BOXES:
        box = np.zeros(a.shape[:2], dtype=bool)
        box[y0:y1, x0:x1] = True
        a[box & ~keep, 3] = 0
    save(Image.fromarray(a, 'RGBA'), 'radar_grid.png')


# ===== 정확도 모드 리포트 아이콘 =====
def target(d, s, w):
    c = s / 2
    for r in (s * 0.30, s * 0.15):
        d.ellipse([c - r, c - r, c + r, c + r], outline=255, width=w)
    d.ellipse([c - w, c - w, c + w, c + w], fill=255)
    for dx, dy in ((0, -1), (0, 1), (-1, 0), (1, 0)):
        d.line([(c + dx * s * 0.30, c + dy * s * 0.30), (c + dx * s * 0.42, c + dy * s * 0.42)], fill=255, width=w)


def aim(d, s, w):
    cx, cy = s * 0.46, s * 0.54
    for r in (s * 0.28, s * 0.15):
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=255, width=w)
    d.ellipse([cx - w * 0.8, cy - w * 0.8, cx + w * 0.8, cy + w * 0.8], fill=255)
    d.line([(cx, cy), (s * 0.80, s * 0.22)], fill=255, width=w)
    d.line([(s * 0.80, s * 0.22), (s * 0.80, s * 0.36)], fill=255, width=w)
    d.line([(s * 0.80, s * 0.22), (s * 0.66, s * 0.22)], fill=255, width=w)


def repeat(d, s, w):
    c, r = s / 2, s * 0.28
    d.arc([c - r, c - r, c + r, c + r], 200, 330, fill=255, width=w)
    d.arc([c - r, c - r, c + r, c + r], 20, 150, fill=255, width=w)
    for ang in (330, 150):
        a = np.deg2rad(ang)
        p = np.array([c + r * np.cos(a), c + r * np.sin(a)])
        t = np.array([-np.sin(a), np.cos(a)])   # 시계 방향 진행
        n = np.array([np.cos(a), np.sin(a)])
        d.polygon([tuple(p + t * s * 0.08), tuple(p - t * s * 0.04 + n * s * 0.09), tuple(p - t * s * 0.04 - n * s * 0.09)], fill=255)


def warning(d, s, w):
    pts = [(s * 0.50, s * 0.18), (s * 0.84, s * 0.78), (s * 0.16, s * 0.78)]
    d.line(pts + [pts[0]], fill=255, width=w, joint='curve')
    d.line([(s * 0.50, s * 0.38), (s * 0.50, s * 0.58)], fill=255, width=w)
    d.ellipse([s * 0.50 - w * 0.8, s * 0.67 - w * 0.8, s * 0.50 + w * 0.8, s * 0.67 + w * 0.8], fill=255)


def gauge(d, s, w):
    c, cy, r = s / 2, s * 0.58, s * 0.32
    d.arc([c - r, cy - r, c + r, cy + r], 150, 390, fill=255, width=w)
    d.line([(c, cy), (c + s * 0.17, cy - s * 0.18)], fill=255, width=w)
    d.ellipse([c - w, cy - w, c + w, cy + w], fill=255)


def wave(d, s, w):
    hs = [0.10, 0.22, 0.34, 0.20, 0.30, 0.14, 0.06]
    for i, h in enumerate(hs):
        x = s * (0.20 + 0.10 * i)
        d.line([(x, s / 2 - s * h), (x, s / 2 + s * h)], fill=255, width=w)


def diamond(d, s, w):
    out = [(s * 0.30, s * 0.28), (s * 0.70, s * 0.28), (s * 0.84, s * 0.42), (s * 0.50, s * 0.80), (s * 0.16, s * 0.42)]
    d.line(out + [out[0]], fill=255, width=w, joint='curve')
    d.line([(s * 0.16, s * 0.42), (s * 0.84, s * 0.42)], fill=255, width=w)
    d.line([(s * 0.40, s * 0.28), (s * 0.34, s * 0.42), (s * 0.50, s * 0.80), (s * 0.66, s * 0.42), (s * 0.60, s * 0.28)],
           fill=255, width=max(4, w // 2))


def link(d, s, w):
    for cx, cy in ((s * 0.38, s * 0.62), (s * 0.62, s * 0.38)):
        ring = Image.new('L', (s, s), 0)
        ImageDraw.Draw(ring).rounded_rectangle([cx - s * 0.20, cy - s * 0.11, cx + s * 0.20, cy + s * 0.11],
                                               radius=s * 0.11, outline=255, width=w)
        ring = ring.rotate(45, resample=Image.BICUBIC, center=(cx, cy))
        d.bitmap((0, 0), ring, fill=255)


def stop(d, s, w):
    c, r = s / 2, s * 0.40
    d.ellipse([c - r, c - r, c + r, c + r], outline=255, width=max(6, w * 2 // 3))
    k = s * 0.13
    d.rounded_rectangle([c - k, c - k, c + k, c + k], radius=s * 0.03, fill=255)


def close_x(d, s, w):
    c, r = s / 2, s * 0.40
    d.ellipse([c - r, c - r, c + r, c + r], outline=255, width=max(4, w // 2))
    k = s * 0.15
    d.line([(c - k, c - k), (c + k, c + k)], fill=255, width=w)
    d.line([(c - k, c + k), (c + k, c - k)], fill=255, width=w)


def play(d, s, w):
    c, r = s / 2, s * 0.40
    d.ellipse([c - r, c - r, c + r, c + r], outline=255, width=max(6, w * 2 // 3))
    d.polygon([(s * 0.42, s * 0.33), (s * 0.42, s * 0.67), (s * 0.70, s * 0.50)], fill=255)


def icon_alert():
    s = 256
    disc = Image.new('L', (s, s), 0)
    ImageDraw.Draw(disc).ellipse([s * 0.14, s * 0.14, s * 0.86, s * 0.86], fill=255)
    mark = Image.new('L', (s, s), 0)
    md = ImageDraw.Draw(mark)
    md.rounded_rectangle([s * 0.455, s * 0.27, s * 0.545, s * 0.58], radius=s * 0.04, fill=255)
    md.ellipse([s * 0.45, s * 0.64, s * 0.55, s * 0.74], fill=255)
    base = glow_compose(disc, ((90, 175, 255), (40, 110, 240)), [(GLOW, 14, 1.3)])
    a = np.asarray(base).astype(np.float64)
    m = np.asarray(mark).astype(np.float64)[..., None] / 255
    a[..., :3] = a[..., :3] * (1 - m) + 255 * m
    save(Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA'), 'icon_alert.png')


# 판 안쪽(사각형)을 테두리에서 매끈하게 다시 메워 아이콘·글자를 지운다(쿤스 면 + 라플라스 다듬기)
def refill(a, x0, y0, x1, y1, iters=2500):
    top = np.median(a[y0 - 3:y0, x0:x1, :3], axis=0)
    bot = np.median(a[y1:y1 + 3, x0:x1, :3], axis=0)
    left = np.median(a[y0:y1, x0 - 3:x0, :3], axis=1)
    right = np.median(a[y0:y1, x1:x1 + 3, :3], axis=1)
    h, w = y1 - y0, x1 - x0
    u = (np.arange(w) + 0.5)[None, :, None] / w
    v = (np.arange(h) + 0.5)[:, None, None] / h
    fill = ((1 - v) * top[None] + v * bot[None] + (1 - u) * left[:, None] + u * right[:, None]
            - ((1 - u) * (1 - v) * top[0] + u * (1 - v) * top[-1] + (1 - u) * v * bot[0] + u * v * bot[-1]))
    ext = a[y0 - 1:y1 + 1, x0 - 1:x1 + 1, :3].copy()
    ext[1:-1, 1:-1] = fill
    for _ in range(iters):
        ext[1:-1, 1:-1] = (ext[:-2, 1:-1] + ext[2:, 1:-1] + ext[1:-1, :-2] + ext[1:-1, 2:]) / 4
    out = a.copy()
    out[y0:y1, x0:x1, :3] = ext[1:-1, 1:-1]
    return out


def blank_plate(src, box, name):
    a = np.asarray(Image.open(SRC + src).convert('RGBA')).astype(np.float64)
    out = refill(a, *box)
    save(Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), 'RGBA'), name)


if __name__ == '__main__':
    icon_ring()
    line_icon('icon_clock.png', clock)
    line_icon('icon_chat.png', chat)
    line_icon('icon_sparkle.png', sparkle)
    line_icon('icon_flag.png', flag)
    line_icon('icon_back.png', back)
    line_icon('icon_bulb.png', bulb, color=(120, 235, 255))
    line_icon('icon_check.png', check)
    line_icon('arrow_up.png', lambda d, s, w: arrow(d, s, w, True), stroke=22)
    line_icon('arrow_down.png', lambda d, s, w: arrow(d, s, w, False), stroke=22)
    crop_icon('14.png', (230, 244, 514, 485), 30, 'icon_bars.png')
    crop_icon('15.png', (182, 181, 613, 576), 30, 'icon_chat_fill.png')
    for i, (f, box) in enumerate([('15.png', (182, 181, 613, 576)), ('16.png', (485, 210, 774, 544)),
                                  ('17.png', (404, 158, 736, 583)), ('18.png', (342, 225, 714, 529))]):
        crop_icon(f, box, 30, f'icon_row{i}.png')
    panel('chip.png', 360, 120, 60, (14, 40, 96), (10, 30, 80), 0.45, (90, 225, 255, 0.95), 7, 10, 0.35)
    panel('bubble_npc.png', 600, 168, 52, (20, 38, 84), (12, 26, 66), 0.88, (150, 195, 255, 0.6), 5, 10, 0.18,
          tail=(0, 84, 22, 18))
    panel('bubble_user.png', 600, 168, 34, (40, 120, 255), (24, 86, 236), 0.96, (140, 215, 255, 0.95), 5, 14, 0.5)
    bar_fill('fill_blue.png', (52, 96, 250), (86, 226, 255))
    bar_fill('fill_violet.png', (104, 84, 246), (222, 226, 255))
    bar_fill('fill_cyan.png', (24, 196, 226), (96, 250, 255))
    dot()
    badge_blank()
    radar_grid()
    # 정확도 모드 리포트
    line_icon('icon_target.png', target)
    line_icon('icon_aim.png', aim)
    line_icon('icon_repeat.png', repeat)
    line_icon('icon_warning.png', warning)
    line_icon('icon_gauge.png', gauge)
    line_icon('icon_wave.png', wave)
    line_icon('icon_diamond.png', diamond)
    line_icon('icon_link.png', link)
    line_icon('icon_play.png', play)
    line_icon('icon_stop.png', stop)   # 재생 중에 재생 버튼 자리에 보이는 멈춤
    icon_alert()
    blank_plate('10.png', (540, 205, 1640, 515), 'nav_normal.png')
    blank_plate('11.png', (540, 195, 1700, 525), 'nav_active.png')
    blank_plate('13.png', (640, 262, 1420, 508), 'btn_retry_blank.png')
    panel('row_plate.png', 600, 200, 40, (34, 70, 150), (20, 46, 112), 0.55, (150, 200, 255, 0.55), 5, 10, 0.18)
    panel('tag_pink.png', 360, 120, 60, (110, 24, 70), (80, 16, 54), 0.55, (255, 120, 170, 0.95), 7, 10, 0.35,
          glow=(255, 90, 150))
    # 전체 대화 기록 창: 여러 줄 말풍선은 세로로 늘어나므로 점원 꼬리를 위쪽(늘어나지 않는 칸)에 둔 말풍선 + 닫기 X
    panel('bubble_npc_top.png', 600, 168, 52, (20, 38, 84), (12, 26, 66), 0.88, (150, 195, 255, 0.6), 5, 10, 0.18,
          tail=(0, 52, 22, 16))
    line_icon('icon_close.png', close_x)
    # 기간 탭 막대: 선택 표시(1.png, 양 끝 반원)와 곡률이 같은 알약 모양(9.png는 모서리만 둥근 사각이라 안 맞았다)
    panel('tab_bar.png', 900, 100, 50, (16, 36, 92), (8, 22, 66), 0.62, (140, 200, 255, 0.85), 5, 12, 0.3)
