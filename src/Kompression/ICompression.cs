﻿using System.IO;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("KompressionUnitTests")]

namespace Kompression
{
    public interface ICompression
    {
        string[] Names { get; }

        void Decompress(Stream input, Stream output);
        void Compress(Stream input, Stream output);
    }
}
