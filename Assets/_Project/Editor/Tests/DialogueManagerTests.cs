#if UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Artti.Training;

namespace Artti.EditorTests.Training
{
    public class DialogueManagerTests
    {
        private static readonly string[] Order =
        {
            "greeting", "select_items", "extras", "checkout", "farewell"
        };

        [Test]
        public void ResolveNextObjective_CurrentCompleted_MovesOneStep()
        {
            var manager = CreateManager(_ => true);

            Assert.That(manager.ResolveNextObjective("greeting"), Is.EqualTo("select_items"));
        }

        [Test]
        public void ResolveNextObjective_ImmediateTransition_MovesOneStep()
        {
            var manager = CreateManager(_ => true);

            Assert.That(manager.ResolveNextObjective("select_items"), Is.EqualTo("select_items"));
        }

        [Test]
        public void ResolveNextObjective_FutureTransition_IsClampedToOneStep()
        {
            var manager = CreateManager(_ => true);
            LogAssert.Expect(LogType.Warning,
                "[DialogueManager] objective 점프 보정: greeting → checkout — 바로 다음 단계로 제한");

            Assert.That(manager.ResolveNextObjective("checkout"), Is.EqualTo("select_items"));
        }

        [Test]
        public void ResolveNextObjective_MissingImmediateCards_DoesNotSkip()
        {
            var manager = CreateManager(id => id != "select_items");
            LogAssert.Expect(LogType.Warning,
                "[DialogueManager] 바로 다음 objective 'select_items' 카드 없음 — 순차 진행 중단");

            Assert.That(manager.ResolveNextObjective("checkout"), Is.Null);
        }

        [Test]
        public void AppendSlotListItem_MultipleItems_AreAccumulated()
        {
            var manager = CreateManager(_ => true);

            manager.AppendSlotListItem("items_requested", "물");
            manager.AppendSlotListItem("items_requested", "과자");

            Assert.That(manager.SlotsSnapshot(), Does.Contain("items_requested=[\"물\",\"과자\"]"));
        }

        [Test]
        public void AppendSlotListItem_DuplicateItem_IsStoredOnce()
        {
            var manager = CreateManager(_ => true);

            manager.AppendSlotListItem("items_requested", "물");
            manager.AppendSlotListItem("items_requested", "물");

            Assert.That(manager.SlotsSnapshot(), Does.Contain("items_requested=[\"물\"]"));
        }

        private static DialogueManager CreateManager(Func<string, bool> hasCards)
        {
            var manager = new DialogueManager();
            manager.Initialize("greeting", Order, hasCards);
            return manager;
        }
    }
}
#endif
