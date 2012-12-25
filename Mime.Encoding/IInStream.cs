namespace Mime.Encoding
{
	public interface IInStream
	{
		uint ReadBytes(byte[] array, uint offset, uint length);
	}
}
