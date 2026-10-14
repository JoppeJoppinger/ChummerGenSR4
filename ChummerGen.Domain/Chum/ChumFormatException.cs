namespace ChummerGen.Domain.Chum;

public sealed class ChumFormatException : FormatException
{
    public ChumFormatException(string message) : base(message)
    {
    }

    public ChumFormatException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
