using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace ChummerGen.Domain.Character.Gear;

public sealed class GearItem
{
    private static readonly Regex CleanRegex = new(@"[^0-9+\-*\/.\(\)]", RegexOptions.Compiled);

    public GearItem(XElement element, GearItem? parent = null)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
        Parent = parent;
        LoadChildren();
    }

    public XElement Element { get; }
    public GearItem? Parent { get; }
    public List<GearItem> Children { get; } = [];

    public string Guid => GetText("guid");
    public string Name { get => GetText("name"); set => SetText("name", value); }
    public string Category { get => GetText("category"); set => SetText("category", value); }
    public int Rating { get => GetInt("rating"); set => SetInt("rating", value); }
    public int MinRating => GetInt("minrating");
    public int MaxRating => GetInt("maxrating");
    public int Quantity { get => Math.Max(1, GetInt("qty", 1)); set => SetInt("qty", Math.Max(1, value)); }
    public int CostFor => Math.Max(1, GetInt("costfor", 1));
    public string CostExpression { get => GetText("cost"); set => SetText("cost", value); }
    public string Cost3 => GetText("cost3");
    public string Cost6 => GetText("cost6");
    public string Cost10 => GetText("cost10");
    public string AvailExpression { get => GetText("avail"); set => SetText("avail", value); }
    public string Avail3 => GetText("avail3");
    public string Avail6 => GetText("avail6");
    public string Avail10 => GetText("avail10");
    public string CapacityExpression => GetText("capacity");
    public string ArmorCapacityExpression => GetText("armorcapacity");
    public string Location { get => GetText("location"); set => SetText("location", value); }
    public string Notes { get => GetText("notes"); set => SetText("notes", value); }
    public string Extra => GetText("extra");
    public string GearName => GetText("gearname");
    public string Source => GetText("source");
    public string Page => GetText("page");
    public bool Equipped { get => GetBool("equipped", true); set => SetBool("equipped", value); }
    public bool DiscountCost { get => GetBool("discountedcost", false); set => SetBool("discountedcost", value); }

    public int Response => GetInt("response");
    public int Signal => GetInt("signal");
    public int Firewall => GetInt("firewall");
    public int System => GetInt("system");

    public string DisplayNameShort => string.IsNullOrWhiteSpace(Name) ? "Unbekannt" : Name;

    public string DisplayName
    {
        get
        {
            var text = DisplayNameShort;
            if (Quantity > 1)
                text = $"{Quantity} {text}";
            if (Rating > 0)
                text += $" (Stufe {Rating})";
            if (!string.IsNullOrWhiteSpace(Extra))
                text += $" ({Extra})";
            if (!string.IsNullOrWhiteSpace(GearName))
                text += $" (\"{GearName}\")";
            return text;
        }
    }

    public int TotalCost
    {
        get
        {
            var unitCost = CalculateUnitCost();
            if (DiscountCost)
                unitCost = (int)Math.Floor(unitCost * 0.9);

            var childrenCost = Children.Sum(c => c.TotalCost);
            var itemTotal = (unitCost * Quantity) / CostFor;
            return itemTotal + (childrenCost * Quantity);
        }
    }

    public int CalculateUnitCost()
    {
        var expr = GetCostExpressionForRating();
        if (string.IsNullOrWhiteSpace(expr))
            return 0;

        if (expr.Contains("Gear Cost", StringComparison.OrdinalIgnoreCase))
        {
            if (Parent is null)
                return 0;
            var parentCost = Parent.CalculateUnitCost();
            expr = Regex.Replace(expr, "Gear Cost", parentCost.ToString(CultureInfo.InvariantCulture), RegexOptions.IgnoreCase);
            return EvaluateMathExpression(expr);
        }

        if (expr.Contains("Rating", StringComparison.OrdinalIgnoreCase) || expr.Contains('*') || expr.Contains('+') || expr.Contains('-') || expr.Contains('/'))
        {
            expr = Regex.Replace(expr, "Rating", Rating.ToString(CultureInfo.InvariantCulture), RegexOptions.IgnoreCase);
            return EvaluateMathExpression(expr);
        }

        return int.TryParse(expr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    public int CalculateAvailValue()
    {
        var expr = GetAvailExpressionForRating();
        if (string.IsNullOrWhiteSpace(expr))
            return 0;

        expr = expr.Trim();
        if (expr.StartsWith('+'))
            expr = expr[1..];

        if (expr.EndsWith('F') || expr.EndsWith('R') || expr.EndsWith('V') || expr.EndsWith('E'))
            expr = expr[..^1];

        if (expr.Contains("Rating", StringComparison.OrdinalIgnoreCase) || expr.Contains('*') || expr.Contains('+') || expr.Contains('-') || expr.Contains('/'))
        {
            expr = Regex.Replace(expr, "Rating", Rating.ToString(CultureInfo.InvariantCulture), RegexOptions.IgnoreCase);
            return EvaluateMathExpression(expr);
        }

        return int.TryParse(expr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    public string TotalAvail
    {
        get
        {
            var expr = GetAvailExpressionForRating();
            if (string.IsNullOrWhiteSpace(expr))
                return "0";

            var suffix = "";
            var trimmed = expr.Trim();
            if (trimmed.EndsWith('F') || trimmed.EndsWith('R') || trimmed.EndsWith('V') || trimmed.EndsWith('E'))
                suffix = trimmed[^1].ToString();

            var val = CalculateAvailValue();
            return val.ToString(CultureInfo.InvariantCulture) + suffix;
        }
    }

    public int CapacityRemaining
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CapacityExpression))
                return 0;

            if (CapacityExpression.Contains('[') && !CapacityExpression.Contains("/["))
                return 0;

            var baseCapStr = CapacityExpression;
            if (baseCapStr.Contains("/["))
            {
                var idx = baseCapStr.IndexOf("/[", StringComparison.Ordinal);
                baseCapStr = baseCapStr[..idx];
            }

            if (baseCapStr.Contains("Rating", StringComparison.OrdinalIgnoreCase))
            {
                baseCapStr = Regex.Replace(baseCapStr, "Rating", Rating.ToString(CultureInfo.InvariantCulture), RegexOptions.IgnoreCase);
                baseCapStr = EvaluateMathExpression(baseCapStr).ToString(CultureInfo.InvariantCulture);
            }

            if (!int.TryParse(baseCapStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var capacity))
                return 0;

            foreach (var child in Children)
            {
                var childCap = child.GetPluginCapacity();
                capacity -= (childCap * child.Quantity);
            }

            return capacity;
        }
    }

    public int GetPluginCapacity()
    {
        var cap = CapacityExpression;
        if (string.IsNullOrWhiteSpace(cap))
            return 0;

        if (cap.Contains("/["))
        {
            var idx = cap.IndexOf("/[", StringComparison.Ordinal);
            cap = cap[(idx + 1)..];
        }

        if (cap.Contains('[') && cap.Contains(']'))
        {
            var start = cap.IndexOf('[') + 1;
            var end = cap.IndexOf(']');
            if (end > start)
            {
                var inner = cap[start..end];
                if (inner.Contains("Rating", StringComparison.OrdinalIgnoreCase))
                {
                    inner = Regex.Replace(inner, "Rating", Rating.ToString(CultureInfo.InvariantCulture), RegexOptions.IgnoreCase);
                    return EvaluateMathExpression(inner);
                }
                return int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : 0;
            }
        }

        return 0;
    }

    private string GetCostExpressionForRating()
    {
        if (!string.IsNullOrWhiteSpace(CostExpression))
            return CostExpression;
        if (Rating <= 3 && !string.IsNullOrWhiteSpace(Cost3))
            return Cost3;
        if (Rating <= 6 && !string.IsNullOrWhiteSpace(Cost6))
            return Cost6;
        if (!string.IsNullOrWhiteSpace(Cost10))
            return Cost10;
        return string.Empty;
    }

    private string GetAvailExpressionForRating()
    {
        if (Rating <= 3 && !string.IsNullOrWhiteSpace(Avail3))
            return Avail3;
        if (Rating <= 6 && !string.IsNullOrWhiteSpace(Avail6))
            return Avail6;
        if (Rating >= 7 && !string.IsNullOrWhiteSpace(Avail10))
            return Avail10;
        return AvailExpression;
    }

    private static int EvaluateMathExpression(string expression)
    {
        try
        {
            var sanitized = CleanRegex.Replace(expression, "").Trim();
            if (string.IsNullOrWhiteSpace(sanitized))
                return 0;

            var doc = new global::System.Xml.XmlDocument();
            var nav = doc.CreateNavigator();
            var result = nav?.Evaluate(global::System.Xml.XPath.XPathExpression.Compile(sanitized));
            if (result is null)
                return 0;

            var dbl = Convert.ToDouble(result, CultureInfo.InvariantCulture);
            return (int)Math.Ceiling(dbl);
        }
        catch
        {
            return 0;
        }
    }

    private void LoadChildren()
    {
        Children.Clear();
        var childrenNode = Element.Element("children");
        if (childrenNode is null)
            return;

        foreach (var childEl in childrenNode.Elements("gear"))
        {
            Children.Add(new GearItem(childEl, this));
        }
    }

    public string GetText(string elementName) => Element.Element(elementName)?.Value ?? string.Empty;

    public void SetText(string elementName, string value)
    {
        var node = Element.Element(elementName);
        if (node is null)
            Element.Add(new XElement(elementName, value));
        else
            node.Value = value;
    }

    public int GetInt(string elementName, int defaultValue = 0)
    {
        var val = Element.Element(elementName)?.Value;
        return int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
    }

    public void SetInt(string elementName, int value) => SetText(elementName, value.ToString(CultureInfo.InvariantCulture));

    public bool GetBool(string elementName, bool defaultValue = false)
    {
        var val = Element.Element(elementName)?.Value;
        return bool.TryParse(val, out var result) ? result : defaultValue;
    }

    public void SetBool(string elementName, bool value) => SetText(elementName, value ? "True" : "False");
}