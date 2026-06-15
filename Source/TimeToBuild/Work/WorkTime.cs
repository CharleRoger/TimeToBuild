namespace TimeToBuild
{
    public class WorkTime
    {
        public struct WorkTimeIdentifier
        {
            public string Name;
            public SpaceCenterFacility Facility;
        }

        public WorkTimeIdentifier Identifier { get; protected set; }
        public string Title { get; private set; } = "";
        public string Description { get; private set; } = "";
        public TimeFormula TimeFormula { get; private set; }

        public WorkTime(ConfigNode node, SpaceCenterFacility facility)
        {
            var identifier = new WorkTimeIdentifier();
            identifier.Facility = facility;
            if (node.HasValue("name")) identifier.Name = node.GetValue("name");
            if (node.HasValue("title")) Title = node.GetValue("title");
            if (node.HasValue("description")) Description = node.GetValue("description");
            if (node.HasNode("TimeFormula")) TimeFormula = new TimeFormula(node.GetNode("TimeFormula"));
            Identifier = identifier;
        }
    }

    public class BuildTime : WorkTime
    {
        public bool PerNewPart { get; private set; } = false;
        public bool PerReusedPart { get; private set; } = false;
        public bool WholeVessel { get; private set; } = false;

        public BuildTime(ConfigNode node, SpaceCenterFacility facility) : base(node, facility)
        {
            if (node.HasValue("PerNewPart")) PerNewPart = bool.Parse(node.GetValue("PerNewPart"));
            if (node.HasValue("PerReusedPart")) PerReusedPart = bool.Parse(node.GetValue("PerReusedPart"));
            if (node.HasValue("WholeVessel")) WholeVessel = bool.Parse(node.GetValue("WholeVessel"));
        }
    }

    public class ResearchTime : WorkTime
    {
        public ResearchTime(ConfigNode node) : base(node, SpaceCenterFacility.ResearchAndDevelopment)
        {

        }
    }
}