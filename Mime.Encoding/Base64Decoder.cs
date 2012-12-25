using System;
using System.Diagnostics;
using TextEncoding = System.Text.Encoding;

namespace Mime.Encoding
{
	/// RFC 2045

	sealed class Base64Decoder : IInStream
	{
		readonly IInStream _source;

		byte _buf;
		uint _state;

		const uint BufSize = 0x8000;

		readonly byte[] _inBuf = new byte[BufSize];
		uint _offset;
		uint _end;

		const uint EOF = 5;

		public Base64Decoder(IInStream source)
		{
			_source = source;
		}

		#region IInStream Members

		public uint ReadBytes(byte[] array, uint offset, uint length)
		{
			if (_state == EOF || length == 0)
				return 0;

			uint end = offset + length;
			Debug.Assert(end <= array.Length);

			while (offset != end)
			{
				if (_offset == _end)
					if (!readIn())
					{
						if (_state != EOF && _state != 0)
							throw new Exception("invalid end of file, base64 decoding state: " + _state);

						return length - (end-offset);
					}

				byte decoded = DecodeTable[_inBuf[_offset++]];
				if (decoded == Ignore)
					continue;

				if (decoded == Padding)
				{
					switch (_state)
					{
						case 0: ///< padding at the beginning is illegal
						case 1: ///< padding after six bits, too (partial bytes are not supported)
							throw new Exception(string.Format("invalid base64 decoding state {0} when received padding, data malformed", _state));

						case 2: ///< got already 12 bits, 8 of them are valid only, exit.
							_state = 4; // state 4: expect one more padding.
							break;

						case 3: ///< got already 18 bits, 16 of them are valid (and written), exit.
						case 4: ///< received padding, waited for second, end
							_state = EOF;
							return length - (end - offset);

						default:
							Debug.Assert(false);
							throw new Exception("internal error");
					}

					// may expect one more padding!
					continue;
				}

				Debug.Assert(decoded < 0x40);

				// decide what to do with the next byte
				switch (_state)
				{
					// 0 byte read, 0 bits in _buf
					case 0:
						_buf = decoded;
						++_state;
						break;

					// 1 byte read, 6 bits in _buf
					case 1:
						array[offset++] = (byte)(_buf << 2 | decoded >> 4);
						_buf = decoded;
						++_state;
						break;

					// 2 byte read, 4 bits in _buf.
					case 2:
						array[offset++] = (byte)(_buf << 4 | decoded >> 2);
						_buf = decoded;
						++_state;
						break;

					// 3 bytes read, 2 bits in _buf
					case 3:
						array[offset++] = (byte)(_buf << 6 | decoded);
						_state = 0;
						break;

					case 4:
						throw new Exception("seen base64 encoded bits after padding, error, expected '='");

					default:
						Debug.Assert(false);
						throw new Exception("internal error");
				}
			}

			return length;
		}

		bool readIn()
		{
			Debug.Assert(_offset == _end);

			_offset = 0;
			_end = _source.ReadBytes(_inBuf, 0, BufSize);
	
			return _end != 0;
		}

		#endregion

		static readonly byte[] EncodeTable = 
			TextEncoding.ASCII.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/");

		static readonly byte[] DecodeTable = makeDecodeTable();

		const byte Ignore = 0xff;
		const byte Padding = 0x40;

		static byte[] makeDecodeTable()
		{
			var table = new byte[0x100];

			for (int i = 0; i != table.Length; ++i)
				table[i] = Ignore;

			for (int i = 0; i != EncodeTable.Length; ++i)
				table[EncodeTable[i]] = (byte)i;

			table['='] = Padding;

			return table;
		}
	}
}
