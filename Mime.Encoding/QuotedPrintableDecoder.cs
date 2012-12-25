using System.Diagnostics;
using System;

namespace Mime.Encoding
{
	sealed class QuotedPrintableDecoder : IInStream
	{
		readonly IInStream _source;

		/// current decoded bytes

		const uint BufSize = 0x8000;

		/// Maximum number of encoded bytes (excluding CRLF)
		/// robustness: regular size is 76, 
		/// but some are encoded using 77-79 ;(

		/// I think we need to add some more robustness here and allow a certain number of more
		/// characters.
	
		/// todo: report violations in the log!
		
		const uint MaxEncodedLength = 79;

		readonly byte[] _encodedLine;

		uint _lineOffset;
		uint _lineLength;
		readonly byte[] _decodedLine;

		readonly byte[] _inBuf = new byte[BufSize];
		uint _offset;
		uint _end;

		public QuotedPrintableDecoder(IInStream source)
		{
			_source = source;
			_encodedLine = new byte[MaxEncodedLength];
			_decodedLine = new byte[MaxEncodedLength + 2];
		}

		#region IInStream Members

		public uint ReadBytes(byte[] array, uint offset, uint length)
		{
			if (length == 0)
				return 0;

			uint end = offset + length;
			Debug.Assert(end <= array.Length);


			while (offset != end)
			{
				if (_lineOffset == _lineLength && !readAndDecodeLine())
					return length - (end - offset);

				Debug.Assert(_lineOffset != _lineLength);

				uint toCopy = Math.Min(end - offset, _lineLength - _lineOffset);
				Array.Copy(_decodedLine, _lineOffset, array, offset, toCopy);

				offset += toCopy;
				_lineOffset += toCopy;
			}

			return length;
		}

		#endregion

		bool readAndDecodeLine()
		{
			uint read = 0;

			while (read != MaxEncodedLength)
			{
				if (_offset == _end && !readIn())
					return read != 0 && decodeLine(read, false);

				byte c = _inBuf[_offset++];
				if (c == 13)
					goto seenCR;

				_encodedLine[read++] = c;
			}

			// either we seen a CR at the current position, or
			// the line has reached its maximum number of characters,
			// in both cases we expect an CR / LF

			// todo: when we reach here, and MaxEncodedLength is > 79, we could treat this as an error?

			if (_offset == _end && !readIn())
				return decodeLine(read, false);

			if (_inBuf[_offset++] != 13)
				throw new Exception("QuotedPrintable: expecting CR");

		seenCR:

			if (_offset == _end && !readIn())
				throw new Exception("QuotedPrintable: End of file before line end (LF)");

			if (_inBuf[_offset++] != 10)
				throw new Exception("QuotedPrintable: expecting LF");

			return decodeLine(read, true);
		}

		bool decodeLine(uint length, bool seenEOL)
		{
			// When decoding a Quoted-Printable
			// body, any trailing white space on a line must be
			// deleted, as it will necessarily have been added by
			// intermediate transport agents.

			while (length != 0)
			{
				byte b = _encodedLine[length - 1];
				if (b != 32 && b != 9)
					break;

				--length;
			}

			uint decoded = 0;
			uint state = 0; // 0: normal, 1: seen '=', 2: seen '=x'
			byte v = 0;

			for (int i = 0; i != length; ++i)
			{
				var b = _encodedLine[i];

				switch (state)
				{
					case 0:
						if (b == '=')
							state = 1;
						else
							if (b == 9 || b >= 32 && b <= 126)
								_decodedLine[decoded++] = b;
							else
								throw new Exception(string.Format("QuotedPrintable: invalid character: {0:x2}", b));
						break;

					case 1:
						v = getHex(b);
						++state;
						break;

					case 2:
						_decodedLine[decoded++] = (byte)(v << 4 | getHex(b));
						state = 0;
						break;
				}
			}

			switch (state)
			{
				case 0:
					if (seenEOL)
					{
						_decodedLine[decoded++] = 13;
						_decodedLine[decoded++] = 10;
					}
					break;

				case 1:
					break; // soft line break

				case 2:
					throw new Exception("QuotedPrintable: seen line break in partial '=' escape");
			}


			_lineOffset = 0;
			_lineLength = decoded;

			return _lineLength != 0;
		}

		static byte getHex(byte b)
		{
			if (b >= '0' && b <= '9')
				return (byte) (b - '0');
			if (b >= 'A' && b <= 'F')
				return (byte)(b - 'A' + 10);

			throw new Exception("QuotedPrintable: invalid hex character");
		}

		bool readIn()
		{
			Debug.Assert(_offset == _end);
			_offset = 0;
			_end = _source.ReadBytes(_inBuf, 0, BufSize);
			return _end != 0;
		}
	}
}
