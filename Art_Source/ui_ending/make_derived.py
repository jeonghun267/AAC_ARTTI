# 편의점 훈련 완료 화면(ending 시안)에 쓰는 파생 이미지를 디자이너 원본에서 만든다.
#   원본: Assets/_Project/Art/UI/Home/ending/2.png(점수 판) 3~5.png(정확도 / 잘한 표현 / 새 기록 판)
#   결과: Assets/_Project/Art/UI/Home/ending/derived/
#     score_card.png                  - 2.png에서 그림 속 별 5개를 지운 판(별은 점수만큼 따로 켠다) + 바깥 빛
#     star_on.png, star_off.png       - 2.png의 별을 떼어 낸 것(켜짐) / 어둡게 바꾼 것(꺼짐)
#     stat_accuracy.png               - 3.png. 세 판의 몸통 비율(가로/세로 1.25)을 맞추려고 빈 유리 줄을 세로로 늘린 것 + 바깥 빛
#     stat_expression.png             - 4.png. 위와 같다
#     stat_record.png                 - 5.png('새 기록' '최고 점수!'가 그려진 판). 위와 같다
#     stat_record_blank.png           - stat_record.png에서 '새 기록' '최고 점수!' 글자를 지운 판(최고 기록이 아닐 때 TMP 글자로)
#     title.png                       - "참 잘했어요!" 제목 + 왕관(원본에 없어 직접 그림)
#     hand_growth.png, hand_cheer.png - 왼쪽 손글씨 문구 두 개(원본 배경에 없어 직접 그림, 나눔바른펜)
# 디자이너가 진짜 그림을 주면 같은 이름으로 바꿔 넣으면 된다.
# 원본을 바꾸면 이 스크립트를 다시 돌리고 Unity에서 Artti/Fix/TrainingConvenience - Apply Ending Design을 실행한다.
#   그림 속 몸통 좌표가 바뀌면 ConvenienceTrainingConLayout.Ending.cs의 측정값도 같이 고친다(실행하면 출력된다).
# 필요: Python 3 + numpy + Pillow.   실행: python Art_Source/ui_ending/make_derived.py
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
SRC = os.path.join(ROOT, 'Assets/_Project/Art/UI/Home/ending/').replace(os.sep, '/')
OUT = os.path.join(SRC, 'derived')
os.makedirs(OUT, exist_ok=True)

FONT_DIR = 'C:/Windows/Fonts/'
TITLE_FONT = FONT_DIR + 'NotoSansKR-VF.ttf'
HAND_FONT = FONT_DIR + 'NanumBarunpenR.ttf'

STAT_ASPECT = 1.25          # 세 기록 판 몸통 가로/세로
GLOW = (70, 160, 255)       # 판 바깥 빛 색


def load(name):
    return np.asarray(Image.open(SRC + name).convert('RGBA')).astype(np.float64)


def save(a, name):
    Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, name))
    print('saved', name, a.shape[1], 'x', a.shape[0])


def body(a, thr=245):
    ys, xs = np.where(a[..., 3] >= thr)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def dilate(mask, r):
    m = np.pad(mask.astype(np.int32), r)
    c = np.pad(m.cumsum(0).cumsum(1), ((1, 0), (1, 0)))
    k = 2 * r + 1
    return (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) > 0


# ===== 글자·별 지우기: 영역 테두리에서 메운 뒤(쿤스 면) 지울 화소만 이웃 평균으로 다듬는다 =====
def coons(a, x0, y0, x1, y1):
    """영역 [x0,x1) x [y0,y1)의 RGB를 바깥 테두리 줄(위·아래·왼쪽·오른쪽)에서 매끈하게 이어 메운 값."""
    def strip(arr):
        return np.median(arr, axis=0)
    top = strip(a[y0 - 3:y0, x0:x1, :3])
    bot = strip(a[y1:y1 + 3, x0:x1, :3])
    left = np.median(a[y0:y1, x0 - 3:x0, :3], axis=1)
    right = np.median(a[y0:y1, x1:x1 + 3, :3], axis=1)
    h, w = y1 - y0, x1 - x0
    u = (np.arange(w) + 0.5)[None, :, None] / w
    v = (np.arange(h) + 0.5)[:, None, None] / h
    tl, tr = top[0], top[-1]
    bl, br = bot[0], bot[-1]
    return ((1 - v) * top[None] + v * bot[None] + (1 - u) * left[:, None] + u * right[:, None]
            - ((1 - u) * (1 - v) * tl + u * (1 - v) * tr + (1 - u) * v * bl + u * v * br))


def erase(a, x0, y0, x1, y1, pick, grow=5, iters=500):
    """영역 안에서 pick(영역 RGBA, 테두리로 메운 RGB)이 True인 화소(+grow 픽셀)를 지우고 둘레 유리로 메운다."""
    out = a.copy()
    region = out[y0:y1, x0:x1]
    fill = coons(a, x0, y0, x1, y1)
    mask = dilate(pick(region, fill), grow)
    # 영역 바깥 1픽셀(원본 그대로)을 경계값으로 두고, 지운 화소만 상하좌우 평균(라플라스 방정식)으로 다듬는다
    ext = a[y0 - 1:y1 + 1, x0 - 1:x1 + 1, :3].copy()
    inner = ext[1:-1, 1:-1]
    inner[mask] = fill[mask]
    for _ in range(iters):
        avg = (ext[:-2, 1:-1] + ext[2:, 1:-1] + ext[1:-1, :-2] + ext[1:-1, 2:]) / 4
        inner[mask] = avg[mask]
    # 둘레 유리의 잔결(고운 노이즈)을 조금 얹어 메운 자리가 매끈하게 튀지 않게
    rng = np.random.default_rng(7)
    inner[mask] += rng.normal(0, 1.6, inner.shape)[mask]
    region[..., :3] = inner
    return out, mask


# ===== 바깥 빛: 몸통 알파를 흐려 판 뒤에 깐다(원본은 빛 번짐이 RGB에만 있고 알파가 거의 0) =====
def add_glow(a, pad, radius, strength):
    h, w = a.shape[:2]
    canvas = np.zeros((h + 2 * pad, w + 2 * pad, 4))
    canvas[pad:pad + h, pad:pad + w] = a
    alpha = Image.fromarray((canvas[..., 3]).astype(np.uint8), 'L')
    g = np.asarray(alpha.filter(ImageFilter.GaussianBlur(radius))).astype(np.float64) / 255
    g = np.clip(g * strength, 0, 1)
    fa = canvas[..., 3:4] / 255
    out_a = fa + g[..., None] * (1 - fa)
    glow_rgb = np.array(GLOW, dtype=np.float64)
    rgb = (canvas[..., :3] * fa + glow_rgb * g[..., None] * (1 - fa)) / np.maximum(out_a, 1e-6)
    canvas[..., :3] = rgb
    canvas[..., 3] = out_a[..., 0] * 255
    return canvas


# ===== 빈 유리 줄을 세로로 늘려 몸통 비율을 맞춘다 =====
def stretch_rows(a, bands):
    """bands = [(y0, y1, extra)] 위에서부터. 각 줄 묶음 [y0,y1)을 extra 픽셀만큼 세로로 늘린다."""
    parts, last = [], 0
    for y0, y1, extra in bands:
        parts.append(a[last:y0])
        band = Image.fromarray(np.clip(a[y0:y1], 0, 255).astype(np.uint8), 'RGBA')
        band = band.resize((a.shape[1], y1 - y0 + extra), Image.BICUBIC)
        parts.append(np.asarray(band).astype(np.float64))
        last = y1
    parts.append(a[last:])
    return np.concatenate(parts, axis=0)


def stat_bands(a, plain0, plain1):
    """몸통 비율이 STAT_ASPECT가 되게 [plain0, plain1) 줄을 늘릴 양."""
    x0, y0, x1, y1 = body(a)
    want = round((x1 - x0) / STAT_ASPECT)
    return [(plain0, plain1, max(0, want - (y1 - y0)))]


def report_body(name, a):
    x0, y0, x1, y1 = body(a)
    print(f'  {name}: size {a.shape[1]}x{a.shape[0]} body ({x0},{y0})-({x1},{y1}) = {x1 - x0}x{y1 - y0}')


# ===== 1) 점수 판: 별 지우기 + 별 떼어 내기 =====
def build_score_card():
    a = load('2.png')
    # 별 5개(노랑, 오른쪽으로 갈수록 살짝 아래) 영역. 별 그림자·빛까지 영역째 다시 메운다.
    #   아래 가로 빛줄(y 약 705)과 양옆 월계수·반짝이는 영역 밖이다
    X0, Y0, X1, Y1 = 432, 546, 1110, 698

    def whole(r, fill):
        return np.ones(r.shape[:2], dtype=bool)

    blank, mask = erase(a, X0, Y0, X1, Y1, whole, grow=0, iters=3000)
    # 첫 별: 원본과 빈 판의 차이로 알파를 구해 별만 떼어 낸다(바깥 노란 번짐 포함)
    cx0, cy0, cx1, cy1 = 438, 552, 568, 676
    o = a[cy0:cy1, cx0:cx1]
    b = blank[cy0:cy1, cx0:cx1]
    diff = np.abs(o[..., :3] - b[..., :3]).max(axis=2)
    alpha = np.clip((diff - 8) / 70, 0, 1)
    # 칸 가장자리까지 번진 옅은 빛은 둥글게 잘라 낸다(별 반지름 약 54px, 별 가운데 = 첫 별 중심)
    yy, xx = np.mgrid[cy0:cy1, cx0:cx1]
    dist = np.hypot(xx - 503, yy - 613)
    alpha *= np.clip((64 - dist) / 10, 0, 1)
    color = b[..., :3] + (o[..., :3] - b[..., :3]) / np.maximum(alpha[..., None], 1e-3)
    star = np.zeros(o.shape)
    star[..., :3] = np.clip(color, 0, 255)
    star[..., 3] = alpha * 255
    save(star, 'star_on.png')
    # 꺼진 별: 색을 빼고 남색 유리 위에 어둡게 비치는 별(빛 번짐은 줄인다)
    lumv = 0.299 * star[..., 0] + 0.587 * star[..., 1] + 0.114 * star[..., 2]
    off = star.copy()
    off[..., 0] = 44 + lumv * 0.22
    off[..., 1] = 74 + lumv * 0.28
    off[..., 2] = 140 + lumv * 0.34
    off[..., 3] = (alpha ** 1.6) * 255 * 0.6
    save(off, 'star_off.png')
    card = add_glow(blank, 0, 16, 1.6)
    save(card, 'score_card.png')
    report_body('score_card', card)
    # 원본 별 중심(그림 px): 위치 기준으로 C# 쪽에서 쓴다
    print('  star cell', (cx0, cy0, cx1, cy1), 'original centers x 503,638,771.5,906,1039 / y 613,617.5,622.5,627.5,632')


# ===== 2) 기록 판 세 장 =====
def build_stat_cards():
    pad, radius, strength = 70, 30, 1.6

    a = load('3.png')
    a = stretch_rows(a, stat_bands(a, 640, 900))
    a = add_glow(a, pad, radius, strength)
    save(a, 'stat_accuracy.png')
    report_body('stat_accuracy', a)

    a = load('4.png')
    a = stretch_rows(a, stat_bands(a, 590, 930))
    a = add_glow(a, pad, radius, strength)
    save(a, 'stat_expression.png')
    report_body('stat_expression', a)

    a = load('5.png')
    x0, y0, x1, y1 = body(a)
    extra = round((x1 - x0) / STAT_ASPECT) - (y1 - y0)
    top_extra = extra * 55 // 100
    bands = [(558, 596, top_extra), (742, 774, extra - top_extra)]

    def deviates(r, fill, thr):   # 둘레 유리에서 벗어난 화소 = 글자 + 빛 + 그림자
        return np.abs(r[..., :3] - fill).max(axis=2) > thr

    def white_text(r, fill):   # '새 기록' 흰 글자(+빛·그림자). 영역 왼쪽 끝(아이콘 원 쪽)은 건드리지 않는다
        m = deviates(r, fill, 12)
        m[:, :8] = False
        return m

    # '최고 점수!' 금색 글자의 빛 번짐은 넓게(x 240~1030, y 520~770) 퍼져 있어 그 영역을 통째로 다시 메운다.
    #   영역 위 왼쪽에 걸친 아이콘 원(가운데 360,445, 테두리 빛 포함 반지름 약 122)만 남겨 두고 경계로 쓴다.
    GX0, GY0, GX1, GY1 = 232, 516, 1036, 774

    def gold_text(r, fill):
        yy, xx = np.mgrid[GY0:GY1, GX0:GX1]
        icon = ((xx - 360) / 124.0) ** 2 + ((yy - 445) / 118.0) ** 2 <= 1
        return ~icon

    blank, _ = erase(a, 500, 392, 830, 506, white_text, grow=5)
    blank, _ = erase(blank, GX0, GY0, GX1, GY1, gold_text, grow=0, iters=3000)
    full = add_glow(stretch_rows(a, bands), pad, radius, strength)
    blank = add_glow(stretch_rows(blank, bands), pad, radius, strength)
    save(full, 'stat_record.png')
    save(blank, 'stat_record_blank.png')
    report_body('stat_record', full)
    print(f'  stat_record bands {bands} -> 글자/띠 y는 아래로 {top_extra}(제목줄 아래) / {extra}(최고 점수 아래) 밀린다 (+pad {pad})')


# ===== 3) 제목·손글씨 =====
def text_layer(size, draw_fn):
    img = Image.new('L', size, 0)
    draw_fn(ImageDraw.Draw(img))
    return img


def glow_compose(mask, fill_top, fill_bottom, glow_layers):
    """흰(그라데이션) 글자 mask + 바깥 빛 여러 겹을 RGBA로."""
    w, h = mask.size
    m = np.asarray(mask).astype(np.float64) / 255
    out_rgb = np.zeros((h, w, 3))
    out_a = np.zeros((h, w))
    for color, radius, strength in glow_layers:
        g = np.asarray(mask.filter(ImageFilter.GaussianBlur(radius))).astype(np.float64) / 255
        g = np.clip(g * strength, 0, 1)
        c = np.array(color, dtype=np.float64)
        out_rgb = (c * g[..., None] + out_rgb * out_a[..., None] * (1 - g[..., None])) / np.maximum(
            g + out_a * (1 - g), 1e-6)[..., None]
        out_a = g + out_a * (1 - g)
    t = np.linspace(0, 1, h)[:, None, None]
    fill = np.array(fill_top, dtype=np.float64) * (1 - t) + np.array(fill_bottom, dtype=np.float64) * t
    out_rgb = (fill * m[..., None] + out_rgb * out_a[..., None] * (1 - m[..., None])) / np.maximum(
        m + out_a * (1 - m), 1e-6)[..., None]
    out_a = m + out_a * (1 - m)
    return np.dstack([out_rgb, out_a * 255])


def crown_lines(d, cx, cy, s, width):
    """왕관 윤곽(아래 띠 + 뾰족 3개 + 끝 구슬). cx, cy = 아래 띠 가운데, s = 크기."""
    pts = [(-0.5, 0), (-0.58, -0.62), (-0.25, -0.28), (0, -0.78), (0.25, -0.28), (0.58, -0.62), (0.5, 0)]
    pts = [(cx + x * s, cy + y * s) for x, y in pts]
    d.line(pts + [pts[0]], fill=255, width=width, joint='curve')
    d.line([(cx - 0.5 * s, cy + 0.14 * s), (cx + 0.5 * s, cy + 0.14 * s)], fill=255, width=width)
    for x, y in [(-0.58, -0.62), (0, -0.78), (0.58, -0.62)]:
        r = width * 0.9
        px, py = cx + x * s, cy + y * s - r * 0.6
        d.ellipse([px - r, py - r, px + r, py + r], outline=255, width=max(2, width // 2))


def build_title():
    S = 3   # 시안 px의 3배로 그린다
    font = ImageFont.truetype(TITLE_FONT, 116 * S)
    try:
        font.set_variation_by_axes([900])
    except Exception:
        pass
    text = '참 잘했어요!'
    pad = 60 * S // 2
    tmp = Image.new('L', (10, 10))
    bb = ImageDraw.Draw(tmp).textbbox((0, 0), text, font=font)
    crown_h = 60 * S
    w = bb[2] - bb[0] + 2 * pad
    h = bb[3] - bb[1] + 2 * pad + crown_h
    ox, oy = pad - bb[0], pad + crown_h - bb[1]

    def draw(d):
        d.text((ox, oy), text, font=font, fill=255)

    mask = text_layer((w, h), draw)
    # 왕관: '요' 오른쪽 위, 살짝 기울여서 (시안: 제목 끝에서 약 50px 왼쪽, 제목 위 30px)
    crown = Image.new('L', (90 * S, 90 * S), 0)
    crown_lines(ImageDraw.Draw(crown), 45 * S, 62 * S, 46 * S, 4 * S)
    crown = crown.rotate(-14, resample=Image.BICUBIC, center=(45 * S, 50 * S))
    text_right = ox + bb[2]
    mask.paste(Image.eval(crown, lambda v: v), (int(text_right - 118 * S), int(oy + bb[1] - 76 * S)), crown)
    rgba = glow_compose(mask, (255, 255, 255), (214, 238, 255),
                        [((40, 110, 255), 26 * S // 2, 1.3), ((120, 200, 255), 8 * S // 2, 1.6)])
    save(rgba, 'title.png')
    tb = (ox + bb[0], oy + bb[1], ox + bb[2], oy + bb[3])
    print(f'  title text body {tb}  (시안 제목 글자 상자 662,135 - 1140,240)')


def build_hand(name, lines, offsets, size, angle, extras):
    S = 3
    font = ImageFont.truetype(HAND_FONT, size * S)
    lh = int(size * S * 1.18)
    w = int(max(off + ImageDraw.Draw(Image.new('L', (1, 1))).textlength(t, font=font) for t, off in zip(lines, [o * S for o in offsets])) + 120 * S)
    h = lh * len(lines) + 120 * S

    def draw(d):
        for i, (t, off) in enumerate(zip(lines, offsets)):
            d.text((30 * S + off * S, 20 * S + i * lh), t, font=font, fill=255)
        extras(d, S, lh, font)

    mask = text_layer((w, h), draw)
    mask = mask.rotate(angle, resample=Image.BICUBIC, expand=True)
    bbox = mask.getbbox()
    mask = mask.crop((bbox[0] - 24 * S, bbox[1] - 24 * S, bbox[2] + 24 * S, bbox[3] + 24 * S))
    rgba = glow_compose(mask, (236, 244, 255), (214, 232, 255), [((70, 150, 255), 6 * S, 1.2)])
    save(rgba, name)
    print(f'  {name} ink box w {bbox[2] - bbox[0]} h {bbox[3] - bbox[1]} (pad {24 * S}, 3x)')


def smiley(d, S, lh, font):
    x, y = 30 * S + 2 * S, 20 * S + 4 * lh + 6 * S
    r = 15 * S
    d.arc([x, y, x + 2 * r, y + 2 * r], 20, 160, fill=255, width=2 * S)
    d.ellipse([x + 8 * S, y + 2 * S, x + 11 * S, y + 7 * S], fill=255)
    d.ellipse([x + 19 * S, y + 2 * S, x + 22 * S, y + 7 * S], fill=255)


def heart_after(d, S, lh, font):
    line = '당신을 응원해요.'
    x = 30 * S + 22 * S + d.textlength(line, font=font) + 10 * S
    y = 20 * S + lh + 6 * S
    s = 11 * S
    pts = []
    for i in range(41):   # 하트 곡선(윤곽만)
        t = np.pi * 2 * i / 40
        hx = 16 * np.sin(t) ** 3
        hy = -(13 * np.cos(t) - 5 * np.cos(2 * t) - 2 * np.cos(3 * t) - np.cos(4 * t))
        pts.append((x + s + hx * s / 16, y + s + hy * s / 16))
    d.line(pts, fill=255, width=2 * S, joint='curve')


if __name__ == '__main__':
    build_score_card()
    build_stat_cards()
    build_title()
    build_hand('hand_growth.png', ['당신의', '성장이', '더 나은 내일을', '만듭니다.'], [6, 18, 0, 18], 33, 4, smiley)
    build_hand('hand_cheer.png', ['늘 멋진', '당신을 응원해요.'], [0, 22], 33, 12, heart_after)
