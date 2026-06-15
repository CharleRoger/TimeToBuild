using System.Collections.Generic;

namespace TimeToBuild.Work
{
    public class WorkItemTech : WorkItem
    {
        [Persistent]
        public string TechID { get; private set; } = "";

        public WorkItemTech(double startTime, List<WorkChunk> workChunks, string techID) : base(startTime, workChunks)
        {
            TechID = techID;
        }

        public override ConfigNode Save()
        {
            var node = base.Save();

            node.AddValue("TechID", TechID);

            return node;
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);

            if (node.HasValue("TechID")) TechID = node.GetValue("TechID");
        }

        public WorkItemTech(ConfigNode node)
        {
            Load(node);
        }
    }
}
