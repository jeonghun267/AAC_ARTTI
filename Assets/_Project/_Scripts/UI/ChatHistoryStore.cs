using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Artti.UI
{
    // 입력해서 말한 문장 목록(최근 것이 맨 앞). 같은 문장을 다시 말하면 맨 앞으로 옮기고, 최대 maxItems개까지 남긴다.
    // 파일(JSON)에 저장해 앱을 다시 켜도 남는다. 화면은 HelpChatDrawerView가 그린다.
    // 변경점: 사용자(프로필)마다 따로 저장한다(ForUser). 한 사용자의 목록은 모든 채팅 탭이 같이 쓴다.
    public sealed class ChatHistoryStore
    {
        private const string Folder = "chat_history";   // persistentDataPath/chat_history/{프로필id}.json (aac_logs와 같은 방식)
        private const string GuestId = "guest";          // 사용자를 고르지 않은 채 들어왔을 때

        // 같은 파일은 한 객체로 같이 쓴다(한 탭에서 더한 문장이 다른 탭에도 바로 보이게)
        private static readonly Dictionary<string, ChatHistoryStore> Shared = new Dictionary<string, ChatHistoryStore>();

        public static ChatHistoryStore ForUser(string profileId, int maxItems)
        {
            string path = PathForUser(profileId);
            if (!Shared.TryGetValue(path, out var store))
            {
                store = new ChatHistoryStore(path, maxItems);
                Shared[path] = store;
            }
            return store;
        }

        // 변경점: 사용자를 지우면 그 사람의 보낸 문장 목록도 지운다(ProfileManager.DeleteProfile이 부름)
        public static void DeleteForUser(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId)) return;   // guest 목록은 사용자 삭제로 지우지 않는다
            string path = PathForUser(profileId);
            Shared.Remove(path);
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChatHistory] 삭제 실패: {e.Message}");
            }
        }

        public static string PathForUser(string profileId)
        {
            string id = string.IsNullOrWhiteSpace(profileId) ? GuestId : profileId.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) id = id.Replace(c, '_');
            return Path.Combine(Application.persistentDataPath, Folder, id + ".json");
        }

        [Serializable]
        private sealed class Data
        {
            public List<string> items = new List<string>();
        }

        private readonly string _path;
        private readonly int _maxItems;
        private Data _data;

        public ChatHistoryStore(string path, int maxItems)
        {
            _path = path;
            _maxItems = Mathf.Max(1, maxItems);
        }

        public IReadOnlyList<string> Items
        {
            get
            {
                EnsureLoaded();
                return _data.items;
            }
        }

        public void Add(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();
            EnsureLoaded();
            _data.items.Remove(text);
            _data.items.Insert(0, text);
            if (_data.items.Count > _maxItems) _data.items.RemoveRange(_maxItems, _data.items.Count - _maxItems);
            Save();
        }

        private void EnsureLoaded()
        {
            if (_data != null) return;
            try
            {
                if (File.Exists(_path)) _data = JsonUtility.FromJson<Data>(File.ReadAllText(_path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChatHistory] 불러오기 실패, 새로 시작: {e.Message}");
            }
            if (_data == null || _data.items == null) _data = new Data();
        }

        private void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_path, JsonUtility.ToJson(_data));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChatHistory] 저장 실패: {e.Message}");
            }
        }
    }
}
