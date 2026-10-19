namespace ProjectCleaner.Models;

/// <summary>Byte sizes, in units a human reads at a glance.</summary>
public static class ByteFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        // Whole bytes never need decimals; everything above KB gets one decimal place.
        string format = unit == 0 ? "0" : (value < 10 ? "0.0" : "0.#");

        return $"{value.ToString(format, System.Globalization.CultureInfo.CurrentCulture)} {Units[unit]}";
    }
}