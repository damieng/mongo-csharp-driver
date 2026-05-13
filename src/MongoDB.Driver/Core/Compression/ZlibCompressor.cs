/* Copyright 2019-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.IO;
using MongoDB.Driver.Core.Misc;
#if NET6_0_OR_GREATER
using System.IO.Compression;
#else
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
#endif

namespace MongoDB.Driver.Core.Compression
{
    /// <summary>
    /// Compressor according to the zlib algorithm.
    /// </summary>
    internal sealed class ZlibCompressor : ICompressor
    {
#if NET6_0_OR_GREATER
        private readonly CompressionLevel _compressionLevel;
#else
        private readonly CompressionLevel _compressionLevel;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibCompressor" /> class.
        /// </summary>
        /// <param name="compressionLevel">The compression level.</param>
        public ZlibCompressor(int? compressionLevel = 0)
        {
            _compressionLevel = GetCompressionLevel(compressionLevel);
        }

        /// <inheritdoc />
        public CompressorType Type => CompressorType.Zlib;

        /// <inheritdoc />
        public void Compress(Stream input, Stream output)
        {
#if NET6_0_OR_GREATER
            using (var zlibStream = new ZLibStream(output, _compressionLevel, leaveOpen: true))
            {
                input.EfficientCopyTo(zlibStream);
            }
#else
            using (var zlibStream = new ZlibStream(new NonDisposingStream(output), CompressionMode.Compress, _compressionLevel))
            {
                zlibStream.FlushMode = FlushType.Sync;
                input.EfficientCopyTo(zlibStream);
            }
#endif
        }

        /// <inheritdoc />
        public void Decompress(Stream input, Stream output)
        {
#if NET6_0_OR_GREATER
            using (var zlibStream = new ZLibStream(input, CompressionMode.Decompress, leaveOpen: true))
            {
                zlibStream.CopyTo(output);
            }
#else
            using (var zlibStream = new ZlibStream(new NonDisposingStream(input), CompressionMode.Decompress))
            {
                zlibStream.CopyTo(output);
            }
#endif
        }

#if NET6_0_OR_GREATER
        private static CompressionLevel GetCompressionLevel(int? compressionLevel)
        {
            if (!compressionLevel.HasValue)
            {
                compressionLevel = -1;
            }

            switch (compressionLevel)
            {
                case -1:
                    return CompressionLevel.Optimal;
                case 0:
                    return CompressionLevel.NoCompression;
                case int l when l >= 1 && l <= 5:
                    return CompressionLevel.Fastest;
                case int l when l >= 6 && l <= 8:
                    return CompressionLevel.Optimal;
                case 9:
                    return CompressionLevel.SmallestSize;
                default:
                    throw new ArgumentOutOfRangeException(nameof(compressionLevel));
            }
        }
#else
        private static CompressionLevel GetCompressionLevel(int? compressionLevel)
        {
            if (!compressionLevel.HasValue)
            {
                compressionLevel = -1;
            }

            switch (compressionLevel)
            {
                case -1:
                    return CompressionLevel.Default;
                case int _ when compressionLevel >= 0 && compressionLevel <= 9:
                    return (CompressionLevel)compressionLevel;
                default:
                    throw new ArgumentOutOfRangeException(nameof(compressionLevel));
            }
        }

        private sealed class NonDisposingStream : Stream
        {
            private readonly Stream _inner;

            public NonDisposingStream(Stream inner) { _inner = inner; }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => _inner.CanWrite;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }
            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => _inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
            protected override void Dispose(bool disposing) { /* intentionally does not dispose the inner stream */ }
        }
#endif
    }
}
