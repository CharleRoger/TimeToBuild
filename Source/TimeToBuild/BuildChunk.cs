using static TimeToBuild.WorkTime;

namespace TimeToBuild
{
    public class WorkChunk
    {
        public WorkTimeIdentifier Identifier { get; private set; }
        public double Work = 0;
        public double Overhead = 0;
        public double CompletionTime = -1;

        public WorkChunk(WorkTimeIdentifier identifier)
        {
            Identifier = identifier;
        }

        public WorkChunk(ConfigNode node)
        {
            var identifier = new WorkTimeIdentifier();
            if (node.HasValue("name")) identifier.Name = node.GetValue("name");
            if (node.HasValue("Facility"))
            {
                switch (node.GetValue("Facility"))
                {
                    case "VehicleAssemblyBuilding":
                        identifier.Facility = SpaceCenterFacility.VehicleAssemblyBuilding;
                        break;
                    case "SpaceplaneHangar":
                        identifier.Facility = SpaceCenterFacility.SpaceplaneHangar;
                        break;
                    case "LaunchPad":
                        identifier.Facility = SpaceCenterFacility.LaunchPad;
                        break;
                    case "Runway":
                        identifier.Facility = SpaceCenterFacility.Runway;
                        break;
                    default:
                        break;
                }
            }
            if (node.HasValue("Work")) Work = double.Parse(node.GetValue("Work"));
            if (node.HasValue("Overhead")) Overhead = double.Parse(node.GetValue("Overhead"));
            if (node.HasValue("CompletionTime")) CompletionTime = double.Parse(node.GetValue("CompletionTime"));
            Identifier = identifier;
        }

        public ConfigNode GetConfigNode()
        {
            ConfigNode node = new ConfigNode();

            node.AddValue("name", Identifier.Name);
            switch (Identifier.Facility)
            {
                case SpaceCenterFacility.VehicleAssemblyBuilding:
                    node.AddValue("Facility", "VehicleAssemblyBuilding");
                    break;
                case SpaceCenterFacility.SpaceplaneHangar:
                    node.AddValue("Facility", "SpaceplaneHangar");
                    break;
                case SpaceCenterFacility.LaunchPad:
                    node.AddValue("Facility", "LaunchPad");
                    break;
                case SpaceCenterFacility.Runway:
                    node.AddValue("Facility", "Runway");
                    break;
                default:
                    break;
            }
            node.AddValue("Work", Work);
            node.AddValue("Overhead", Overhead);
            if (CompletionTime > 0) node.AddValue("CompletionTime", CompletionTime);

            return node;
        }

        public struct WorkChunkDatum
        {
            public string Title;
            public int Duration;
            public int NewPartCount;
            public int ReusedPartCount;
        }
    }
}
