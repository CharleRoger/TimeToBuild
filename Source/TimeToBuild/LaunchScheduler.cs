using System;
using System.Collections.Generic;
using System.Linq;
using static TimeToBuild.TimeToBuildUtils;

namespace TimeToBuild
{
    public class LaunchScheduler
    {
        public Calendar Calendar { get; private set; }
        public TimeToBuildProfile Profile { get; private set; }
        public TimeToBuildScenario Scenario { get; private set; }
        public double LaunchTime { get; private set; } = -1;
        public double LaunchTimeEarliest { get; private set; } = -1;
        public double LaunchTimeNextMorning { get; private set; } = -1;
        public bool LaunchScheduled => LaunchTime > 0;

        public LaunchScheduler(TimeToBuildProfile profile, CelestialBody homeWorld)
        {
            Calendar = new Calendar(homeWorld);
            Profile = profile;
            Scenario = GetScenarioModule();
        }

        public Dictionary<BuildTime.BuildTimeIdentifier, double> GetBuildRates()
        {
            var buildRates = new Dictionary<BuildTime.BuildTimeIdentifier, double>();

            var timeUnitVariables = Calendar.GetTimeUnitVariables();
            var facilityVariables = GetFacilityVariables();

            foreach (var buildTime in Profile.BuildTimes)
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
                new Tuple<double, string>(Scenario.EditorStartTime, LocalizerCache.CurrentTime),
                new Tuple<double, string>(LaunchTimeEarliest, LocalizerCache.LaunchTimeEarliest),
                new Tuple<double, string>(LaunchTimeNextMorning, LocalizerCache.LaunchTimeNextMorning)
            };

            if (!(AlarmClockScenario.Instance is null))
            {
                foreach (var alarm in AlarmClockScenario.Instance.alarms.Values)
                {
                    if (alarm.ut < LaunchTimeNextMorning + Profile.AlarmWarningBufferTime)
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
                    if (contract.TimeDeadline < LaunchTimeNextMorning + Profile.AlarmWarningBufferTime)
                    {
                        var contractMessage = contract.Title;
                        salientDates.Add(new Tuple<double, string>(contract.TimeDeadline, contractMessage));
                    }
                }
            }

            return salientDates.OrderBy(p => p.Item1);
        }

        public void SetLaunchTimeToEarliest()
        {
            LaunchTime = LaunchTimeEarliest;
        }

        public void SetLaunchTimeToNextMorning()
        {
            LaunchTime = LaunchTimeNextMorning;
        }

        public void UnsetLaunchTime()
        {
            LaunchTime = -1;
        }

        public void WarpToLaunchTime()
        {
            HighLogic.CurrentGame.flightState.universalTime = LaunchTime;
            UnsetLaunchTime();
        }

        public void SetBuildTime(double buildTime)
        {
            LaunchTimeEarliest = Scenario.EditorStartTime + buildTime;
            LaunchTimeNextMorning = Math.Ceiling((LaunchTimeEarliest - Profile.MorningTime) / Calendar.Day) * Calendar.Day + Profile.MorningTime;
        }
    }
}