namespace ChummerGen.Infrastructure.Localization;

public class LocalizationException : Exception
{
    public LocalizationException(string message) : base(message)
    {
    }

    public LocalizationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class CultureNotSupportedException : LocalizationException
{
    public string Culture { get; }

    public CultureNotSupportedException(string culture)
        : base($"Culture '{culture}' is not supported or no matching language file was found.")
    {
        Culture = culture;
    }
}

public class LocalizationKeyNotFoundException : LocalizationException
{
    public string Key { get; }
    public string Culture { get; }

    public LocalizationKeyNotFoundException(string key, string culture)
        : base($"Localization key '{key}' was not found for culture '{culture}'.")
    {
        Key = key;
        Culture = culture;
    }
}
