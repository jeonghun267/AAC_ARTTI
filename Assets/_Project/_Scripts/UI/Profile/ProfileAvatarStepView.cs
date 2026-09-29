using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.Common;

namespace Artti.UI
{
    // 프로필 등록 1단계: 아바타 선택.
    // 분류 탭(전체/여성/남성/중성/스타일)으로 그리드를 거르고, 고른 아바타를 왼쪽 큰 카드에 보여 준다.
    // 다음 -> 고른 아바타를 ProfileCreateView에 넘기고 이 패널을 닫아 2단계(이름/생년월) 폼을 보인다.
    public class ProfileAvatarStepView : MonoBehaviour
    {
        [SerializeField] private ProfileCreateView form;
        [SerializeField] private RectTransform tileRoot;
        [SerializeField] private Button[] tabs;            // 전체, 여성, 남성, 중성, 스타일 순서
        [SerializeField] private Image previewImage;
        [SerializeField] private Button randomButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private string closeScene = "ProfileSelectScene";

        const float TabDimAlpha = 0.55f;
        const float TabSelectedScale = 1.06f;

        // 탭 순서와 같은 순서. None은 전체.
        static readonly AvatarTile.Category[] TabFilter =
        {
            AvatarTile.Category.None,
            AvatarTile.Category.Female,
            AvatarTile.Category.Male,
            AvatarTile.Category.Neutral,
            AvatarTile.Category.Style,
        };

        private AvatarTile[] _tiles;
        private Image[] _tabVisuals;
        private AvatarTile _selected;
        private AvatarTile _initial;
        private readonly List<AvatarTile> _visible = new List<AvatarTile>();

        public void Setup(ProfileCreateView formView, RectTransform tiles, Button[] tabButtons, Image preview,
            Button random, Button next, Button close)
        {
            form = formView;
            tileRoot = tiles;
            tabs = tabButtons;
            previewImage = preview;
            randomButton = random;
            nextButton = next;
            closeButton = close;
        }

        private void Awake()
        {
            _tiles = tileRoot != null ? tileRoot.GetComponentsInChildren<AvatarTile>(true) : new AvatarTile[0];

            _tabVisuals = new Image[tabs != null ? tabs.Length : 0];
            for (int i = 0; i < _tabVisuals.Length; i++)
            {
                var v = tabs[i] != null ? tabs[i].transform.Find("Visual") : null;
                _tabVisuals[i] = v != null ? v.GetComponent<Image>() : null;
            }

            // 수정 모드면 기존 아바타를 미리 고른다. ProfileCreateView.Start가 EditTargetId를 소비하기 전에 읽어야 해서 Awake에서 처리.
            _initial = _tiles.Length > 0 ? _tiles[0] : null;
            var editId = ProfileCreateView.EditTargetId;
            if (!string.IsNullOrEmpty(editId))
            {
                var profiles = AppBootstrap.Instance?.ProfileManager?.Profiles;
                var editing = profiles != null ? profiles.Find(p => p.id == editId) : null;
                if (editing != null)
                    foreach (var t in _tiles)
                        if (t.avatarId == editing.avatarId) { _initial = t; break; }
            }
        }

        private void Start()
        {
            foreach (var t in _tiles)
            {
                var tile = t;
                if (tile.button != null) tile.button.onClick.AddListener(() => Select(tile));
            }
            for (int i = 0; tabs != null && i < tabs.Length; i++)
            {
                int index = i;
                if (tabs[i] != null) tabs[i].onClick.AddListener(() => SelectTab(index));
            }
            if (randomButton != null) randomButton.onClick.AddListener(SelectRandom);
            if (nextButton != null) nextButton.onClick.AddListener(Next);
            if (closeButton != null) closeButton.onClick.AddListener(() => SceneManager.LoadScene(closeScene));

            SelectTab(0);
            Select(_initial);
        }

        private void SelectTab(int index)
        {
            var filter = index >= 0 && index < TabFilter.Length ? TabFilter[index] : AvatarTile.Category.None;
            _visible.Clear();
            foreach (var t in _tiles)
            {
                bool show = filter == AvatarTile.Category.None || (t.categories & filter) != 0;
                t.gameObject.SetActive(show);
                if (show) _visible.Add(t);
            }

            for (int i = 0; i < _tabVisuals.Length; i++)
            {
                bool on = i == index;
                if (_tabVisuals[i] != null)
                {
                    var c = _tabVisuals[i].color;
                    c.a = on ? 1f : TabDimAlpha;
                    _tabVisuals[i].color = c;
                }
                if (tabs[i] != null) tabs[i].transform.localScale = Vector3.one * (on ? TabSelectedScale : 1f);
            }
        }

        private void Select(AvatarTile tile)
        {
            if (tile == null) return;
            _selected = tile;
            foreach (var t in _tiles)
                if (t.selectedMark != null) t.selectedMark.SetActive(t == tile);
            if (previewImage != null && tile.sprite != null) previewImage.sprite = tile.sprite;
        }

        // 지금 보이는 칸 중에서 현재 선택이 아닌 것 하나를 고른다.
        private void SelectRandom()
        {
            if (_visible.Count == 0) return;
            if (_visible.Count == 1) { Select(_visible[0]); return; }
            AvatarTile pick;
            do pick = _visible[Random.Range(0, _visible.Count)];
            while (pick == _selected);
            Select(pick);
        }

        private void Next()
        {
            if (_selected != null && form != null) form.SetAvatar(_selected.avatarId);
            gameObject.SetActive(false);
        }
    }
}
