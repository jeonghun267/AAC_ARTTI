using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Artti.ARField
{
    public interface IOcrService
    {
        UniTask<string[]> RecognizeAsync(Texture2D frame, CancellationToken ct = default);
    }

    public enum ARFieldState
    {
        CameraScan,
        ConfirmRecognition,
        SelectCategory,
        SelectSubCategory,
        ResultPanel
    }
}
