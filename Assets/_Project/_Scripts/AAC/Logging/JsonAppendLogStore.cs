using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Artti.AAC.Logging
{
    // PLAN.MD v3 8.4 - MVP local store. Appends one JSON object per line (JSONL) under
    // Application.persistentDataPath/aac_logs/{profileId}.jsonl.
    // Internal queue is drained synchronously in FlushAsync to avoid extra threads on Android.
    public sealed class JsonAppendLogStore : ILogStore
    {
        readonly string _filePath;
        readonly List<AACEvent> _queue = new List<AACEvent>(capacity: 64);
        readonly object _lock = new object();
        bool _disposed;

        public JsonAppendLogStore(string profileId, string subfolder = "aac_logs")
        {
            if (string.IsNullOrEmpty(profileId)) profileId = "anonymous";
            var dir = Path.Combine(Application.persistentDataPath, subfolder);
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, profileId + ".jsonl");
        }

        public string FilePath => _filePath;

        public void Log(AACEvent ev)
        {
            if (_disposed || ev == null) return;
            lock (_lock) _queue.Add(ev);
        }

        public async UniTask FlushAsync(CancellationToken ct = default)
        {
            if (_disposed) return;
            List<AACEvent> drained;
            lock (_lock)
            {
                if (_queue.Count == 0) return;
                drained = new List<AACEvent>(_queue);
                _queue.Clear();
            }

            await UniTask.SwitchToThreadPool();
            ct.ThrowIfCancellationRequested();
            try
            {
                using (var sw = new StreamWriter(_filePath, append: true))
                {
                    foreach (var ev in drained)
                    {
                        ct.ThrowIfCancellationRequested();
                        sw.WriteLine(JsonUtility.ToJson(ev));
                    }
                }
            }
            finally
            {
                await UniTask.SwitchToMainThread();
            }
        }

        public async UniTask DisposeAsync(CancellationToken ct = default)
        {
            if (_disposed) return;
            await FlushAsync(ct);
            _disposed = true;
        }
    }
}
