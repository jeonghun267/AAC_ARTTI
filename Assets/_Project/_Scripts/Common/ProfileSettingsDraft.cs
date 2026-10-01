namespace Artti.Common
{
    // 프로필 설정 화면의 훈련 설정 변경분(카드 읽어주기 / 말 빠르기 / 읽어주는 목소리).
    // 화면에서 고르는 동안은 여기에만 담고, 저장할 때 프로필에 옮겨 쓴다.
    public class ProfileSettingsDraft
    {
        public bool SpeakCardOnTap;
        public SpeechRate SpeechRate;
        public ReadingVoice ReadingVoice;

        public static ProfileSettingsDraft From(ProfileData profile)
        {
            var d = new ProfileSettingsDraft();
            if (profile != null)
            {
                d.SpeakCardOnTap = profile.speakCardOnTap;
                d.SpeechRate = profile.speechRate;
                d.ReadingVoice = profile.readingVoice;
            }
            return d;
        }

        public bool DiffersFrom(ProfileData profile) =>
            profile != null && (profile.speakCardOnTap != SpeakCardOnTap
                                || profile.speechRate != SpeechRate
                                || profile.readingVoice != ReadingVoice);

        // 프로필에 옮겨 쓰고 파일에 저장한다. 바뀐 것이 없으면 쓰지 않는다. 저장했으면 true.
        public bool SaveTo(ProfileData profile, ProfileManager manager)
        {
            if (!DiffersFrom(profile)) return false;
            profile.speakCardOnTap = SpeakCardOnTap;
            profile.speechRate = SpeechRate;
            profile.readingVoice = ReadingVoice;
            manager?.Save();
            return true;
        }
    }
}
