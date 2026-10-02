# AR 현장모드 도움 화면(HelpDetailPanel)의 네온 아이콘/틀/입력창 파생 그림을 만든다.
#   결과: Assets/_Project/Art/UI/Generated/derived/
#     ui_helpdetail_icon_greeting.png     인사: 말풍선 + 점 세 개   (카드 왼쪽 원 안)
#     ui_helpdetail_icon_order.png        주문: 장바구니
#     ui_helpdetail_icon_payment.png      결제: 카드
#     ui_helpdetail_icon_convenience.png  편의: 톱니바퀴
#     ui_helpdetail_icon_coffee.png       최신 메뉴 제목 옆: 뚜껑 덮인 테이크아웃 컵
#     ui_helpdetail_chat_input_clean.png  입력창(ui_helpdetail_chat_input)에서 그려진 말풍선 아이콘과 오른쪽 원을 지운 것.
#                                         그 자리에 받은 아이콘(ui_helpdetail_icon_chat / _clear)을 올린다. 세로 구분선은 남긴다.
#     ui_helpdetail_panel_frame.png       카테고리 카드를 묶는 틀(9-slice). 배경에 그려진 최신 메뉴 틀과 같은 모양:
#                                         흰 심 바깥 네온 선 + 안쪽으로 더 넓은 파란 빛 + 안쪽 얇은 선 + 살짝 밝은 유리 채움.
#                                         보드 px의 2배로 그린다(가장자리 FRAME_BORDER px, Image.pixelsPerUnitMultiplier = 2).
#   모양: 흰 선(심) + 카드 색 빛 번짐(가까운 빛 + 넓은 빛). 1024 캔버스에 그려 256으로 줄인다.
#   실행: python Art_Source/ui_arfield/make_helpdetail_icons.py      필요: Python 3 + numpy + Pillow
import math
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'UI', 'Generated', 'derived')
os.makedirs(OUT, exist_ok=True)

N = 1024            # 그리는 캔버스
SIZE = 256          # 결과 크기
STROKE = 64         # 선 굵기(1024 기준)
CORE = (244, 249, 255)
GLOW_NEAR, GLOW_FAR = 26, 70     # 빛 번짐 반지름(1024 기준)
NEAR_ALPHA, FAR_ALPHA = 0.95, 0.6

# 카드 색(카드 그림 테두리 빛과 맞춘다)
COLORS = {
    'greeting': (70, 150, 255),
    'order': (176, 110, 255),
    'payment': (40, 214, 255),
    'convenience': (196, 112, 255),
    'coffee': (40, 140, 255),
}


def canvas():
    m = Image.new('L', (N, N), 0)
    return m, ImageDraw.Draw(m)


def dot(d, x, y, r):
    d.ellipse([x - r, y - r, x + r, y + r], fill=255)


def polyline(d, pts, closed=False, w=STROKE):
    if closed:
        pts = list(pts) + [pts[0], pts[1]]
    d.line(pts, fill=255, width=w, joint='curve')
    for x, y in (pts[0], pts[-1]):
        dot(d, x, y, w / 2)


def greeting():
    m, d = canvas()
    # 말풍선: 둥근 네모 + 왼쪽 아래 꼬리
    x0, y0, x1, y1, r = 180, 200, 844, 700, 170
    d.rounded_rectangle([x0, y0, x1, y1], radius=r, outline=255, width=STROKE)
    # 꼬리 자리의 아래 테두리를 지우고 꼬리를 그린다
    tail_l, tail_r, tail_tip = 300, 470, (250, 850)
    d.rectangle([tail_l + STROKE / 2, y1 - STROKE, tail_r - STROKE / 2, y1 + 4], fill=0)
    polyline(d, [(tail_l + STROKE / 2, y1 - STROKE / 2), tail_tip, (tail_r, y1 - STROKE / 2)])
    for cx in (362, 512, 662):
        dot(d, cx, 450, 46)
    return m


def order():
    m, d = canvas()
    # 손잡이 -> 바구니 왼쪽 아래 -> 바닥
    polyline(d, [(120, 230), (240, 230), (350, 640), (800, 640)])
    # 바구니(사다리꼴)
    polyline(d, [(268, 330), (880, 330), (812, 560), (330, 560)], closed=True)
    # 바퀴
    for cx in (395, 755):
        d.ellipse([cx - 62, 768 - 62, cx + 62, 768 + 62], outline=255, width=int(STROKE * 0.85))
    return m


def payment():
    m, d = canvas()
    d.rounded_rectangle([150, 250, 874, 774], radius=96, outline=255, width=STROKE)
    d.rectangle([150 + STROKE / 2, 400, 874 - STROKE / 2, 400 + STROKE], fill=255)   # 카드 띠
    polyline(d, [(270, 630), (420, 630)])
    polyline(d, [(500, 630), (580, 630)])
    return m


def convenience():
    m, d = canvas()
    cx, cy = N / 2, N / 2
    teeth, r_root, r_tip = 8, 268, 372
    a_root, a_tip = math.radians(13), math.radians(8.5)
    pts = []
    for k in range(teeth):
        c = 2 * math.pi * k / teeth
        # 이 이빨 앞의 골(뿌리 원 위 몇 점)
        prev_end = c - 2 * math.pi / teeth + a_root
        for t in range(1, 4):
            a = prev_end + (c - a_root - prev_end) * t / 4
            pts.append((cx + r_root * math.cos(a), cy + r_root * math.sin(a)))
        for a, r in ((c - a_root, r_root), (c - a_tip, r_tip), (c + a_tip, r_tip), (c + a_root, r_root)):
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    polyline(d, pts, closed=True)
    d.ellipse([cx - 112, cy - 112, cx + 112, cy + 112], outline=255, width=STROKE)
    return m


def coffee():
    m, d = canvas()
    w = 50
    # 뚜껑 윗턱 + 테, 아래로 좁아지는 컵, 슬리브 띠(가로선 두 줄)
    polyline(d, [(362, 262), (404, 196), (620, 196), (662, 262)], w=w)
    d.rounded_rectangle([282, 262, 742, 344], radius=38, outline=255, width=w)
    polyline(d, [(330, 344), (388, 852), (636, 852), (694, 344)], w=w)
    polyline(d, [(352, 534), (672, 534)], w=w)
    polyline(d, [(370, 690), (654, 690)], w=w)
    return m


def compose(mask, color):
    def layer(alpha_img, rgb, k):
        a = alpha_img.point(lambda v: int(v * k))
        img = Image.new('RGBA', (N, N), rgb + (0,))
        img.putalpha(a)
        return img

    far = mask.filter(ImageFilter.GaussianBlur(GLOW_FAR))
    near = mask.filter(ImageFilter.GaussianBlur(GLOW_NEAR))
    # 흐린 빛은 최대값이 낮아지므로 정규화해서 선 바로 옆이 충분히 밝게
    far = far.point(lambda v: min(255, v * 2))
    near = near.point(lambda v: min(255, int(v * 1.6)))
    out = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    out.alpha_composite(layer(far, color, FAR_ALPHA))
    out.alpha_composite(layer(near, color, NEAR_ALPHA))
    out.alpha_composite(layer(mask, CORE, 1.0))
    return out.resize((SIZE, SIZE), Image.LANCZOS)


def make_icons():
    for name, fn in (('icon_greeting', greeting), ('icon_order', order), ('icon_payment', payment),
                     ('icon_convenience', convenience), ('icon_coffee', coffee)):
        key = name.replace('icon_', '')
        compose(fn(), COLORS[key]).save(os.path.join(OUT, 'ui_helpdetail_%s.png' % name))

# ===== 입력창에서 그려진 아이콘 지우기 =====
# 각 줄을 지울 칸 왼쪽/오른쪽 바로 바깥 색으로 이어 칠한다(안쪽은 가로로 부드러운 그라데이션이라 자국이 남지 않는다).
# 위아래 끝 몇 줄은 원래 그림과 섞어 이음매를 없앤다. 좌표는 원본 그림 px(1280x147).
CHAT_ERASE = [(60, 131), (1170, 1238)]   # 말풍선 아이콘 / 오른쪽 원 (세로 구분선 x 136~144는 남긴다)
CHAT_ROWS = (41, 101)                    # 안쪽 테두리 곡선(y 약 37 / 105)에 닿지 않게 아이콘 높이만
CHAT_FADE = 4


def make_chat_clean():
    chat = np.asarray(Image.open(os.path.join(OUT, '..', 'ui_helpdetail_chat_input.png')).convert('RGBA')).astype(float)
    clean = chat.copy()
    y0, y1 = CHAT_ROWS
    for x0, x1 in CHAT_ERASE:
        left, right = chat[:, x0 - 1], chat[:, x1 + 1]
        t = ((np.arange(x0, x1 + 1) - (x0 - 1)) / (x1 + 2 - x0))[None, :, None]
        fill = left[:, None, :] * (1 - t) + right[:, None, :] * t
        for y in range(y0, y1 + 1):
            w = min(1.0, (y - y0 + 1) / CHAT_FADE, (y1 - y + 1) / CHAT_FADE)
            clean[y, x0:x1 + 1] = chat[y, x0:x1 + 1] * (1 - w) + fill[y] * w
    Image.fromarray(np.clip(clean + 0.5, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'ui_helpdetail_chat_input_clean.png'))

# ===== 카테고리 틀 (9-slice, 보드 px x2) =====
FRAME_SIZE = 320          # 그림 크기(가운데 96px만 늘어난다)
FRAME_BORDER = 112        # 9-slice 가장자리 = 보드 56px -> ARFieldHelpDetailLayout.FrameBorder
FRAME_OUT = 24            # 그림 가장자리 ~ 바깥 선 (보드 12px, 바깥 빛 자리) -> ARFieldHelpDetailLayout.FrameGlow
FRAME_RADIUS = 52         # 바깥 선 모서리 (보드 26px)
FRAME_INSET = 34          # 바깥 선 ~ 안쪽 얇은 선 (보드 17px)
LINE_CORE = (225, 240, 255)
LINE_GLOW = (10, 95, 255)
INNER_LINE = (80, 160, 255)
GLASS = (40, 110, 255)


def frame():
    F = FRAME_SIZE
    o = FRAME_OUT
    outer = [o, o, F - 1 - o, F - 1 - o]

    def mask(draw_fn):
        m = Image.new('L', (F, F), 0)
        draw_fn(ImageDraw.Draw(m))
        return m

    fill = mask(lambda d: d.rounded_rectangle(outer, radius=FRAME_RADIUS, fill=255))
    core = mask(lambda d: d.rounded_rectangle(outer, radius=FRAME_RADIUS, outline=255, width=3))
    i = o + FRAME_INSET
    inner = mask(lambda d: d.rounded_rectangle([i, i, F - 1 - i, F - 1 - i], radius=FRAME_RADIUS - 18, outline=255, width=3))
    # 빛: 가까운 빛(양쪽) + 안쪽으로만 넓게 번지는 빛(틀 안이 더 밝다)
    near = core.filter(ImageFilter.GaussianBlur(5)).point(lambda v: min(255, v * 6))
    wide = Image.composite(core.filter(ImageFilter.GaussianBlur(16)).point(lambda v: min(255, v * 5)), Image.new('L', (F, F), 0), fill)
    inner_glow = inner.filter(ImageFilter.GaussianBlur(4)).point(lambda v: min(255, v * 3))

    def layer(a, rgb, k):
        img = Image.new('RGBA', (F, F), rgb + (0,))
        img.putalpha(a.point(lambda v: int(v * k)))
        return img

    out = Image.new('RGBA', (F, F), (0, 0, 0, 0))
    out.alpha_composite(layer(fill, GLASS, 0.07))
    out.alpha_composite(layer(wide, LINE_GLOW, 0.6))
    out.alpha_composite(layer(near, LINE_GLOW, 1.0))
    out.alpha_composite(layer(inner_glow, LINE_GLOW, 0.45))
    out.alpha_composite(layer(inner.filter(ImageFilter.GaussianBlur(0.8)), INNER_LINE, 0.9))
    out.alpha_composite(layer(core, LINE_CORE, 1.0))
    return out


def main():
    make_icons()
    make_chat_clean()
    frame().save(os.path.join(OUT, 'ui_helpdetail_panel_frame.png'))
    print('-> %s (ui_helpdetail_icon_*.png x5 %dx%d, ui_helpdetail_chat_input_clean.png, ui_helpdetail_panel_frame.png %dx%d 9-slice %d)'
          % (os.path.relpath(OUT, ROOT), SIZE, SIZE, FRAME_SIZE, FRAME_SIZE, FRAME_BORDER))


# 다른 스크립트(make_aac_greeting_art.py)가 네온 그리기 함수만 가져다 쓸 수 있게 직접 실행할 때만 만든다
if __name__ == '__main__':
    main()
