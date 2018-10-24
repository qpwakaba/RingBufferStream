using System;
using System.IO;
using System.Threading;

namespace qpwakaba.RingBufferStream
{
    public class RingBufferStream : Stream
    {
        private const int BufferSize = 4096;
        private readonly byte[] buffer;

        public RingBufferStream() : this(BufferSize) { }
        public RingBufferStream(int bufferSize)
        {
            if (bufferSize == 0)
            {
                throw new ArgumentException("Buffer size cannot be 0");
            }
            this.buffer = new byte[bufferSize];
            this.lockObject = this.buffer;
        }

        private int readPosition = 0;
        private int writePosition = 0;
        private int available = 0;

        private readonly object readLockObject = new object();
        private readonly object writeLockObject = new object();
        private readonly object lockObject;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override void Flush() { }

        public override int Read(byte[] dest, int offset, int count)
        {
            if (count == 0) return 0;
            lock (readLockObject)
            {
                int read = 0;
                while (read < count)
                {
                    int reading = count - read;
                    int readable;
                    lock (lockObject)
                    {
                        while ((readable = Math.Min(this.buffer.Length - readPosition, available)) == 0)
                        {
                            if (Monitor.TryEnter(writeLockObject))
                            {
                                //No Data Available
                                Monitor.Exit(writeLockObject);
                                return read;
                            }
                            Monitor.Wait(lockObject);
                        }
                    }
                    reading = Math.Min(reading, readable);

                    Buffer.BlockCopy(this.buffer, readPosition, dest, offset + read, reading);
                    read += reading;

                    readPosition += reading;
                    lock (lockObject)
                    {
                        available -= reading;
                        Monitor.Pulse(lockObject);
                    }

                    if (readPosition == this.buffer.Length)
                    {
                        readPosition = 0;
                    }
                }
                return read;
            }
        }

        public override void Write(byte[] data, int offset, int count)
        {
            if (count == 0) return;
            lock (writeLockObject)
            {
                int wrote = 0;
                while (wrote < count)
                {
                    int writing = count - wrote;
                    int writable;

                    lock (lockObject)
                    {
                        while ((writable = Math.Min(this.buffer.Length - writePosition, this.buffer.Length - available)) == 0)
                        {
                            Monitor.Wait(lockObject);
                        }
                    }
                    writing = Math.Min(writing, writable);

                    Buffer.BlockCopy(data, offset + wrote, this.buffer, writePosition, writing);
                    wrote += writing;

                    writePosition += writing;
                    lock (lockObject)
                    {
                        available += writing;
                        Monitor.Pulse(lockObject);
                    }

                    if (writePosition == this.buffer.Length)
                    {
                        writePosition = 0;
                    }
                }
            }
        }

        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

    }
}
