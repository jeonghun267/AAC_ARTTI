using System;

namespace Artti.UI
{
    // AAC 문장 카드의 모양(아이콘/색) 번호를 고른다.
    //   문장에 어떤 모양의 낱말(예: "감사", "고맙")이 들어 있으면 그 모양, 없으면 카드 순서대로 모양을 돌려 쓴다.
    //   카드 문장은 Gemini가 만들어 미리 알 수 없으므로 낱말로 맞춘다.
    public static class PhraseStylePicker
    {
        // keywords[i] = i번 모양의 낱말들. 모양이 하나도 없으면 -1.
        public static int Pick(string phrase, int index, string[][] keywords)
        {
            if (keywords == null || keywords.Length == 0) return -1;
            if (!string.IsNullOrEmpty(phrase))
            {
                for (int i = 0; i < keywords.Length; i++)
                {
                    var words = keywords[i];
                    if (words == null) continue;
                    for (int k = 0; k < words.Length; k++)
                    {
                        if (!string.IsNullOrEmpty(words[k]) && phrase.IndexOf(words[k], StringComparison.Ordinal) >= 0)
                            return i;
                    }
                }
            }
            int n = keywords.Length;
            return ((index % n) + n) % n;
        }
    }
}
