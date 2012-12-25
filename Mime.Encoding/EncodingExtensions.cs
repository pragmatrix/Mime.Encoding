namespace Mime.Encoding
{
	public static class EncodingExtensions
	{
		public static byte[] Decode(this byte[] bytes, EncodingType encoding)
		{
			return bytes.ToInStream().Decode(encoding).ToArray();
		}

		public static byte[] Encode(this byte[] bytes, EncodingType encoding)
		{
			return bytes.ToInStream().Encode(encoding).ToArray();
		}

		#region IInStream

		public static IInStream Decode(this IInStream input, EncodingType encoding)
		{
			switch (encoding)
			{
				case EncodingType.Base64:
					return new Base64Decoder(input);

				case EncodingType.QuotedPrintable:
					return new QuotedPrintableDecoder(input);

				default:
					return input;
			}
		}

		public static IInStream Encode(this IInStream input, EncodingType encoding)
		{
			switch (encoding)
			{
				case EncodingType.Base64:
					return new Base64Encoder(input);

				case EncodingType.QuotedPrintable:
					return new QuotedPrintableEncoder(input);

				default:
					return input;
			}
		}

		#endregion
	}
}
