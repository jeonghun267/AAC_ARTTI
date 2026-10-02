#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Artti.UI;

namespace Artti.EditorTests.UI
{
    // 인사 카드 모양 고르기: 문장 속 낱말이 있으면 그 모양, 없으면 카드 순서대로 돌려 쓴다.
    public class PhraseStylePickerTests
    {
        private static readonly string[][] Greeting =
        {
            new[] { "안녕", "처음" },
            new[] { "반가", "만나" },
            new[] { "도와", "도움" },
            new[] { "감사", "고맙" },
            new[] { "죄송", "미안" },
            new[] { "다시", "천천히" },
        };

        [TestCase("안녕하세요", 4, 0)]
        [TestCase("만나서 반가워요", 0, 1)]
        [TestCase("도와주세요", 0, 2)]
        [TestCase("고맙습니다", 0, 3)]
        [TestCase("미안해요", 0, 4)]
        [TestCase("다시 말해 주세요", 0, 5)]
        public void 낱말이_있으면_그_모양(string phrase, int index, int expected)
        {
            Assert.AreEqual(expected, PhraseStylePicker.Pick(phrase, index, Greeting));
        }

        [TestCase(0, 0)]
        [TestCase(5, 5)]
        [TestCase(7, 1)]
        public void 낱말이_없으면_카드_순서대로(int index, int expected)
        {
            Assert.AreEqual(expected, PhraseStylePicker.Pick("오늘 날씨가 좋네요", index, Greeting));
        }

        [Test]
        public void 모양이_없으면_마이너스1()
        {
            Assert.AreEqual(-1, PhraseStylePicker.Pick("안녕하세요", 0, new string[0][]));
            Assert.AreEqual(-1, PhraseStylePicker.Pick("안녕하세요", 0, null));
        }
    }
}
#endif
