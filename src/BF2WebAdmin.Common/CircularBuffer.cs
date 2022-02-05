using System.Collections.Generic;
using System.Linq;

namespace BF2WebAdmin.Common;

public class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _nextFree;

    public CircularBuffer(int length)
    {
        _buffer = new T[length];
        _nextFree = 0;
    }

    public void Add(T item)
    {
        _buffer[_nextFree] = item;
        _nextFree = (_nextFree + 1) % _buffer.Length;
    }

    public IEnumerable<T> Get()
    {
        return _buffer.ToList();
    }
}
