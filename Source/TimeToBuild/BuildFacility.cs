using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TimeToBuild.TimeToBuildUtils;

namespace TimeToBuild
{
    public class BuildFacility
    {
        private SpaceCenterFacility Facility;
        private List<SpaceCenterFacility> UsingFacilities = new List<SpaceCenterFacility>();

        [Persistent]
        public List<WorkLoad> WorkLoads { get; private set; } = new List<WorkLoad>();
        private BuildVessel BuildVesselToLaunch = null;

        private LaunchScheduler LaunchScheduler => TimeToBuild.Instance.LaunchScheduler;

        public BuildFacility(SpaceCenterFacility facility)
        {
            Facility = facility;

            UsingFacilities.Add(facility);
            if (facility == SpaceCenterFacility.VehicleAssemblyBuilding) UsingFacilities.Add(SpaceCenterFacility.LaunchPad);
            if (facility == SpaceCenterFacility.SpaceplaneHangar) UsingFacilities.Add(SpaceCenterFacility.Runway);
        }

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

        public List<WorkChunk> ComputeWorkChunks(List<BuildPart> buildParts)
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

            var timeUnitVariables = LaunchScheduler.Calendar.GetTimeUnitVariables();

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

        public List<WorkChunk.WorkChunkDatum> GetWorkChunkData()
        {
            var workChunkData = new List<WorkChunk.WorkChunkDatum>();

            var buildParts = GatherBuildParts(EditorLogic.fetch.ship.parts);
            var numNewParts = buildParts.Count(buildPart => !buildPart.ReuseFromInventory);
            var numReusedParts = buildParts.Count(buildPart => buildPart.ReuseFromInventory);

            var workChunks = ComputeWorkChunks(buildParts);

            var buildRates = LaunchScheduler.GetBuildRates();

            foreach (var workChunk in workChunks)
            {
                if (!TimeToBuild.Instance.Profile.BuildTimes.ContainsKey(workChunk.Identifier)) continue;

                var buildTimeConfig = TimeToBuild.Instance.Profile.BuildTimes[workChunk.Identifier];

                if (workChunk.Work > 0 || workChunk.Overhead > 0)
                {
                    var workChunkDatum = new WorkChunk.WorkChunkDatum();
                    workChunkDatum.Title = buildTimeConfig.Title;

                    var rate = buildRates[workChunk.Identifier];
                    workChunkDatum.Duration = Convert.ToInt32(Math.Ceiling(workChunk.Work / rate + workChunk.Overhead));
                    if (workChunkDatum.Duration < 0) workChunkDatum.Duration = 0;
                    workChunkDatum.Duration = LaunchScheduler.Calendar.RoundDuration(workChunkDatum.Duration);

                    if (buildTimeConfig.PerNewPart) workChunkDatum.NewPartCount = numNewParts;

                    if (buildTimeConfig.PerReusedPart) workChunkDatum.ReusedPartCount = numReusedParts;

                    workChunkData.Add(workChunkDatum);
                }
            }

            return workChunkData;
        }

        public void SpawnBuildDialog()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            var workChunkData = GetWorkChunkData();

            var title = "";
            var totalBuildTime = 0;
            foreach (var workChunkDatum in workChunkData)
            {
                title += workChunkDatum.Title;

                totalBuildTime += workChunkDatum.Duration;

                if (workChunkDatum.NewPartCount > 0 || workChunkDatum.ReusedPartCount > 0)
                {
                    title += " (";
                    if (workChunkDatum.NewPartCount > 0) title += workChunkDatum.NewPartCount.ToString() + " " + (workChunkDatum.NewPartCount > 1 ? LocalizerCache.NewParts : LocalizerCache.NewPart);
                    if (workChunkDatum.NewPartCount > 0 && workChunkDatum.ReusedPartCount > 0) title += ", ";
                    if (workChunkDatum.ReusedPartCount > 0) title += workChunkDatum.ReusedPartCount.ToString() + " " + (workChunkDatum.ReusedPartCount > 1 ? LocalizerCache.ReusedParts : LocalizerCache.ReusedPart);
                    title += ")";
                }

                title += "\n" + LaunchScheduler.Calendar.GetDurationString(workChunkDatum.Duration) + "\n\n";
            }
            title += LocalizerCache.Total + "\n" + LaunchScheduler.Calendar.GetDurationString(totalBuildTime) + "\n\n";

            LaunchScheduler.LaunchTimeEarliest = TimeToBuild.Instance.Scenario.EditorStartTime + totalBuildTime;

            var message = "";
            foreach (var date in LaunchScheduler.GetSalientDates()) message += LaunchScheduler.Calendar.GetDateString(date.Item1) + " — " + date.Item2 + "\n";

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

        public void UpdateWorkLoad(int workLoadIndex, Dictionary<BuildTime.BuildTimeIdentifier, double> buildRates)
        {
            var workLoad = WorkLoads[workLoadIndex];

            bool workComplete = workLoad.UpdateWorkDone(buildRates);

            if (workComplete)
            {
                WorkLoads.RemoveAt(workLoadIndex);

                TimeWarp.SetRate(0, true);

                if (!(workLoad.BuildVessel is null))
                {
                    BuildVesselToLaunch = workLoad.BuildVessel;
                    SpawnLaunchDialog();
                }
            }
        }

        public IEnumerator UpdateWorkLoads_Coroutine()
        {
            while (TimeToBuild.Instance is null || LaunchScheduler is null || HighLogic.LoadedSceneIsEditor) yield return new WaitForFixedUpdate();

            while (true)
            {
                var buildRates = LaunchScheduler.GetBuildRates();

                for (int workLoadIndex = 0; workLoadIndex < WorkLoads.Count; workLoadIndex++)
                {
                    UpdateWorkLoad(workLoadIndex, buildRates);
                }

                yield return new WaitForFixedUpdate();
            }
        }

        private void LaunchBuildVessel()
        {
            if (!LaunchScheduler.LaunchScheduled || BuildVesselToLaunch is null) return;

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

        public bool TryAddWorkLoad(WorkLoad workLoad, bool actuallyAddIt)
        {
            if (WorkLoads.Count > 0) return false;

            if (actuallyAddIt) WorkLoads.Add(workLoad);

            return true;
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
            var workChunks = ComputeWorkChunks(buildParts);

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
            var workChunks = ComputeWorkChunks(buildParts);

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
            var workChunks = ComputeWorkChunks(buildParts);

            if (TryStartBuild(workChunks, false))
            {
                LaunchScheduler.ScheduleLaunch(LaunchScheduler.LaunchTimeNextMorning, EditorLogic.fetch.ship.shipName);
                EditorLaunchVessel();
            }
        }
    }
}
