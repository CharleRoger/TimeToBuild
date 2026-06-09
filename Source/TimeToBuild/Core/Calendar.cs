using System;
using System.Collections.Generic;
using System.Linq;

namespace TimeToBuild
{
    public class Calendar
    {
        private readonly Dictionary<string, int> TimeUnits;
        public int Second => TimeUnits["second"];
        public int Minute => TimeUnits["minute"];
        public int Hour => TimeUnits["hour"];
        public int Day => TimeUnits["day"];
        public int Year => TimeUnits["year"];

        public Calendar(double dayLength, double yearLength)
        {
            TimeUnits = new Dictionary<string, int> { { "second", 1 }, { "minute", 60 }, { "hour", 3600 } };
            TimeUnits["day"] = (Convert.ToInt32(dayLength) / Hour) * Hour;
            TimeUnits["year"] = (Convert.ToInt32(yearLength) / Day) * Day;
        }

        public Calendar(CelestialBody celestialBody) : this(1 / (1 / celestialBody.rotationPeriod - 1 / celestialBody.orbit.period), celestialBody.orbit.period)
        {

        }

        public Dictionary<string, double> GetTimeUnitVariables()
        {
            var variables = new Dictionary<string, double>();

            foreach (var timeUnit in TimeUnits) variables[timeUnit.Key] = timeUnit.Value;

            return variables;
        }

        public Dictionary<string, int> ChunkUpDuration(int seconds)
        {
            var chunks = new Dictionary<string, int>();
            foreach (var timeUnit in TimeUnits.OrderBy(p => -p.Value))
            {
                chunks[timeUnit.Key] = seconds / timeUnit.Value;
                seconds -= chunks[timeUnit.Key] * timeUnit.Value;
            }
            return chunks;
        }

        public string GetTimeUnitString(string timeUnit, bool plural)
        {
            switch (timeUnit)
            {
                case "second":
                    return plural ? LocalizerCache.Seconds : LocalizerCache.Second;
                case "minute":
                    return plural ? LocalizerCache.Minutes : LocalizerCache.Minute;
                case "hour":
                    return plural ? LocalizerCache.Hours : LocalizerCache.Hour;
                case "day":
                    return plural ? LocalizerCache.Days : LocalizerCache.Day;
                case "year":
                    return plural ? LocalizerCache.Years : LocalizerCache.Year;
                default:
                    return "";
            }
        }

        public string GetDurationString(int seconds)
        {
            var chunks = ChunkUpDuration(seconds);

            var str = "";
            foreach (var chunk in chunks)
            {
                if (chunk.Value > 0)
                {
                    str += chunk.Value + " " + GetTimeUnitString(chunk.Key, chunk.Value > 1);
                    str += ", ";
                }
            }
            if (str.Length > 0) str = str[0].ToString().ToUpper() + str.Substring(1, str.Length - 3);

            return str;
        }

        public string GetDateString(double universalTime)
        {
            var chunks = ChunkUpDuration(Convert.ToInt32(universalTime));
            return char.ToUpper(LocalizerCache.Year.First()) + LocalizerCache.Year.Substring(1) + " " + (chunks["year"] + 1).ToString()
                + ", " + LocalizerCache.Day + " " + (chunks["day"] + 1).ToString()
                + ", " + chunks["hour"].ToString("D2") + ":" + chunks["minute"].ToString("D2");
        }

        public int RoundDuration(int duration)
        {
            if (duration > 7 * Day) return Day * (duration / Day);
            else if (duration > 6 * Hour) return Hour * (duration / Hour);
            else if (duration > Hour) return 15 * Minute * (duration / (15 * Minute));
            else if (duration > 20 * Minute) return 5 * Minute * (duration / (5 * Minute));
            else if (duration > 5 * Minute) return Minute * (duration / Minute);
            return duration;
        }
    }
}