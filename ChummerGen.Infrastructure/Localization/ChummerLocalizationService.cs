using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;

namespace ChummerGen.Infrastructure.Localization;

public sealed class ChummerLocalizationService : IChummerLocalizationService
{
    private static readonly Dictionary<string, string> AttributeShortCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BOD"] = "String_AttributeBODShort",
        ["AGI"] = "String_AttributeAGIShort",
        ["REA"] = "String_AttributeREAShort",
        ["STR"] = "String_AttributeSTRShort",
        ["CHA"] = "String_AttributeCHAShort",
        ["INT"] = "String_AttributeINTShort",
        ["LOG"] = "String_AttributeLOGShort",
        ["WIL"] = "String_AttributeWILShort",
        ["EDG"] = "String_AttributeEDGShort",
        ["MAG"] = "String_AttributeMAGShort",
        ["RES"] = "String_AttributeRESShort"
    };

    private readonly string _languageDirectory;
    private readonly IMemoryCache _cache;
    private readonly Lazy<Dictionary<string, string>> _cultureFileMap;

    public ChummerLocalizationService(string languageDirectory, IMemoryCache cache)
    {
        _languageDirectory = Path.GetFullPath(languageDirectory);
        _cache = cache;
        _cultureFileMap = new Lazy<Dictionary<string, string>>(DiscoverCultures, LazyThreadSafetyMode.PublicationOnly);
    }

    public IReadOnlyList<string> GetSupportedCultures()
    {
        return _cultureFileMap.Value.Keys.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool IsCultureSupported(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return false;

        return TryNormalizeCulture(culture, out _);
    }

    public string GetVersion(string culture)
    {
        var entry = GetOrLoadBundle(culture);
        return entry.Version;
    }

    public bool TryGetString(string key, string culture, out string? value)
    {
        if (string.IsNullOrEmpty(key))
        {
            value = null;
            return false;
        }

        var entry = GetOrLoadBundle(culture);
        return entry.Strings.TryGetValue(key, out value);
    }

    public string GetRequiredString(string key, string culture)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));

        if (TryGetString(key, culture, out var value) && value is not null)
            return value;

        throw new LocalizationKeyNotFoundException(key, culture);
    }

    public bool TryTranslateData(string originalText, string culture, out string? translatedText)
    {
        if (string.IsNullOrWhiteSpace(originalText))
        {
            translatedText = null;
            return false;
        }

        var trimmed = originalText.Trim();
        var normalizedCulture = ResolveCultureCode(culture);

        // English baseline: data files are originally English
        if (normalizedCulture.Equals("en-us", StringComparison.OrdinalIgnoreCase))
        {
            translatedText = trimmed;
            return true;
        }

        var entry = GetOrLoadBundle(normalizedCulture);

        // 1. Direct match in data translations
        if (entry.DataTranslations.TryGetValue(trimmed, out translatedText))
            return true;

        // 2. Attribute short code translation
        if (AttributeShortCodes.TryGetValue(trimmed, out var attrKey) &&
            entry.Strings.TryGetValue(attrKey, out var localizedAttr) &&
            !string.IsNullOrEmpty(localizedAttr))
        {
            translatedText = localizedAttr;
            return true;
        }

        translatedText = null;
        return false;
    }

    public string TranslateData(string originalText, string culture)
    {
        if (string.IsNullOrWhiteSpace(originalText))
            return originalText;

        if (TryTranslateData(originalText, culture, out var translated) && translated is not null)
            return translated;

        return originalText;
    }

    private LocalizationBundle GetOrLoadBundle(string culture)
    {
        var normalized = ResolveCultureCode(culture);
        var cacheKey = $"chummer-loc:{_languageDirectory}:{normalized}";

        var files = GetFilesForCulture(normalized);
        var contents = new List<(string Path, byte[] Bytes)>(files.Count);
        foreach (var file in files)
        {
            contents.Add((file, File.ReadAllBytes(file)));
        }

        var currentVersion = ComputeVersion(contents);

        if (_cache.TryGetValue<LocalizationBundle>(cacheKey, out var cached) &&
            cached is not null &&
            cached.Version == currentVersion)
        {
            return cached;
        }

        var strings = LoadUiStrings(contents[0].Bytes, contents[0].Path);
        var dataTranslations = contents.Count > 1
            ? LoadDataTranslations(contents[1].Bytes, contents[1].Path)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        var bundle = new LocalizationBundle(normalized, currentVersion, strings, dataTranslations);

        _cache.Set(cacheKey, bundle, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(12)
        });

        return bundle;
    }

    private string ResolveCultureCode(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            throw new ArgumentException("Culture cannot be null or empty.", nameof(culture));

        ValidateCultureFormat(culture);

        if (!TryNormalizeCulture(culture, out var normalized))
            throw new CultureNotSupportedException(culture);

        return normalized;
    }

    private bool TryNormalizeCulture(string culture, out string normalized)
    {
        var trimmed = culture.Trim();
        if (_cultureFileMap.Value.TryGetValue(trimmed, out var mapped))
        {
            normalized = mapped;
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    private List<string> GetFilesForCulture(string normalizedCulture)
    {
        var uiFile = Path.Combine(_languageDirectory, $"{normalizedCulture}.xml");
        ValidatePathWithinDirectory(uiFile);

        if (!File.Exists(uiFile))
            throw new FileNotFoundException($"Language UI file '{uiFile}' was not found.", uiFile);

        var list = new List<string> { uiFile };

        var dataFile = Path.Combine(_languageDirectory, $"{normalizedCulture}_data.xml");
        ValidatePathWithinDirectory(dataFile);
        if (File.Exists(dataFile))
        {
            list.Add(dataFile);
        }

        return list;
    }

    private void ValidatePathWithinDirectory(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!fullPath.StartsWith(_languageDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Path traversal is not permitted.", nameof(filePath));
        }
    }

    private static void ValidateCultureFormat(string culture)
    {
        if (culture.Contains('/') || culture.Contains('\\') || culture.Contains(".."))
            throw new ArgumentException("Invalid characters or path traversal attempt in culture.", nameof(culture));

        foreach (var c in culture)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')
                throw new ArgumentException($"Invalid character '{c}' in culture name.", nameof(culture));
        }
    }

    private Dictionary<string, string> DiscoverCultures()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(_languageDirectory))
            return map;

        var xmlFiles = Directory.GetFiles(_languageDirectory, "*.xml", SearchOption.TopDirectoryOnly);
        foreach (var filePath in xmlFiles)
        {
            var fileName = Path.GetFileName(filePath);
            if (fileName.EndsWith("_data.xml", StringComparison.OrdinalIgnoreCase))
                continue;

            var code = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();

            map[code] = code;

            if (code == "en-us")
            {
                map["en"] = "en-us";
                map["en-US"] = "en-us";
            }
            else if (code == "de")
            {
                map["de-DE"] = "de";
                map["de-de"] = "de";
            }
            else if (code == "fr")
            {
                map["fr-FR"] = "fr";
                map["fr-fr"] = "fr";
            }
            else if (code == "jp")
            {
                map["ja"] = "jp";
                map["ja-JP"] = "jp";
                map["jp-JP"] = "jp";
                map["ja-jp"] = "jp";
            }
        }

        return map;
    }

    private static Dictionary<string, string> LoadUiStrings(byte[] bytes, string filePath)
    {
        var doc = LoadXml(bytes, filePath);
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);

        var stringNodes = doc.Root?.Element("strings")?.Elements("string") ?? Enumerable.Empty<XElement>();
        foreach (var node in stringNodes)
        {
            var key = node.Element("key")?.Value;
            var text = node.Element("text")?.Value ?? string.Empty;

            if (string.IsNullOrEmpty(key))
                continue;

            var unescaped = text.Replace("\\n", "\n");
            dict[key] = unescaped;
        }

        return dict;
    }

    private static Dictionary<string, string> LoadDataTranslations(byte[] bytes, string filePath)
    {
        var doc = LoadXml(bytes, filePath);
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);

        var fileSections = doc.Root?.Elements("chummer") ?? Enumerable.Empty<XElement>();
        foreach (var section in fileSections)
        {
            ParseDataSection(section, dict);
        }

        return dict;
    }

    private static void ParseDataSection(XElement section, Dictionary<string, string> dict)
    {
        foreach (var container in section.Elements())
        {
            foreach (var item in container.Elements())
            {
                // 1. Check for 'translate' attribute (e.g. <category translate="Klingenwaffen">Blades</category>)
                var translateAttr = item.Attribute("translate")?.Value;
                if (!string.IsNullOrEmpty(translateAttr))
                {
                    var original = item.Value.Trim();
                    if (!string.IsNullOrEmpty(original) && !dict.ContainsKey(original))
                    {
                        dict[original] = translateAttr;
                    }
                }

                // 2. Check for child <translate> and <name> (e.g. <weapon><name>Katana</name><translate>Katana</translate></weapon>)
                var nameEl = item.Element("name");
                var translateEl = item.Element("translate");
                if (nameEl != null && translateEl != null)
                {
                    var originalName = nameEl.Value.Trim();
                    var transName = translateEl.Value.Trim();
                    if (!string.IsNullOrEmpty(originalName) && !string.IsNullOrEmpty(transName) && !dict.ContainsKey(originalName))
                    {
                        dict[originalName] = transName;
                    }
                }

                // 3. Check for nested collections:
                // specs/spec
                var specs = item.Element("specs");
                if (specs != null)
                {
                    foreach (var spec in specs.Elements("spec"))
                    {
                        var specTrans = spec.Attribute("translate")?.Value;
                        if (!string.IsNullOrEmpty(specTrans))
                        {
                            var originalSpec = spec.Value.Trim();
                            if (!string.IsNullOrEmpty(originalSpec) && !dict.ContainsKey(originalSpec))
                            {
                                dict[originalSpec] = specTrans;
                            }
                        }
                    }
                }

                // metavariants/metavariant
                var metavariants = item.Element("metavariants");
                if (metavariants != null)
                {
                    foreach (var mv in metavariants.Elements("metavariant"))
                    {
                        var mvName = mv.Element("name")?.Value.Trim();
                        var mvTrans = mv.Element("translate")?.Value.Trim();
                        if (!string.IsNullOrEmpty(mvName) && !string.IsNullOrEmpty(mvTrans) && !dict.ContainsKey(mvName))
                        {
                            dict[mvName] = mvTrans;
                        }
                    }
                }

                // advantages/advantage
                var advantages = item.Element("advantages");
                if (advantages != null)
                {
                    foreach (var adv in advantages.Elements("advantage"))
                    {
                        var advTrans = adv.Attribute("translate")?.Value;
                        if (!string.IsNullOrEmpty(advTrans))
                        {
                            var originalAdv = adv.Value.Trim();
                            if (!string.IsNullOrEmpty(originalAdv) && !dict.ContainsKey(originalAdv))
                            {
                                dict[originalAdv] = advTrans;
                            }
                        }
                    }
                }

                // choices/choice
                var choices = item.Element("choices");
                if (choices != null)
                {
                    foreach (var ch in choices.Elements("choice"))
                    {
                        var chName = ch.Element("name")?.Value.Trim();
                        var chTrans = ch.Element("translate")?.Value.Trim();
                        if (!string.IsNullOrEmpty(chName) && !string.IsNullOrEmpty(chTrans) && !dict.ContainsKey(chName))
                        {
                            dict[chName] = chTrans;
                        }
                    }
                }
            }
        }
    }

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
        catch (XmlException ex)
        {
            throw new XmlException($"XML file '{path}' is invalid.", ex);
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

    private sealed class LocalizationBundle
    {
        public string Culture { get; }
        public string Version { get; }
        public IReadOnlyDictionary<string, string> Strings { get; }
        public IReadOnlyDictionary<string, string> DataTranslations { get; }

        public LocalizationBundle(
            string culture,
            string version,
            IReadOnlyDictionary<string, string> strings,
            IReadOnlyDictionary<string, string> dataTranslations)
        {
            Culture = culture;
            Version = version;
            Strings = strings;
            DataTranslations = dataTranslations;
        }
    }
}
