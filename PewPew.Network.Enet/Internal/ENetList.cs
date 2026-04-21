using System;
using System.Collections.Generic;

namespace PewPew.Network.Enet.Internal
{
    /// <summary>
    /// Intrusive doubly-linked list node. Embed this as a field in any class that
    /// needs to participate in an ENetList, matching the C ENetListNode pattern.
    /// </summary>
    internal class ENetListNode<T> where T : class
    {
        public ENetListNode<T>? Next;
        public ENetListNode<T>? Previous;
        public T? Owner; // back-reference to the owning object
    }

    /// <summary>
    /// Intrusive circular doubly-linked list with a sentinel node.
    /// Mirrors the C ENetList / ENetListNode structure exactly.
    /// </summary>
    internal class ENetList<T> where T : class
    {
        // Sentinel node – its Next/Previous always point into the list
        private readonly ENetListNode<T> _sentinel;

        public ENetList()
        {
            _sentinel = new ENetListNode<T>();
            _sentinel.Next = _sentinel;
            _sentinel.Previous = _sentinel;
        }

        public ENetListNode<T> Sentinel => _sentinel;

        public ENetListNode<T> Begin => _sentinel.Next!;
        public ENetListNode<T> End => _sentinel;

        public bool IsEmpty => _sentinel.Next == _sentinel;

        public T? Front => _sentinel.Next == _sentinel ? null : _sentinel.Next!.Owner;
        public T? Back => _sentinel.Previous == _sentinel ? null : _sentinel.Previous!.Owner;

        public void Clear()
        {
            _sentinel.Next = _sentinel;
            _sentinel.Previous = _sentinel;
        }

        /// <summary>Insert <paramref name="node"/> before <paramref name="position"/>.</summary>
        public void Insert(ENetListNode<T> position, ENetListNode<T> node)
        {
            node.Previous = position.Previous;
            node.Next = position;
            node.Previous!.Next = node;
            position.Previous = node;
        }

        /// <summary>Remove <paramref name="node"/> from the list.</summary>
        public void Remove(ENetListNode<T> node)
        {
            node.Previous!.Next = node.Next;
            node.Next!.Previous = node.Previous;
            node.Next = null;
            node.Previous = null;
        }

        /// <summary>
        /// Move the range [first..last] (inclusive) from their current list to
        /// just before <paramref name="position"/> in this list.
        /// </summary>
        public void Move(ENetListNode<T> position, ENetListNode<T> first, ENetListNode<T> last)
        {
            // Detach [first..last] from their current chain
            first.Previous!.Next = last.Next;
            last.Next!.Previous = first.Previous;

            // Splice before position
            first.Previous = position.Previous;
            last.Next = position;
            first.Previous!.Next = first;
            position.Previous = last;
        }

        public int Count()
        {
            int n = 0;
            for (var cur = _sentinel.Next; cur != _sentinel; cur = cur!.Next)
                n++;
            return n;
        }

        /// <summary>Enumerate owning objects (not nodes).</summary>
        public IEnumerable<T> Items()
        {
            for (var cur = _sentinel.Next; cur != _sentinel; cur = cur!.Next)
            {
                if (cur!.Owner != null)
                    yield return cur.Owner;
            }
        }
    }
}
