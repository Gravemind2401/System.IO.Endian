using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.IO.Endian
{
    internal interface IEndianStream
    {
        ByteOrder ByteOrder { get; }
        long Position { get; }
        void Seek(long offset, SeekOrigin origin);
    }
}
