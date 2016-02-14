/// 
/// Author: James Dickson 
/// More tools and contact information: www.wallparse.com
///
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

namespace nurl
{
    /// <summary>
    /// Class is used for reading lines from network stream as a simple means of parsing http-headers.
    /// 
    /// </summary>
    class BinaryLineStream : Stream
    {
        Stream instream = null;

        public BinaryLineStream(Stream instream)
        {
            this.instream = instream;
        }

        public Decoder decoder = Encoding.ASCII.GetDecoder();

        public string ReadLine()
        {
            string strRetval = null;
            byte[] btsAll = new byte[4096];

            int bt = instream.ReadByte();
            int pos = 0;

            while (bt >= 0 && bt != 13)
            {
                if (bt != 10)
                {
                    if (btsAll.Length <= (pos + 1))
                    {
                        byte[] btTemp = new byte[btsAll.Length * 2];
                        Array.Copy(btsAll, btTemp, btsAll.Length);
                        btsAll = btTemp;
                    }

                    btsAll[pos++] = (byte)bt;
                }

                bt = instream.ReadByte();
            }

            char[] chars = new char[decoder.GetCharCount(btsAll, 0, pos)];
            decoder.GetChars(btsAll, 0, pos, chars, 0);

            strRetval = new String(chars);


            return strRetval;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return 0;
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
