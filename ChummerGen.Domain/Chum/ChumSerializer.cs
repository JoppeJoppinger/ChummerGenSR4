using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ChummerGen.Domain.Chum;

public static class ChumSerializer
{
    public static ChumDocument LoadFile(string filePath, long maxCharacters = ChumDocument.DefaultMaxCharacters)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ChumFormatException("Der Pfad zur .chum-Datei darf nicht leer sein.");

        try
        {
            using var stream = File.OpenRead(filePath);
            return Load(stream, maxCharacters);
        }
        catch (ChumFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new ChumFormatException("Die .chum-Datei konnte nicht gelesen werden.", exception);
        }
    }

    public static ChumDocument Parse(string xml, long maxCharacters = ChumDocument.DefaultMaxCharacters)
    {
        if (xml is null)
            throw new ChumFormatException("Der XML-Inhalt darf nicht null sein.");

        try
        {
            using var textReader = new StringReader(xml);
            using var reader = XmlReader.Create(textReader, CreateReaderSettings(maxCharacters));
            return LoadDocument(reader);
        }
        catch (XmlException exception)
        {
            throw CreateXmlException(exception);
        }
    }

    public static ChumDocument Load(Stream stream, long maxCharacters = ChumDocument.DefaultMaxCharacters)
    {
        if (stream is null || !stream.CanRead)
            throw new ChumFormatException("Der .chum-Datenstrom kann nicht gelesen werden.");

        try
        {
            using var reader = XmlReader.Create(stream, CreateReaderSettings(maxCharacters));
            return LoadDocument(reader);
        }
        catch (XmlException exception)
        {
            throw CreateXmlException(exception);
        }
    }

    public static string Serialize(ChumDocument document)
    {
        using var stream = new MemoryStream();
        Write(document, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static void Write(ChumDocument document, Stream destination)
    {
        if (document is null)
            throw new ChumFormatException("Das .chum-Dokument darf nicht null sein.");
        if (destination is null || !destination.CanWrite)
            throw new ChumFormatException("Der Zieldatenstrom kann nicht beschrieben werden.");

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            NewLineHandling = NewLineHandling.None,
            OmitXmlDeclaration = document.Xml.Declaration is null,
            CloseOutput = false
        };
        using var writer = XmlWriter.Create(destination, settings);
        document.Xml.WriteTo(writer);
        writer.Flush();
    }

    private static XmlReaderSettings CreateReaderSettings(long maxCharacters)
    {
        if (maxCharacters <= 0)
            throw new ChumFormatException("Das Größenlimit muss größer als null sein.");

        return new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maxCharacters,
            CloseInput = false
        };
    }

    private static ChumDocument LoadDocument(XmlReader reader)
    {
        try
        {
            var xml = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            if (xml.Root?.Name != "character")
                throw new ChumFormatException("Das XML-Wurzelelement muss 'character' heißen.");

            return new ChumDocument(xml);
        }
        catch (ChumFormatException)
        {
            throw;
        }
    }

    private static ChumFormatException CreateXmlException(XmlException exception) =>
        new("Die .chum-Datei enthält ungültiges XML, eine verbotene DTD oder überschreitet das Größenlimit.", exception);
}
