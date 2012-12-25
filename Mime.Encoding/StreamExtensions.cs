using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mime.Encoding
{
	static class StreamExtensions
	{
		public static IInStream ToInStream(this Stream stream)
		{
			return new StreamInStream(stream);
		}

		sealed class StreamInStream : IInStream
		{
			readonly Stream _stream;

			public StreamInStream(Stream stream)
			{
				_stream = stream;
			}

			public uint ReadBytes(byte[] array, uint offset, uint length)
			{
				return (uint)_stream.Read(array, (int)offset, (int)length);
			}
		}

		public static IInStream ToInStream(this byte[] bytes)
		{
			return new ByteInStream(bytes);
		}

		sealed class ByteInStream : IInStream
		{
			readonly byte[] _bytes;
			ulong _offset;

			public ByteInStream(byte[] bytes)
			{
				_bytes = bytes;
			}

			public uint ReadBytes(byte[] array, uint offset, uint length)
			{
				ulong toCopy = Math.Min((ulong)_bytes.LongLength - _offset, length);
				if (toCopy == 0)
					return 0;

				Array.Copy(_bytes, (long)_offset, array, offset, (long)toCopy);
				_offset += toCopy;

				return (uint)toCopy;
			}
		}

		public static byte[] ToArray(this IInStream stream, uint bufSize = 8192)
		{
			var r = new List<byte>();
			var buf = new byte[bufSize];
			for (;;)
			{
				var read = stream.ReadBytes(buf, 0, bufSize);
				r.AddRange(buf.Take((int) read));
				if (read != bufSize)
					return r.ToArray();
			}
		}
	}
}
