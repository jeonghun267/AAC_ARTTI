# Unity Device Simulator에 Galaxy Tab S7 FE(2560x1600) 기기를 추가한다.
#   결과: Assets/_Project/Editor/DeviceSimulator/
#     Samsung Galaxy Tab S7 FE.device                       기기 정의(JSON). 기기 목록에 "Samsung Galaxy Tab S7 FE (2560x1600)"로 나온다.
#     DeviceOverlays/Samsung Galaxy Tab S7 FE_Overlay.png   기기 테두리(화면 자리는 투명)
#   기기 정의 형식은 com.unity.device-simulator.devices 패키지의 Samsung Galaxy Z Fold2 5G를 바탕으로 했다.
#     화면은 패키지 기기들처럼 세로 기준(1600x2560)으로 적는다. 앱이 가로 고정이라 시뮬레이터에서는 2560x1600으로 돈다.
#   실물 치수: 12.4인치 2560x1600(약 243ppi), 본체 284.8 x 185.0 mm -> 베젤 약 9 mm(약 88 px).
#     전면 카메라는 긴 변(가로로 들었을 때 위)에 있다. 세로 그림에서는 오른쪽 변 = LandscapeLeft에서 위.
#   실행: python Art_Source/device_simulator/make_tab_s7fe_device.py      필요: Python 3 + Pillow
import json
import os
import uuid
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Editor', 'DeviceSimulator')
OVERLAY_DIR = os.path.join(OUT, 'DeviceOverlays')
NAME = 'Samsung Galaxy Tab S7 FE'
os.makedirs(OVERLAY_DIR, exist_ok=True)

SCREEN_W, SCREEN_H = 1600, 2560    # 세로 기준
BORDER = 88                        # 베젤(px)
SCREEN_RADIUS = 36                 # 화면 모서리
OUTER_RADIUS = 150                 # 본체 모서리
DPI = 243.0

# ===== 테두리 그림 (2배로 그려 줄여서 매끄럽게) =====
S = 2
W, H = SCREEN_W + BORDER * 2, SCREEN_H + BORDER * 2
img = Image.new('RGBA', (W * S, H * S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
# 금속 테두리 -> 검은 유리 베젤
d.rounded_rectangle([0, 0, W * S - 1, H * S - 1], radius=OUTER_RADIUS * S, fill=(118, 122, 130, 255))
rim = 5 * S
d.rounded_rectangle([rim, rim, W * S - 1 - rim, H * S - 1 - rim], radius=(OUTER_RADIUS - 5) * S, fill=(14, 15, 18, 255))
# 화면 자리(투명)
d.rounded_rectangle([BORDER * S, BORDER * S, (BORDER + SCREEN_W) * S - 1, (BORDER + SCREEN_H) * S - 1],
                    radius=SCREEN_RADIUS * S, fill=(0, 0, 0, 0))
# 전면 카메라: 오른쪽 변 가운데
cx, cy, r = (W - BORDER / 2) * S, H / 2 * S, 11 * S
d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(4, 4, 6, 255))
r2 = 5 * S
d.ellipse([cx - r2 - 2 * S, cy - r2 - 2 * S, cx + r2 - 2 * S, cy + r2 - 2 * S], fill=(28, 40, 70, 255))
img = img.resize((W, H), Image.LANCZOS)
overlay_name = NAME + '_Overlay.png'
img.save(os.path.join(OVERLAY_DIR, overlay_name))


def full_area(w, h):
    return {'serializedVersion': '2', 'x': 0.0, 'y': 0.0, 'width': float(w), 'height': float(h)}


# ===== 기기 정의 =====
base_path = None
cache = os.path.join(ROOT, 'Library', 'PackageCache')
for pkg in os.listdir(cache):
    if pkg.startswith('com.unity.device-simulator.devices@'):
        base_path = os.path.join(cache, pkg, 'Editor', 'Devices', 'Samsung Galaxy Z Fold2 5G.device')
if base_path is None or not os.path.exists(base_path):
    raise SystemExit('com.unity.device-simulator.devices 패키지가 없음 (Library/PackageCache)')
base = json.load(open(base_path, encoding='utf-8-sig'))

gfx = []
for g in base['systemInfo']['graphicsDependentData']:
    if g['graphicsDeviceType'] not in (21, 11):   # Vulkan, OpenGL ES 3
        continue
    g = dict(g)
    g['graphicsDeviceName'] = 'Adreno (TM) 619'
    g['graphicsMemorySize'] = 2048
    g['graphicsDeviceVersion'] = 'Vulkan 1.1.128' if g['graphicsDeviceType'] == 21 else 'OpenGL ES 3.2 V@0502.0'
    gfx.append(g)

device = {
    'friendlyName': NAME + ' (2560x1600)',
    'version': 1,
    'screens': [{
        'width': SCREEN_W,
        'height': SCREEN_H,
        'navigationBarHeight': 72,
        'dpi': DPI,
        'orientations': [
            {'orientation': 1, 'safeArea': full_area(SCREEN_W, SCREEN_H), 'cutouts': []},
            {'orientation': 2, 'safeArea': full_area(SCREEN_W, SCREEN_H), 'cutouts': []},
            {'orientation': 3, 'safeArea': full_area(SCREEN_H, SCREEN_W), 'cutouts': []},
            {'orientation': 4, 'safeArea': full_area(SCREEN_H, SCREEN_W), 'cutouts': []},
        ],
        'presentation': {
            'overlayPath': 'DeviceOverlays/' + overlay_name,
            'borderSize': {'x': float(BORDER), 'y': float(BORDER), 'z': float(BORDER), 'w': float(BORDER)},
            'cornerRadius': float(SCREEN_RADIUS),
        },
    }],
    'systemInfo': {
        'deviceModel': 'samsung SM-T733',
        'deviceType': 1,
        'operatingSystem': 'Android OS 14 / API-34',
        'operatingSystemFamily': 0,
        'processorCount': 8,
        'processorFrequency': 2208,
        'processorType': 'ARM64 FP ASIMD AES',
        'supportsAccelerometer': True,
        'supportsAudio': True,
        'supportsGyroscope': True,
        'supportsLocationService': True,
        'supportsVibration': False,
        'systemMemorySize': 5550,
        'unsupportedIdentifier': 'n/a',
        'graphicsDependentData': gfx,
    },
}
device_path = os.path.join(OUT, NAME + '.device')
with open(device_path, 'w', encoding='utf-8', newline='\n') as f:
    json.dump(device, f, ensure_ascii=False, indent=4)


# ===== .meta (없을 때만 만든다. 있으면 guid를 그대로 둔다) =====
def write_meta(path, body):
    meta = path + '.meta'
    if os.path.exists(meta):
        return
    with open(meta, 'w', encoding='utf-8', newline='\n') as f:
        f.write('fileFormatVersion: 2\nguid: %s\n' % uuid.uuid4().hex + body)


write_meta(device_path, """ScriptedImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 2
  userData:
  assetBundleName:
  assetBundleVariant:
  script: {fileID: 13973, guid: 0000000000000000e000000000000000, type: 0}
""")
# 패키지 기기 테두리와 같은 가져오기 설정(읽기 가능), 세로 2736px라 최대 크기만 4096
write_meta(os.path.join(OVERLAY_DIR, overlay_name), """TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 11
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 1
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 4096
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 4096
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  spritePackingTag:
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData:
  assetBundleName:
  assetBundleVariant:
""")
print('-> %s (%s.device, DeviceOverlays/%s %dx%d)' % (os.path.relpath(OUT, ROOT), NAME, overlay_name, W, H))
