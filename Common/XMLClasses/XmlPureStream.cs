using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.XMLClasses
{
    /// <summary>
    /// Wrapper for invalid xml files
    /// </summary>
    public class XmlPureStream : Stream
    {
        private readonly Stream _baseStream;

        /// <summary>
        /// Byte that will replace invalid bytes (default is space)
        /// </summary>
        public byte ReplaceByte { get; set; }

        public XmlPureStream(Stream baseStream)
        {
            _baseStream = baseStream;
            ReplaceByte = (byte)' ';
        }

        /// <summary>
        /// Whether a given character is allowed by XML 1.0.
        /// </summary>
        public static bool IsLegalXmlChar(int character)
        {
            return
                (
                    character == 0x9 /* == '\t' == 9   */          ||
                    character == 0xA /* == '\n' == 10  */          ||
                    character == 0xD /* == '\r' == 13  */          ||
                    (character >= 0x20 && character <= 0xD7FF) ||
                    (character >= 0xE000 && character <= 0xFFFD) ||
                    (character >= 0x10000 && character <= 0x10FFFF)
                );
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _baseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytes = _baseStream.Read(buffer, offset, count);

            for (var i = 0; i < bytes; i++)
            {
                if (!IsLegalXmlChar(buffer[i]))
                    buffer[i] = ReplaceByte;
            }

            return bytes;
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
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { return _baseStream.Length; }
        }

        public override long Position
        {
            get { return _baseStream.Position; }
            set { _baseStream.Position = value; }
        }
    }
}
