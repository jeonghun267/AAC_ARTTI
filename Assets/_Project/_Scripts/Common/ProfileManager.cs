using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Artti.Common
{
    [Serializable]
    public class ProfileData
    {
        public string id;
        public string nickname;
        public string avatarId;
        public string colorHex;
        public bool isTeacherMode;
        public int birthYear;
        public int birthMonth;
        public long lastUsedAtTicks; // 마지막 사용일자 (DateTime.Ticks)

        public ProfileData()
        {
            id = Guid.NewGuid().ToString();
        }
    }

    [Serializable]
    public class ProfileListWrapper
    {
        public List<ProfileData> profiles = new List<ProfileData>();
    }

    public class ProfileManager
    {
        private const string FileName = "profiles.json";
        private string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public List<ProfileData> Profiles { get; private set; } = new List<ProfileData>();
        public ProfileData ActiveProfile { get; private set; }

        public ProfileManager()
        {
            Load();
        }

        public void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(new ProfileListWrapper { profiles = Profiles }, true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfileManager] Failed to save profiles: {e.Message}");
            }
        }

        public void Load()
        {
            if (!File.Exists(SavePath))
            {
                Profiles = new List<ProfileData>();
                return;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                var wrapper = JsonUtility.FromJson<ProfileListWrapper>(json);
                Profiles = wrapper?.profiles ?? new List<ProfileData>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfileManager] Failed to load profiles: {e.Message}");
                Profiles = new List<ProfileData>();
            }
        }

        public void AddProfile(ProfileData profile)
        {
            Profiles.Add(profile);
            Save();
        }

        public void SetActiveProfile(string id)
        {
            ActiveProfile = Profiles.Find(p => p.id == id);
        }

        // 마지막 사용일자 갱신 (사용자 선택/생성 시)
        public void UpdateLastUsed(string id)
        {
            var p = Profiles.Find(x => x.id == id);
            if (p != null)
            {
                p.lastUsedAtTicks = DateTime.Now.Ticks;
                Save();
            }
        }
        
        public void DeleteProfile(string id)
        {
            Profiles.RemoveAll(p => p.id == id);
            if (ActiveProfile?.id == id) ActiveProfile = null;
            Save();
        }
    }
}
