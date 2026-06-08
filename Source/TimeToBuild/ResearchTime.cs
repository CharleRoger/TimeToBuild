namespace TimeToBuild
{
    public class ResearchTime
    {
        public string Name;
        public TimeFormula TimeFormula { get; private set; }

        public ResearchTime(ConfigNode node)
        {
            if (node.HasValue("name")) Name = node.GetValue("name");
            if (node.HasNode("TimeFormula")) TimeFormula = new TimeFormula(node.GetNode("TimeFormula"));
        }
    }
}