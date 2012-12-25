using System;
using System.Diagnostics;
using TextEncoding = System.Text.Encoding;

namespace Mime.Encoding
{
	public sealed class Base64Encoder : IInStream
	{
		readonly IInStream _stream;

		const uint BufSize = 0x8000;

		const uint EncodedCharactersPerLine = 76; // max!
		const uint DecodedBytesPerLine = EncodedCharactersPerLine / 4 * 3; // 57
		const uint CRLFBytes = 2;
		const uint EncodedBytesPerLine = EncodedCharactersPerLine + CRLFBytes; // CRLF

		readonly byte[] _inBuf = new byte[BufSize];
		uint _inOffset;
		uint _inEnd;

		readonly byte[] _inLineBuf = new byte[DecodedBytesPerLine];
		readonly byte[] _lineBuf = new byte[EncodedBytesPerLine];
		uint _lineOffset;
		uint _lineEnd;
		const byte Padding = (byte) '=';
		const byte CR = (byte) '\r';
		const byte LF = (byte) '\n';

		public Base64Encoder(IInStream stream)
		{
			_stream = stream;
		}

		#region IInStream Members

		public uint ReadBytes(byte[] array, uint offset, uint length)
		{
			Debug.Assert(offset + length <= array.Length);

			uint end = offset + length;

			while (offset != end)
			{
				if (_lineOffset == _lineEnd)
				{
					if (!encodeLine())
						return length - (end - offset);

					Debug.Assert(_lineOffset == 0);
					Debug.Assert(_lineEnd != 0);
				}

				uint now = Math.Min(end - offset, _lineEnd - _lineOffset);
				Array.Copy(_lineBuf, _lineOffset, array, offset, now);

				_lineOffset += now;
				offset += now;
			}

			return length;
		}

		bool encodeLine()
		{
			Debug.Assert(_lineOffset == _lineEnd);
			uint decoded = extractLine();
			if (decoded == 0)
				return false;

			Debug.Assert(decoded <= DecodedBytesPerLine);

			uint trailing = decoded%3;
			
			uint target = 0;
			uint srcEnd = decoded - trailing;
			uint src = 0;

			while (src != srcEnd)
			{
				byte a = _inLineBuf[src++];
				byte b = _inLineBuf[src++];
				byte c = _inLineBuf[src++];

				_lineBuf[target++] = EncodeTable[a >> 2];
				_lineBuf[target++] = EncodeTable[(a << 4 | b >> 4) & 0x3f];
				_lineBuf[target++] = EncodeTable[(b << 2 | c >> 6) & 0x3f];
				_lineBuf[target++] = EncodeTable[c & 0x3f];
			}

			switch(trailing)
			{
				case 0:
					break;

				case 1:
					{
						byte a = _inLineBuf[src++];
						_lineBuf[target++] = EncodeTable[a >> 2];
						_lineBuf[target++] = EncodeTable[a << 4 & 0x3f];
						_lineBuf[target++] = Padding;
						_lineBuf[target++] = Padding;
					}
					break;

				case 2:
					{
						byte a = _inLineBuf[src++];
						byte b = _inLineBuf[src++];
						_lineBuf[target++] = EncodeTable[a >> 2];
						_lineBuf[target++] = EncodeTable[(a << 4 | b >> 4) & 0x3f];
						_lineBuf[target++] = EncodeTable[b << 2 & 0x3f];
						_lineBuf[target++] = Padding;
					}
					break;

				default:
					Debug.Assert(false, "internal error");
					break;
			}

			Debug.Assert(src == decoded);
			
			_lineBuf[target++] = CR;
			_lineBuf[target++] = LF;

			_lineOffset = 0;
			_lineEnd = target;

			return true;
		}

		uint extractLine()
		{
			uint end = 0;
			const uint length = DecodedBytesPerLine;

			while (end != DecodedBytesPerLine)
			{
				if (_inOffset == _inEnd)
				{
					if (!getMoreData())
						return end;
				}

				uint now = Math.Min(length - end, _inEnd - _inOffset);
				Array.Copy(_inBuf, _inOffset, _inLineBuf, end, now);
				_inOffset += now;
				end += now;
			}

			return end;
		}

		bool getMoreData()
		{
			Debug.Assert(_inOffset == _inEnd);
			
			_inEnd = _stream.ReadBytes(_inBuf, 0, (uint)_inBuf.Length);
			_inOffset = 0;

			return _inEnd != 0;
		}

		#endregion


		public static ulong computeEncodedLength(ulong inputSize)
		{
			ulong encodedCharacters = ((inputSize + 2)/3)*4;
			ulong lines = 
				(encodedCharacters + (EncodedCharactersPerLine - 1) ) /
				EncodedCharactersPerLine;

			return encodedCharacters + (lines*CRLFBytes);
		}

		static readonly byte[] EncodeTable =
			TextEncoding.ASCII.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/");
	}
}
