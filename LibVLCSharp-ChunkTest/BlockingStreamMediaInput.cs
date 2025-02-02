using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
namespace LibVLCSharp_ChunkTest
{
    public class BlockingStreamMediaInput : MediaInput
    {
        private readonly object _lock = new();
        private int _currentOffset = 0;
        private byte[] _buffer;
        private bool isFinished = false;

        public BlockingStreamMediaInput()
        {
            _buffer = [];
            this.CanSeek = false;
        }

        /// <summary>
        /// Marks the stream as finished.
        /// </summary>
        public void SetFinished()
        {
            lock (_lock)
            {
                Console.WriteLine($"BlockingStreamMediaInput:AddChunk - Marking the stream as finished.");
                isFinished = true;
                Monitor.PulseAll(_lock);
            }
        }

        /// <summary>
        /// Adds data to the buffer and signals waiting readers.
        /// </summary>
        public void AddChunk(byte[] newChunk)
        {
            lock (_lock)
            {
                Console.WriteLine($"BlockingStreamMediaInput:AddChunk - Adding chunk of size: {newChunk.Length}B.");

                byte[] newBuffer = new byte[_buffer.Length + newChunk.Length];
                Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _buffer.Length);
                Buffer.BlockCopy(newChunk, 0, newBuffer, _buffer.Length, newChunk.Length);
                _buffer = newBuffer;
                Monitor.PulseAll(_lock); // Notify any waiting readers
            }
        }

        /// <summary>
        /// LibVLC calls this method to open the media. Set the size if known.
        /// </summary>
        public override bool Open(out ulong size)
        {
            size = ulong.MaxValue; // Let LibVLC know the total size (or MaxValue if unknown)
            return true;
        }

        /// <summary>
        /// LibVLC calls this to read data into its buffer.
        /// </summary>
        public override unsafe int Read(IntPtr buf, uint len)
        {
            Console.WriteLine($"BlockingStreamMediaInput:Read - Read call with buffer of size: {len}B.");

            lock (_lock)
            {
                if (isFinished)
                {
                    Console.WriteLine($"BlockingStreamMediaInput:Read - isFinished true, returning 0 (EOF).");
                    return 0;
                }
                while (_buffer.Length - _currentOffset == 0)
                {
                    Console.WriteLine($"BlockingStreamMediaInput:Read - No new data to read. Waiting...");
                    Monitor.Wait(_lock);
                    if (isFinished)
                    {
                        Console.WriteLine($"BlockingStreamMediaInput:Read - isFinished true, returning 0 (EOF).");
                        return 0;
                    }
                }
              
                if (_buffer.Length - _currentOffset < 0)
                {
                    Console.WriteLine($"BlockingStreamMediaInput:Read - Error: Buffer length - currentOffset is < 0.");
                    return -1;
                }
            

                int bytesToCopy = Math.Min((int)len, _buffer.Length - _currentOffset);

                Console.WriteLine($"BlockingStreamMediaInput:Read - Reading {bytesToCopy}B, at offset: {_currentOffset} of buffer size: {_buffer.Length}");


                Marshal.Copy(_buffer, _currentOffset, buf, bytesToCopy);
                _currentOffset += bytesToCopy;
                Console.WriteLine($"BlockingStreamMediaInput:Read - New current offset: {_currentOffset}");


                return bytesToCopy;
            }
        }

        /// <summary>
        /// LibVLC calls this to seek to a specific position in the stream.
        /// </summary>
        public override bool Seek(ulong offset)
        {
            Console.WriteLine($"BlockingStreamMediaInput:Seek - LibVlc tried to seek. Returning false.");
            return false;
        }

        /// <summary>
        /// Closes the media. Cleanup logic if needed.
        /// </summary>
        public override void Close()
        {
            // lock (_lock)
            // {
            //     _bufferStream.SetLength(0); // Clear the buffer
            //     _bufferStream.Seek(0, SeekOrigin.Begin);
            // }
        }


    }
}
