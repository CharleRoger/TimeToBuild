using System.Collections.Generic;
using static TimeToBuild.TimeToBuildUtils;

namespace TimeToBuild
{
    public class BuildFacility
    {
        [Persistent]
        public List<BuildVessel> BuildVessels { get; private set; } = new List<BuildVessel>();

        private BuildVessel VesselToLaunch = null;

        public BuildFacility()
        {

        }

        public BuildFacility(ConfigNode node)
        {
            foreach (var buildVesselNode in node.GetNodes("BuildVessel"))
            {
                BuildVessels.Add(new BuildVessel(buildVesselNode));
            }
        }

        public ConfigNode GetConfigNode()
        {
            ConfigNode node = new ConfigNode();

            foreach (var buildVessel in BuildVessels)
            {
                node.AddNode("BuildVessel", buildVessel.GetConfigNode());
            }

            return node;
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
                VesselToLaunch = BuildVessels[vesselIndex];

                BuildVessels.RemoveAt(vesselIndex);

                TimeWarp.SetRate(0, true);

                var optionLaunchNow = GetBuildDialogButton(LocalizerCache.LaunchNow, LaunchVessel);
                SpawnMultiOptionDialog(LocalizerCache.BuildComplete, buildVessel.ShipConstruct.shipName + " " + LocalizerCache.ReadyToLaunch, optionLaunchNow);
            }
        }

        private void LaunchVessel()
        {
            var tempFile = KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/Ships/temp.craft";
            VesselToLaunch.ShipConstruct.SaveShip().Save(tempFile);

            FlightDriver.StartWithNewLaunch(tempFile, VesselToLaunch.ShipConstruct.missionFlag, VesselToLaunch.LaunchSiteName, new VesselCrewManifest());
        }
    }
}
