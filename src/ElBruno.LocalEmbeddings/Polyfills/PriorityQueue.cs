#if NETSTANDARD2_0
namespace System.Collections.Generic;

/// <summary>
/// The members of <c>PriorityQueue</c> that the library uses, for netstandard2.0, which does not have the type. Same algorithm as .NET:
/// a quaternary min-heap with the default comparer, so the order of the results is the same on every target.
/// </summary>
internal sealed class PriorityQueue<TElement, TPriority>
{
    private const int Log2Arity = 2;
    private const int Arity = 1 << Log2Arity;

    private (TElement Element, TPriority Priority)[] _nodes;
    private int _size;

    public PriorityQueue(int initialCapacity)
    {
        if (initialCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }

        _nodes = new (TElement, TPriority)[initialCapacity];
    }

    public int Count => _size;

    public void Enqueue(TElement element, TPriority priority)
    {
        int currentSize = _size;
        if (_nodes.Length == currentSize)
        {
            Array.Resize(ref _nodes, Math.Max(currentSize * 2, Math.Max(currentSize + 1, 4)));
        }

        _size = currentSize + 1;
        MoveUp((element, priority), currentSize);
    }

    public bool TryPeek(out TElement element, out TPriority priority)
    {
        if (_size != 0)
        {
            (element, priority) = _nodes[0];
            return true;
        }

        element = default!;
        priority = default!;
        return false;
    }

    public TElement DequeueEnqueue(TElement element, TPriority priority)
    {
        if (_size == 0)
        {
            throw new InvalidOperationException("Queue empty.");
        }

        var root = _nodes[0];
        if (Comparer<TPriority>.Default.Compare(priority, root.Priority) > 0)
        {
            MoveDown((element, priority), 0);
        }
        else
        {
            _nodes[0] = (element, priority);
        }

        return root.Element;
    }

    public bool TryDequeue(out TElement element, out TPriority priority)
    {
        if (_size != 0)
        {
            (element, priority) = _nodes[0];
            int lastNodeIndex = --_size;
            if (lastNodeIndex > 0)
            {
                MoveDown(_nodes[lastNodeIndex], 0);
            }

            _nodes[lastNodeIndex] = default;
            return true;
        }

        element = default!;
        priority = default!;
        return false;
    }

    private void MoveUp((TElement Element, TPriority Priority) node, int nodeIndex)
    {
        while (nodeIndex > 0)
        {
            int parentIndex = (nodeIndex - 1) >> Log2Arity;
            var parent = _nodes[parentIndex];
            if (Comparer<TPriority>.Default.Compare(node.Priority, parent.Priority) < 0)
            {
                _nodes[nodeIndex] = parent;
                nodeIndex = parentIndex;
            }
            else
            {
                break;
            }
        }

        _nodes[nodeIndex] = node;
    }

    private void MoveDown((TElement Element, TPriority Priority) node, int nodeIndex)
    {
        int i;
        while ((i = (nodeIndex << Log2Arity) + 1) < _size)
        {
            var minChild = _nodes[i];
            int minChildIndex = i;
            int childIndexUpperBound = Math.Min(i + Arity, _size);
            while (++i < childIndexUpperBound)
            {
                var nextChild = _nodes[i];
                if (Comparer<TPriority>.Default.Compare(nextChild.Priority, minChild.Priority) < 0)
                {
                    minChild = nextChild;
                    minChildIndex = i;
                }
            }

            if (Comparer<TPriority>.Default.Compare(node.Priority, minChild.Priority) <= 0)
            {
                break;
            }

            _nodes[nodeIndex] = minChild;
            nodeIndex = minChildIndex;
        }

        _nodes[nodeIndex] = node;
    }
}
#endif
