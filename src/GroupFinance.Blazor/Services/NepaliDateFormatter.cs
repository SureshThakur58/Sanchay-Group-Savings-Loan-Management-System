using NepDate;

namespace GroupFinance.Blazor.Services;

public static class NepaliDateFormatter
{
    public static string Format(DateTime date) => new NepaliDate(date).ToString("dd/MM/yyyy");
    public static string Format(DateTime? date) => date.HasValue ? Format(date.Value) : string.Empty;
    public static string FormatDateTime(DateTime date) => $"{Format(date)} {date:HH:mm}";
    public static string FormatDateTime(DateTimeOffset date) => $"{Format(date.LocalDateTime)} {date:HH:mm}";
    public static int Year(DateTime date) => new NepaliDate(date).Year;
    public static int Month(DateTime date) => new NepaliDate(date).Month;
    public static string MonthName(DateTime date) => new NepaliDate(date).MonthName.ToString();
    public static string Long(DateTime date) => $"{date.ToNepaliDate().Day:00} {date.ToNepaliDate().MonthName} {date.ToNepaliDate().Year}";
    public static string Today() => Long(DateTime.Today);
}

public static class NepaliDateExtensions
{
    public static string ToNepaliDateString(this DateTime date) => NepaliDateFormatter.Format(date);
    public static string ToNepaliDateTimeString(this DateTime date) => NepaliDateFormatter.FormatDateTime(date);
    public static string ToNepaliDateTimeString(this DateTimeOffset date) => NepaliDateFormatter.FormatDateTime(date);
}
