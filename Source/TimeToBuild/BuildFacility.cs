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
        public List<BuildVessel> BuildVessels { get; private set; } = new List<BuildVessel>();
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
            foreach (var buildVesselNode in node.GetNodes("BuildVessel"))
            {
                BuildVessels.Add(new BuildVessel(buildVesselNode));
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (var buildVessel in BuildVessels)
            {
                node.AddNode("BuildVessel", buildVessel.GetConfigNode());
            }
        }

        public List<BuildChunk> ComputeBuildChunks(List<BuildPart> buildParts)
        {
            var buildChunks = new List<BuildChunk>();

            if (UsingFacilities.Count == 0) return buildChunks;

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

        public List<BuildChunk.BuildChunkDatum> GetBuildChunkData()
        {
            var buildChunkData = new List<BuildChunk.BuildChunkDatum>();

            var buildParts = GatherBuildParts(EditorLogic.fetch.ship.parts);
            var numNewParts = buildParts.Count(buildPart => !buildPart.ReuseFromInventory);
            var numReusedParts = buildParts.Count(buildPart => buildPart.ReuseFromInventory);

            var buildChunks = ComputeBuildChunks(buildParts);

            var buildRates = LaunchScheduler.GetBuildRates();

            foreach (var buildChunk in buildChunks)
            {
                if (!TimeToBuild.Instance.Profile.BuildTimes.ContainsKey(buildChunk.Identifier)) continue;

                var buildTimeConfig = TimeToBuild.Instance.Profile.BuildTimes[buildChunk.Identifier];

                if (buildChunk.Work > 0 || buildChunk.Overhead > 0)
                {
                    var buildChunkDatum = new BuildChunk.BuildChunkDatum();
                    buildChunkDatum.Title = buildTimeConfig.Title;

                    var rate = buildRates[buildChunk.Identifier];
                    buildChunkDatum.Duration = Convert.ToInt32(Math.Ceiling(buildChunk.Work / rate + buildChunk.Overhead));
                    if (buildChunkDatum.Duration < 0) buildChunkDatum.Duration = 0;
                    buildChunkDatum.Duration = LaunchScheduler.Calendar.RoundDuration(buildChunkDatum.Duration);

                    if (buildTimeConfig.PerNewPart) buildChunkDatum.NewPartCount = numNewParts;

                    if (buildTimeConfig.PerReusedPart) buildChunkDatum.ReusedPartCount = numReusedParts;

                    buildChunkData.Add(buildChunkDatum);
                }
            }

            return buildChunkData;
        }

        public void SpawnBuildDialog()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            var buildChunkData = GetBuildChunkData();

            var title = "";
            var totalBuildTime = 0;
            foreach (var buildChunkDatum in buildChunkData)
            {
                title += buildChunkDatum.Title;

                totalBuildTime += buildChunkDatum.Duration;

                if (buildChunkDatum.NewPartCount > 0 || buildChunkDatum.ReusedPartCount > 0)
                {
                    title += " (";
                    if (buildChunkDatum.NewPartCount > 0) title += buildChunkDatum.NewPartCount.ToString() + " " + (buildChunkDatum.NewPartCount > 1 ? LocalizerCache.NewParts : LocalizerCache.NewPart);
                    if (buildChunkDatum.NewPartCount > 0 && buildChunkDatum.ReusedPartCount > 0) title += ", ";
                    if (buildChunkDatum.ReusedPartCount > 0) title += buildChunkDatum.ReusedPartCount.ToString() + " " + (buildChunkDatum.ReusedPartCount > 1 ? LocalizerCache.ReusedParts : LocalizerCache.ReusedPart);
                    title += ")";
                }

                title += "\n" + LaunchScheduler.Calendar.GetDurationString(buildChunkDatum.Duration) + "\n\n";
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

        public void UpdateWorkDoneOnBuildVessel(int vesselIndex, Dictionary<BuildTime.BuildTimeIdentifier, double> buildRates)
        {
            var buildVessel = BuildVessels[vesselIndex];

            bool buildComplete = buildVessel.UpdateWorkDone(buildRates);

            if (buildComplete)
            {
                BuildVesselToLaunch = buildVessel;

                BuildVessels.RemoveAt(vesselIndex);

                TimeWarp.SetRate(0, true);

                SpawnLaunchDialog();
            }
        }

        public IEnumerator UpdateBuildVessels_Coroutine()
        {
            while (TimeToBuild.Instance is null || LaunchScheduler is null || HighLogic.LoadedSceneIsEditor) yield return new WaitForFixedUpdate();

            while (true)
            {
                var buildRates = LaunchScheduler.GetBuildRates();

                for (int vesselIndex = 0; vesselIndex < BuildVessels.Count; vesselIndex++)
                {
                    UpdateWorkDoneOnBuildVessel(vesselIndex, buildRates);
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

        public bool TryAddBuildVessel(BuildVessel buildVessel, bool actuallyAddIt)
        {
            if (BuildVessels.Count > 0) return false;

            if (actuallyAddIt) BuildVessels.Add(buildVessel);

            return true;
        }

        private bool TryStartBuild(List<BuildChunk> buildChunks, bool actuallyAddIt)
        {
            var success = true;
            if (HighLogic.LoadedSceneIsEditor)
            {
                var buildVessel = new BuildVessel(EditorLogic.fetch.ship, EditorLogic.fetch.launchSiteName, TimeToBuild.Instance.Scenario.EditorStartTime, buildChunks);

                success = TryAddBuildVessel(buildVessel, actuallyAddIt);

                if (!success) SpawnMultiOptionDialog(LocalizerCache.CannotStartBuild, LocalizerCache.FacilityBusy);
            }

            return success;
        }

        public void OnStartBuild()
        {
            var buildParts = GatherBuildParts(EditorLogic.fetch.ship.parts);
            var buildChunks = ComputeBuildChunks(buildParts);

            if (TryStartBuild(buildChunks, true))
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
            var buildChunks = ComputeBuildChunks(buildParts);

            if (TryStartBuild(buildChunks, false))
            {
                LaunchScheduler.ScheduleLaunch(LaunchScheduler.LaunchTimeEarliest, EditorLogic.fetch.ship.shipName);
                EditorLaunchVessel();
            }
        }

        public void OnEditorLaunchNextMorning()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            var buildParts = GatherBuildParts(EditorLogic.fetch.ship.parts);
            var buildChunks = ComputeBuildChunks(buildParts);

            if (TryStartBuild(buildChunks, false))
            {
                LaunchScheduler.ScheduleLaunch(LaunchScheduler.LaunchTimeNextMorning, EditorLogic.fetch.ship.shipName);
                EditorLaunchVessel();
            }
        }
    }
}
