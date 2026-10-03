#if UNITY_INCLUDE_TESTS
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Artti.UI;

namespace Artti.EditorTests.UI
{
    // 도움 화면 채팅창의 보낸 문장 목록: 최근 것이 맨 앞, 같은 문장은 맨 앞으로, 최대 개수, 파일로 다시 불러오기.
    public class ChatHistoryStoreTests
    {
        private string _path;

        [SetUp]
        public void SetUp() => _path = Path.Combine(Application.temporaryCachePath, "chat_history_test.json");

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        [Test]
        public void 최근_문장이_맨_앞()
        {
            var store = new ChatHistoryStore(_path, 10);
            store.Add("안녕하세요");
            store.Add("포장해 주세요");
            CollectionAssert.AreEqual(new[] { "포장해 주세요", "안녕하세요" }, store.Items);
        }

        [Test]
        public void 같은_문장은_맨_앞으로_하나만()
        {
            var store = new ChatHistoryStore(_path, 10);
            store.Add("안녕하세요");
            store.Add("포장해 주세요");
            store.Add(" 안녕하세요 ");
            CollectionAssert.AreEqual(new[] { "안녕하세요", "포장해 주세요" }, store.Items);
        }

        [Test]
        public void 빈_문장은_남기지_않음()
        {
            var store = new ChatHistoryStore(_path, 10);
            store.Add("   ");
            store.Add(null);
            Assert.AreEqual(0, store.Items.Count);
        }

        [Test]
        public void 최대_개수를_넘으면_오래된_것부터_지움()
        {
            var store = new ChatHistoryStore(_path, 3);
            foreach (var s in new[] { "1", "2", "3", "4" }) store.Add(s);
            CollectionAssert.AreEqual(new[] { "4", "3", "2" }, store.Items);
        }

        [Test]
        public void 사용자마다_따로_저장하고_같은_사용자는_같이_씀()
        {
            string a = "test_user_a_" + System.Guid.NewGuid().ToString("N"), b = "test_user_b_" + System.Guid.NewGuid().ToString("N");
            try
            {
                ChatHistoryStore.ForUser(a, 10).Add("반가워요");
                Assert.AreEqual(0, ChatHistoryStore.ForUser(b, 10).Items.Count);
                Assert.AreSame(ChatHistoryStore.ForUser(a, 10), ChatHistoryStore.ForUser(a, 10));   // 모든 탭이 같은 목록
                CollectionAssert.AreEqual(new[] { "반가워요" }, new ChatHistoryStore(ChatHistoryStore.PathForUser(a), 10).Items);
            }
            finally
            {
                foreach (var id in new[] { a, b })
                    if (File.Exists(ChatHistoryStore.PathForUser(id))) File.Delete(ChatHistoryStore.PathForUser(id));
            }
        }

        [Test]
        public void 사용자를_지우면_목록도_지움()
        {
            string id = "test_user_del_" + System.Guid.NewGuid().ToString("N");
            ChatHistoryStore.ForUser(id, 10).Add("죄송합니다");
            Assert.IsTrue(File.Exists(ChatHistoryStore.PathForUser(id)));
            ChatHistoryStore.DeleteForUser(id);
            Assert.IsFalse(File.Exists(ChatHistoryStore.PathForUser(id)));
            Assert.AreEqual(0, ChatHistoryStore.ForUser(id, 10).Items.Count);   // 다시 열어도 빈 목록
        }

        [Test]
        public void 사용자를_고르지_않으면_guest()
        {
            StringAssert.EndsWith("guest.json", ChatHistoryStore.PathForUser(null));
            StringAssert.EndsWith("guest.json", ChatHistoryStore.PathForUser("  "));
        }

        [Test]
        public void 다시_켜도_남아_있음()
        {
            new ChatHistoryStore(_path, 10).Add("감사합니다");
            var reopened = new ChatHistoryStore(_path, 10);
            CollectionAssert.AreEqual(new[] { "감사합니다" }, reopened.Items);
        }
    }
}
#endif
