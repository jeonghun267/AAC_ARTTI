#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Artti.Training;

namespace Artti.EditorTests.Training
{
    public class ConvenienceHudViewTests
    {
        [TestCase(0, 5, 0f)]
        [TestCase(1, 5, 0.25f)]
        [TestCase(2, 5, 0.5f)]
        [TestCase(3, 5, 0.75f)]
        [TestCase(4, 5, 1f)]
        public void StepFillRatio_AlignsWithNodePositions(int index, int count, float expected)
        {
            var method = typeof(ConvenienceHudView).GetMethod(
                "CalculateStepFillRatio", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            var actual = (float)method.Invoke(null, new object[] { index, count });
            Assert.That(actual, Is.EqualTo(expected).Within(0.0001f));
        }

        // 진행 배: 별(단계 점) 간격이 고르지 않아도 정수 단계에서는 그 별 위, 사이 값에서는 두 별 사이에 선다(+ 띄움)
        [TestCase(0f, 0f)]
        [TestCase(1f, 100f)]
        [TestCase(1.5f, 175f)]
        [TestCase(2f, 250f)]
        [TestCase(9f, 250f)]
        public void Boat_FollowsUnevenDots(float progress, float expectedX)
        {
            var go = new GameObject("Hud");
            try
            {
                var hud = go.AddComponent<ConvenienceHudView>();
                float[] xs = { 0f, 100f, 250f };
                var dots = new Image[xs.Length];
                for (int i = 0; i < xs.Length; i++)
                {
                    var dot = new GameObject("Dot" + i, typeof(RectTransform)).AddComponent<Image>();
                    dot.transform.SetParent(go.transform, false);
                    dot.rectTransform.anchoredPosition = new Vector2(xs[i], 0f);
                    dots[i] = dot;
                }
                var boat = new GameObject("Boat", typeof(RectTransform)).GetComponent<RectTransform>();
                boat.SetParent(go.transform, false);
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(ConvenienceHudView).GetField("stepDots", flags).SetValue(hud, dots);
                typeof(ConvenienceHudView).GetField("stepBoat", flags).SetValue(hud, boat);
                typeof(ConvenienceHudView).GetField("stepBoatOffset", flags).SetValue(hud, new Vector2(0f, 4f));

                typeof(ConvenienceHudView).GetMethod("SetBoat", flags).Invoke(hud, new object[] { progress });

                Assert.That(boat.anchoredPosition.x, Is.EqualTo(expectedX).Within(0.001f));
                Assert.That(boat.anchoredPosition.y, Is.EqualTo(4f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
