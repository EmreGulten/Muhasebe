namespace Accounting.Application.Common;

/// <summary>Belge kalemi hesaplamaları — yuvarlama kuralı tek yerde tanımlanır.</summary>
public static class LineMath
{
    /// <summary>
    /// Kalem tutarları: brüt = Round(miktar × fiyat, 2); net = Round(brüt × (1 − iskonto), 2);
    /// KDV = Round(net × oran, 2). Tutarlar numeric(18,2). Satış ve alış ortak kullanır.
    /// </summary>
    public static (decimal Net, decimal Vat) Line(
        decimal quantity, decimal unitPrice, decimal discountRate, decimal vatRate)
    {
        var gross = decimal.Round(quantity * unitPrice, 2);
        var net = decimal.Round(gross * (1 - discountRate / 100m), 2);
        var vat = decimal.Round(net * vatRate / 100m, 2);
        return (net, vat);
    }
}
