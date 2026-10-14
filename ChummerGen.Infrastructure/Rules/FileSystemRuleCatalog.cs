using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ChummerGen.Domain.Abstractions;
using ChummerGen.Domain.Models;
using Microsoft.Extensions.Caching.Memory;

namespace ChummerGen.Infrastructure.Rules;

public sealed class FileSystemRuleCatalog : IRuleCatalog
{
    private readonly string dataDirectory;
    private readonly string languageDirectory;
    private readonly IMemoryCache cache;

    public FileSystemRuleCatalog(string dataDirectory, string languageDirectory, IMemoryCache cache)
    {
        this.dataDirectory = Path.GetFullPath(dataDirectory);
        this.languageDirectory = Path.GetFullPath(languageDirectory);
        this.cache = cache;
    }

    public async Task<RuleCatalogDocument?> GetAsync(
        string catalogName,
        string culture,
        CancellationToken cancellationToken)
    {
        ValidateCatalogName(catalogName);
        var normalizedCulture = ValidateCulture(culture);
        var basePath = Path.Combine(dataDirectory, catalogName);
        if (!File.Exists(basePath))
        {
            throw new FileNotFoundException(
                $"Regelkatalog '{catalogName}' wurde im Datenverzeichnis '{dataDirectory}' nicht gefunden.",
                basePath);
        }

        var isImprovements = catalogName.Equals("improvements.xml", StringComparison.OrdinalIgnoreCase);
        var overrideFiles = isImprovements ? [] : ResolveMatchingFiles($"override*_{catalogName}");
        var languageFile = GetLanguageFile(normalizedCulture);
        var customFiles = isImprovements ? [] : ResolveMatchingFiles($"custom*_{catalogName}");
        var files = new List<string> { basePath };
        files.AddRange(overrideFiles);
        if (languageFile is not null)
        {
            files.Add(languageFile);
        }
        files.AddRange(customFiles);

        var contents = new List<(string Path, byte[] Bytes)>(files.Count);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            contents.Add((file, await File.ReadAllBytesAsync(file, cancellationToken)));
        }

        var version = ComputeVersion(contents);
        var key = $"rule-catalog:{dataDirectory}:{languageDirectory}:{catalogName}:{normalizedCulture}";
        if (cache.TryGetValue<CachedCatalog>(key, out var cached) && cached?.Version == version)
        {
            return new RuleCatalogDocument(catalogName, normalizedCulture, version, cached.Content);
        }

        var baseDocument = LoadXml(contents[0].Bytes, contents[0].Path);
        var index = 1;
        foreach (var file in overrideFiles)
        {
            ApplyOverrides(baseDocument, LoadXml(contents[index].Bytes, file));
            index++;
        }

        if (languageFile is not null)
        {
            ApplyTranslations(baseDocument, LoadXml(contents[index].Bytes, languageFile), catalogName);
            index++;
        }

        foreach (var file in customFiles)
        {
            ApplyCustomContent(baseDocument, LoadXml(contents[index].Bytes, file));
            index++;
        }

        var content = baseDocument.ToString(SaveOptions.DisableFormatting);
        cache.Set(key, new CachedCatalog(version, content), new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(12)
        });
        return new RuleCatalogDocument(catalogName, normalizedCulture, version, content);
    }

    private List<string> ResolveMatchingFiles(string pattern) =>
        Directory.Exists(dataDirectory)
            ? Directory.GetFiles(dataDirectory, pattern, SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .ToList()
            : [];

    private string? GetLanguageFile(string culture)
    {
        if (culture.Equals("en-US", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = Path.Combine(languageDirectory, $"{culture}_data.xml");
        return File.Exists(path) ? path : null;
    }

    private static void ApplyOverrides(XDocument document, XDocument overrides)
    {
        foreach (var sourceSection in overrides.Root?.Elements() ?? [])
        {
            var targetSection = document.Root?.Elements(sourceSection.Name).FirstOrDefault();
            if (targetSection is null)
            {
                continue;
            }

            foreach (var sourceItem in sourceSection.Elements().Where(item => item.Element("name") is not null))
            {
                var name = sourceItem.Element("name")!.Value;
                var targetItem = targetSection.Elements(sourceItem.Name)
                    .FirstOrDefault(item => item.Element("name")?.Value == name);
                if (targetItem is not null)
                {
                    targetItem.ReplaceNodes(sourceItem.Nodes().Select(CloneNode));
                }
            }
        }
    }

    private static void ApplyCustomContent(XDocument document, XDocument custom)
    {
        foreach (var sourceSection in custom.Root?.Elements() ?? [])
        {
            var section = new XElement(sourceSection.Name);
            foreach (var sourceItem in sourceSection.Elements())
            {
                var name = sourceItem.Element("name")?.Value;
                if (name is not null && document.Root?.Elements(sourceSection.Name)
                        .SelectMany(existingSection => existingSection.Elements(sourceItem.Name))
                        .Any(existingItem => existingItem.Element("name")?.Value == name) == true)
                {
                    continue;
                }

                section.Add(new XElement(sourceItem));
            }

            if (section.HasElements)
            {
                document.Root?.Add(section);
            }
        }
    }

    private static void ApplyTranslations(XDocument document, XDocument translations, string catalogName)
    {
        var translationBlocks = translations.Root?.Elements("chummer")
            .Where(element => string.Equals((string?)element.Attribute("file"), catalogName, StringComparison.OrdinalIgnoreCase))
            ?? [];

        foreach (var translationBlock in translationBlocks)
        {
            foreach (var sourceSection in translationBlock.Elements())
            {
                var targetSections = document.Root?.Elements(sourceSection.Name) ?? [];
                foreach (var sourceItem in sourceSection.Elements())
                {
                    var targetItem = targetSections.SelectMany(section => section.Elements(sourceItem.Name))
                        .FirstOrDefault(candidate => Matches(candidate, sourceItem));
                    if (targetItem is not null)
                    {
                        MergeTranslation(targetItem, sourceItem);
                    }
                }
            }
        }
    }

    private static bool Matches(XElement target, XElement source)
    {
        var sourceName = source.Element("name")?.Value;
        if (sourceName is not null)
        {
            return target.Element("name")?.Value == sourceName;
        }

        var identifyingAttributes = source.Attributes()
            .Where(attribute => attribute.Name != "translate")
            .ToArray();
        if (identifyingAttributes.Length > 0)
        {
            return identifyingAttributes.All(attribute =>
                       string.Equals((string?)target.Attribute(attribute.Name), attribute.Value, StringComparison.Ordinal)) &&
                   (source.HasElements || string.Equals(target.Value, source.Value, StringComparison.Ordinal));
        }

        if (!source.HasElements)
        {
            return string.Equals(target.Value, source.Value, StringComparison.Ordinal);
        }

        return true;
    }

    private static void MergeTranslation(XElement target, XElement source)
    {
        var translatedAttribute = source.Attribute("translate");
        if (translatedAttribute is not null)
        {
            target.SetAttributeValue("translate", translatedAttribute.Value);
        }

        foreach (var sourceChild in source.Elements())
        {
            var alternateName = sourceChild.Name.LocalName switch
            {
                "page" => "altpage",
                "code" => "altcode",
                "advantage" => "altadvantage",
                "disadvantage" => "altdisadvantage",
                _ => null
            };
            if (alternateName is not null)
            {
                SetChildValue(target, alternateName, sourceChild.Value);
                continue;
            }

            if (sourceChild.Name == "translate")
            {
                SetChildValue(target, "translate", sourceChild.Value);
                continue;
            }

            var targetChild = target.Elements(sourceChild.Name)
                .FirstOrDefault(candidate => Matches(candidate, sourceChild));
            if (targetChild is not null)
            {
                MergeTranslation(targetChild, sourceChild);
            }
        }
    }

    private static void SetChildValue(XElement parent, string name, string value)
    {
        var child = parent.Element(name);
        if (child is null)
        {
            parent.Add(new XElement(name, value));
        }
        else
        {
            child.Value = value;
        }
    }

    private static XNode CloneNode(XNode node) => node switch
    {
        XElement element => new XElement(element),
        XCData cdata => new XCData(cdata.Value),
        XText text => new XText(text.Value),
        XComment comment => new XComment(comment.Value),
        XProcessingInstruction instruction => new XProcessingInstruction(instruction.Target, instruction.Data),
        _ => new XText(node.ToString())
    };

    private static XDocument LoadXml(byte[] bytes, string path)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        try
        {
            return XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException exception)
        {
            throw new XmlException($"Die XML-Regeldatei '{path}' ist ungültig.", exception);
        }
    }

    private static string ComputeVersion(IEnumerable<(string Path, byte[] Bytes)> contents)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(long)];
        foreach (var (path, bytes) in contents)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetFileName(path)));
            hash.AppendData([0]);
            BinaryPrimitives.WriteInt64BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void ValidateCatalogName(string catalogName)
    {
        if (string.IsNullOrWhiteSpace(catalogName) ||
            catalogName.Contains('/') || catalogName.Contains('\\') ||
            !catalogName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
            catalogName.Length <= 4 ||
            catalogName[..^4].Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new ArgumentException(
                "Der Katalogname muss ein einfacher XML-Dateiname wie 'skills.xml' sein; Pfade sind nicht zulässig.",
                nameof(catalogName));
        }
    }

    private static string ValidateCulture(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture) || culture.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("Die Sprachkennung ist ungültig.", nameof(culture));
        }

        try
        {
            return System.Globalization.CultureInfo.GetCultureInfo(culture).Name;
        }
        catch (System.Globalization.CultureNotFoundException exception)
        {
            throw new ArgumentException("Die Sprachkennung ist ungültig.", nameof(culture), exception);
        }
    }

    private sealed record CachedCatalog(string Version, string Content);
}
