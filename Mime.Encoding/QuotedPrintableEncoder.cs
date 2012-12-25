using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Mime.Encoding
{
	public sealed class QuotedPrintableEncoder : IInStream
	{
		public const uint RecommendedBufferSize = 0x8000;

		readonly IInStream _stream;

		readonly IEnumerator<uint> _lines;

		readonly byte[] _buf;

		uint _offset;
		uint _end;

		readonly byte[] _line;

		uint _lineOffset;
		uint _lineEnd;

		const uint MaxEncodedCharactersPerLine = 76;
		const uint CRLFLength = 2;
		const uint EncodedBytesPerLine = 76 + CRLFLength;
		const byte CR = 13;
		const byte LF = 10;
		const byte Escape = (byte) '=';

		public QuotedPrintableEncoder(IInStream stream)
			: this(stream, RecommendedBufferSize)
		{
		}

		public QuotedPrintableEncoder(IInStream stream, uint internalBufSize)
		{
			Debug.Assert(internalBufSize != 0);

			_stream = stream;
			_line = new byte[EncodedBytesPerLine];
			_buf = new byte[internalBufSize];

			_lines = readLines().GetEnumerator();
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
					if (!_lines.MoveNext())
						return length - (end - offset);
					_lineOffset = 0;
					_lineEnd = _lines.Current;
					Debug.Assert(_lineEnd != 0);
				}

				uint now = Math.Min(end - offset, _lineEnd - _lineOffset);
				Array.Copy(_line, _lineOffset, array, offset, now);
				_lineOffset += now;
				offset += now;
			}

			return length;
		}
		
		enum Code : byte
		{
			// also used in token

			Literal,
			Encoded,
			Space,
			CRLF,

			// non-token use

			CR,
			LF
		}

		struct Token
		{
			public Token(Code code, byte value)
			{
				Code = code;
				Value = value;
			}

			public readonly Code Code;
			public readonly byte Value;

			public byte HexFirst
			{
				get { return HexTable[Value >> 4]; }
			}

			public byte HexSecond
			{
				get { return HexTable[Value & 0xf]; }
			}
		};

		IEnumerable<uint> readLines()
		{
			IEnumerator<Token> tokens = tokenize().GetEnumerator();
			const uint MaxChars = MaxEncodedCharactersPerLine;

			uint encoded = 0;

			var redo = false;
			var token = new Token();

			while (true)
			{
				if (!redo)
				{
					if (!tokens.MoveNext())
					{
						if (encoded != 0)
							yield return encoded;
						yield break;
					}

					token = tokens.Current;
				}
				else
					redo = false;

				switch (token.Code)
				{
					case Code.Encoded:
						if (encoded + 3 < MaxChars)
						{
							_line[encoded++] = Escape;
							_line[encoded++] = token.HexFirst;
							_line[encoded++] = token.HexSecond;
						}
						else
							goto softBreakAndRedo;
						break;

					case Code.Literal:
						if (encoded + 1 < MaxChars)
							_line[encoded++] = token.Value;
						else
							goto softBreakAndRedo;
						break;

					case Code.Space:
						if (encoded + 1 < MaxChars)
							_line[encoded++] = token.Value;
						else
							goto softBreakAndRedo;
						break;

					case Code.CRLF:
						// always can encode CRLFs, but we may need to encode a soft break first
						if (!previousWasSpace(encoded))
						{
							// encode verbatim!
							_line[encoded++] = CR;
							_line[encoded++] = LF;
							yield return encoded;
							encoded = 0;
						}
						else
							goto softBreakAndRedo;

						break;

					default:
						Debug.Assert(false, "internal error");
						break;
				}

				continue;

			softBreakAndRedo:
				// escape must fit
				Debug.Assert(encoded + 1 <= MaxEncodedCharactersPerLine);
				_line[encoded++] = Escape;
				_line[encoded++] = CR;
				_line[encoded++] = LF;
				yield return encoded;
				redo = true;
				encoded = 0;
			}
		}

		bool previousWasSpace(uint encoded)
		{
			return encoded != 0 && CodeTable[_line[encoded - 1]] == Code.Space;
		}

		IEnumerable<Token> tokenize()
		{
			bool seenCR = false;

			while (true)
			{
				if (_offset == _end)
					if (!readMoreData())
					{
						if (seenCR)
							yield return new Token(Code.Encoded, 13);

						yield break;
					}

				Debug.Assert(_offset != _end);

				byte c = _buf[_offset++];

				Code code = CodeTable[c];

				if (seenCR)
				{
					seenCR = false;

					if (code == Code.LF)
					{
						yield return new Token(Code.CRLF, 0);
						continue;
					}

					// flush CR encoded!
					yield return new Token(Code.Encoded, 13);
				}

				switch (code)
				{
					case Code.Literal:
					case Code.Encoded:
					case Code.Space:
						yield return new Token(code, c);
						break;

					case Code.CR:
						seenCR = true;
						break;

					case Code.LF:
						yield return new Token(Code.Encoded, 10);
						break;

					default:
						Debug.Assert(false);
						break;
				}
			}
		}


		bool readMoreData()
		{
			Debug.Assert(_offset == _end);
			_end = _stream.ReadBytes(_buf, 0, (uint)_buf.Length);
			// important to set _offset afterwards, so we
			// can detect this situation again if readBytes failed!
			_offset = 0;
			return _end != 0;
		}

		static readonly byte[] HexTable = makeHexTable().ToArray();
		static readonly Code[] CodeTable = makeCodeTable();

		static Code[] makeCodeTable()
		{
			var codes = new Code[256];
			for (int i = 0 ;i != 256; ++i)
				codes[i] = Code.Encoded;

			for (int i = 33 ;i != 127; ++i) // 33 - 127 are literally encoded (except '=', see below)
				codes[i] = Code.Literal;

			codes[61] = Code.Encoded; // '='

			codes[9] = Code.Space;
			codes[32] = Code.Space;

			codes[10] = Code.LF;
			codes[13] = Code.CR;

			return codes;
		}

		static IEnumerable<byte> makeHexTable()
		{
			return from c in "0123456789ABCDEF" select (byte) c;
		}

		#endregion
	}
}
