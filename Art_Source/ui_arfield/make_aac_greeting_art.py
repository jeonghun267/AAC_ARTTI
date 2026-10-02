# AR 현장모드 인사/결제/편의 화면(GreetingPanel / PaymentPanel / OtherPanel)에서 쓰는 파생 그림/네온 아이콘을 만든다.
#   결과: Assets/_Project/Art/UI/Generated/derived/
#     ui_aac_icon_pay_*.png         결제 카드: card 카드 / cash 현금(보라) / receipt 영수증 / point 포인트 별 / price 가격표(보라) / bag 봉투
#     ui_aac_icon_conv_*.png        편의 카드: restroom 화장실 / wifi 와이파이(보라) / charge 충전 / water 물 / seat 자리(보라) / question 물음표
#     ui_aac_panel_frame_glass.png  큰 틀(ui_aac_panel_frame) 안쪽을 반투명하게 한 것(테두리 빛은 그대로). 틀 안으로 배경이 비친다.
#     ui_aac_card_frame_glass.png   카드 틀(ui_aac_card_frame) 같은 방식
#     ui_aac_icon_hello.png         안녕하세요: 가운데 꼬리 말풍선 + 점 세 개     (파랑)
#     ui_aac_icon_nice.png          반가워요: 두 사람                              (보라)
#     ui_aac_icon_help.png          도와주세요: 구명환                             (파랑)
#     ui_aac_icon_thanks.png        감사합니다: 하트                               (파랑)
#     ui_aac_icon_sorry.png         죄송합니다: 모은 두 손                         (보라)
#     ui_aac_icon_again.png         다시 말해 주세요: 말풍선 + 소리 물결           (파랑)
#     ui_aac_icon_info.png          안내 바: 원 + i                                (파랑)
#     ui_aac_icon_ring.png          카드 아이콘 둘레: 옅은 원 + 반짝이는 점 4개(흰색, 카드 색은 Image.color로)
#   네온 모양(흰 심 + 색 빛 번짐)은 make_helpdetail_icons.py의 함수를 그대로 쓴다.
#   실행: python Art_Source/ui_arfield/make_aac_greeting_art.py      필요: Python 3 + numpy + Pillow
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_helpdetail_icons import N, SIZE, STROKE, OUT, ROOT, canvas, dot, polyline, compose, payment as card_shape   # noqa: E402

GEN = os.path.join(OUT, '..')
BLUE = (70, 150, 255)
PURPLE = (176, 110, 255)

# 틀 안쪽 반투명: 밝은 테두리/빛(가장 밝은 채널이 GLASS_LO 이상)은 그대로, 어두운 안쪽은 알파를 k배로
GLASS_LO, GLASS_HI = 120, 210
PANEL_GLASS, CARD_FRAME_GLASS = 0.35, 0.5


def glass(name, k):
    a = np.asarray(Image.open(os.path.join(GEN, name + '.png')).convert('RGBA')).astype(float)
    t = np.clip((a[:, :, :3].max(axis=2) - GLASS_LO) / (GLASS_HI - GLASS_LO), 0, 1)
    t = t * t * (3 - 2 * t)
    a[:, :, 3] *= k + (1 - k) * t
    Image.fromarray(np.clip(a + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, name + '_glass.png'))


def bubble(d, tail_mid=True):
    # 둥근 네모 말풍선 + 아래 가운데 꼬리
    x0, y0, x1, y1, r = 190, 200, 834, 680, 150
    d.rounded_rectangle([x0, y0, x1, y1], radius=r, outline=255, width=STROKE)
    tl, tr, tip = (430, 590, (470, 830)) if tail_mid else (300, 470, (250, 850))
    d.rectangle([tl + STROKE / 2, y1 - STROKE, tr - STROKE / 2, y1 + 4], fill=0)
    polyline(d, [(tl + STROKE / 2, y1 - STROKE / 2), tip, (tr, y1 - STROKE / 2)])


def hello():
    m, d = canvas()
    bubble(d)
    for cx in (372, 512, 652):
        dot(d, cx, 440, 44)
    return m


def nice():
    m, d = canvas()
    w = 56
    # 뒤 사람(오른쪽 위): 머리 + 반타원 몸(아래 선까지)
    d.ellipse([590, 200, 790, 400], outline=255, width=w)
    d.arc([500, 460, 880, 860], 180, 360, fill=255, width=w)
    polyline(d, [(500 + w / 2, 660), (880 - w / 2, 660)], w=w)
    # 앞 사람이 가리는 자리를 지운다(앞 사람 몸 + 테두리 두께)
    d.ellipse([270 - w, 250 - w, 510 + w, 490 + w], fill=0)
    d.pieslice([150 - w, 560 - w, 630 + w, 1040 + w], 180, 360, fill=0)
    # 앞 사람(왼쪽 아래)
    d.ellipse([270, 250, 510, 490], outline=255, width=w)
    d.arc([150, 560, 630, 1040], 180, 360, fill=255, width=w)
    polyline(d, [(150 + w / 2, 800), (630 - w / 2, 800)], w=w)
    return m


def help_():
    m, d = canvas()
    c, ro, ri = N / 2, 330, 150
    d.ellipse([c - ro, c - ro, c + ro, c + ro], outline=255, width=STROKE)
    d.ellipse([c - ri, c - ri, c + ri, c + ri], outline=255, width=STROKE)
    # 대각선 네 띠
    for k in range(4):
        base = math.radians(45 + 90 * k)
        for off in (-0.26, 0.26):
            a = base + off
            polyline(d, [(c + ri * math.cos(a), c + ri * math.sin(a)), (c + ro * math.cos(a), c + ro * math.sin(a))], w=int(STROKE * 0.8))
    return m


def thanks():
    m, d = canvas()
    pts = []
    for i in range(240):
        t = 2 * math.pi * i / 240
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((512 + x * 21, 470 - y * 21))
    polyline(d, pts, closed=True)
    return m


def sorry():
    m, d = canvas()
    w = 52
    # 가운데 맞댄 손바닥 선 + 양쪽 손 바깥선(손끝 -> 손목) + 기울어진 소매 + 엄지 + 위 움직임 표시
    polyline(d, [(512, 200), (512, 730)], w=w)
    for s in (-1, 1):
        x = lambda v: 512 + s * v
        polyline(d, [(x(0), 200), (x(64), 240), (x(132), 390), (x(180), 548), (x(190), 666), (x(166), 744)], w=w)
        polyline(d, [(x(140), 726), (x(262), 812), (x(212), 888), (x(84), 800)], closed=True, w=w)
        polyline(d, [(x(108), 520), (x(76), 612), (x(26), 668)], w=int(w * 0.8))
        polyline(d, [(x(136), 150), (x(186), 92)], w=int(w * 0.8))
    return m


def again():
    m, d = canvas()
    bubble(d)
    cx, cy = 400, 440
    dot(d, cx, cy, 40)
    for r in (110, 190):
        d.arc([cx - r, cy - r, cx + r, cy + r], -50, 50, fill=255, width=int(STROKE * 0.8))
    return m


# ===== 결제 =====
def pay_cash():
    m, d = canvas()
    d.rounded_rectangle([140, 290, 884, 734], radius=60, outline=255, width=STROKE)
    d.ellipse([512 - 112, 512 - 112, 512 + 112, 512 + 112], outline=255, width=int(STROKE * 0.85))
    for x in (250, 774):
        dot(d, x, 512, 34)
    return m


def pay_receipt():
    m, d = canvas()
    x0, x1, y0, y1 = 280, 744, 170, 790
    zig = [(x1, y1)]
    n = 6
    for i in range(1, n + 1):
        x = x1 - (x1 - x0) * i / n
        zig.append((x, y1 + (50 if i % 2 else 0)))
    polyline(d, [(x0, y1)] + [(x0, y0), (x1, y0)] + zig, closed=False)
    for y, x_end in ((330, 640), (450, 640), (570, 540)):
        polyline(d, [(380, y), (x_end, y)], w=int(STROKE * 0.85))
    return m


def pay_point():
    m, d = canvas()
    pts = []
    for i in range(10):
        r = 360 if i % 2 == 0 else 160
        a = math.radians(-90 + 36 * i)
        pts.append((512 + r * math.cos(a), 530 + r * math.sin(a)))
    polyline(d, pts, closed=True)
    return m


def pay_price():
    m, d = canvas()
    # 오른쪽 위로 기운 가격표 + 구멍
    c, s = math.cos(math.radians(-35)), math.sin(math.radians(-35))
    base = [(-330, -150), (170, -150), (330, 0), (170, 150), (-330, 150)]
    pts = [(512 + x * c - y * s, 512 + x * s + y * c) for x, y in base]
    polyline(d, pts, closed=True)
    hx, hy = 512 + 170 * c, 512 + 170 * s
    d.ellipse([hx - 46, hy - 46, hx + 46, hy + 46], outline=255, width=int(STROKE * 0.7))
    return m


def pay_bag():
    m, d = canvas()
    polyline(d, [(230, 380), (794, 380), (840, 850), (184, 850)], closed=True)
    d.arc([352, 180, 672, 520], 180, 360, fill=255, width=STROKE)
    return m


# ===== 편의 =====
def conv_restroom():
    m, d = canvas()
    w = 56
    # 남자(왼쪽): 머리 + 몸통 + 다리 / 여자(오른쪽): 머리 + 치마 + 다리 / 가운데 세로줄
    for cx in (300, 724):
        d.ellipse([cx - 62, 170, cx + 62, 294], outline=255, width=w)
    polyline(d, [(220, 380), (380, 380), (380, 620), (220, 620)], closed=True, w=w)
    polyline(d, [(260, 620), (260, 850)], w=w)
    polyline(d, [(340, 620), (340, 850)], w=w)
    polyline(d, [(724, 370), (840, 650), (608, 650)], closed=True, w=w)
    polyline(d, [(684, 650), (684, 850)], w=w)
    polyline(d, [(764, 650), (764, 850)], w=w)
    polyline(d, [(512, 160), (512, 860)], w=int(w * 0.6))
    return m


def conv_wifi():
    m, d = canvas()
    cx, cy = 512, 760
    for r in (150, 300, 450):
        d.arc([cx - r, cy - r, cx + r, cy + r], 225, 315, fill=255, width=STROKE)
    dot(d, cx, cy - 10, 54)
    return m


def conv_charge():
    m, d = canvas()
    d.rounded_rectangle([180, 330, 790, 694], radius=56, outline=255, width=STROKE)
    d.rounded_rectangle([790, 440, 860, 584], radius=20, fill=255)
    polyline(d, [(530, 390), (420, 530), (540, 530), (450, 640)], w=int(STROKE * 0.9))
    return m


def conv_water():
    m, d = canvas()
    pts = []
    for i in range(121):
        t = math.pi * 2 * i / 120
        # 물방울: 아래는 원, 위는 뾰족하게
        r = 250
        x = r * math.sin(t) * (1 - 0.0 * math.cos(t))
        y = -r * math.cos(t)
        k = (1 - math.cos(t)) / 2          # 위(0) -> 아래(1)
        x *= 0.25 + 0.75 * k ** 0.5
        y = y * 1.25 if y < 0 else y
        pts.append((512 + x, 560 + y))
    polyline(d, pts, closed=True)
    d.arc([400, 520, 600, 720], 100, 170, fill=255, width=int(STROKE * 0.7))
    return m


def conv_seat():
    m, d = canvas()
    polyline(d, [(330, 170), (330, 560), (720, 560), (720, 860)])
    polyline(d, [(330, 560), (330, 860)])
    polyline(d, [(250, 560), (800, 560)])
    return m


def conv_question():
    m, d = canvas()
    c, r = N / 2, 360
    d.ellipse([c - r, c - r, c + r, c + r], outline=255, width=int(STROKE * 0.8))
    d.arc([392, 250, 632, 490], 180, 400, fill=255, width=STROKE)
    polyline(d, [(512 + 120 * math.cos(math.radians(40)), 370 + 120 * math.sin(math.radians(40))), (512, 560), (512, 600)])
    dot(d, 512, 720, 44)
    return m


def info():
    m, d = canvas()
    c, r = N / 2, 330
    d.ellipse([c - r, c - r, c + r, c + r], outline=255, width=int(STROKE * 0.8))
    dot(d, c, 350, 46)
    polyline(d, [(c, 470), (c, 690)], w=int(STROKE * 1.1))
    return m


def ring():
    # 옅은 원 + 반짝이는 점(흰색). 카드 색은 Unity에서 Image.color로 입힌다.
    F = 512
    img = Image.new('RGBA', (F, F), (0, 0, 0, 0))
    m = Image.new('L', (F, F), 0)
    d = ImageDraw.Draw(m)
    r = 200
    d.ellipse([F / 2 - r, F / 2 - r, F / 2 + r, F / 2 + r], outline=255, width=4)
    ring_layer = Image.new('RGBA', (F, F), (235, 245, 255, 0))
    ring_layer.putalpha(m.filter(ImageFilter.GaussianBlur(1.2)).point(lambda v: int(v * 0.45)))
    glow = Image.new('RGBA', (F, F), (180, 210, 255, 0))
    glow.putalpha(m.filter(ImageFilter.GaussianBlur(8)).point(lambda v: min(255, int(v * 0.9))))
    img.alpha_composite(glow)
    img.alpha_composite(ring_layer)
    dots = Image.new('L', (F, F), 0)
    dd = ImageDraw.Draw(dots)
    for ang in (-130, -45, 140, 215):
        a = math.radians(ang)
        x, y = F / 2 + r * math.cos(a), F / 2 + r * math.sin(a)
        dd.ellipse([x - 6, y - 6, x + 6, y + 6], fill=255)
    halo = Image.new('RGBA', (F, F), (200, 225, 255, 0))
    halo.putalpha(dots.filter(ImageFilter.GaussianBlur(10)).point(lambda v: min(255, v * 3)))
    core = Image.new('RGBA', (F, F), (255, 255, 255, 0))
    core.putalpha(dots.filter(ImageFilter.GaussianBlur(1.5)))
    img.alpha_composite(halo)
    img.alpha_composite(core)
    return img.resize((256, 256), Image.LANCZOS)


def main():
    glass('ui_aac_panel_frame', PANEL_GLASS)
    glass('ui_aac_card_frame', CARD_FRAME_GLASS)
    for name, fn, color in (('hello', hello, BLUE), ('nice', nice, PURPLE), ('help', help_, BLUE),
                            ('thanks', thanks, BLUE), ('sorry', sorry, PURPLE), ('again', again, BLUE),
                            ('info', info, BLUE),
                            ('pay_card', card_shape, BLUE), ('pay_cash', pay_cash, PURPLE), ('pay_receipt', pay_receipt, BLUE),
                            ('pay_point', pay_point, BLUE), ('pay_price', pay_price, PURPLE), ('pay_bag', pay_bag, BLUE),
                            ('conv_restroom', conv_restroom, BLUE), ('conv_wifi', conv_wifi, PURPLE), ('conv_charge', conv_charge, BLUE),
                            ('conv_water', conv_water, BLUE), ('conv_seat', conv_seat, PURPLE), ('conv_question', conv_question, BLUE)):
        compose(fn(), color).save(os.path.join(OUT, 'ui_aac_icon_%s.png' % name))
    ring().save(os.path.join(OUT, 'ui_aac_icon_ring.png'))
    print('-> %s (ui_aac_*_glass.png x2, ui_aac_icon_*.png x20 %dx%d)' % (os.path.relpath(OUT, ROOT), SIZE, SIZE))


if __name__ == '__main__':
    main()
