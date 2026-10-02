using UnityEditor;
using UnityEngine;

namespace Artti.Editor
{
    // 배경음악(Resources/Audio/BGM/*) 가져오기 설정을 고정한다. wav를 지우고 다시 넣어 .meta가 초기화돼도 같은 설정이 된다.
    //   원본 wav 11MB(48kHz 스테레오 60초) -> Vorbis 압축 + 재생하면서 읽기(Streaming)로 앱 용량·메모리를 줄인다.
    public sealed class BgmImportSettings : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Resources/Audio/BGM/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }
    }
}
