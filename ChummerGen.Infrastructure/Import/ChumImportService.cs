using System.Text;
using System.Text.RegularExpressions;
using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Chum;
using ChummerGen.Domain.Models;

namespace ChummerGen.Infrastructure.Import;

public sealed class ChumImportService(ICharacterStore characterStore, ICurrentUser currentUser)
{
    public const long MaximumFileSizeBytes = 10 * 1024 * 1024;
    public const string MaximumSizeMessage = "Die Datei ist zu groß. Erlaubt sind höchstens 10 MB.";

    public async Task<ChumImportPreview> PreviewAsync(
        string? fileName,
        Stream upload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(Path.GetExtension(fileName), ".chum", StringComparison.OrdinalIgnoreCase))
            throw new ChumImportException("Bitte wählen Sie eine Datei mit der Endung .chum aus.");
        if (upload is null || !upload.CanRead)
            throw new ChumImportException("Die ausgewählte Datei kann nicht gelesen werden.");

        var bytes = await ReadBoundedAsync(upload, cancellationToken);
        if (bytes.Length == 0)
            throw new ChumImportException("Die ausgewählte Datei ist leer.");

        var xml = DecodeXml(bytes);
        ChumDocument document;
        try
        {
            document = ChumSerializer.Parse(xml, MaximumFileSizeBytes);
        }
        catch (ChumFormatException exception)
        {
            throw new ChumImportException(exception.Message, exception);
        }

        if (string.IsNullOrWhiteSpace(document.Name) && string.IsNullOrWhiteSpace(document.Alias))
            throw new ChumImportException("Die .chum-Datei enthält weder einen Charakternamen noch einen Alias.");

        return new ChumImportPreview(
            document.Name,
            document.Alias,
            document.Metatype,
            document.Version,
            document.Karma,
            document.Nuyen,
            document.BuildPoints,
            string.IsNullOrWhiteSpace(document.Alias) ? document.Name! : document.Alias,
            xml);
    }

    public static byte[] EncodeForDownload(string xml)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var header = Regex.Match(xml, "<\\?xml[^>]*encoding\\s*=\\s*['\"](?<encoding>[^'\"]+)", RegexOptions.IgnoreCase);
        var encoding = header.Success
            ? Encoding.GetEncoding(header.Groups["encoding"].Value, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
            : new UTF8Encoding(false, true);
        return encoding.GetBytes(xml);
    }

    public async Task<CharacterArchive> SaveAsync(
        ChumImportPreview preview,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var ownerId = await currentUser.GetUserIdAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ChumImportException("Zum Import müssen Sie angemeldet sein.");

        return await characterStore.SaveAsync(
            new CharacterArchive(Guid.NewGuid(), ownerId, preview.CharacterName, preview.OriginalXml, 0, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream upload, CancellationToken cancellationToken)
    {
        using var content = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            int read;
            try
            {
                read = await upload.ReadAsync(buffer, cancellationToken);
            }
            catch (IOException exception)
            {
                throw new ChumImportException("Die ausgewählte Datei konnte nicht vollständig gelesen werden.", exception);
            }

            if (read == 0)
                break;
            if (content.Length + read > MaximumFileSizeBytes)
                throw new ChumImportException(MaximumSizeMessage);
            await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return content.ToArray();
    }

    private static string DecodeXml(byte[] bytes)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var offset = 0;
        Encoding encoding;
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE, 0x00, 0x00 }))
        {
            encoding = new UTF32Encoding(false, false, true);
            offset = 4;
        }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0x00, 0x00, 0xFE, 0xFF }))
        {
            encoding = new UTF32Encoding(true, false, true);
            offset = 4;
        }
        else if (bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble))
        {
            encoding = new UTF8Encoding(false, true);
            offset = Encoding.UTF8.Preamble.Length;
        }
        else if (bytes.AsSpan().StartsWith(Encoding.Unicode.Preamble))
        {
            encoding = new UnicodeEncoding(false, false, true);
            offset = Encoding.Unicode.Preamble.Length;
        }
        else if (bytes.AsSpan().StartsWith(Encoding.BigEndianUnicode.Preamble))
        {
            encoding = new UnicodeEncoding(true, false, true);
            offset = Encoding.BigEndianUnicode.Preamble.Length;
        }
        else if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0 && bytes[3] == '<')
        {
            encoding = new UTF32Encoding(true, false, true);
        }
        else if (bytes.Length >= 4 && bytes[0] == '<' && bytes[1] == 0 && bytes[2] == 0 && bytes[3] == 0)
        {
            encoding = new UTF32Encoding(false, false, true);
        }
        else if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == '<' && bytes[2] == 0)
        {
            encoding = new UnicodeEncoding(true, false, true);
        }
        else if (bytes.Length >= 4 && bytes[0] == '<' && bytes[1] == 0 && bytes[2] == '?')
        {
            encoding = new UnicodeEncoding(false, false, true);
        }
        else
        {
            var header = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 512));
            var match = Regex.Match(header, "<\\?xml[^>]*encoding\\s*=\\s*['\\\"](?<encoding>[^'\\\"]+)", RegexOptions.IgnoreCase);
            try
            {
                encoding = match.Success
                    ? Encoding.GetEncoding(match.Groups["encoding"].Value, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
                    : new UTF8Encoding(false, true);
            }
            catch (ArgumentException exception)
            {
                throw new ChumImportException("Die Zeichencodierung der .chum-Datei wird nicht unterstützt.", exception);
            }
        }

        try
        {
            return encoding.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException exception)
        {
            throw new ChumImportException("Die .chum-Datei enthält ungültige Zeichen für ihre angegebene Zeichencodierung.", exception);
        }
    }
}

public sealed class ChumImportPreview
{
    internal ChumImportPreview(
        string? name,
        string? alias,
        string? metatype,
        string? version,
        int? karma,
        int? nuyen,
        int? buildPoints,
        string characterName,
        string originalXml)
    {
        Name = name;
        Alias = alias;
        Metatype = metatype;
        Version = version;
        Karma = karma;
        Nuyen = nuyen;
        BuildPoints = buildPoints;
        CharacterName = characterName;
        OriginalXml = originalXml;
    }

    public string? Name { get; }
    public string? Alias { get; }
    public string? Metatype { get; }
    public string? Version { get; }
    public int? Karma { get; }
    public int? Nuyen { get; }
    public int? BuildPoints { get; }
    internal string CharacterName { get; }
    internal string OriginalXml { get; }
}

public sealed class ChumImportException(string message, Exception? innerException = null)
    : Exception(message, innerException);
