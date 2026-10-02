# AR 현장모드 결과 화면의 분류 칩(HJ/97, 2078x757 네온 유리 막대)을 9-slice로 쓰기 좋게 줄인다.
#   결과: Assets/_Project/Art/UI/Home/HJ/derived/97_chip.png (1024 폭)
#     칩 폭이 장소 이름 길이에 따라 바뀌므로(TextChipAutoWidth) 양끝 둥근 부분은 그대로 두고 가운데만 늘린다.
#     9-slice 가장자리: 왼쪽/오른쪽 CAP px, 위 165 / 아래 168 px(둥근 모서리 반지름 약 50 + 빛)
#       (ARFieldResultLayout.ChipCapPx/ChipCapTopPx/ChipCapBottomPx). 칸이 두 줄로 높아져도 모서리가 찌그러지지 않는다.
#     막대 본체(알파 200 이상): 원본 x 58~2020, y 222~522 -> 이 그림에서 BODY (ARFieldResultLayout.ChipBody)
#     1024 폭이면 임포트 최대 크기(1024)에 그대로 들어가 9-slice 가장자리 px이 줄어들지 않는다.
#   실행: python Art_Source/ui_arfield/make_result_chip.py      필요: Python 3 + Pillow
import os
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
HJ = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'UI', 'Home', 'HJ')
OUT = os.path.join(HJ, 'derived')
os.makedirs(OUT, exist_ok=True)

WIDTH = 1024
CAP = 128   # 양끝 둥근 부분 + 빛 번짐(원본 약 260px)

src = Image.open(os.path.join(HJ, '97.png')).convert('RGBA')
k = WIDTH / src.width
dst = src.resize((WIDTH, round(src.height * k)), Image.LANCZOS)
dst.save(os.path.join(OUT, '97_chip.png'))
body = (58 * k, 222 * k, 2020 * k, 522 * k)
print('-> %s (97_chip.png %dx%d, cap %d, body x %.1f~%.1f y %.1f~%.1f)'
      % (os.path.relpath(OUT, ROOT), dst.width, dst.height, CAP, body[0], body[2], body[1], body[3]))
