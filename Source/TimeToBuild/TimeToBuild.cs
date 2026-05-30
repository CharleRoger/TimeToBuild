using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Smooth.Collections;
using static TimeToBuild.TimeToBuildProfile;
using static TimeToBuild.TimeToBuildUtils;
using KSP.UI.Screens;
using static TimeToBuild.BuildTime;

namespace TimeToBuild
{
    public abstract class TimeToBuild : MonoBehaviour
    {
        protected TimeToBuildProfile Profile { get; private set; }

        protected TimeToBuildScenario Scenario { get; private set; }

        protected Calendar Calendar { get; private set; }
        protected double LaunchTime = -1;
        protected double LaunchTimeEarliest = -1;
        protected double LaunchTimeNextMorning = -1;

        protected bool ScrapYardUseTracker => !(ScrapYard.ScrapYard.Instance is null) && ScrapYard.ScrapYard.Instance.Settings.CurrentSaveSettings.UseTracker;
        protected bool ScrapYardUseInventory => !(ScrapYard.ScrapYard.Instance is null) && ScrapYard.ScrapYard.Instance.Settings.CurrentSaveSettings.UseInventory;

        protected abstract List<SpaceCenterFacility> GetUsingFacilities();
        protected abstract bool ButtonsAreActive();
        protected abstract void HandleButtons();
        protected abstract void SpawnBuildDialog();
        protected abstract void TryLaunchVessel();

        private IEnumerator InitialiseCalendar_Coroutine()
        {
            while (SpaceCenter.Instance.cb is null) yield return new WaitForFixedUpdate();

            Calendar = new Calendar(SpaceCenter.Instance.cb);
        }

        private IEnumerator HandleButtons_Coroutine()
        {
            while (true)
            {
                if (ButtonsAreActive()) HandleButtons();
                yield return new WaitForFixedUpdate();
            }
        }

        protected void Start()
        {
            var settings = HighLogic.CurrentGame.Parameters.CustomParams<TimeToBuildSettings>();
            Profile = GetTimeToBuildProfile(settings.Profile);

            Scenario = GetScenarioModule();

            StartCoroutine(InitialiseCalendar_Coroutine());

            StartCoroutine(HandleButtons_Coroutine());
        }

        protected List<BuildChunk> ComputeBuildChunks(List<BuildPart> buildParts)
        {
            var buildChunks = new List<BuildChunk>();

            var usingFacilities = GetUsingFacilities();
            if (usingFacilities.Count == 0) return buildChunks;

            var shipVariables = new Dictionary<string, double>
            {
                { VariableDryMass, 0 },
                { VariableWetMass, 0 },
                { VariableDryCost, 0 },
                { VariableWetCost, 0 },
                { VariableNumParts, buildParts.Count }
            };

            var partVariables = new Dictionary<BuildPart, Dictionary<string, double>>();
            foreach (var buildPart in buildParts)
            {
                var variables = GetPartVariables(buildPart);
                partVariables[buildPart] = variables;

                shipVariables[VariableDryMass] += variables[VariableDryMass];
                shipVariables[VariableWetMass] += variables[VariableWetMass];
                shipVariables[VariableDryCost] += variables[VariableDryCost];
                shipVariables[VariableWetCost] += variables[VariableWetCost];
            }

            var timeUnitVariables = Calendar.GetTimeUnitVariables();

            foreach (var buildTime in Profile.BuildTimes.Values)
            {
                if (!usingFacilities.Contains(buildTime.Identifier.Facility)) continue;

                var buildChunk = new BuildChunk(buildTime.Identifier);
                buildChunk.Work = 0;
                buildChunk.Overhead = 0;

                var facilityVariables = GetFacilityVariables();
                facilityVariables["facility_level"] = GetFacilityLevel(buildTime.Identifier.Facility);

                if (buildTime.PerNewPart)
                {
                    foreach (var buildPart in buildParts)
                    {
                        if (!buildPart.ReuseFromInventory)
                        {
                            buildChunk.Work += FormulaParser.ParseAndComputeFormula(buildTime.WorkFormula, timeUnitVariables, facilityVariables, partVariables[buildPart]);
                            buildChunk.Overhead += FormulaParser.ParseAndComputeFormula(buildTime.OverheadFormula, timeUnitVariables, facilityVariables, partVariables[buildPart]);
                        }
                    }
                }
                if (buildTime.PerReusedPart)
                {
                    foreach (var buildPart in buildParts)
                    {
                        if (buildPart.ReuseFromInventory)
                        {
                            buildChunk.Work += FormulaParser.ParseAndComputeFormula(buildTime.WorkFormula, timeUnitVariables, facilityVariables, partVariables[buildPart]);
                            buildChunk.Overhead += FormulaParser.ParseAndComputeFormula(buildTime.OverheadFormula, timeUnitVariables, facilityVariables, partVariables[buildPart]);
                        }
                    }
                }
                if (buildTime.WholeVessel)
                {
                    buildChunk.Work += FormulaParser.ParseAndComputeFormula(buildTime.WorkFormula, timeUnitVariables, facilityVariables, shipVariables);
                    buildChunk.Overhead += FormulaParser.ParseAndComputeFormula(buildTime.OverheadFormula, timeUnitVariables, facilityVariables, shipVariables);
                }

                if (buildChunk.Work > 0 || buildChunk.Overhead > 0) buildChunks.Add(buildChunk);
            }

            return buildChunks;
        }

        protected List<BuildPart> GatherBuildParts(List<Part> parts)
        {
            var buildParts = new List<BuildPart>();

            var partDictionary = new Dictionary<string, List<Part>>();
            foreach (var part in parts)
            {
                if (!partDictionary.ContainsKey(part.name)) partDictionary[part.name] = new List<Part>();
                partDictionary[part.name].Add(part);
            }

            foreach (var partName in partDictionary.Keys)
            {
                var inventoryParts = new List<ScrapYard.InventoryPart>();
                if (ScrapYardUseInventory) inventoryParts = ScrapYard.ScrapYard.Instance.TheInventory.FindPartsByName(partName).ToList();

                for (int i = 0; i < partDictionary[partName].Count(); i++)
                {
                    var part = partDictionary[partName][i];

                    var buildPart = new BuildPart();

                    if (i < inventoryParts.Count())
                    {
                        buildPart.ReuseFromInventory = true;
                        buildPart.ID = inventoryParts[i].ID;
                        part = inventoryParts[i].ToPart();
                    }
                    else
                    {
                        buildPart.ReuseFromInventory = false;
                        buildPart.ID = (uint)partDictionary[partName][i].GetInstanceID();
                    }

                    buildPart.DryMass = part.mass;
                    buildPart.WetMass = part.mass + part.GetResourceMass();
                    buildPart.DryCost = part.partInfo.cost;
                    buildPart.WetCost = part.partInfo.cost;
                    foreach (var resource in part.Resources) buildPart.WetCost += resource.amount * PartResourceLibrary.Instance.GetDefinition(resource.resourceName).unitCost;
                    buildPart.NumBuilds = ScrapYardUseTracker ? ScrapYard.ScrapYard.Instance.PartTracker.GetBuildsForPart(part, ScrapYard.PartTracker.TrackType.NEW) : 0;

                    buildParts.Add(buildPart);
                }
            }

            return buildParts;
        }

        // List of tuples instead of dictionary in case of duplicate times or names
        protected IOrderedEnumerable<Tuple<double, string>> GetSalientDates()
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

        protected void OpenBuildDialog()
        {
            SpawnBuildDialog();
        }

        protected void OnWarpToEarliestLaunch()
        {
            LaunchTime = LaunchTimeEarliest;
            TryLaunchVessel();
        }

        protected void OnWarpToLaunchNextMorning()
        {
            LaunchTime = LaunchTimeNextMorning;
            TryLaunchVessel();
        }

        protected void CloseBuildDialog()
        {
            LaunchTime = -1;
        }

        protected DialogGUIButton GetBuildDialogButton(string optionText, Callback callback = null, double date = -1)
        {
            if (date > 0) optionText += "\n" + Calendar.GetDateString(date);

            return new DialogGUIButton(optionText, callback, 300, 40, true);
        }

        protected void SpawnMultiOptionDialog(string title, string message, params DialogGUIBase[] optionButtons)
        {
            var optionClose = GetBuildDialogButton(LocalizerCache.Close);

            var allOptionButtons = optionButtons.ToList();
            allOptionButtons.Add(optionClose);

            var dialog = new MultiOptionDialog("TimeToBuildDialog", message, title, HighLogic.UISkin, allOptionButtons.ToArray());

            PopupDialog.SpawnPopupDialog(dialog, false, HighLogic.UISkin);
        }
    }

    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class TimeToBuildEditor : TimeToBuild
    {
        protected void Reset()
        {
            HighLogic.CurrentGame.flightState.universalTime = Scenario.EditorStartTime;
            LaunchTime = -1;
        }

        protected void WarpToLaunchTime()
        {
            HighLogic.CurrentGame.flightState.universalTime = LaunchTime;
            LaunchTime = -1;
        }

        protected void OnSave(ConfigNode node)
        {
            if (LaunchTime < 0) return;

            WarpToLaunchTime();

            if (!ScrapYardUseInventory) return;

            var buildParts = GatherBuildParts(EditorLogic.fetch.ship.parts);
            foreach (var buildPart in buildParts)
            {
                if (buildPart.ReuseFromInventory) ScrapYard.ScrapYard.Instance.TheInventory.RemovePart(buildPart.ID);
            }
        }

        protected new void Start()
        {
            base.Start();

            if (Scenario.EditorStartTime > 0 && SceneTracker.RevertedFromFlight) Reset();

            Scenario.EditorStartTime = HighLogic.CurrentGame.flightState.universalTime;

            GameEvents.onGameStateSave.Add(OnSave);
        }

        protected void OnDestroy()
        {
            GameEvents.onGameStateSave.Remove(OnSave);
        }

        protected override List<SpaceCenterFacility> GetUsingFacilities()
        {
            var usingFacilities = new List<SpaceCenterFacility>();

            if (EditorDriver.editorFacility == EditorFacility.VAB)
            {
                usingFacilities.Add(SpaceCenterFacility.VehicleAssemblyBuilding);
                usingFacilities.Add(SpaceCenterFacility.LaunchPad);
            }
            else if (EditorDriver.editorFacility == EditorFacility.SPH)
            {
                usingFacilities.Add(SpaceCenterFacility.SpaceplaneHangar);
                usingFacilities.Add(SpaceCenterFacility.Runway);
            }

            return usingFacilities;
        }

        protected override bool ButtonsAreActive()
        {
            return HighLogic.LoadedSceneIsEditor;
        }

        protected override void HandleButtons()
        {
            EditorLogic.fetch.launchBtn.onClick.RemoveAllListeners();
            EditorLogic.fetch.launchBtn.onClick.AddListener(() => OpenBuildDialog());

            EditorLogic.fetch.exitBtn.onClick.AddListener(() => Reset());

            var launchSiteSelector = GetMember<GameObject>(EditorLogic.fetch, "launchSiteSelector");
            launchSiteSelector.SetActive(false);
        }

        protected override void TryLaunchVessel()
        {
            EditorLogic.fetch.launchVessel();
        }

        protected override void SpawnBuildDialog()
        {
            Reset();

            var buildParts = GatherBuildParts(EditorLogic.fetch.ship.parts);

            var buildChunks = ComputeBuildChunks(buildParts);

            var title = "";
            var totalBuildTime = 0;
            foreach (var buildChunk in buildChunks)
            {
                var buildTimeConfig = Profile.BuildTimes[buildChunk.Identifier];

                if (buildChunk.Work > 0)
                {
                    title += buildTimeConfig.Title;

                    if (!Profile.BuildTimes.ContainsKey(buildChunk.Identifier)) continue;

                    var buildRates = GetBuildRates(Calendar, Profile.BuildTimes.Values);

                    var rate = buildRates[buildChunk.Identifier];

                    var buildTime = Convert.ToInt32(Math.Ceiling(buildChunk.Work / rate + buildChunk.Overhead));
                    if (buildTime < 0) buildTime = 0;
                    buildTime = Calendar.RoundDuration(buildTime);

                    totalBuildTime += buildTime;

                    var numNewParts = buildParts.Count(buildPart => !buildPart.ReuseFromInventory);
                    var numReusedParts = buildParts.Count(buildPart => buildPart.ReuseFromInventory);

                    var newPartsRelevant = buildTimeConfig.PerNewPart && numNewParts > 0;
                    var reusedPartsRelevant = buildTimeConfig.PerReusedPart && numReusedParts > 0;

                    if (newPartsRelevant || reusedPartsRelevant)
                    {
                        title += " (";
                        if (newPartsRelevant) title += numNewParts.ToString() + " " + (numNewParts > 1 ? LocalizerCache.NewParts : LocalizerCache.NewPart);
                        if (newPartsRelevant && reusedPartsRelevant) title += ", ";
                        if (reusedPartsRelevant) title += numReusedParts.ToString() + " " + (numReusedParts > 1 ? LocalizerCache.ReusedParts : LocalizerCache.ReusedPart);
                        title += ")";
                    }

                    title += "\n" + Calendar.GetDurationString(buildTime) + "\n\n";
                }
            }
            title += LocalizerCache.Total + "\n" + Calendar.GetDurationString(totalBuildTime) + "\n\n";

            LaunchTimeEarliest = Scenario.EditorStartTime + totalBuildTime;
            LaunchTimeNextMorning = Math.Ceiling((LaunchTimeEarliest - Profile.MorningTime) / Calendar.Day) * Calendar.Day + Profile.MorningTime;

            var message = "";
            foreach (var date in GetSalientDates()) message += Calendar.GetDateString(date.Item1) + " — " + date.Item2 + "\n";

            var optionWarpToEarliestLaunch = GetBuildDialogButton(LocalizerCache.WarpToEarliestLaunch, OnWarpToEarliestLaunch, LaunchTimeEarliest);
            var optionWarpToNextMorning = GetBuildDialogButton(LocalizerCache.WarpToNextMorning, OnWarpToLaunchNextMorning, LaunchTimeNextMorning);

            SpawnMultiOptionDialog(title, message, optionWarpToEarliestLaunch, optionWarpToNextMorning);
        }
    }

    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class TimeToBuildSpaceCentre : TimeToBuild
    {
        protected override List<SpaceCenterFacility> GetUsingFacilities()
        {
            var usingFacilities = new List<SpaceCenterFacility>();

            if (ButtonsAreActive())
            {
                var launchSiteFacility = GetMember<LaunchSiteFacility>(VesselSpawnDialog.Instance, "launchSiteFacility");
                if (!(launchSiteFacility is null))
                {
                    if (launchSiteFacility.facilityType == EditorFacility.VAB)
                    {
                        usingFacilities.Add(SpaceCenterFacility.LaunchPad);
                    }
                    else if (launchSiteFacility.facilityType == EditorFacility.SPH)
                    {
                        usingFacilities.Add(SpaceCenterFacility.Runway);
                    }
                }
            }

            return usingFacilities;
        }

        protected override bool ButtonsAreActive()
        {
            return HighLogic.LoadedSceneIsGame && !(VesselSpawnDialog.Instance is null) && VesselSpawnDialog.Instance.isActiveAndEnabled;
        }

        protected override void HandleButtons()
        {
            var button = GetMember<UnityEngine.UI.Button>(VesselSpawnDialog.Instance, "buttonLaunch");
            if (!(button is null)) button.interactable = false;

            var launchSiteSelector = GetMember<GameObject>(VesselSpawnDialog.Instance, "launchSiteSelector");
            launchSiteSelector.SetActive(false);
        }

        protected override void SpawnBuildDialog()
        {
            // Not used yet
        }

        protected override void TryLaunchVessel()
        {
            // Not used yet
        }
    }
}