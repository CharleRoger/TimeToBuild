namespace TimeToBuild
{
    public class BuildTime
    {
        public struct BuildTimeIdentifier
        {
            public string Name;
            public SpaceCenterFacility Facility;
        }

        public BuildTimeIdentifier Identifier { get; private set; }
        public string Title { get; private set; } = "";
        public string Description { get; private set; } = "";
        public bool PerNewPart { get; private set; } = false;
        public bool PerReusedPart { get; private set; } = false;
        public bool WholeVessel { get; private set; } = false;
        public TimeFormula TimeFormula { get; private set; }

        public BuildTime(ConfigNode node, SpaceCenterFacility facility)
        {
            var identifier = new BuildTimeIdentifier();
            identifier.Facility = facility;
            if (node.HasValue("name")) identifier.Name = node.GetValue("name");
            if (node.HasValue("title")) Title = node.GetValue("title");
            if (node.HasValue("description")) Description = node.GetValue("description");
            if (node.HasValue("PerNewPart")) PerNewPart = bool.Parse(node.GetValue("PerNewPart"));
            if (node.HasValue("PerReusedPart")) PerReusedPart = bool.Parse(node.GetValue("PerReusedPart"));
            if (node.HasValue("WholeVessel")) WholeVessel = bool.Parse(node.GetValue("WholeVessel"));
            if (node.HasNode("TimeFormula")) TimeFormula = new TimeFormula(node.GetNode("TimeFormula"));
            Identifier = identifier;
        }
    }
}