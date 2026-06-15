namespace TimeToBuild.Work
{
    public class WorkItemTech : WorkItem
    {
        [Persistent]
        public string TechID { get; private set; } = "";

        public WorkItemTech(string techID)
        {
            TechID = techID;
        }

        public override ConfigNode Save()
        {
            ConfigNode node = new ConfigNode();

            node.AddValue("TechID", TechID);

            return node;
        }

        public override void Load(ConfigNode node)
        {
            if (node.HasValue("TechID")) TechID = node.GetValue("TechID");
        }

        public WorkItemTech(ConfigNode node)
        {
            Load(node);
        }
    }
}
