using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LibVLCSharp.Shared;

namespace LibVLCSharp_ChunkTest
{
    public class ChunkStream : MediaInput
    {
        private readonly ConcurrentQueue<byte[]> _chunks = new();
        private readonly object _lock = new();
        private int _currentChunkOffset = 0;
        private readonly int _minChunksToKeep = 2;
        private long _totalLength = 0;

        public ChunkStream()
        {
            this.CanSeek = false;
        }

        // Add a new chunk to the stream
        public void AddChunk(byte[] chunk)
        {
            lock (_lock)
            {
                _chunks.Enqueue(chunk);
                _totalLength += chunk.Length;

                Monitor.PulseAll(_lock); // Notify any waiting readers
            }
        }

        public override int Read(IntPtr buf, uint len)
        {
            lock (_lock)
            {
                while (_chunks.IsEmpty)
                {
                    Monitor.Wait(_lock); // Wait until a chunk is available
                }

                if (!_chunks.TryPeek(out var currentChunk))
                    return 0;
                Marshal.Copy(currentChunk, 0, buf, currentChunk.Length);
                _chunks.TryDequeue(out _);
                return currentChunk.Length;
            }
        }
        public override bool Open(out ulong size)
        {
            size = ulong.MaxValue; // Let LibVLC know the total size (or MaxValue if unknown)
            return true;
        }
        public override bool Seek(ulong offset)
        {
            return false;
        }

        public override void Close()
        {
            throw new NotImplementedException();
        }
    }
}
