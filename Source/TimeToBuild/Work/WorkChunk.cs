using System;
using static TimeToBuild.Work.WorkTime;

namespace TimeToBuild.Work
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
            if (node.HasValue("Facility")) Enum.TryParse(node.GetValue("Facility"), out identifier.Facility);
            if (node.HasValue("Work")) Work = double.Parse(node.GetValue("Work"));
            if (node.HasValue("Overhead")) Overhead = double.Parse(node.GetValue("Overhead"));
            if (node.HasValue("CompletionTime")) CompletionTime = double.Parse(node.GetValue("CompletionTime"));
            Identifier = identifier;
        }

        public ConfigNode GetConfigNode()
        {
            ConfigNode node = new ConfigNode();

            node.AddValue("name", Identifier.Name);
            node.AddValue("Facility", Identifier.Facility.ToString());
            node.AddValue("Work", Work);
            node.AddValue("Overhead", Overhead);
            if (CompletionTime > 0) node.AddValue("CompletionTime", CompletionTime);

            return node;
        }
    }
}
