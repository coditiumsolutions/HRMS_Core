using System.Globalization;

namespace HRMBT.Web.Infrastructure;

/// <summary>
/// The one place that decides how values are shown on screen (see docs/ui_standards.md).
/// Always uses the invariant culture, so a page looks the same whatever the server's regional settings are.
/// Null or blank values show as an em dash. Use this in views instead of ToString("N2"), ToString("yyyy-MM-dd") and friends.
/// Do not use it for values that are posted back or read by code (input value attributes, JSON, file names): those stay ISO.
/// </summary>
public static class Fmt
{
    public const string Empty = "—";

    public const string ShortDateFormat = "dd/MM/yyyy";            // 23/11/2026
    public const string LongDateFormat = "dddd, dd MMMM yyyy";    // Monday, 23 November 2026
    public const string TimeFormat = "hh:mm tt";                   // 02:05 PM (12-hour)
    public const string DateTimeFormat = "hh:mm tt dd/MM/yyyy";      // 02:05 PM 23/11/2026 (time first, then date)
    public const string TimeSecondsFormat = "hh:mm:ss tt";            // 02:05:09 PM
    public const string DateTimeSecondsFormat = "hh:mm:ss tt dd/MM/yyyy"; // 02:05:09 PM 23/11/2026
    public const string LongDateTimeFormat = "hh:mm tt dddd, dd MMMM yyyy"; // 02:05 PM Monday, 23 November 2026

    private static readonly CultureInfo C = CultureInfo.InvariantCulture;

    // ---- numbers ----

    /// <summary>Whole number with thousands separators: 12,345.</summary>
    public static string Int(int? v) => v.HasValue ? v.Value.ToString("N0", C) : Empty;
    public static string Int(long? v) => v.HasValue ? v.Value.ToString("N0", C) : Empty;
    public static string Int(decimal? v) => v.HasValue ? v.Value.ToString("N0", C) : Empty;
    public static string Int(double? v) => v.HasValue ? v.Value.ToString("N0", C) : Empty;

    /// <summary>Money-style amount, always two decimals: 12,345.50.</summary>
    public static string Amount(decimal? v) => v.HasValue ? v.Value.ToString("N2", C) : Empty;
    public static string Amount(double? v) => v.HasValue ? v.Value.ToString("N2", C) : Empty;
    public static string Amount(int? v) => v.HasValue ? v.Value.ToString("N2", C) : Empty;

    /// <summary>Amount with the currency: PKR 12,345.50.</summary>
    public static string Money(decimal? v) => v.HasValue ? "PKR " + v.Value.ToString("N2", C) : Empty;

    /// <summary>Quantity or day count, up to two decimals and no trailing zeros: 12, 12.5, 1,200.25.</summary>
    public static string Number(decimal? v) => v.HasValue ? v.Value.ToString("#,##0.##", C) : Empty;
    public static string Number(double? v) => v.HasValue ? v.Value.ToString("#,##0.##", C) : Empty;
    public static string Number(int? v) => v.HasValue ? v.Value.ToString("#,##0", C) : Empty;

    /// <summary>Leave days, always one decimal because leave is tracked in half days: 10.0, 0.5, -1.5.</summary>
    public static string Days(decimal? v) => v.HasValue ? v.Value.ToString("0.0", C) : Empty;

    /// <summary>Percentage given as 12.5 (not 0.125): 12.5%.</summary>
    public static string Percent(decimal? v) => v.HasValue ? v.Value.ToString("#,##0.##", C) + "%" : Empty;

    /// <summary>How a payslip line was worked out: its note, or "100 × 280.00". Empty when there is nothing to add to the amount.</summary>
    public static string LineWorking(HRMBT.Web.Models.PayslipDetail line)
    {
        if (!string.IsNullOrWhiteSpace(line.Note))
            return line.Note;
        if (line.Quantity is not { } quantity || line.UnitAmount is not { } unit)
            return string.Empty;
        return quantity == 1m && unit == line.Amount ? string.Empty : $"{Number(quantity)} × {Amount(unit)}";
    }

    // ---- ranges ----

    /// <summary>Number range with an en dash: 10 – 20. A missing upper end reads "50,000 and above"; a missing lower end "up to 20".</summary>
    public static string Range(decimal? from, decimal? to, bool money = false)
    {
        string F(decimal x) => money ? x.ToString("N2", C) : x.ToString("#,##0.##", C);
        if (from.HasValue && to.HasValue) return $"{F(from.Value)} – {F(to.Value)}";
        if (from.HasValue) return $"{F(from.Value)} and above";
        if (to.HasValue) return $"up to {F(to.Value)}";
        return Empty;
    }

    /// <summary>Date range: 01 Jan 2026 – 05 Jan 2026. One day shows once; an open end reads "from 01 Jan 2026 onwards".</summary>
    public static string DateRange(System.DateTime? from, System.DateTime? to)
    {
        if (from.HasValue && to.HasValue)
            return from.Value.Date == to.Value.Date
                ? ShortDate(from)
                : $"{ShortDate(from)} – {ShortDate(to)}";
        if (from.HasValue) return $"from {ShortDate(from)} onwards";
        if (to.HasValue) return $"until {ShortDate(to)}";
        return Empty;
    }

    // ---- dates and times ----

    public static string ShortDate(System.DateTime? v) => v.HasValue ? v.Value.ToString(ShortDateFormat, C) : Empty;
    public static string LongDate(System.DateTime? v) => v.HasValue ? v.Value.ToString(LongDateFormat, C) : Empty;
    public static string Time(System.DateTime? v) => v.HasValue ? v.Value.ToString(TimeFormat, C) : Empty;
    public static string Time(TimeSpan? v) => v.HasValue ? System.DateTime.Today.Add(v.Value).ToString(TimeFormat, C) : Empty;
    public static string TimeWithSeconds(System.DateTime? v) => v.HasValue ? v.Value.ToString(TimeSecondsFormat, C) : Empty;
    public static string DateAndTimeWithSeconds(System.DateTime? v) => v.HasValue ? v.Value.ToString(DateTimeSecondsFormat, C) : Empty;
    public static string LongDateAndTime(System.DateTime? v) => v.HasValue ? v.Value.ToString(LongDateTimeFormat, C) : Empty;
    public static string DateAndTime(System.DateTime? v) => v.HasValue ? v.Value.ToString(DateTimeFormat, C) : Empty;

    /// <summary>For legacy text columns (Employee.DOB, Employee.ModifiedOn): formatted when it parses as a date, otherwise shown as stored.</summary>
    public static string ShortDate(string? text) => TryParse(text, out var d) ? d.ToString(ShortDateFormat, C) : Blank(text);
    public static string DateAndTime(string? text) =>
        TryParse(text, out var d)
            ? (d.TimeOfDay == TimeSpan.Zero ? d.ToString(ShortDateFormat, C) : d.ToString(DateTimeFormat, C))
            : Blank(text);

    private static bool TryParse(string? text, out System.DateTime value) =>
        System.DateTime.TryParse(text, C, DateTimeStyles.None, out value);

    private static string Blank(string? text) => string.IsNullOrWhiteSpace(text) ? Empty : text!;
}
