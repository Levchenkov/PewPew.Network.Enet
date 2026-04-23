using System.Collections.Generic;
using Xunit;
using PewPew.Network.Enet.Internal;
#pragma warning disable CS8602

namespace PewPew.Network.Enet.Tests
{
    public class ENetListTests
    {
        // Helper: create an acknowledgement and return the object + its intrusive node.
        private static ENetAcknowledgement MakeAck(uint sentTime = 0)
        {
            return new ENetAcknowledgement { SentTime = sentTime };
        }

        // ── Empty-state ───────────────────────────────────────────────────────

        [Fact]
        public void NewList_IsEmpty()
        {
            var list = new ENetList<ENetAcknowledgement>();
            Assert.True(list.IsEmpty);
        }

        [Fact]
        public void NewList_Count_IsZero()
        {
            var list = new ENetList<ENetAcknowledgement>();
            Assert.Equal(0, list.Count());
        }

        [Fact]
        public void NewList_Front_IsNull()
        {
            var list = new ENetList<ENetAcknowledgement>();
            Assert.Null(list.Front);
        }

        [Fact]
        public void NewList_Back_IsNull()
        {
            var list = new ENetList<ENetAcknowledgement>();
            Assert.Null(list.Back);
        }

        [Fact]
        public void NewList_Begin_EqualsSentinel()
        {
            var list = new ENetList<ENetAcknowledgement>();
            Assert.Same(list.Sentinel, list.Begin);
        }

        [Fact]
        public void NewList_End_EqualsSentinel()
        {
            var list = new ENetList<ENetAcknowledgement>();
            Assert.Same(list.Sentinel, list.End);
        }

        // ── Insert ────────────────────────────────────────────────────────────

        [Fact]
        public void Insert_SingleNode_ListIsNotEmpty()
        {
            var list = new ENetList<ENetAcknowledgement>();
            list.Insert(list.End, MakeAck(1).ListNode);

            Assert.False(list.IsEmpty);
        }

        [Fact]
        public void Insert_SingleNode_CountIsOne()
        {
            var list = new ENetList<ENetAcknowledgement>();
            list.Insert(list.End, MakeAck(1).ListNode);
            Assert.Equal(1, list.Count());
        }

        [Fact]
        public void Insert_BeforeEnd_AppendsToBack()
        {
            var list = new ENetList<ENetAcknowledgement>();
            list.Insert(list.End, MakeAck(10).ListNode);
            list.Insert(list.End, MakeAck(20).ListNode);

            Assert.Equal(20u, list.Back!.SentTime);
        }

        [Fact]
        public void Insert_BeforeBegin_PrependsToFront()
        {
            var list = new ENetList<ENetAcknowledgement>();
            list.Insert(list.End, MakeAck(10).ListNode);   // append 10
            list.Insert(list.Begin, MakeAck(5).ListNode);  // prepend 5 before first

            Assert.Equal(5u, list.Front!.SentTime);
        }

        [Fact]
        public void Insert_ThreeNodes_OrderPreserved()
        {
            var list = new ENetList<ENetAcknowledgement>();
            list.Insert(list.End, MakeAck(1).ListNode);
            list.Insert(list.End, MakeAck(2).ListNode);
            list.Insert(list.End, MakeAck(3).ListNode);

            var items = new List<uint>();
            foreach (var a in list.Items())
                items.Add(a.SentTime);

            Assert.Equal(new[] { 1u, 2u, 3u }, items);
        }

        // ── Remove ────────────────────────────────────────────────────────────

        [Fact]
        public void Remove_OnlyNode_ListBecomesEmpty()
        {
            var list = new ENetList<ENetAcknowledgement>();
            var ack = MakeAck(1);
            list.Insert(list.End, ack.ListNode);
            list.Remove(ack.ListNode);

            Assert.True(list.IsEmpty);
        }

        [Fact]
        public void Remove_FirstNode_FrontUpdated()
        {
            var list = new ENetList<ENetAcknowledgement>();
            var a1 = MakeAck(1);
            var a2 = MakeAck(2);
            list.Insert(list.End, a1.ListNode);
            list.Insert(list.End, a2.ListNode);

            list.Remove(a1.ListNode);

            Assert.Equal(2u, list.Front!.SentTime);
        }

        [Fact]
        public void Remove_LastNode_BackUpdated()
        {
            var list = new ENetList<ENetAcknowledgement>();
            var a1 = MakeAck(1);
            var a2 = MakeAck(2);
            list.Insert(list.End, a1.ListNode);
            list.Insert(list.End, a2.ListNode);

            list.Remove(a2.ListNode);

            Assert.Equal(1u, list.Back!.SentTime);
        }

        [Fact]
        public void Remove_MiddleNode_OrderKept()
        {
            var list = new ENetList<ENetAcknowledgement>();
            var a1 = MakeAck(1);
            var a2 = MakeAck(2);
            var a3 = MakeAck(3);
            list.Insert(list.End, a1.ListNode);
            list.Insert(list.End, a2.ListNode);
            list.Insert(list.End, a3.ListNode);

            list.Remove(a2.ListNode);

            var items = new List<uint>();
            foreach (var a in list.Items())
                items.Add(a.SentTime);

            Assert.Equal(new[] { 1u, 3u }, items);
        }

        [Fact]
        public void Remove_DecreasesCount()
        {
            var list = new ENetList<ENetAcknowledgement>();
            var a1 = MakeAck(1);
            var a2 = MakeAck(2);
            list.Insert(list.End, a1.ListNode);
            list.Insert(list.End, a2.ListNode);

            list.Remove(a1.ListNode);

            Assert.Equal(1, list.Count());
        }

        // ── Clear ─────────────────────────────────────────────────────────────

        [Fact]
        public void Clear_EmptiesList()
        {
            var list = new ENetList<ENetAcknowledgement>();
            list.Insert(list.End, MakeAck(1).ListNode);
            list.Insert(list.End, MakeAck(2).ListNode);

            list.Clear();

            Assert.True(list.IsEmpty);
            Assert.Equal(0, list.Count());
        }

        [Fact]
        public void Clear_ThenInsert_Works()
        {
            var list = new ENetList<ENetAcknowledgement>();
            list.Insert(list.End, MakeAck(1).ListNode);
            list.Clear();
            list.Insert(list.End, MakeAck(99).ListNode);

            Assert.Equal(1, list.Count());
            Assert.Equal(99u, list.Front!.SentTime);
        }

        // ── Move ──────────────────────────────────────────────────────────────

        [Fact]
        public void Move_TransfersRangeToAnotherList()
        {
            var src = new ENetList<ENetAcknowledgement>();
            var dst = new ENetList<ENetAcknowledgement>();

            src.Insert(src.End, MakeAck(10).ListNode);
            src.Insert(src.End, MakeAck(20).ListNode);

            // Move all items from src to dst
            dst.Move(dst.End, src.Begin, src.Sentinel.Previous!);

            Assert.True(src.IsEmpty);
            Assert.Equal(2, dst.Count());
        }

        [Fact]
        public void Move_PreservesOrder()
        {
            var src = new ENetList<ENetAcknowledgement>();
            var dst = new ENetList<ENetAcknowledgement>();

            src.Insert(src.End, MakeAck(1).ListNode);
            src.Insert(src.End, MakeAck(2).ListNode);
            src.Insert(src.End, MakeAck(3).ListNode);

            dst.Move(dst.End, src.Begin, src.Sentinel.Previous!);

            var items = new List<uint>();
            foreach (var a in dst.Items())
                items.Add(a.SentTime);

            Assert.Equal(new[] { 1u, 2u, 3u }, items);
        }

        // ── Items enumeration ─────────────────────────────────────────────────

        [Fact]
        public void Items_EmptyList_YieldsNothing()
        {
            var list = new ENetList<ENetAcknowledgement>();
            int count = 0;
            foreach (var _ in list.Items())
                count++;
            Assert.Equal(0, count);
        }

        [Fact]
        public void Items_SentinelNeverReturned()
        {
            var list = new ENetList<ENetAcknowledgement>();
            list.Insert(list.End, MakeAck(5).ListNode);

            foreach (var item in list.Items())
                Assert.NotNull(item);
        }

        // ── Front / Back ──────────────────────────────────────────────────────

        [Fact]
        public void Front_AfterInsert_ReturnsFirstItem()
        {
            var list = new ENetList<ENetAcknowledgement>();
            list.Insert(list.End, MakeAck(42).ListNode);
            list.Insert(list.End, MakeAck(99).ListNode);
            Assert.Equal(42u, list.Front!.SentTime);
        }

        [Fact]
        public void Back_AfterInsert_ReturnsLastItem()
        {
            var list = new ENetList<ENetAcknowledgement>();
            list.Insert(list.End, MakeAck(42).ListNode);
            list.Insert(list.End, MakeAck(99).ListNode);
            Assert.Equal(99u, list.Back!.SentTime);
        }
    }
}
