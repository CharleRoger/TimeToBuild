using System;
using System.Collections.Generic;
using System.Linq;
using static TimeToBuild.TimeToBuildUtils;

namespace TimeToBuild
{
    public class LaunchScheduler
    {
        public Calendar Calendar { get; private set; }
        public double LaunchTime { get; private set; } = -1;
        public double LaunchTimeEarliest = -1;
        public double LaunchTimeNextMorning => Math.Ceiling((LaunchTimeEarliest - TimeToBuild.Instance.Profile.MorningTime) / Calendar.Day) * Calendar.Day + TimeToBuild.Instance.Profile.MorningTime;
        public bool LaunchScheduled => LaunchTime > 0;

        public LaunchScheduler(Calendar calendar)
        {
            Calendar = calendar;
        }

        public Dictionary<BuildTime.BuildTimeIdentifier, double> GetBuildRates()
        {
            var buildRates = new Dictionary<BuildTime.BuildTimeIdentifier, double>();

            var timeUnitVariables = Calendar.GetTimeUnitVariables();
            var facilityVariables = GetFacilityVariables();

            foreach (var buildTime in TimeToBuild.Instance.Profile.BuildTimes)
            {
                var facility = buildTime.Key.Facility;

                var facilityVariable = new Dictionary<string, double>();
                facilityVariable["facility_level"] = GetFacilityLevel(buildTime.Key.Facility);
                buildRates[buildTime.Key] = FormulaParser.ParseAndComputeFormula(buildTime.Value.RateFormula, timeUnitVariables, facilityVariables, facilityVariable);
            }

            return buildRates;
        }

        // List of tuples instead of dictionary in case of duplicate times or names
        public IOrderedEnumerable<Tuple<double, string>> GetSalientDates()
        {
            var salientDates = new List<Tuple<double, string>>
            {
                new Tuple<double, string>(TimeToBuild.Instance.Scenario.EditorStartTime, LocalizerCache.CurrentTime),
                new Tuple<double, string>(LaunchTimeEarliest, LocalizerCache.LaunchTimeEarliest),
                new Tuple<double, string>(LaunchTimeNextMorning, LocalizerCache.LaunchTimeNextMorning)
            };

            if (!(AlarmClockScenario.Instance is null))
            {
                foreach (var alarm in AlarmClockScenario.Instance.alarms.Values)
                {
                    if (alarm.ut < LaunchTimeNextMorning + TimeToBuild.Instance.Profile.AlarmWarningBufferTime)
                    {
                        var alarmMessage = alarm.title;
                        if (alarm.vesselName != null && alarm.vesselName != "") alarmMessage += " (" + alarm.vesselName + ")";
                        salientDates.Add(new Tuple<double, string>(alarm.ut, alarmMessage));
                    }
                }
            }

            if (!(Contracts.ContractSystem.Instance is null))
            {
                foreach (var contract in Contracts.ContractSystem.Instance.GetCurrentActiveContracts<Contracts.Contract>())
                {
                    if (contract.TimeDeadline < LaunchTimeNextMorning + TimeToBuild.Instance.Profile.AlarmWarningBufferTime)
                    {
                        var contractMessage = contract.Title;
                        salientDates.Add(new Tuple<double, string>(contract.TimeDeadline, contractMessage));
                    }
                }
            }

            return salientDates.OrderBy(p => p.Item1);
        }

        public void ScheduleLaunch(double launchTime, string vesselName)
        {
            UnscheduleLaunch();

            LaunchTime = launchTime;
        }

        public void UnscheduleLaunch()
        {
            LaunchTime = -1;
        }

        public void ResetTime()
        {
            HighLogic.CurrentGame.flightState.universalTime = TimeToBuild.Instance.Scenario.EditorStartTime;
            UnscheduleLaunch();
        }

        public void WarpToLaunchTime()
        {
            HighLogic.CurrentGame.flightState.universalTime = LaunchTime;
            UnscheduleLaunch();
        }
    }
}