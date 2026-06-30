using System.Globalization;
using System.Resources;
using Spotster.Domain;
using Spotster.Resources;

namespace Spotster.Services.Localization;

public class PaymentMethodLabels
{
    private static readonly ResourceManager ResourceManager =
        new(typeof(SharedResources).FullName!, typeof(SharedResources).Assembly);

    public string Get(string code)
    {
        if (PaymentMethodCodes.TryGetOtherLabel(code, out var otherLabel))
        {
            return otherLabel;
        }

        if (PaymentMethodCodes.IsBareOther(code))
        {
            return GetResource("Payment_Other") ?? "Other";
        }

        var key = code switch
        {
            PaymentMethodCodes.Cash => "Payment_Cash",
            PaymentMethodCodes.Satispay => "Payment_Satispay",
            PaymentMethodCodes.Paypal => "Payment_Paypal",
            PaymentMethodCodes.BankTransfer => "Payment_BankTransfer",
            PaymentMethodCodes.Revolut => "Payment_Revolut",
            PaymentMethodCodes.Other => "Payment_Other",
            _ => null
        };

        if (key is null) return code;

        return GetResource(key) ?? code;
    }

    private static string? GetResource(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture);
}
