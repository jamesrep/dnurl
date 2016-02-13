using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

namespace nurl
{
    /// <summary>
    /// Not used yet. Maybe will be used for chunk-streams when used.
    /// </summary>
    class ChunkedStream : Stream
    {
        Stream instream = null;

        public ChunkedStream(Stream instream)
        {
            this.instream = instream;
        }

        static uint getChunkSize(Stream inputStream)
        {
            byte[] btsChunk = new byte[8];
            inputStream.Read(btsChunk, 0, btsChunk.Length);

            string hexString = System.Text.ASCIIEncoding.ASCII.GetString(btsChunk);
            uint num = (uint)Int32.Parse(hexString, System.Globalization.NumberStyles.HexNumber);

            int btRead = inputStream.ReadByte();

            while (btRead != -1 && btRead != ((int)'\n'))
            {
                btRead = inputStream.ReadByte();
            }

            return num;
        }

        public static byte[] readAllChunksFromStream(Stream inputStream)
        {
            MemoryStream temporaryMemstream = new MemoryStream();
            int readBytes = 0;

            uint chunkSize = getChunkSize(inputStream);

            while (chunkSize > 0)
            {

                int receivedCount = 0;

                while (receivedCount < chunkSize)
                {
                    byte[] buffer = new byte[chunkSize - receivedCount];

                    readBytes = inputStream.Read(buffer, 0, buffer.Length);

                    receivedCount += readBytes;
                    temporaryMemstream.Write(buffer, 0, readBytes);
                }
            }


            return temporaryMemstream.ToArray();
        }

        byte[] btsAll = null;
        int position = 0;

        void readAll()
        {
            if (btsAll == null)
            {
                btsAll = readAllChunksFromStream(this.instream);
            }
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            readAll(); // Make sure all is read

            int readbytes = Math.Min(count, btsAll.Length - position);
            Array.Copy(btsAll, 0, buffer, offset, readbytes);

            return readbytes;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }


        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {

        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }


    }
}
