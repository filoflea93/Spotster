namespace Spotster.Domain;

public static class PaymentMethodCodes
{
    public const string Cash = "cash";
    public const string Satispay = "satispay";
    public const string Paypal = "paypal";
    public const string BankTransfer = "bank_transfer";
    public const string Revolut = "revolut";
    public const string Other = "other";
    public const string OtherPrefix = "other:";

    public const int MaxOtherLabelLength = 80;

    public static readonly IReadOnlyList<string> All =
    [
        Cash, Satispay, Paypal, BankTransfer, Revolut, Other
    ];

    public static string GetLabel(string code)
    {
        if (TryGetOtherLabel(code, out var otherLabel))
        {
            return otherLabel;
        }

        return code switch
        {
            Cash => "Cash",
            Satispay => "Satispay",
            Paypal => "PayPal",
            BankTransfer => "Bank transfer",
            Revolut => "Revolut",
            Other => "Other",
            _ => code
        };
    }

    public static IReadOnlyList<string> Normalize(IEnumerable<string>? methods)
    {
        if (methods is null)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var method in methods.Where(m => !string.IsNullOrWhiteSpace(m)))
        {
            var trimmed = method.Trim();
            if (trimmed.StartsWith(OtherPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var label = trimmed[OtherPrefix.Length..].Trim();
                if (label.Length > 0 && label.Length <= MaxOtherLabelLength)
                {
                    result.Add(OtherPrefix + label);
                }
            }
            else
            {
                var lower = trimmed.ToLowerInvariant();
                if (All.Contains(lower))
                {
                    result.Add(lower);
                }
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool TryGetOtherLabel(string code, out string label)
    {
        if (code.StartsWith(OtherPrefix, StringComparison.OrdinalIgnoreCase))
        {
            label = code[OtherPrefix.Length..].Trim();
            return label.Length > 0;
        }

        label = string.Empty;
        return false;
    }

    public static bool IsBareOther(string code) =>
        code.Equals(Other, StringComparison.OrdinalIgnoreCase);
}
