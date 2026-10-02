# AR 현장모드 주문하기 화면(OrderPanel, 한 화면 시안)에서 쓰는 네온 UI 조각을 만든다.
#   결과: Assets/_Project/Art/UI/Generated/derived/  (9-slice 그림은 보드 px의 2배로 그린다 -> Image.pixelsPerUnitMultiplier = 2)
#     ui_order_glass_blue.png / _orange.png / _purple.png   카드 바탕(9-slice, 가장자리 GLASS_BORDER px): 색 유리 + 네온 테두리
#     ui_order_glass_glow.png       고른 카드 위에 겹치는 밝은 빛 테두리(9-slice, 안은 투명)
#     ui_order_badge.png            단계 번호 배지(9-slice, 가장자리 BADGE_BORDER px)
#     ui_order_pill.png             '돌아가기' 알약 버튼(9-slice, 가장자리 PILL_BORDER px)
#     ui_order_complete.png         '주문 완료하기' 버튼(통 그림, 파랑 -> 보라 테두리). 선 자리는 COMPLETE_* 참고
#     ui_order_title_icon.png       제목 왼쪽: 빛나는 파란 원 + 흰 머그잔
#     ui_order_icon_back.png        '돌아가기' 왼쪽 흰 화살표
#     ui_order_panel_frame.png      큰 틀(9-slice, 가장자리 FRAME_BORDER px): 선 하나 + 옅은 유리. (인사 화면 큰 틀은 안쪽 선이 하나 더 있는 이중 테두리)
#     ui_order_section_frame.png    단계 틀(9-slice, 가장자리 FRAME_BORDER px): 선 하나 + 아주 옅은 유리
#   네온 모양(흰 심 + 색 빛 번짐)은 make_helpdetail_icons.py의 함수를 그대로 쓴다.
#   실행: python Art_Source/ui_arfield/make_order_art.py      필요: Python 3 + numpy + Pillow
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_helpdetail_icons import N, OUT, ROOT, canvas, dot, polyline, compose   # noqa: E402

GLASS_SIZE, GLASS_BORDER, GLASS_OUT, GLASS_RADIUS = 256, 88, 20, 44     # 보드 px: 가장자리 44, 바깥 빛 10, 모서리 22
BADGE_SIZE, BADGE_BORDER, BADGE_OUT, BADGE_RADIUS = 128, 32, 8, 24       # 보드 px: 가장자리 16(배지 높이 37 > 32), 모서리 12
PILL_SIZE, PILL_BORDER, PILL_OUT, PILL_RADIUS = 128, 52, 10, 42            # 보드 px: 가장자리 26(알약 높이 53), 모서리 21(가장자리 안에 들어가야 늘려도 안 깨진다)
COMPLETE_W, COMPLETE_H, COMPLETE_OUT = 702, 156, 26                      # 보드 351x78의 2배 + 바깥 빛
FRAME_SIZE, FRAME_BORDER, FRAME_OUT = 320, 112, 24                       # 보드 px: 가장자리 56, 바깥 빛 12 (ARFieldOrderLayout.FrameBorderPx/FrameGlow)
PANEL_RADIUS, SECTION_RADIUS = 80, 52                                    # 보드 px: 큰 틀 모서리 40, 단계 틀 모서리 26

# 색: (유리 위, 유리 아래, 테두리 빛, 테두리 심)
GLASS = {
    'blue': ((40, 95, 210), (14, 40, 130), (40, 130, 255), (215, 235, 255)),
    'orange': ((235, 160, 110), (150, 85, 65), (255, 165, 90), (255, 240, 222)),
    'purple': ((105, 70, 220), (45, 30, 140), (165, 105, 255), (240, 225, 255)),
}
GLASS_ALPHA = 0.82


def layer(size, alpha_img, rgb, k=1.0):
    img = Image.new('RGBA', size, tuple(rgb) + (0,))
    img.putalpha(alpha_img.point(lambda v: int(v * k)))
    return img


def gradient_fill(size, box, radius, top, bottom, alpha):
    w, h = size
    m = Image.new('L', size, 0)
    ImageDraw.Draw(m).rounded_rectangle(box, radius=radius, fill=255)
    t = np.linspace(0, 1, h)[:, None, None]
    rgb = np.array(top, float)[None, None, :] * (1 - t) + np.array(bottom, float)[None, None, :] * t
    rgb = np.broadcast_to(rgb, (h, w, 3))
    a = np.asarray(m, float) * alpha
    return Image.fromarray(np.clip(np.dstack([rgb, a]) + 0.5, 0, 255).astype(np.uint8), 'RGBA')


def neon_box(size, out, radius, colors, fill_alpha, core_w=3, glow=6, inner_glow=14, highlight=True):
    """둥근 네모: 그라데이션 유리 + 흰 심 테두리 + 색 빛(바깥 짧게, 안쪽 넓게) + 위쪽 옅은 반사"""
    top, bottom, glow_rgb, core_rgb = colors
    w, h = size
    box = [out, out, w - 1 - out, h - 1 - out]
    img = Image.new('RGBA', size, (0, 0, 0, 0))
    if fill_alpha > 0:
        img.alpha_composite(gradient_fill(size, box, radius, top, bottom, fill_alpha))
    core = Image.new('L', size, 0)
    ImageDraw.Draw(core).rounded_rectangle(box, radius=radius, outline=255, width=core_w)
    fill = Image.new('L', size, 0)
    ImageDraw.Draw(fill).rounded_rectangle(box, radius=radius, fill=255)
    near = core.filter(ImageFilter.GaussianBlur(glow)).point(lambda v: min(255, v * 6))
    wide = Image.composite(core.filter(ImageFilter.GaussianBlur(inner_glow)).point(lambda v: min(255, v * 4)), Image.new('L', size, 0), fill)
    img.alpha_composite(layer(size, wide, glow_rgb, 0.55))
    img.alpha_composite(layer(size, near, glow_rgb, 1.0))
    if highlight:
        hl = Image.new('L', size, 0)
        i = out + core_w * 2
        ImageDraw.Draw(hl).rounded_rectangle([i, i, w - 1 - i, h - 1 - i], radius=max(1, radius - core_w * 2), outline=255, width=2)
        hl = Image.composite(hl, Image.new('L', size, 0), Image.linear_gradient('L').resize(size).point(lambda v: 255 - v))
        img.alpha_composite(layer(size, hl.filter(ImageFilter.GaussianBlur(1)), (255, 255, 255), 0.35))
    img.alpha_composite(layer(size, core.filter(ImageFilter.GaussianBlur(0.7)), core_rgb, 1.0))
    return img


def glow_ring(size, out, radius):
    """선택 표시: 안은 투명, 밝은 흰-하늘 테두리 + 넓은 빛"""
    w, h = size
    box = [out, out, w - 1 - out, h - 1 - out]
    core = Image.new('L', size, 0)
    ImageDraw.Draw(core).rounded_rectangle(box, radius=radius, outline=255, width=5)
    img = Image.new('RGBA', size, (0, 0, 0, 0))
    img.alpha_composite(layer(size, core.filter(ImageFilter.GaussianBlur(10)).point(lambda v: min(255, v * 5)), (120, 200, 255), 0.9))
    img.alpha_composite(layer(size, core.filter(ImageFilter.GaussianBlur(4)).point(lambda v: min(255, v * 4)), (190, 230, 255), 1.0))
    img.alpha_composite(layer(size, core.filter(ImageFilter.GaussianBlur(0.8)), (255, 255, 255), 1.0))
    return img


def complete_button():
    """주문 완료하기: 어두운 남색 유리 + 파랑 -> 보라 가로 그라데이션 테두리(통 그림)"""
    o = COMPLETE_OUT
    w, h = COMPLETE_W + 2 * o, COMPLETE_H + 2 * o
    size = (w, h)
    box = [o, o, w - 1 - o, h - 1 - o]
    r = COMPLETE_H // 2
    img = Image.new('RGBA', size, (0, 0, 0, 0))
    img.alpha_composite(gradient_fill(size, box, r, (25, 55, 170), (10, 25, 100), 0.9))
    core = Image.new('L', size, 0)
    ImageDraw.Draw(core).rounded_rectangle(box, radius=r, outline=255, width=4)
    t = np.linspace(0, 1, w)[None, :, None]
    left, right = np.array([40, 150, 255.]), np.array([170, 100, 255.])
    grad = np.broadcast_to(left * (1 - t) + right * t, (h, w, 3))

    def colored(alpha_img, k):
        a = np.asarray(alpha_img, float) * k
        return Image.fromarray(np.clip(np.dstack([grad, a]) + 0.5, 0, 255).astype(np.uint8), 'RGBA')

    img.alpha_composite(colored(core.filter(ImageFilter.GaussianBlur(12)).point(lambda v: min(255, v * 4)), 0.8))
    img.alpha_composite(colored(core.filter(ImageFilter.GaussianBlur(5)).point(lambda v: min(255, v * 5)), 1.0))
    img.alpha_composite(layer(size, core.filter(ImageFilter.GaussianBlur(0.8)), (235, 240, 255), 1.0))
    return img


def title_icon():
    """빛나는 파란 원 + 흰 머그잔(네온)"""
    F = 512
    img = Image.new('RGBA', (F, F), (0, 0, 0, 0))
    disc = Image.new('L', (F, F), 0)
    r = 176
    ImageDraw.Draw(disc).ellipse([F / 2 - r, F / 2 - r, F / 2 + r, F / 2 + r], fill=255)
    img.alpha_composite(layer((F, F), disc.filter(ImageFilter.GaussianBlur(28)).point(lambda v: min(255, v * 2)), (40, 120, 255), 0.85))
    img.alpha_composite(gradient_fill((F, F), [F / 2 - r, F / 2 - r, F / 2 + r, F / 2 + r], r, (40, 110, 240), (12, 40, 150), 1.0))
    ring = Image.new('L', (F, F), 0)
    ImageDraw.Draw(ring).ellipse([F / 2 - r, F / 2 - r, F / 2 + r, F / 2 + r], outline=255, width=6)
    img.alpha_composite(layer((F, F), ring.filter(ImageFilter.GaussianBlur(5)).point(lambda v: min(255, v * 4)), (90, 170, 255), 1.0))
    img.alpha_composite(layer((F, F), ring.filter(ImageFilter.GaussianBlur(0.8)), (225, 240, 255), 1.0))
    # 머그잔(1024 기준으로 그려 512로)
    m, d = canvas()
    w = 46
    polyline(d, [(340, 400), (340, 610), (380, 680), (560, 680), (600, 610), (600, 400)], w=w)
    polyline(d, [(320, 400), (620, 400)], w=w)
    d.arc([560, 450, 700, 590], -90, 90, fill=255, width=w)
    polyline(d, [(420, 300), (440, 260), (420, 220)], w=int(w * 0.7))
    polyline(d, [(520, 300), (540, 260), (520, 220)], w=int(w * 0.7))
    mug = compose(m, (120, 190, 255)).resize((F, F), Image.LANCZOS)
    img.alpha_composite(mug)
    return img.resize((256, 256), Image.LANCZOS)


def back_arrow():
    m, d = canvas()
    w = 80
    polyline(d, [(260, 512), (780, 512)], w=w)
    polyline(d, [(470, 300), (260, 512), (470, 724)], w=w)
    return compose(m, (120, 190, 255))


def main():
    for name, colors in GLASS.items():
        neon_box((GLASS_SIZE, GLASS_SIZE), GLASS_OUT, GLASS_RADIUS, colors, GLASS_ALPHA).save(os.path.join(OUT, 'ui_order_glass_%s.png' % name))
    glow_ring((GLASS_SIZE, GLASS_SIZE), GLASS_OUT, GLASS_RADIUS).save(os.path.join(OUT, 'ui_order_glass_glow.png'))
    neon_box((BADGE_SIZE, BADGE_SIZE), BADGE_OUT, BADGE_RADIUS, ((25, 60, 170), (10, 30, 110), (40, 130, 255), (220, 238, 255)), 0.92,
             core_w=3, glow=4, inner_glow=8, highlight=False).save(os.path.join(OUT, 'ui_order_badge.png'))
    neon_box((PILL_SIZE, PILL_SIZE), PILL_OUT, PILL_RADIUS, ((20, 50, 150), (8, 25, 95), (40, 130, 255), (220, 238, 255)), 0.8,
             core_w=3, glow=5, inner_glow=10, highlight=False).save(os.path.join(OUT, 'ui_order_pill.png'))
    complete_button().save(os.path.join(OUT, 'ui_order_complete.png'))
    neon_box((FRAME_SIZE, FRAME_SIZE), FRAME_OUT, PANEL_RADIUS, ((35, 85, 200), (12, 35, 120), (40, 140, 255), (225, 240, 255)), 0.42,
             core_w=3, glow=6, inner_glow=18, highlight=False).save(os.path.join(OUT, 'ui_order_panel_frame.png'))
    neon_box((FRAME_SIZE, FRAME_SIZE), FRAME_OUT, SECTION_RADIUS, ((40, 110, 255), (30, 80, 210), (10, 95, 255), (225, 240, 255)), 0.08,
             core_w=3, glow=5, inner_glow=16, highlight=False).save(os.path.join(OUT, 'ui_order_section_frame.png'))
    title_icon().save(os.path.join(OUT, 'ui_order_title_icon.png'))
    back_arrow().save(os.path.join(OUT, 'ui_order_icon_back.png'))
    print('-> %s (ui_order_glass_*.png x4, badge, pill, complete, title_icon, icon_back, panel_frame, section_frame)' % os.path.relpath(OUT, ROOT))


if __name__ == '__main__':
    main()
