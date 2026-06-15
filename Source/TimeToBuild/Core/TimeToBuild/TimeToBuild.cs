using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static TimeToBuild.TimeToBuildProfile;
using static TimeToBuild.MiscUtils;

namespace TimeToBuild
{
    public abstract class TimeToBuild : MonoBehaviour
    {
        public static TimeToBuild Instance = null;
        public TimeToBuildProfile Profile { get; private set; }
        public TimeToBuildScenario Scenario { get; private set; }
        public Calendar Calendar { get; private set; }
        public LaunchScheduler LaunchScheduler { get; private set; } = new LaunchScheduler();

        protected abstract void HandleButtons();
        protected abstract void OnLaunchButtonClicked();

        private IEnumerator InitialiseCalendar_Coroutine()
        {
            while (SpaceCenter.Instance is null || SpaceCenter.Instance.cb is null) yield return new WaitForFixedUpdate();

            Calendar = new Calendar(SpaceCenter.Instance.cb);
        }

        protected void Start()
        {
            Instance = this;

            var settings = HighLogic.CurrentGame.Parameters.CustomParams<TimeToBuildSettings>();
            Profile = GetTimeToBuildProfile(settings.Profile);

            Scenario = HighLogic.CurrentGame.scenarios.FirstOrDefault(s => s.moduleRef is TimeToBuildScenario)?.moduleRef as TimeToBuildScenario;

            if (Scenario is null || Scenario.BuildFacilityVAB is null || Scenario.BuildFacilitySPH is null) return;

            StartCoroutine(InitialiseCalendar_Coroutine());

            GameEvents.onGameStateSave.Add(OnSave);
        }

        protected void Update()
        {
            HandleButtons();
        }

        protected void OnSave(ConfigNode node)
        {
            // Bit of a hack, but trying to warp any earlier won't work

            if (LaunchScheduler.LaunchScheduled) LaunchScheduler.WarpToLaunchTime();
        }

        protected void OnDestroy()
        {
            GameEvents.onGameStateSave.Remove(OnSave);
        }

        public Dictionary<WorkTime.WorkTimeIdentifier, double> GetWorkRates()
        {
            var workRates = new Dictionary<WorkTime.WorkTimeIdentifier, double>();

            var timeUnitVariables = Calendar.GetTimeUnitVariables();
            var facilityVariables = GetFacilityVariables();

            foreach (var buildTime in Profile.BuildTimes)
            {
                var facility = buildTime.Key.Facility;

                var facilityVariable = new Dictionary<string, double>();
                facilityVariable["facility_level"] = GetFacilityLevel(buildTime.Key.Facility);
                workRates[buildTime.Key] = FormulaParser.ParseAndComputeFormula(buildTime.Value.TimeFormula.Rate, timeUnitVariables, facilityVariables, facilityVariable);
            }

            facilityVariables = GetFacilityVariables();

            foreach (var researchTime in Profile.ResearchTimes)
            {
                var facility = researchTime.Key.Facility;

                var facilityVariable = new Dictionary<string, double>();
                facilityVariable["facility_level"] = GetFacilityLevel(researchTime.Key.Facility);
                workRates[researchTime.Key] = FormulaParser.ParseAndComputeFormula(researchTime.Value.TimeFormula.Rate, timeUnitVariables, facilityVariables, facilityVariable);
            }

            return workRates;
        }

        // List of tuples instead of dictionary in case of duplicate times or names
        public IOrderedEnumerable<Tuple<double, string>> GetSalientDates(double endTime)
        {
            endTime += Profile.AlarmWarningBufferTime;

            var salientDates = new List<Tuple<double, string>>{ new Tuple<double, string>(CurrentTime, LocalizerCache.CurrentTime) };

            if (LaunchScheduler.LaunchTimeEarliest > 0 && LaunchScheduler.LaunchTimeEarliest < endTime) salientDates.Add(new Tuple<double, string>(LaunchScheduler.LaunchTimeEarliest, LocalizerCache.LaunchTimeEarliest));
            if (LaunchScheduler.LaunchTimeNextMorning > 0 && LaunchScheduler.LaunchTimeNextMorning < endTime) salientDates.Add(new Tuple<double, string>(LaunchScheduler.LaunchTimeNextMorning, LocalizerCache.LaunchTimeNextMorning));

            if (!(AlarmClockScenario.Instance is null))
            {
                foreach (var alarm in AlarmClockScenario.Instance.alarms.Values)
                {
                    if (alarm.ut < endTime)
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
                    if (contract.TimeDeadline < endTime)
                    {
                        var contractMessage = contract.Title;
                        salientDates.Add(new Tuple<double, string>(contract.TimeDeadline, contractMessage));
                    }
                }
            }

            return salientDates.OrderBy(p => p.Item1);
        }
    }
}