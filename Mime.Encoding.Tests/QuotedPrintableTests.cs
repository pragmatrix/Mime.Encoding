using NUnit.Framework;
using TextEncoding = System.Text.Encoding;

namespace Mime.Encoding.Tests
{
	[TestFixture]
	public sealed class QuotedPrintableTests
	{
		[Test]
		public void test()
		{
			for (int i = 0; i != Tests.Length; i += 2)
			{
				var from = Tests[i];
				var to = Tests[i+1];

				var toRes = encode(from);
				Assert.AreEqual(to, toRes, (i/2).ToString());
				var fromRes = decode(toRes);
				Assert.AreEqual(from, fromRes, (i/2).ToString());
			}
		}

		static string encode(string str)
		{
			var inBytes = TextEncoding.UTF8.GetBytes(str);

			var encoder = new QuotedPrintableEncoder(inBytes.ToInStream());

			var encoded = encoder.ToArray();
			return TextEncoding.UTF8.GetString(encoded);
		}

		static string decode(string str)
		{
			var inBytes = TextEncoding.UTF8.GetBytes(str);

			var encoder = new QuotedPrintableDecoder(inBytes.ToInStream());

			var encoded = encoder.ToArray();
			return TextEncoding.UTF8.GetString(encoded);
		}

		static readonly string[] Tests = new string[]
		{
			"A\r\nB", "A\r\nB",
			// we treat isolated LF / CR as binary
			"A\nB", "A=0AB",
			"A\rB", "A=0DB"
		};
	}
}
