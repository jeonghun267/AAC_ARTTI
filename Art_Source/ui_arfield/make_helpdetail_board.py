# AR 현장모드 도움 화면(HelpDetailPanel)의 보드 그림과 뒤 배경을 만든다.
#   입력: Assets/_Project/Art/UI/Generated/ui_helpdetail_background_neon.png (제목/뒤로 가기/최신 메뉴 틀이 그려진 16:9, 1672x941)
#   결과: Assets/_Project/Art/UI/Generated/derived/
#     ui_helpdetail_board.png     16:10(1672x1045). 원본 위아래에 52px씩 늘려 붙여 Galaxy Tab S7 FE(2560x1600)에 딱 맞는다.
#                                 16:9 원본을 16:10 화면에 덮으면 좌우가 잘려 바깥 네온 틀 양옆/위 모서리와 양쪽 간판이 화면 밖으로 나갔다.
#                                 늘린 부분은 틀 위의 흐린 빌딩 빛(원본 위 60줄)과 입력 줄 아래 바닥(원본 아래 60줄)을 세로로 늘려 채운다.
#                                 그림 속 UI(제목/틀/뒤로 가기)는 그대로이고 원본 좌표에서 y만 +52.
#     ui_helpdetail_backdrop.png  보드를 흐리고 어둡게 한 것. 폰(20:9)/4:3처럼 보드가 줄어들 때 남는 자리를 채운다(그림 속 글자가 두 번 보이지 않게).
#   실행: python Art_Source/ui_arfield/make_helpdetail_board.py      필요: Python 3 + Pillow
import os
from PIL import Image, ImageFilter, ImageEnhance

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
GEN = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'UI', 'Generated')
OUT = os.path.join(GEN, 'derived')
os.makedirs(OUT, exist_ok=True)

EXT = 52            # 위아래로 늘리는 양(px). 1672 x (941 + 2*52) = 1672 x 1045 = 16:10
TOP_SRC = 60        # 원본 위 60줄(바깥 틀 y 68 위, 흐린 빌딩 빛)을 60 + EXT줄로 늘린다
BOTTOM_SRC = 60     # 원본 아래 60줄(입력 줄 y 875 아래 바닥)을 60 + EXT줄로 늘린다
BACKDROP_BLUR = 18  # 뒤 배경 흐림(1/2 크기 기준 px)
BACKDROP_DIM = 0.55

src = Image.open(os.path.join(GEN, 'ui_helpdetail_background_neon.png')).convert('RGB')
W, H = src.size
board = Image.new('RGB', (W, H + EXT * 2))
board.paste(src.crop((0, 0, W, TOP_SRC)).resize((W, TOP_SRC + EXT), Image.LANCZOS), (0, 0))
board.paste(src.crop((0, TOP_SRC, W, H - BOTTOM_SRC)), (0, TOP_SRC + EXT))
board.paste(src.crop((0, H - BOTTOM_SRC, W, H)).resize((W, BOTTOM_SRC + EXT), Image.LANCZOS), (0, H - BOTTOM_SRC + EXT))
board.save(os.path.join(OUT, 'ui_helpdetail_board.png'))

back = board.resize((W // 2, (H + EXT * 2) // 2), Image.LANCZOS).filter(ImageFilter.GaussianBlur(BACKDROP_BLUR))
back = ImageEnhance.Brightness(back).enhance(BACKDROP_DIM)
back.save(os.path.join(OUT, 'ui_helpdetail_backdrop.png'))
print('-> %s (ui_helpdetail_board.png %dx%d, ui_helpdetail_backdrop.png %dx%d)'
      % (os.path.relpath(OUT, ROOT), board.width, board.height, back.width, back.height))
