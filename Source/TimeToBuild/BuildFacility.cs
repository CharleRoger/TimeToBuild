using System.Collections.Generic;

namespace TimeToBuild
{
    public class BuildFacility
    {
        [Persistent]
        public List<BuildVessel> BuildVessels { get; private set; } = new List<BuildVessel>();

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
    }
}
