using System;
using System.Buffers;
using System.IO;

namespace ProtobuffEncoder;

/// <summary>
/// A memory stream that uses ArrayPool for its backing buffer to avoid large LOH allocations during serialization.
/// It fully overrides Span-based methods to ensure it correctly writes to the pooled buffer.
/// </summary>
internal sealed class PooledMemoryStream : Stream
{
    private byte[] _buffer;
    private int _length;
    private int _position;

    public PooledMemoryStream(int capacity = 256)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(capacity);
    }

    public int Capacity
    {
        get => _buffer.Length;
        set
        {
            if (value > _buffer.Length)
            {
                var newBuffer = ArrayPool<byte>.Shared.Rent(value);
                Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _length);
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = newBuffer;
            }
        }
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;

    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set => _position = (int)value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesToCopy = Math.Min(count, _length - _position);
        if (bytesToCopy <= 0) return 0;
        Buffer.BlockCopy(_buffer, _position, buffer, offset, bytesToCopy);
        _position += bytesToCopy;
        return bytesToCopy;
    }

    public override int Read(Span<byte> buffer)
    {
        int bytesToCopy = Math.Min(buffer.Length, _length - _position);
        if (bytesToCopy <= 0) return 0;
        new ReadOnlySpan<byte>(_buffer, _position, bytesToCopy).CopyTo(buffer);
        _position += bytesToCopy;
        return bytesToCopy;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureCapacity(_position + count);
        Buffer.BlockCopy(buffer, offset, _buffer, _position, count);
        _position += count;
        if (_position > _length) _length = _position;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureCapacity(_position + buffer.Length);
        buffer.CopyTo(new Span<byte>(_buffer, _position, buffer.Length));
        _position += buffer.Length;
        if (_position > _length) _length = _position;
    }

    public override System.Threading.Tasks.Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
    {
        Write(buffer, offset, count);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public override System.Threading.Tasks.ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, System.Threading.CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return default;
    }

    public override void WriteByte(byte value)
    {
        EnsureCapacity(_position + 1);
        _buffer[_position++] = value;
        if (_position > _length) _length = _position;
    }

    public void WriteTo(Stream stream)
    {
        stream.Write(_buffer, 0, _length);
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        destination.Write(_buffer, _position, _length - _position);
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                _position = (int)offset;
                break;
            case SeekOrigin.Current:
                _position += (int)offset;
                break;
            case SeekOrigin.End:
                _position = _length + (int)offset;
                break;
        }
        return _position;
    }

    public override void SetLength(long value)
    {
        _length = (int)value;
        if (_position > _length) _position = _length;
    }

    public ReadOnlySpan<byte> GetBufferSpan() => new ReadOnlySpan<byte>(_buffer, 0, _length);

    public byte[] ToArray()
    {
        var result = new byte[_length];
        Buffer.BlockCopy(_buffer, 0, result, 0, _length);
        return result;
    }

    private void EnsureCapacity(int min)
    {
        if (_buffer.Length < min)
        {
            int newCapacity = _buffer.Length * 2;
            if (newCapacity < min) newCapacity = min;
            Capacity = newCapacity;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _buffer != null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null!;
        }
        base.Dispose(disposing);
    }
}
