namespace Almer.Application;

public static class TaxCalculator
{
    public static (decimal Net, decimal Tax, decimal Gross) FromNet(decimal net, decimal rate)
    {
        Validate(net, rate); var tax = decimal.Round(net * rate / 100m, 2, MidpointRounding.AwayFromZero); return (net, tax, net + tax);
    }
    public static (decimal Net, decimal Tax, decimal Gross) FromGross(decimal gross, decimal rate)
    {
        Validate(gross, rate); var net = decimal.Round(gross / (1m + rate / 100m), 2, MidpointRounding.AwayFromZero); return (net, gross - net, gross);
    }
    private static void Validate(decimal amount, decimal rate) { if (amount < 0 || rate is < 0 or > 100) throw new ArgumentException("Invalid amount or tax rate."); }
}
