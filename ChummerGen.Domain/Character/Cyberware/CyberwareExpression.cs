using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

namespace ChummerGen.Domain.Character.Cyberware;

// Port of the expression handling in legacy Cyberware (Rating substitution, FixedValues(...), XPath arithmetic).
internal static class CyberwareExpression
{
    private static readonly Regex SafeArithmetic = new(@"^[0-9+\-*.() \t]+$", RegexOptions.Compiled);

    public static bool IsFixedValues(string expression) =>
        expression.StartsWith("FixedValues", StringComparison.Ordinal);

    public static string[] FixedValues(string expression) =>
        expression.Replace("FixedValues(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal)
            .Split(',', StringSplitOptions.TrimEntries);

    public static string SubstituteRating(string expression, int rating) =>
        expression.Replace("Rating", rating.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    public static decimal? EvaluateArithmetic(string expression)
    {
        var text = expression.Trim();
        if (text.Length == 0)
            return null;
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var plain))
            return plain;
        if (!SafeArithmetic.IsMatch(text))
            return null;

        try
        {
            var navigator = new XmlDocument().CreateNavigator()!;
            var result = navigator.Evaluate(XPathExpression.Compile(text));
            var number = Convert.ToDouble(result, CultureInfo.InvariantCulture);
            return double.IsFinite(number) ? Convert.ToDecimal(number, CultureInfo.InvariantCulture) : null;
        }
        catch (Exception exception) when (exception is XPathException or FormatException or OverflowException)
        {
            return null;
        }
    }

    // Evaluates plain numbers, Rating expressions and FixedValues; null when the expression cannot be evaluated.
    public static decimal? Evaluate(string expression, int rating)
    {
        var text = expression.Trim();
        if (text.Length == 0)
            return null;

        if (IsFixedValues(text))
        {
            var values = FixedValues(text);
            var index = rating - 1;
            if (index < 0 || index >= values.Length)
                return null;
            return EvaluateArithmetic(values[index]);
        }

        return EvaluateArithmetic(SubstituteRating(text, rating));
    }

    // Convert.ToInt32(double) in the legacy code rounds half to even.
    public static int ToInt(double value) => (int)Math.Round(value, MidpointRounding.ToEven);

    public static bool TryParseVariableCostRange(string expression, out int min, out int max)
    {
        min = 0;
        max = 0;
        var text = expression.Trim();
        if (!text.StartsWith("Variable(", StringComparison.Ordinal) || !text.EndsWith(')'))
            return false;

        var inner = text[9..^1].Trim();
        if (inner.Contains('-'))
        {
            var parts = inner.Split('-', StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                return false;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out min) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out max))
                return false;
        }
        else
        {
            var stripped = inner.Replace("+", string.Empty, StringComparison.Ordinal).Trim();
            if (!int.TryParse(stripped, NumberStyles.Integer, CultureInfo.InvariantCulture, out min))
                return false;
            max = 1000000;
        }

        if (min == 0 && max == 0)
            return false;
        if (max == 0)
            max = 1000000;
        return true;
    }
}