using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TimeToBuild.TimeToBuildUtils;

namespace TimeToBuild
{
    public abstract class WorkFacility
    {
        public List<WorkLoad> WorkLoads { get; private set; } = new List<WorkLoad>();
        public SpaceCenterFacility Facility { get; private set; }
        public List<SpaceCenterFacility> UsingFacilities { get; private set; } = new List<SpaceCenterFacility>();

        public WorkFacility(SpaceCenterFacility facility)
        {
            Facility = facility;

            UsingFacilities.Add(facility);
            if (facility == SpaceCenterFacility.VehicleAssemblyBuilding) UsingFacilities.Add(SpaceCenterFacility.LaunchPad);
            if (facility == SpaceCenterFacility.SpaceplaneHangar) UsingFacilities.Add(SpaceCenterFacility.Runway);
        }

        public abstract void OnWorkLoadComplete(WorkLoad workLoad);

        public void Load(ConfigNode node)
        {
            foreach (var workLoadNode in node.GetNodes("WorkLoad"))
            {
                WorkLoads.Add(new WorkLoad(workLoadNode));
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (var buildVessel in WorkLoads)
            {
                node.AddNode("WorkLoad", buildVessel.GetConfigNode());
            }
        }

        public void UpdateWorkLoad(int workLoadIndex, Dictionary<WorkTime.WorkTimeIdentifier, double> buildRates)
        {
            var workLoad = WorkLoads[workLoadIndex];

            bool workComplete = workLoad.UpdateWorkDone(buildRates);

            if (workComplete)
            {
                WorkLoads.RemoveAt(workLoadIndex);

                TimeWarp.SetRate(0, true);

                OnWorkLoadComplete(workLoad);
            }
        }

        public IEnumerator UpdateWorkLoads_Coroutine()
        {
            while (TimeToBuild.Instance is null || HighLogic.LoadedSceneIsEditor) yield return new WaitForFixedUpdate();

            while (true)
            {
                var buildRates = TimeToBuild.Instance.GetBuildRates();

                for (int workLoadIndex = 0; workLoadIndex < WorkLoads.Count; workLoadIndex++)
                {
                    UpdateWorkLoad(workLoadIndex, buildRates);
                }

                yield return new WaitForFixedUpdate();
            }
        }

        public bool TryAddWorkLoad(WorkLoad workLoad, bool actuallyAddIt)
        {
            if (WorkLoads.Count > 0) return false;

            if (actuallyAddIt) WorkLoads.Add(workLoad);

            return true;
        }
    }

    public class BuildFacility : WorkFacility
    {
        private BuildVessel BuildVesselToLaunch = null;
        private LaunchScheduler LaunchScheduler => TimeToBuild.Instance.LaunchScheduler;

        public BuildFacility(SpaceCenterFacility facility) : base(facility)
        {

        }

        public override void OnWorkLoadComplete(WorkLoad workLoad)
        {
            if (!(workLoad.BuildVessel is null))
            {
                BuildVesselToLaunch = workLoad.BuildVessel;
                SpawnLaunchDialog();
            }
        }

        public List<WorkChunk> ComputeBuildWorkChunks(List<BuildPart> buildParts)
        {
            var workChunks = new List<WorkChunk>();

            if (UsingFacilities.Count == 0) return workChunks;

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

            var timeUnitVariables = TimeToBuild.Instance.Calendar.GetTimeUnitVariables();

            foreach (var buildTime in TimeToBuild.Instance.Profile.BuildTimes.Values)
            {
                if (!UsingFacilities.Contains(buildTime.Identifier.Facility)) continue;

                var workChunk = new WorkChunk(buildTime.Identifier);
                workChunk.Work = 0;
                workChunk.Overhead = 0;

                var facilityVariables = GetFacilityVariables();
                facilityVariables["facility_level"] = GetFacilityLevel(buildTime.Identifier.Facility);

                if (buildTime.PerNewPart)
                {
                    foreach (var buildPart in buildParts)
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
                    foreach (var buildPart in buildParts)
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

        public static List<BuildPart> GatherBuildParts(List<Part> parts)
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

        public List<WorkChunk.BuildDatum> GetBuildData()
        {
            var buildData = new List<WorkChunk.BuildDatum>();

            var buildParts = GatherBuildParts(EditorLogic.fetch.ship.parts);
            var numNewParts = buildParts.Count(buildPart => !buildPart.ReuseFromInventory);
            var numReusedParts = buildParts.Count(buildPart => buildPart.ReuseFromInventory);

            var workChunks = ComputeBuildWorkChunks(buildParts);

            var buildRates = TimeToBuild.Instance.GetBuildRates();

            foreach (var workChunk in workChunks)
            {
                if (!TimeToBuild.Instance.Profile.BuildTimes.ContainsKey(workChunk.Identifier)) continue;

                var buildTimeConfig = TimeToBuild.Instance.Profile.BuildTimes[workChunk.Identifier];

                if (workChunk.Work > 0 || workChunk.Overhead > 0)
                {
                    var buildDatum = new WorkChunk.BuildDatum();
                    buildDatum.Title = buildTimeConfig.Title;

                    var rate = buildRates[workChunk.Identifier];
                    buildDatum.Duration = Convert.ToInt32(Math.Ceiling(workChunk.Work / rate + workChunk.Overhead));
                    if (buildDatum.Duration < 0) buildDatum.Duration = 0;
                    buildDatum.Duration = TimeToBuild.Instance.Calendar.RoundDuration(buildDatum.Duration);

                    if (buildTimeConfig.PerNewPart) buildDatum.NewPartCount = numNewParts;

                    if (buildTimeConfig.PerReusedPart) buildDatum.ReusedPartCount = numReusedParts;

                    buildData.Add(buildDatum);
                }
            }

            return buildData;
        }

        public void SpawnBuildDialog()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            var buildData = GetBuildData();

            var title = "";
            var totalBuildTime = 0;
            foreach (var buildDatum in buildData)
            {
                title += buildDatum.Title;

                totalBuildTime += buildDatum.Duration;

                if (buildDatum.NewPartCount > 0 || buildDatum.ReusedPartCount > 0)
                {
                    title += " (";
                    if (buildDatum.NewPartCount > 0) title += buildDatum.NewPartCount.ToString() + " " + (buildDatum.NewPartCount > 1 ? LocalizerCache.NewParts : LocalizerCache.NewPart);
                    if (buildDatum.NewPartCount > 0 && buildDatum.ReusedPartCount > 0) title += ", ";
                    if (buildDatum.ReusedPartCount > 0) title += buildDatum.ReusedPartCount.ToString() + " " + (buildDatum.ReusedPartCount > 1 ? LocalizerCache.ReusedParts : LocalizerCache.ReusedPart);
                    title += ")";
                }

                title += "\n" + TimeToBuild.Instance.Calendar.GetDurationString(buildDatum.Duration) + "\n\n";
            }
            title += LocalizerCache.Total + "\n" + TimeToBuild.Instance.Calendar.GetDurationString(totalBuildTime) + "\n\n";

            LaunchScheduler.LaunchTimeEarliest = TimeToBuild.Instance.Scenario.EditorStartTime + totalBuildTime;

            var message = "";
            foreach (var date in LaunchScheduler.GetSalientDates()) message += TimeToBuild.Instance.Calendar.GetDateString(date.Item1) + " — " + date.Item2 + "\n";

            var optionStartBuild = GetBuildDialogButton(LocalizerCache.StartBuild, OnStartBuild);
            var optionWarpToEarliestLaunch = GetBuildDialogButton(LocalizerCache.WarpToEarliestLaunch, OnEditorLaunchEarliest, LaunchScheduler.LaunchTimeEarliest);
            var optionWarpToNextMorning = GetBuildDialogButton(LocalizerCache.WarpToNextMorning, OnEditorLaunchNextMorning, LaunchScheduler.LaunchTimeNextMorning);

            SpawnMultiOptionDialog(title, message, optionStartBuild, optionWarpToEarliestLaunch, optionWarpToNextMorning);
        }

        public void SpawnLaunchDialog()
        {
            LaunchScheduler.LaunchTimeEarliest = CurrentTime;

            var optionLaunchNow = GetBuildDialogButton(LocalizerCache.LaunchNow, LaunchBuildVesselNow, CurrentTime);
            var optionWarpToNextMorning = GetBuildDialogButton(LocalizerCache.WarpToNextMorning, LaunchBuildVesselNextMorning, LaunchScheduler.LaunchTimeNextMorning);
            
            SpawnMultiOptionDialog(LocalizerCache.BuildComplete, BuildVesselToLaunch.ShipConstruct.shipName + " " + LocalizerCache.ReadyToLaunch, optionLaunchNow, optionWarpToNextMorning);
        }

        private void LaunchBuildVessel()
        {
            if (LaunchScheduler is null || !LaunchScheduler.LaunchScheduled || BuildVesselToLaunch is null) return;

            var tempFile = KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/Ships/temp.craft";
            BuildVesselToLaunch.ShipConstruct.SaveShip().Save(tempFile);

            FlightDriver.StartWithNewLaunch(tempFile, BuildVesselToLaunch.ShipConstruct.missionFlag, BuildVesselToLaunch.LaunchSiteName, new VesselCrewManifest());

            BuildVesselToLaunch = null;
        }

        private void LaunchBuildVesselNow()
        {
            LaunchScheduler.ScheduleLaunch(CurrentTime, BuildVesselToLaunch.ShipConstruct.shipName);

            LaunchBuildVessel();
        }

        private void LaunchBuildVesselNextMorning()
        {
            LaunchScheduler.ScheduleLaunch(LaunchScheduler.LaunchTimeNextMorning, BuildVesselToLaunch.ShipConstruct.shipName);

            LaunchBuildVessel();
        }

        private bool TryStartBuild(List<WorkChunk> workChunks, bool actuallyAddIt)
        {
            var success = true;
            if (HighLogic.LoadedSceneIsEditor)
            {
                var buildVessel = new BuildVessel(EditorLogic.fetch.launchSiteName, EditorLogic.fetch.ship);
                var workLoad = new WorkLoad(TimeToBuild.Instance.Scenario.EditorStartTime, workChunks, buildVessel);
                success = TryAddWorkLoad(workLoad, actuallyAddIt);

                if (!success) SpawnMultiOptionDialog(LocalizerCache.CannotStartBuild, LocalizerCache.FacilityBusy);
            }

            return success;
        }

        public void OnStartBuild()
        {
            var buildParts = GatherBuildParts(EditorLogic.fetch.ship.parts);
            var workChunks = ComputeBuildWorkChunks(buildParts);

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

            var buildParts = GatherBuildParts(EditorLogic.fetch.ship.parts);
            var workChunks = ComputeBuildWorkChunks(buildParts);

            if (TryStartBuild(workChunks, false))
            {
                LaunchScheduler.ScheduleLaunch(LaunchScheduler.LaunchTimeEarliest, EditorLogic.fetch.ship.shipName);
                EditorLaunchVessel();
            }
        }

        public void OnEditorLaunchNextMorning()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            var buildParts = GatherBuildParts(EditorLogic.fetch.ship.parts);
            var workChunks = ComputeBuildWorkChunks(buildParts);

            if (TryStartBuild(workChunks, false))
            {
                LaunchScheduler.ScheduleLaunch(LaunchScheduler.LaunchTimeNextMorning, EditorLogic.fetch.ship.shipName);
                EditorLaunchVessel();
            }
        }
    }
}
