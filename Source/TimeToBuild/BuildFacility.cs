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

            var timeUnitVariables = TimeToBuild.Instance.LaunchScheduler.Calendar.GetTimeUnitVariables();

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

        public bool TryAddBuildVessel(BuildVessel buildVessel, bool actuallyAddIt)
        {
            if (BuildVessels.Count > 0) return false;

            if (actuallyAddIt) BuildVessels.Add(buildVessel);

            return true;
        }

        public void UpdateWorkDoneOnBuildVessel(int vesselIndex, Dictionary<BuildTime.BuildTimeIdentifier, double> buildRates)
        {
            var buildVessel = BuildVessels[vesselIndex];

            bool buildComplete = buildVessel.UpdateWorkDone(buildRates);

            if (buildComplete)
            {
                BuildVesselToLaunch = BuildVessels[vesselIndex];

                BuildVessels.RemoveAt(vesselIndex);

                TimeWarp.SetRate(0, true);

                var optionLaunchNow = GetBuildDialogButton(LocalizerCache.LaunchNow, LaunchBuildVessel);
                SpawnMultiOptionDialog(LocalizerCache.BuildComplete, buildVessel.ShipConstruct.shipName + " " + LocalizerCache.ReadyToLaunch, optionLaunchNow);
            }
        }

        public IEnumerator UpdateBuildVessels_Coroutine()
        {
            while (TimeToBuild.Instance is null || TimeToBuild.Instance.LaunchScheduler is null || HighLogic.LoadedSceneIsEditor) yield return new WaitForFixedUpdate();

            while (true)
            {
                var buildRates = TimeToBuild.Instance.LaunchScheduler.GetBuildRates();

                for (int vesselIndex = 0; vesselIndex < BuildVessels.Count; vesselIndex++)
                {
                    UpdateWorkDoneOnBuildVessel(vesselIndex, buildRates);
                }

                yield return new WaitForFixedUpdate();
            }
        }

        private void LaunchBuildVessel()
        {
            var tempFile = KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/Ships/temp.craft";
            BuildVesselToLaunch.ShipConstruct.SaveShip().Save(tempFile);

            FlightDriver.StartWithNewLaunch(tempFile, BuildVesselToLaunch.ShipConstruct.missionFlag, BuildVesselToLaunch.LaunchSiteName, new VesselCrewManifest());
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

            TryStartBuild(buildChunks, true);
        }

        private void TryLaunchVessel()
        {
            if (Facility == SpaceCenterFacility.VehicleAssemblyBuilding || Facility == SpaceCenterFacility.SpaceplaneHangar)
            {
                EditorLogic.fetch.launchVessel();
            }
        }

        public void OnTryLaunchEarliest()
        {
            var buildParts = GatherBuildParts(EditorLogic.fetch.ship.parts);
            var buildChunks = ComputeBuildChunks(buildParts);

            if (TryStartBuild(buildChunks, false))
            {
                TimeToBuild.Instance.LaunchScheduler.SetLaunchTimeToEarliest();
                TryLaunchVessel();
            }
        }

        public void OnTryLaunchNextMorning()
        {
            var buildParts = GatherBuildParts(EditorLogic.fetch.ship.parts);
            var buildChunks = ComputeBuildChunks(buildParts);

            if (TryStartBuild(buildChunks, false))
            {
                TimeToBuild.Instance.LaunchScheduler.SetLaunchTimeToNextMorning();
                TryLaunchVessel();
            }
        }
    }
}
