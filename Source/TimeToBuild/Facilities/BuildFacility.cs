using System;
using System.Collections.Generic;
using System.Linq;
using TimeToBuild.Utils;
using static TimeToBuild.Utils.MiscUtils;
using TimeToBuild.Core;
using TimeToBuild.Work;

namespace TimeToBuild.Facilities
{
    public class BuildFacility : WorkFacility
    {
        private List<WorkItemVessel.BuildPart> PartsToBuild = new List<WorkItemVessel.BuildPart>();
        private WorkItemVessel VesselToLaunch = null;
        private LaunchScheduler LaunchScheduler => TimeToBuildManager.Instance.LaunchScheduler;

        public BuildFacility(SpaceCenterFacility facility) : base(facility)
        {

        }

        public override void OnWorkLoadComplete(WorkLoad workLoad)
        {
            if (workLoad.Vessel is null) return;

            VesselToLaunch = workLoad.Vessel;
        }

        public override List<WorkChunk> ComputeWorkChunks()
        {
            var workChunks = new List<WorkChunk>();

            if (UsingFacilities.Count == 0) return workChunks;

            var shipVariables = new Dictionary<string, double>
            {
                { VariableDryMass, 0 },
                { VariableWetMass, 0 },
                { VariableDryCost, 0 },
                { VariableWetCost, 0 },
                { VariableNumParts, PartsToBuild.Count }
            };

            var partVariables = new Dictionary<WorkItemVessel.BuildPart, Dictionary<string, double>>();
            foreach (var buildPart in PartsToBuild)
            {
                var variables = GetPartVariables(buildPart);
                partVariables[buildPart] = variables;

                shipVariables[VariableDryMass] += variables[VariableDryMass];
                shipVariables[VariableWetMass] += variables[VariableWetMass];
                shipVariables[VariableDryCost] += variables[VariableDryCost];
                shipVariables[VariableWetCost] += variables[VariableWetCost];
            }

            var timeUnitVariables = TimeToBuildManager.Instance.Calendar.GetTimeUnitVariables();

            foreach (var buildTime in TimeToBuildManager.Instance.Profile.BuildTimes.Values)
            {
                if (!UsingFacilities.Contains(buildTime.Identifier.Facility)) continue;

                var workChunk = new WorkChunk(buildTime.Identifier);
                workChunk.Work = 0;
                workChunk.Overhead = 0;

                var facilityVariables = GetFacilityVariables();
                facilityVariables["facility_level"] = GetFacilityLevel(buildTime.Identifier.Facility);

                if (buildTime.PerNewPart)
                {
                    foreach (var buildPart in PartsToBuild)
                    {
                        if (!buildPart.ReuseFromInventory)
                        {
                            workChunk.Work += FormulaParser.ParseAndComputeFormula(buildTime.TimeFormula.Work, timeUnitVariables, facilityVariables, partVariables[buildPart]);
                            workChunk.Overhead += FormulaParser.ParseAndComputeFormula(buildTime.TimeFormula.Overhead, timeUnitVariables, facilityVariables, partVariables[buildPart]);
                        }
                    }
                }
                if (buildTime.PerReusedPart)
                {
                    foreach (var buildPart in PartsToBuild)
                    {
                        if (buildPart.ReuseFromInventory)
                        {
                            workChunk.Work += FormulaParser.ParseAndComputeFormula(buildTime.TimeFormula.Work, timeUnitVariables, facilityVariables, partVariables[buildPart]);
                            workChunk.Overhead += FormulaParser.ParseAndComputeFormula(buildTime.TimeFormula.Overhead, timeUnitVariables, facilityVariables, partVariables[buildPart]);
                        }
                    }
                }
                if (buildTime.WholeVessel)
                {
                    workChunk.Work += FormulaParser.ParseAndComputeFormula(buildTime.TimeFormula.Work, timeUnitVariables, facilityVariables, shipVariables);
                    workChunk.Overhead += FormulaParser.ParseAndComputeFormula(buildTime.TimeFormula.Overhead, timeUnitVariables, facilityVariables, shipVariables);
                }

                if (workChunk.Work > 0 || workChunk.Overhead > 0) workChunks.Add(workChunk);
            }

            return workChunks;
        }

        public static List<WorkItemVessel.BuildPart> GatherBuildParts(List<Part> parts)
        {
            var buildParts = new List<WorkItemVessel.BuildPart>();

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

                    var buildPart = new WorkItemVessel.BuildPart();

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

        public override List<WorkChunk.WorkChunkDatum> GetWorkChunkData()
        {
            var workChunkData = new List<WorkChunk.WorkChunkDatum>();

            PartsToBuild = GatherBuildParts(EditorLogic.fetch.ship.parts);
            var numNewParts = PartsToBuild.Count(buildPart => !buildPart.ReuseFromInventory);
            var numReusedParts = PartsToBuild.Count(buildPart => buildPart.ReuseFromInventory);

            var workChunks = ComputeWorkChunks();

            var workRates = TimeToBuildManager.Instance.GetWorkRates();

            foreach (var workChunk in workChunks)
            {
                if (!TimeToBuildManager.Instance.Profile.BuildTimes.ContainsKey(workChunk.Identifier)) continue;

                var buildTimeConfig = TimeToBuildManager.Instance.Profile.BuildTimes[workChunk.Identifier];

                if (workChunk.Work > 0 || workChunk.Overhead > 0)
                {
                    var workChunkDatum = new WorkChunk.WorkChunkDatum();
                    workChunkDatum.Title = buildTimeConfig.Title;

                    var rate = workRates[workChunk.Identifier];
                    workChunkDatum.Duration = Convert.ToInt32(Math.Ceiling(workChunk.Work / rate + workChunk.Overhead));
                    if (workChunkDatum.Duration < 0) workChunkDatum.Duration = 0;
                    workChunkDatum.Duration = TimeToBuildManager.Instance.Calendar.RoundDuration(workChunkDatum.Duration);

                    if (buildTimeConfig.PerNewPart) workChunkDatum.NewPartCount = numNewParts;

                    if (buildTimeConfig.PerReusedPart) workChunkDatum.ReusedPartCount = numReusedParts;

                    workChunkData.Add(workChunkDatum);
                }
            }

            return workChunkData;
        }

        protected override void SetTotalWorkDuration(double workDuration)
        {
            base.SetTotalWorkDuration(workDuration);

            LaunchScheduler.LaunchTimeEarliest = CompletionTime;
        }

        public override void SpawnWorkCompleteDialog(WorkItem workItem)
        {
            var vessel = (WorkItemVessel)workItem;
            if (vessel is null) return;

            LaunchScheduler.LaunchTimeEarliest = CurrentTime;

            var optionLaunchNow = GetBuildDialogButton(LocalizerCache.LaunchNow, LaunchVesselNow, CurrentTime);
            var optionWarpToNextMorning = GetBuildDialogButton(LocalizerCache.WarpToNextMorning, LaunchVesselNextMorning, LaunchScheduler.LaunchTimeNextMorning);
            
            SpawnMultiOptionDialog(LocalizerCache.BuildComplete, vessel.ShipConstruct.shipName + " " + LocalizerCache.ReadyToLaunch, optionLaunchNow, optionWarpToNextMorning);
        }

        private void LaunchVessel()
        {
            if (LaunchScheduler is null || !LaunchScheduler.LaunchScheduled || VesselToLaunch is null) return;

            var tempFile = KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/Ships/temp.craft";
            VesselToLaunch.ShipConstruct.SaveShip().Save(tempFile);

            FlightDriver.StartWithNewLaunch(tempFile, VesselToLaunch.ShipConstruct.missionFlag, VesselToLaunch.LaunchSiteName, new VesselCrewManifest());

            VesselToLaunch = null;
        }

        private void LaunchVesselNow()
        {
            LaunchScheduler.ScheduleLaunch(CurrentTime, VesselToLaunch.ShipConstruct.shipName);

            LaunchVessel();
        }

        private void LaunchVesselNextMorning()
        {
            LaunchScheduler.ScheduleLaunch(LaunchScheduler.LaunchTimeNextMorning, VesselToLaunch.ShipConstruct.shipName);

            LaunchVessel();
        }

        private bool TryStartBuild(List<WorkChunk> workChunks, bool actuallyAddIt)
        {
            if (!HighLogic.LoadedSceneIsEditor) return false;

            var vessel = new WorkItemVessel(EditorLogic.fetch.launchSiteName, EditorLogic.fetch.ship);
            var workLoad = new WorkLoad(CurrentTime, workChunks, vessel);
            var success = TryAddWorkLoad(workLoad, actuallyAddIt);

            if (!success) SpawnMultiOptionDialog(LocalizerCache.CannotStartBuild, LocalizerCache.BuildFacilityBusy);

            return success;
        }

        public override void OnStartWork()
        {
            PartsToBuild = GatherBuildParts(EditorLogic.fetch.ship.parts);
            var workChunks = ComputeWorkChunks();

            if (TryStartBuild(workChunks, true))
            {
                var alarm = new AlarmTypeRaw();
                alarm.ut = LaunchScheduler.LaunchTimeEarliest;
                alarm.title = EditorLogic.fetch.ship.shipName + " " + LocalizerCache.BuildCompleteTitle;
                alarm.description = EditorLogic.fetch.ship.shipName + " " + LocalizerCache.BuildCompleteDescription;

                AlarmClockScenario.Instance.alarms.Add((uint)new System.Random().Next(), alarm);
            }
        }

        private void EditorLaunchVessel()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            if (Facility == SpaceCenterFacility.VehicleAssemblyBuilding || Facility == SpaceCenterFacility.SpaceplaneHangar)
            {
                EditorLogic.fetch.launchVessel();
            }
        }

        public void OnEditorLaunchEarliest()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            PartsToBuild = GatherBuildParts(EditorLogic.fetch.ship.parts);
            var workChunks = ComputeWorkChunks();

            if (TryStartBuild(workChunks, false))
            {
                LaunchScheduler.ScheduleLaunch(LaunchScheduler.LaunchTimeEarliest, EditorLogic.fetch.ship.shipName);
                EditorLaunchVessel();
            }
        }

        public void OnEditorLaunchNextMorning()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            PartsToBuild = GatherBuildParts(EditorLogic.fetch.ship.parts);
            var workChunks = ComputeWorkChunks();

            if (TryStartBuild(workChunks, false))
            {
                LaunchScheduler.ScheduleLaunch(LaunchScheduler.LaunchTimeNextMorning, EditorLogic.fetch.ship.shipName);
                EditorLaunchVessel();
            }
        }

        public override DialogGUIButton[] GetStartWorkDialogButtons()
        {
            return new DialogGUIButton[3]
            {
                GetBuildDialogButton(LocalizerCache.StartBuild, OnStartWork),
                GetBuildDialogButton(LocalizerCache.WarpToEarliestLaunch, OnEditorLaunchEarliest, LaunchScheduler.LaunchTimeEarliest),
                GetBuildDialogButton(LocalizerCache.WarpToNextMorning, OnEditorLaunchNextMorning, LaunchScheduler.LaunchTimeNextMorning)
            };
        }
    }
}
