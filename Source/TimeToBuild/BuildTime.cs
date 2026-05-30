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
        public string WorkFormula { get; private set; } = "0";
        public string RateFormula { get; private set; } = "1";
        public string OverheadFormula { get; private set; } = "0";
        public bool PerNewPart { get; private set; } = false;
        public bool PerReusedPart { get; private set; } = false;
        public bool WholeVessel { get; private set; } = false;

        public BuildTime(ConfigNode node, SpaceCenterFacility facility)
        {
            var identifier = new BuildTimeIdentifier();
            identifier.Facility = facility;
            if (node.HasValue("name")) identifier.Name = node.GetValue("name");
            if (node.HasValue("title")) Title = node.GetValue("title");
            if (node.HasValue("description")) Description = node.GetValue("description");
            if (node.HasValue("WorkFormula")) WorkFormula = node.GetValue("WorkFormula").Replace(" ", "");
            if (node.HasValue("RateFormula")) RateFormula = node.GetValue("RateFormula").Replace(" ", "");
            if (node.HasValue("OverheadFormula")) OverheadFormula = node.GetValue("OverheadFormula").Replace(" ", "");
            if (node.HasValue("PerNewPart")) PerNewPart = bool.Parse(node.GetValue("PerNewPart"));
            if (node.HasValue("PerReusedPart")) PerReusedPart = bool.Parse(node.GetValue("PerReusedPart"));
            if (node.HasValue("WholeVessel")) WholeVessel = bool.Parse(node.GetValue("WholeVessel"));
            Identifier = identifier;
        }
    }
}