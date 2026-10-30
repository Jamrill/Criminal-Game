using System;
using System.Globalization;

namespace JuegoCriminal.Localization
{
    public static class DateFormatUtil
    {
        public static string FormatLastPlayedWithTime(string lastPlayedUtcIso, string cultureCodeOrNull)
        {
            if (string.IsNullOrWhiteSpace(lastPlayedUtcIso))
                return "";

            if (!DateTime.TryParse(
                    lastPlayedUtcIso,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var utc))
                return "";

            var local = utc.ToLocalTime();
            CultureInfo culture = GetCultureOrCurrent(cultureCodeOrNull);

            string date = local.ToString(culture.DateTimeFormat.ShortDatePattern, culture);
            string time = local.ToString(culture.DateTimeFormat.ShortTimePattern, culture);

            // Ejemplos:
            // es-ES -> "13/03/2026 14:07"
            // en-US -> "3/13/2026 2:07 PM"
            // de-DE -> "13.03.2026 14:07"
            // ja-JP -> "2026/03/13 14:07"
            return $"{date} {time}";
        }

        private static CultureInfo GetCultureOrCurrent(string cultureCodeOrNull)
        {
            if (string.IsNullOrWhiteSpace(cultureCodeOrNull))
                return CultureInfo.CurrentCulture;

            try { return CultureInfo.GetCultureInfo(cultureCodeOrNull); }
            catch { return CultureInfo.CurrentCulture; }
        }
    }
}