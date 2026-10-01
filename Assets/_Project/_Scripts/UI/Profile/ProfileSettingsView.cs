using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Artti.Common;
using Artti.Common.Speech;

namespace Artti.UI
{
    // 프로필 설정 화면 (시안 Art/UI/Home/profile_T). 왼쪽 메뉴(사용자 선택 / 프로필 설정 = 지금 화면) + 가운데 패널.
    //  내 프로필: 아바타·닉네임·한마디·생년월일 행. 누르면 프로필 수정 화면(ProfileCreateScene)에 갔다가 이 화면으로 돌아온다.
    //  훈련 설정: 카드 눌러 읽어주기(켬/끔), 점원 말 빠르기(느리게/보통/빠르게), 읽어주는 목소리(여자/남자 + 미리듣기).
    // 훈련 설정은 고르는 동안 ProfileSettingsDraft에만 담고 변경사항 저장에서 프로필에 쓴다.
    // 이 화면에는 취소가 없으므로, 저장을 누르지 않고 다른 화면으로 가도 고친 값을 잃지 않게 떠날 때도 저장한다.
    public class ProfileSettingsView : MonoBehaviour
    {
        // 어느 사용자의 설정인지. 사용자 선택 화면이 정한다. 없으면 지금 쓰는 사용자 -> 첫 사용자.
        // 프로필 수정 화면에 다녀와도 같은 사용자를 열도록 지우지 않는다.
        public static string TargetId;

        [Header("왼쪽 메뉴")]
        [SerializeField] private Button userSelectButton;

        [Header("내 프로필 (행 전체가 버튼)")]
        [SerializeField] private Button avatarRow;
        [SerializeField] private Button nicknameRow;
        [SerializeField] private Button introRow;
        [SerializeField] private Button birthRow;
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text nicknameValue;
        [SerializeField] private TMP_Text introValue;
        [SerializeField] private TMP_Text birthValue;

        [Header("카드 눌러 읽어주기 (행 전체가 스위치)")]
        [SerializeField] private Button cardReadRow;
        [SerializeField] private Graphic toggleTrackOn;      // 켜짐 트랙(79). 꺼짐 트랙(78)은 그 아래에 늘 있다
        [SerializeField] private Graphic toggleKnobOn;       // 켜짐 손잡이(77). 꺼짐 손잡이(76)는 그 아래에 늘 있다
        [SerializeField] private RectTransform toggleKnob;   // 손잡이 묶음(좌우로 미끄러짐)
        [SerializeField] private float knobOffX;
        [SerializeField] private float knobOnX;

        [Header("점원 말 빠르기 (0 느리게 / 1 보통 / 2 빠르게)")]
        [SerializeField] private Button[] speedButtons = new Button[3];
        [SerializeField] private GameObject[] speedNormal = new GameObject[3];
        [SerializeField] private GameObject[] speedSelected = new GameObject[3];

        [Header("읽어주는 목소리 (0 여자 / 1 남자)")]
        [SerializeField] private Button voiceChip;
        [SerializeField] private GameObject voiceChipOpen;   // 목록이 열려 있는 동안 켜지는 칩(67 오른쪽)
        [SerializeField] private TMP_Text voiceLabel;
        [SerializeField] private GameObject voiceList;       // 화면 전체 막이 + Panel(선택지)
        [SerializeField] private Button voiceListBlocker;    // 목록 밖을 누르면 닫힘
        [SerializeField] private Button[] voiceOptions = new Button[2];
        [SerializeField] private GameObject[] voiceOptionNormal = new GameObject[2];
        [SerializeField] private GameObject[] voiceOptionSelected = new GameObject[2];
        [SerializeField] private Button previewButton;
        [SerializeField] private GameObject previewIdle;     // 86
        [SerializeField] private GameObject previewPlaying;  // 87 (읽는 동안)
        [SerializeField] private AudioSource previewSource;

        [Header("저장")]
        [SerializeField] private Button saveButton;
        [SerializeField] private TMP_Text saveLabel;

        [SerializeField] private string profileSelectScene = "ProfileSelectScene";
        [SerializeField] private string editScene = "ProfileCreateScene";
        [SerializeField] private string selfScene = "ProfileSettingsScene";

        private const string PreviewText = "안녕하세요. 이 목소리로 카드를 읽어 드릴게요.";
        private const string SaveText = "변경사항 저장";
        private const string SavedText = "저장했어요";
        private const string EmptyValue = "입력 안 함";
        private const float EmptyAlpha = 0.5f;
        private const float ToggleSeconds = 0.18f;
        private const float SavedSeconds = 1.4f;
        private static readonly SpeechRate[] Rates = { SpeechRate.Slow, SpeechRate.Normal, SpeechRate.Fast };
        private static readonly ReadingVoice[] Voices = { ReadingVoice.Female, ReadingVoice.Male };

        private ProfileData _profile;
        private ProfileSettingsDraft _draft;
        private ITtsService _tts;
        private float _toggleT;
        private bool _previewing;
        private CancellationTokenSource _toggleCts, _previewCts, _savedCts;

        private void Start()
        {
            EnsureBootstrap();
            _profile = ResolveProfile();
            _draft = ProfileSettingsDraft.From(_profile);

            FillProfile();
            SetToggle(_draft.SpeakCardOnTap ? 1f : 0f);
            ApplySpeed();
            ApplyVoice();
            SetVoiceListOpen(false, instant: true);
            SetPreviewPlaying(false);
            if (saveLabel != null) saveLabel.text = SaveText;

            if (userSelectButton != null) userSelectButton.onClick.AddListener(() => Leave(profileSelectScene));
            if (avatarRow != null) avatarRow.onClick.AddListener(() => OpenEdit(openAvatar: true));
            if (nicknameRow != null) nicknameRow.onClick.AddListener(() => OpenEdit(openAvatar: false));
            if (introRow != null) introRow.onClick.AddListener(() => OpenEdit(openAvatar: false));
            if (birthRow != null) birthRow.onClick.AddListener(() => OpenEdit(openAvatar: false));

            if (cardReadRow != null) cardReadRow.onClick.AddListener(OnToggleCardRead);
            for (int i = 0; i < speedButtons.Length; i++)
            {
                int index = i;
                if (speedButtons[i] != null) speedButtons[i].onClick.AddListener(() => OnSpeed(index));
            }
            if (voiceChip != null) voiceChip.onClick.AddListener(() => SetVoiceListOpen(!IsVoiceListOpen(), instant: false));
            if (voiceListBlocker != null) voiceListBlocker.onClick.AddListener(() => SetVoiceListOpen(false, instant: false));
            for (int i = 0; i < voiceOptions.Length; i++)
            {
                int index = i;
                if (voiceOptions[i] != null) voiceOptions[i].onClick.AddListener(() => OnVoice(index));
            }
            if (previewButton != null) previewButton.onClick.AddListener(OnPreview);
            if (saveButton != null) saveButton.onClick.AddListener(OnSave);

            // 사용자가 없으면(에디터에서 이 씬을 바로 실행) 고칠 대상이 없으므로 행과 저장을 막는다
            bool hasProfile = _profile != null;
            foreach (var b in new[] { avatarRow, nicknameRow, introRow, birthRow, saveButton })
                if (b != null) b.interactable = hasProfile;
        }

        private void OnDestroy()
        {
            UIFx.Cancel(ref _toggleCts);
            UIFx.Cancel(ref _previewCts);
            UIFx.Cancel(ref _savedCts);
            _tts?.StopAll();
        }

        // ===== 대상 사용자 =====

        private ProfileData ResolveProfile()
        {
            var pm = ProfileManagerOrNull();
            if (pm == null) return null;
            ProfileData p = null;
            if (!string.IsNullOrEmpty(TargetId)) p = pm.Profiles.Find(x => x.id == TargetId);
            if (p == null) p = pm.ActiveProfile;
            if (p == null && pm.Profiles.Count > 0) p = pm.Profiles[0];
            if (p != null) TargetId = p.id;
            return p;
        }

        private void FillProfile()
        {
            if (avatarImage != null)
            {
                var lib = _profile != null ? AvatarLibrary.Load() : null;
                var sprite = lib != null ? lib.GetById(_profile.avatarId) : null;
                avatarImage.sprite = sprite;
                avatarImage.enabled = sprite != null;
            }
            SetValue(nicknameValue, _profile?.nickname);
            SetValue(introValue, _profile?.intro);
            bool noBirth = _profile == null || (_profile.birthYear == 0 && _profile.birthMonth == 0 && _profile.birthDay == 0);
            SetValue(birthValue, noBirth ? null : ProfileCreateView.FormatBirth(_profile.birthYear, _profile.birthMonth, _profile.birthDay));
        }

        // 비어 있으면 "입력 안 함"을 흐리게
        private static void SetValue(TMP_Text label, string value)
        {
            if (label == null) return;
            bool empty = string.IsNullOrWhiteSpace(value);
            label.text = empty ? EmptyValue : value;
            label.alpha = empty ? EmptyAlpha : 1f;
        }

        // ===== 화면 이동 =====

        // 내 프로필 행: 수정 화면에서 고치고 이 화면으로 돌아온다. 아바타가 아니면 1단계(아바타 고르기)를 건너뛴다.
        private void OpenEdit(bool openAvatar)
        {
            if (_profile == null) return;
            ProfileCreateView.EditTargetId = _profile.id;
            ProfileCreateView.ReturnScene = selfScene;
            ProfileCreateView.SkipAvatarStep = !openAvatar;
            Leave(editScene);
        }

        private void Leave(string scene)
        {
            SaveDraft();
            StopPreview();
            SceneFader.Load(scene);
        }

        // ===== 카드 눌러 읽어주기 =====

        private void OnToggleCardRead()
        {
            _draft.SpeakCardOnTap = !_draft.SpeakCardOnTap;
            float from = _toggleT, to = _draft.SpeakCardOnTap ? 1f : 0f;
            var ct = UIFx.Restart(ref _toggleCts, this);
            UIFx.Tween(this, 0f, ToggleSeconds, k => SetToggle(Mathf.Lerp(from, to, UIFx.EaseOutCubic(k))), ct).Forget();
        }

        // t: 0 꺼짐 ~ 1 켜짐. 켜짐 트랙/손잡이가 서서히 드러나고 손잡이가 오른쪽으로 미끄러진다.
        private void SetToggle(float t)
        {
            _toggleT = t;
            SetAlpha(toggleTrackOn, t);
            SetAlpha(toggleKnobOn, t);
            if (toggleKnob != null)
            {
                var p = toggleKnob.anchoredPosition;
                p.x = Mathf.Lerp(knobOffX, knobOnX, t);
                toggleKnob.anchoredPosition = p;
            }
        }

        private static void SetAlpha(Graphic g, float a)
        {
            if (g == null) return;
            var c = g.color;
            c.a = a;
            g.color = c;
        }

        // ===== 점원 말 빠르기 =====

        private void OnSpeed(int index)
        {
            if (index < 0 || index >= Rates.Length) return;
            _draft.SpeechRate = Rates[index];
            ApplySpeed();
            StopPreview();   // 다음 미리듣기는 새 빠르기로
        }

        private void ApplySpeed()
        {
            for (int i = 0; i < Rates.Length; i++)
            {
                bool on = Rates[i] == _draft.SpeechRate;
                if (i < speedNormal.Length && speedNormal[i] != null) speedNormal[i].SetActive(!on);
                if (i < speedSelected.Length && speedSelected[i] != null) speedSelected[i].SetActive(on);
            }
        }

        // ===== 읽어주는 목소리 =====

        private bool IsVoiceListOpen() => voiceList != null && voiceList.activeSelf;

        private void SetVoiceListOpen(bool open, bool instant)
        {
            if (voiceChipOpen != null) voiceChipOpen.SetActive(open);
            if (voiceList == null) return;
            if (instant) voiceList.SetActive(open);
            else if (open) UIPopupMotion.Open(voiceList);
            else UIPopupMotion.Close(voiceList);
        }

        private void OnVoice(int index)
        {
            if (index < 0 || index >= Voices.Length) return;
            _draft.ReadingVoice = Voices[index];
            ApplyVoice();
            SetVoiceListOpen(false, instant: false);
            StopPreview();
        }

        private void ApplyVoice()
        {
            if (voiceLabel != null) voiceLabel.text = _draft.ReadingVoice.DisplayName();
            for (int i = 0; i < Voices.Length; i++)
            {
                bool on = Voices[i] == _draft.ReadingVoice;
                if (i < voiceOptionNormal.Length && voiceOptionNormal[i] != null) voiceOptionNormal[i].SetActive(!on);
                if (i < voiceOptionSelected.Length && voiceOptionSelected[i] != null) voiceOptionSelected[i].SetActive(on);
            }
        }

        // 미리듣기: 고른 목소리·빠르기로 한 문장을 읽는다. 읽는 중에 다시 누르면 멈춘다.
        private void OnPreview()
        {
            if (_previewing) { StopPreview(); return; }
            var ct = UIFx.Restart(ref _previewCts, this);
            PlayPreviewAsync(ct).Forget();
        }

        private async UniTaskVoid PlayPreviewAsync(CancellationToken ct)
        {
            _tts ??= BuildTts();
            _tts.StopAll();
            _tts.SetVoice(_draft.ReadingVoice.ToVoiceName());
            _tts.SetSpeakingRate(_draft.SpeechRate.ToMultiplier());
            SetPreviewPlaying(true);
            try
            {
                // CloudTtsService.SpeakAsync는 재생을 시작하면 바로 돌아오므로, 끝은 AudioSource로 기다린다
                await _tts.SpeakAsync(PreviewText, ct);
                if (previewSource != null)
                    await UniTask.WaitWhile(() => previewSource != null && previewSource.isPlaying, PlayerLoopTiming.Update, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogWarning($"[ProfileSettings] 미리듣기 실패: {e.Message}"); }
            if (this != null && !ct.IsCancellationRequested) SetPreviewPlaying(false);
        }

        private void StopPreview()
        {
            UIFx.Cancel(ref _previewCts);
            _tts?.StopAll();
            SetPreviewPlaying(false);
        }

        private void SetPreviewPlaying(bool playing)
        {
            _previewing = playing;
            if (previewIdle != null) previewIdle.SetActive(!playing);
            if (previewPlaying != null) previewPlaying.SetActive(playing);
        }

        private ITtsService BuildTts()
        {
            if (previewSource == null)
            {
                previewSource = GetComponent<AudioSource>();
                if (previewSource == null) previewSource = gameObject.AddComponent<AudioSource>();
            }
            var key = ApiKeyLoader.GetOrFallback(ApiKeyLoader.GoogleTtsApi, ApiKeyLoader.GeminiApi);
            if (string.IsNullOrEmpty(key)) return new NoopTtsService();
            return new CloudTtsService(key, previewSource);
        }

        // ===== 저장 =====

        private void OnSave()
        {
            SaveDraft();
            ShowSaved();
        }

        private void SaveDraft()
        {
            if (_profile == null || _draft == null) return;
            _draft.SaveTo(_profile, ProfileManagerOrNull());
        }

        // 저장 버튼 글자가 잠깐 "저장했어요"로 바뀌고 버튼이 살짝 튄다(바뀐 것이 없어도 눌렀다는 확인으로 보여 준다)
        private void ShowSaved()
        {
            var ct = UIFx.Restart(ref _savedCts, this);
            if (saveButton != null) UIFx.PunchScale((RectTransform)saveButton.transform, 0.05f, 0.25f, ct).Forget();
            ShowSavedAsync(ct).Forget();
        }

        private async UniTaskVoid ShowSavedAsync(CancellationToken ct)
        {
            if (saveLabel == null) return;
            saveLabel.text = SavedText;
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(SavedSeconds), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, ct)
                .SuppressCancellationThrow();
            if (canceled || saveLabel == null) return;
            saveLabel.text = SaveText;
        }

        private static ProfileManager ProfileManagerOrNull() =>
            AppBootstrap.Instance != null ? AppBootstrap.Instance.ProfileManager : null;

        private static void EnsureBootstrap()
        {
            if (AppBootstrap.Instance == null)
                new GameObject("[AppBootstrap]").AddComponent<AppBootstrap>();
        }
    }
}
