#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Artti.Training;
using Artti.UI;

namespace Artti.EditorTests.Training
{
    public class ConvenienceHintCatalogTests
    {
        // 단계별로 왼쪽 말풍선에 나올 수 있는 문장은 모두 분류와 아이콘이 있어야 한다(아이콘 빈칸 방지)
        [TestCase("물 주세요", ConvenienceHintCatalog.RequestItem, "basket")]
        [TestCase("어디에 있어요?", ConvenienceHintCatalog.RequestItem, "pin")]
        [TestCase("얼마예요?", ConvenienceHintCatalog.AskPrice, "question")]
        [TestCase("계산할게요", ConvenienceHintCatalog.Pay, "card")]
        [TestCase("카드로 할게요", ConvenienceHintCatalog.Pay, "card")]
        [TestCase("현금으로 할게요", ConvenienceHintCatalog.Pay, "won")]
        [TestCase("봉투 주세요", ConvenienceHintCatalog.Extra, "bag")]
        [TestCase("봉투 필요 없어요", ConvenienceHintCatalog.Extra, "bag")]
        [TestCase("감사합니다", ConvenienceHintCatalog.Greeting, "smile")]
        [TestCase("안녕하세요", ConvenienceHintCatalog.Greeting, "smile")]
        public void KnownPhrase_HasCategoryAndIcon(string phrase, int category, string icon)
        {
            Assert.That(ConvenienceHintCatalog.CategoryOf(phrase), Is.EqualTo(category));
            Assert.That(ConvenienceHintCatalog.IconOf(phrase), Is.EqualTo(icon));
        }

        [Test]
        public void UnknownPhrase_HasNoCategory()
        {
            Assert.That(ConvenienceHintCatalog.CategoryOf("컵라면 주세요"), Is.EqualTo(-1));
            Assert.That(ConvenienceHintCatalog.IconOf("컵라면 주세요"), Is.Null);
        }

        // 분류 버튼 문장은 '안녕하세요'(점원 AI가 받음)만 빼고 모두 고정 문장 정책이 판정할 수 있어야 한다
        [Test]
        public void CategoryPhrases_AreResolvedByPolicy_ExceptHello()
        {
            for (int c = 0; c < ConvenienceHintCatalog.CategoryCount; c++)
            foreach (var phrase in ConvenienceHintCatalog.PhrasesOf(c))
            {
                if (phrase == "안녕하세요") continue;
                Assert.That(ConvenienceQuickPhrasePolicy.TryResolve(phrase, "select_items", null, false, out _), Is.True, phrase);
            }
        }
    }

    public class ConvenienceDashboardHintCategoryTests
    {
        GameObject _go;
        ConvenienceDashboardView _view;
        TMP_Text[] _labels;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Dashboard");
            _view = _go.AddComponent<ConvenienceDashboardView>();
            _labels = new TMP_Text[5];
            for (int i = 0; i < _labels.Length; i++)
            {
                var child = new GameObject("Label" + i, typeof(RectTransform));
                child.transform.SetParent(_go.transform, false);
                _labels[i] = child.AddComponent<TextMeshProUGUI>();
            }
            Set("quickPhraseLabels", _labels);
            Set("quickPhrases", new[] { "물 주세요", "어디에 있어요?", "얼마예요?", "계산할게요", "감사합니다" });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void CheckoutStep_RecommendsCardPayment()
        {
            _view.SetDialogueContext("checkout", true);

            Assert.That(_view.RecommendedPhrase, Is.EqualTo("카드로 할게요"));
            Assert.That(_labels[0].text, Is.EqualTo("카드로 할게요"));
        }

        [Test]
        public void SelectingOtherCategory_PutsItsPhrasesFirst_AndRecommendsFirstOfThem()
        {
            _view.SetDialogueContext("checkout", true);

            SelectCategory(ConvenienceHintCatalog.Extra);

            Assert.That(Texts(), Is.EqualTo(new[] { "봉투 주세요", "봉투 필요 없어요", "카드로 할게요", "현금으로 할게요", "얼마예요?" }));
            Assert.That(_view.RecommendedPhrase, Is.EqualTo("봉투 주세요"));
        }

        [Test]
        public void SelectingStepCategory_KeepsStepRecommendation()
        {
            _view.SetDialogueContext("checkout", true);

            SelectCategory(ConvenienceHintCatalog.Pay);

            Assert.That(Texts(), Is.EqualTo(new[] { "계산할게요", "카드로 할게요", "현금으로 할게요", "얼마예요?", "감사합니다" }));
            Assert.That(_view.RecommendedPhrase, Is.EqualTo("카드로 할게요"));
        }

        [Test]
        public void SelectingSameCategoryAgain_ReturnsToStepPhrases()
        {
            _view.SetDialogueContext("extras", false);
            SelectCategory(ConvenienceHintCatalog.Greeting);

            SelectCategory(ConvenienceHintCatalog.Greeting);

            Assert.That(Texts(), Is.EqualTo(new[] { "봉투 주세요", "봉투 필요 없어요", "얼마예요?", "계산할게요", "감사합니다" }));
            Assert.That(_view.RecommendedPhrase, Is.EqualTo("봉투 필요 없어요"));
        }

        [Test]
        public void StepChange_ResetsBrowsedCategory()
        {
            _view.SetDialogueContext("select_items", false);
            SelectCategory(ConvenienceHintCatalog.Pay);

            _view.SetDialogueContext("extras", false);

            Assert.That(Texts()[0], Is.EqualTo("봉투 주세요"));
            Assert.That(_view.RecommendedPhrase, Is.EqualTo("봉투 필요 없어요"));
        }

        void SelectCategory(int category) =>
            typeof(ConvenienceDashboardView).GetMethod("SelectHintCategory", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_view, new object[] { category });

        string[] Texts()
        {
            var result = new string[_labels.Length];
            for (int i = 0; i < _labels.Length; i++) result[i] = _labels[i].text;
            return result;
        }

        void Set(string field, object value) =>
            typeof(ConvenienceDashboardView).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_view, value);
    }
}
#endif
