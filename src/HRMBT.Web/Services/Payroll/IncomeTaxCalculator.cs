using System.Globalization;

namespace HRMBT.Web.Services.Payroll;

/// <summary>
/// One income-tax slab for a tax year. <see cref="From"/> is the annual income where the slab starts (the "amount over" threshold);
/// tax on income inside the slab is <see cref="FixedAmount"/> + <see cref="Rate"/>% of the part above <see cref="From"/>.
/// <see cref="To"/> (null = no upper limit) is informational; the slab is chosen by <see cref="From"/>.
/// </summary>
public sealed record TaxSlab(decimal From, decimal? To, decimal FixedAmount, decimal Rate);

/// <summary>Result of the annual-slab calculation. <see cref="Slab"/> is null when no tax is due or no slab applies.</summary>
public sealed record IncomeTaxResult(decimal AnnualIncome, decimal AnnualTax, decimal MonthlyTax, TaxSlab? Slab);

/// <summary>
/// Pakistan salaried income tax as a progressive annual-slab calculation (no dependency on the database or any website):
/// 1. annual taxable income = monthly amount x 12;
/// 2. take the slab with the highest start that is not above the income;
/// 3. annual tax = fixed amount + rate % of (income - slab start);
/// 4. monthly tax = annual tax / 12, rounded to the whole rupee.
/// The method and the slab figures are documented in docs/reference/pk-income-tax.md.
/// </summary>
public static class IncomeTaxCalculator
{
    public const int MonthsPerYear = 12;

    public static TaxSlab? FindSlab(decimal annualIncome, IEnumerable<TaxSlab> slabs) =>
        slabs.Where(s => s.From <= annualIncome).OrderByDescending(s => s.From).FirstOrDefault();

    public static IncomeTaxResult Calculate(decimal annualIncome, IEnumerable<TaxSlab> slabs)
    {
        if (annualIncome <= 0m)
            return new IncomeTaxResult(annualIncome, 0m, 0m, null);

        var slab = FindSlab(annualIncome, slabs);
        if (slab == null)
            return new IncomeTaxResult(annualIncome, 0m, 0m, null);

        var annualTax = Math.Round(slab.FixedAmount + (annualIncome - slab.From) * slab.Rate / 100m, 2, MidpointRounding.AwayFromZero);
        var monthlyTax = Math.Round(annualTax / MonthsPerYear, 0, MidpointRounding.AwayFromZero);
        return new IncomeTaxResult(annualIncome, annualTax, monthlyTax, slab);
    }

    /// <summary>Plain-text working shown on the payslip (CalculationDetails).</summary>
    public static string Describe(string taxYear, IncomeTaxResult r)
    {
        var inv = CultureInfo.InvariantCulture;
        var lines = new List<string>
        {
            $"Income tax (tax year {taxYear}, annual slab method):",
            $"  Annual taxable income (basic x 12): {r.AnnualIncome.ToString("N2", inv)}"
        };
        if (r.Slab == null)
        {
            lines.Add("  No slab applies: no tax.");
        }
        else
        {
            var s = r.Slab;
            var to = s.To.HasValue ? s.To.Value.ToString("N0", inv) : "no limit";
            lines.Add($"  Slab {s.From.ToString("N0", inv)} to {to}: {s.FixedAmount.ToString("N2", inv)} + {s.Rate.ToString("0.##", inv)}% of the amount over {s.From.ToString("N0", inv)}");
            lines.Add($"  Annual tax: {r.AnnualTax.ToString("N2", inv)}");
            lines.Add($"  Monthly tax (annual / 12, rounded to the rupee): {r.MonthlyTax.ToString("N2", inv)}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Short label for the payslip tax line (fits PayslipDetails.ItemCategory, 50 characters).</summary>
    public static string Category(IncomeTaxResult r)
    {
        if (r.Slab == null) return "Income tax";
        var inv = CultureInfo.InvariantCulture;
        var s = r.Slab;
        return s.FixedAmount > 0m
            ? $"Slab {s.Rate.ToString("0.##", inv)}% + {s.FixedAmount.ToString("N0", inv)} over {s.From.ToString("N0", inv)}"
            : $"Slab {s.Rate.ToString("0.##", inv)}% over {s.From.ToString("N0", inv)}";
    }
}
