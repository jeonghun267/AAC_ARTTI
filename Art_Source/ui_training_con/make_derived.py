# 편의점 훈련 화면(TrainingConvenienceScene, TrainingCon 시안)에 쓰는 파생 이미지를 디자이너 원본에서 만든다.
#   원본: Assets/_Project/Art/UI/TrainingCon/*.png, Assets/_Project/Art/UI/Training/Dashboard/product_*.png, category_snacks.png
#   결과: Assets/_Project/Art/UI/TrainingCon/derived/
#     bubble_blank.png, bubble_blank_on.png   - 말풍선(보통 / 강조)에서 아이콘과 글자를 지운 빈 말풍선 (글자는 Unity TMP로)
#     icon_*.png                              - 말풍선 문장 앞 아이콘 (말풍선·힌트 버튼에서 떼어 남색으로)
#     hint_request_item.png                   - '물건 요청하기' 버튼을 다른 힌트 버튼과 같은 해상도로 줄인 것(원본은 4.6배 크기)
#     product_tile.png, thumb_*.png           - 추천 상품 칸 바탕과 상품 그림(옛 상품 카드에서 글자를 뺀 부분)
#     title_chat.png, title_bulb.png, title_star.png, title_cart.png - 패널 제목 아이콘(원본에 없어 직접 그림)
#     node_on.png, node_off.png               - 미션 물결 별 표시(지난 별 = 빛 고리, 남은 별 = 어둡게)
#     spot_frame.png                          - 화면 둘러보기(온보딩)에서 짚은 곳 둘레의 빛 테두리(9-slice, 테두리 40px)
#     bottom_glass.png                        - 화면 아래 가로 유리 띠(가운데가 살짝 솟은 곡선 + 하늘색 테두리). 시안 측정값으로 그림
# 디자이너가 진짜 그림을 주면 같은 이름으로 바꿔 넣으면 된다(레이아웃은 Unity 메뉴가 다시 맞춘다).
# 원본을 바꾸면 이 스크립트를 다시 돌리고 Unity에서 Artti/Fix/TrainingConvenience - Apply TrainingCon Design을 실행한다.
# 필요: Python 3 + numpy + Pillow.   실행: python Art_Source/ui_training_con/make_derived.py
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
SRC = os.path.join(ROOT, 'Assets/_Project/Art/UI/TrainingCon/').replace(os.sep, '/')
OLD = os.path.join(ROOT, 'Assets/_Project/Art/UI/Training/Dashboard/').replace(os.sep, '/')
OUT = os.path.join(SRC, 'derived')
os.makedirs(OUT, exist_ok=True)

NAVY = (23, 52, 128)


def load(path):
    return np.asarray(Image.open(path).convert('RGBA')).astype(np.float64)


def save(a, name):
    Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, name))
    print('saved', name, a.shape[1], 'x', a.shape[0])


def lum(a):
    return 0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2]


def box_blur(a, r):
    p = np.pad(a, r, mode='edge')
    c = np.pad(p.cumsum(0), ((1, 0), (0, 0)))
    p = (c[2 * r + 1:] - c[:-2 * r - 1]) / (2 * r + 1)
    c = np.pad(p.cumsum(1), ((0, 0), (1, 0)))
    return (c[:, 2 * r + 1:] - c[:, :-2 * r - 1]) / (2 * r + 1)


def ink_groups(ink_cols, min_gap):
    """잉크가 있는 열을 빈 칸(min_gap 이상)으로 나눈 [시작, 끝) 묶음."""
    groups, start, gap = [], None, 0
    for x, has in enumerate(ink_cols):
        if has:
            if start is None:
                start = x
            gap = 0
            end = x + 1
        elif start is not None:
            gap += 1
            if gap >= min_gap:
                groups.append((start, end))
                start = None
    if start is not None:
        groups.append((start, end))
    return groups


# ===== 1) 빈 말풍선 + 말풍선 아이콘 =====
def erode(mask, r):
    """mask를 r픽셀 안쪽으로 줄인다(가장자리 r픽셀 안의 화소는 False)."""
    m = np.pad(mask.astype(np.int32), r)
    c = m.cumsum(0).cumsum(1)
    c = np.pad(c, ((1, 0), (1, 0)))
    k = 2 * r + 1
    win = c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]
    return win == k * k


def split_bubble(name, clean_x, min_gap, edge):
    """밝은 말풍선 위 남색 잉크(아이콘+글자)를 찾아 지운 빈 말풍선과, 첫 잉크 묶음(아이콘)을 돌려준다.
    edge: 말풍선 테두리(어두운 외곽선) 두께. 이만큼 안쪽에서만 잉크를 찾는다."""
    a = load(SRC + name)
    L = lum(a)
    clean = a[:, clean_x, :]          # 행마다 글자가 없는 안쪽 색
    clean_L = lum(clean)
    inside = erode(a[..., 3] >= 245, edge) & (clean[:, None, 3] >= 245)
    ink = inside & (L < clean_L[:, None] - 10)
    # 안쪽 색은 가로로 고르다. 테두리 안쪽(edge) 영역을 행마다 깨끗한 열의 색으로 다시 칠하면
    # 글자·아이콘과 그 둘레의 옅은 빛 번짐이 모두 사라진다. 가장자리는 부드럽게 섞어 이음매를 없앤다
    soft = erode(a[..., 3] >= 245, edge + 4).astype(np.float64)
    soft = np.clip(box_blur(soft, 3) * 1.4, 0, 1)
    soft[:, clean_x:] = 0
    blank = a.copy()
    blank[..., :3] = a[..., :3] * (1 - soft[..., None]) + clean[:, None, :3] * soft[..., None]

    # 꼬리 근처 작은 얼룩은 버리고 아이콘만 한 너비의 첫 묶음을 고른다
    groups = [g for g in ink_groups(ink.any(axis=0), min_gap) if g[1] - g[0] >= 3 * edge]
    x0, x1 = groups[0]
    ys = np.nonzero(ink[:, x0:x1].any(axis=1))[0]
    y0, y1 = ys[0], ys[-1] + 1
    pad = 3
    x0, y0 = max(0, x0 - pad), max(0, y0 - pad)
    x1, y1 = min(a.shape[1], x1 + pad), min(a.shape[0], y1 + pad)
    crop = a[y0:y1, x0:x1]
    bg = clean_L[y0:y1][:, None]
    ink_L = np.percentile(L[y0:y1, x0:x1][ink[y0:y1, x0:x1]], 5)
    alpha = np.clip((bg - lum(crop)) / np.maximum(1.0, bg - ink_L), 0, 1)
    icon = np.zeros_like(crop)
    icon[..., 0], icon[..., 1], icon[..., 2] = NAVY
    icon[..., 3] = alpha * 255
    return blank, icon, (x0, y0, x1, y1), groups


def light_icon_from_button(name, min_gap):
    """어두운 버튼 위 흰 아이콘을 남색 아이콘으로 떼어낸다(흰 부분 = 남색, 안쪽 구멍은 투명)."""
    a = load(SRC + name)
    L = lum(a)
    h, w = L.shape
    y_mid = slice(int(h * 0.2), int(h * 0.8))
    fill_L = np.median(L[y_mid, int(w * 0.75):int(w * 0.85)])
    ink = np.zeros_like(L, dtype=bool)
    ink[y_mid, 40:] = L[y_mid, 40:] > fill_L + 60
    groups = [g for g in ink_groups(ink.any(axis=0), min_gap) if g[1] - g[0] >= 12]
    x0, x1 = groups[0]
    ys = np.nonzero(ink[:, x0:x1].any(axis=1))[0]
    y0, y1 = ys[0] - 3, ys[-1] + 4
    x0, x1 = x0 - 3, x1 + 3
    crop = L[y0:y1, x0:x1]
    white = np.percentile(crop, 98)
    alpha = np.clip((crop - fill_L - 8) / max(1.0, white - fill_L - 8), 0, 1)
    icon = np.zeros((y1 - y0, x1 - x0, 4))
    icon[..., 0], icon[..., 1], icon[..., 2] = NAVY
    icon[..., 3] = alpha * 255
    return icon


def make_bubbles():
    blank, icon, _, groups = split_bubble('bubble_how_much.png', 520, 14, 8)
    save(blank, 'bubble_blank.png')
    save(icon, 'icon_question.png')
    print('  how_much ink groups', groups[:3])
    for src, out in [('bubble_pay_by_card.png', 'icon_card.png'), ('bubble_bag_please.png', 'icon_bag.png'),
                     ('bubble_thank_you.png', 'icon_smile.png')]:
        _, icon, box, groups = split_bubble(src, 520, 14, 8)
        save(icon, out)
        print('  ', src, 'icon box', box)
    blank_on, icon_on, box, groups = split_bubble('bubble_give_me_this.png', 1700, 50, 26)
    # 강조 말풍선은 원본이 4배 크기라 보통 말풍선과 비슷한 해상도로 줄인다
    on = Image.fromarray(blank_on.astype(np.uint8), 'RGBA')
    on = on.resize((on.width // 3, on.height // 3), Image.LANCZOS)
    save(np.asarray(on).astype(np.float64), 'bubble_blank_on.png')
    ic = Image.fromarray(np.clip(icon_on, 0, 255).astype(np.uint8), 'RGBA')
    ic = ic.resize((ic.width // 3, ic.height // 3), Image.LANCZOS)
    save(np.asarray(ic).astype(np.float64), 'icon_basket.png')
    print('  give_me_this icon box', box)
    save(light_icon_from_button('btn_ask_price.png', 12), 'icon_won.png')
    save(light_icon_from_button('btn_field_mode.png', 12), 'icon_pin.png')


# ===== 2) 대화 힌트 버튼 =====
def make_hint_buttons():
    # '물건 요청하기'만 원본이 4.6배 크기라 다른 힌트 버튼(467px 폭)과 같은 해상도로 줄인다
    im = Image.open(SRC + 'btn_request_item.png').convert('RGBA')
    im = im.resize((467, int(im.height * 467.0 / im.width)), Image.LANCZOS)
    save(np.asarray(im).astype(np.float64), 'hint_request_item.png')


# ===== 3) 추천 상품 칸 =====
def rounded_rect(size, radius, fill, outline=None, width=0, ss=4):
    w, h = size
    im = Image.new('RGBA', (w * ss, h * ss), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, w * ss - 1, h * ss - 1], radius * ss, fill=fill, outline=outline, width=width * ss)
    return im.resize((w, h), Image.LANCZOS)


def make_product_tile():
    w, h = 160, 264
    tile = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    body = rounded_rect((w, h), 22, (60, 92, 170, 120), (150, 190, 255, 110), 2)
    # 위쪽이 조금 밝은 유리 느낌
    grad = Image.new('L', (w, h))
    gd = ImageDraw.Draw(grad)
    for y in range(h):
        gd.line([(0, y), (w, y)], fill=int(70 * (1 - y / h)))
    hi = Image.new('RGBA', (w, h), (190, 215, 255, 0))
    hi.putalpha(grad)
    mask = rounded_rect((w, h), 22, (255, 255, 255, 255)).getchannel('A')
    hi_masked = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    hi_masked.paste(hi, (0, 0), Image.fromarray((np.asarray(grad) * (np.asarray(mask) / 255.0)).astype(np.uint8)))
    tile.alpha_composite(body)
    tile.alpha_composite(hi_masked)
    tile.save(os.path.join(OUT, 'product_tile.png'))
    print('saved product_tile.png', w, 'x', h)


PRODUCTS = [
    ('product_water.png', 'thumb_water.png'), ('product_triangle_gimbap.png', 'thumb_triangle_gimbap.png'),
    ('product_cup_ramen.png', 'thumb_cup_ramen.png'), ('category_snacks.png', 'thumb_snack.png'),
    ('product_chocolate.png', 'thumb_chocolate.png'), ('product_can_coffee.png', 'thumb_can_coffee.png'),
    ('product_milk.png', 'thumb_milk.png'), ('product_sandwich.png', 'thumb_sandwich.png'),
    ('product_gum.png', 'thumb_gum.png'), ('product_mask.png', 'thumb_mask.png'),
    ('product_tissue.png', 'thumb_tissue.png'), ('product_umbrella.png', 'thumb_umbrella.png'),
    ('product_charger.png', 'thumb_charger.png'),
]


def make_thumbs():
    size = 320
    for src, out in PRODUCTS:
        im = Image.open(OLD + src).convert('RGBA')
        w, h = im.size
        # 옛 상품 카드: 위 2/3에 상품, 아래에 글자. 글자 위까지의 정사각형만 쓴다
        if h > w:   # 1122x1402
            box = (int(w * 0.17), int(h * 0.12), int(w * 0.83), int(h * 0.12) + int(w * 0.66))
        else:       # 1254x1254
            box = (int(w * 0.20), int(h * 0.11), int(w * 0.80), int(h * 0.11) + int(w * 0.60))
        crop = im.crop(box).resize((size, size), Image.LANCZOS)
        # 파란 바탕이 칸 가장자리에서 자연스럽게 사라지도록 둥근 비네트
        yy, xx = np.mgrid[0:size, 0:size]
        r = np.sqrt((xx - size / 2) ** 2 + (yy - size / 2) ** 2) / (size / 2)
        vig = np.clip((1.0 - r) / 0.28, 0, 1)
        a = np.asarray(crop).astype(np.float64)
        a[..., 3] *= vig
        save(a, out)


# ===== 4) 제목 아이콘 (원본에 없음 - 임시 그림) =====
def canvas(n=128, ss=4):
    return Image.new('RGBA', (n * ss, n * ss), (0, 0, 0, 0)), ss


def finish(im, ss, name, glow=None):
    n = im.width // ss
    small = im.resize((n, n), Image.LANCZOS)
    if glow:
        g = Image.new('RGBA', small.size, glow + (0,))
        g.putalpha(small.getchannel('A').filter(ImageFilter.GaussianBlur(5)).point(lambda v: int(v * 0.7)))
        base = Image.new('RGBA', small.size, (0, 0, 0, 0))
        base.alpha_composite(g)
        base.alpha_composite(small)
        small = base
    small.save(os.path.join(OUT, name))
    print('saved', name)


def make_nodes():
    # 미션 물결(boatwater.png) 위 별 5개에 겹치는 표시. 켜짐 = 별 둘레 하늘색 빛 고리(가운데 비움), 꺼짐 = 별을 어둡게 덮는 남색 원.
    #   그림 64px 중 가운데 지름 38px = 물결 그림 속 별 원(지름 약 84px)에 맞춰 Unity에서 크기를 정한다.
    n, ss = 64, 4
    on = Image.new('RGBA', (n * ss, n * ss), (0, 0, 0, 0))
    d = ImageDraw.Draw(on)
    d.ellipse([12 * ss, 12 * ss, 52 * ss, 52 * ss], outline=(150, 238, 255, 255), width=3 * ss)
    on = on.resize((n, n), Image.LANCZOS)
    glow = on.getchannel('A').filter(ImageFilter.GaussianBlur(3)).point(lambda v: int(min(255, v * 1.5)))
    out = Image.new('RGBA', (n, n), (64, 214, 255, 0))
    out.putalpha(glow)
    out.alpha_composite(on)
    out.save(os.path.join(OUT, 'node_on.png'))
    print('saved node_on.png')
    off = Image.new('RGBA', (n * ss, n * ss), (0, 0, 0, 0))
    d = ImageDraw.Draw(off)
    d.ellipse([13 * ss, 13 * ss, 51 * ss, 51 * ss], fill=(8, 16, 48, 165))
    off.resize((n, n), Image.LANCZOS).save(os.path.join(OUT, 'node_off.png'))
    print('saved node_off.png')


def make_shade():
    # 화면 위쪽 글자(현장모드 옆 안내, 오른쪽 위 버튼)가 밝은 천장에 묻히지 않게 까는 세로 그늘 (위 진함 -> 아래 투명)
    h = 128
    a = np.zeros((h, 4, 4))
    a[..., 0], a[..., 1], a[..., 2] = 6, 16, 52
    t = np.linspace(0, 1, h)[:, None]
    a[..., 3] = (0.86 * (1 - t) ** 1.4) * 255
    save(a, 'shade_top.png')


def make_bottom_glass():
    # 시안 아래 유리 띠: 위 가장자리 y(시안 px) = 1001 + 25.7 * d^2.18 (d = 화면 가운데에서 떨어진 정도 0~1)
    #   가운데 1001, 좌우 1/3 지점 1009, 양 끝 약 1027. 띠 안쪽은 남색(19,39,84) 75% (시안 픽셀로 역산).
    #   Unity에서 화면 폭에 맞춰 가로로 늘이므로 곡선은 화면 폭 비율로 정의한다. 그림 1px = 시안 0.5px(세로).
    W, H, top = 2048, 184, 994.0          # 그림 맨 위 = 시안 y 994 (테두리 빛 여유)
    ss = 4                                # 세로 가장자리 계단 방지용 세분
    u = (np.arange(W) + 0.5) / W
    d = np.abs(u - 0.5) * 2
    edge = (1001.0 + 25.7 * d ** 2.18 - top) * 2   # 그림 px
    ys = (np.arange(H * ss) + 0.5) / ss
    inside = (ys[:, None] >= edge[None, :]).astype(np.float64)
    cover = inside.reshape(H, ss, W).mean(axis=1)
    y = np.arange(H)[:, None] + 0.5
    dist = y - edge[None, :]                       # 가장자리에서 아래(+)/위(-) 거리
    a = np.zeros((H, W, 4))
    fill = np.array([19, 39, 84.0])
    rim = np.array([196, 222, 255.0])
    rim_k = np.clip(1 - np.abs(dist - 1.5) / 2.5, 0, 1)          # 가장자리 바로 아래 얇은 밝은 선
    glow_k = np.clip(1 + dist / 10.0, 0, 1) * (dist < 0) * 0.28   # 가장자리 위로 번지는 옅은 빛
    color = fill * (1 - rim_k[..., None]) + rim * rim_k[..., None]
    alpha = np.maximum(cover * (0.75 * (1 - rim_k) + 0.9 * rim_k), glow_k)
    color = np.where((dist < 0)[..., None], rim, color)
    a[..., :3] = color
    a[..., 3] = alpha * 255
    save(a, 'bottom_glass.png')


def make_spot_frame():
    # 둘러보기 테두리: 안쪽은 비우고 둥근 하늘색 선 + 바깥으로 옅은 빛. Unity에서 9-slice(40px)로 아무 크기에나 맞춘다
    n, ss = 128, 4
    big = Image.new('RGBA', (n * ss, n * ss), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    m = 22
    d.rounded_rectangle([m * ss, m * ss, (n - m) * ss - 1, (n - m) * ss - 1], 18 * ss, outline=(120, 228, 255, 255), width=4 * ss)
    line = big.resize((n, n), Image.LANCZOS)
    glow = line.getchannel('A').filter(ImageFilter.GaussianBlur(6)).point(lambda v: int(min(255, v * 1.6)))
    out = Image.new('RGBA', (n, n), (64, 214, 255, 0))
    out.putalpha(glow)
    out.alpha_composite(line)
    out.save(os.path.join(OUT, 'spot_frame.png'))
    print('saved spot_frame.png')


def make_title_icons():
    cyan, light = (64, 214, 255, 255), (170, 236, 255, 255)
    # 말풍선(점 3개)
    im, s = canvas()
    d = ImageDraw.Draw(im)
    d.ellipse([10 * s, 14 * s, 108 * s, 98 * s], fill=cyan)
    d.polygon([(22 * s, 80 * s), (12 * s, 116 * s), (52 * s, 94 * s)], fill=cyan)
    d.ellipse([46 * s, 22 * s, 122 * s, 86 * s], fill=(110, 170, 255, 150))
    for cx in (36, 59, 82):
        d.ellipse([(cx - 7) * s, 49 * s, (cx + 7) * s, 63 * s], fill=(22, 50, 120, 255))
    finish(im, s, 'title_chat.png', glow=(64, 214, 255))
    # 전구
    im, s = canvas()
    d = ImageDraw.Draw(im)
    d.ellipse([34 * s, 18 * s, 94 * s, 78 * s], fill=light)
    d.polygon([(44 * s, 66 * s), (84 * s, 66 * s), (78 * s, 90 * s), (50 * s, 90 * s)], fill=light)
    for y in (94, 104):
        d.rounded_rectangle([50 * s, y * s, 78 * s, (y + 6) * s], 3 * s, fill=cyan)
    d.rounded_rectangle([56 * s, 113 * s, 72 * s, 119 * s], 3 * s, fill=cyan)
    for (x0, y0, x1, y1) in [(64, 2, 64, 10), (24, 18, 30, 24), (104, 18, 98, 24), (8, 48, 18, 48), (120, 48, 110, 48)]:
        d.line([(x0 * s, y0 * s), (x1 * s, y1 * s)], fill=cyan, width=5 * s)
    finish(im, s, 'title_bulb.png', glow=(64, 214, 255))
    # 별
    import math
    im, s = canvas()
    d = ImageDraw.Draw(im)
    pts = []
    for i in range(10):
        r = 58 if i % 2 == 0 else 25
        t = -math.pi / 2 + i * math.pi / 5
        pts.append(((64 + r * math.cos(t)) * s, (68 + r * math.sin(t)) * s))
    d.polygon(pts, fill=(255, 205, 64, 255))
    finish(im, s, 'title_star.png', glow=(255, 190, 60))
    # 장바구니(카트)
    im, s = canvas()
    d = ImageDraw.Draw(im)
    d.line([(6 * s, 20 * s), (26 * s, 20 * s), (40 * s, 84 * s), (104 * s, 84 * s)], fill=cyan, width=10 * s, joint='curve')
    d.polygon([(30 * s, 34 * s), (118 * s, 34 * s), (106 * s, 72 * s), (40 * s, 72 * s)], fill=cyan)
    for cx in (48, 98):
        d.ellipse([(cx - 10) * s, 94 * s, (cx + 10) * s, 114 * s], fill=cyan)
    finish(im, s, 'title_cart.png', glow=(64, 214, 255))


if __name__ == '__main__':
    make_bubbles()
    make_hint_buttons()
    make_product_tile()
    make_thumbs()
    make_title_icons()
    make_nodes()
    make_shade()
    make_bottom_glass()
    make_spot_frame()
