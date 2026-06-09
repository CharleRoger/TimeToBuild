namespace TimeToBuild
{
    public class TimeFormula
    {
        public string Work { get; private set; } = "0";
        public string Rate { get; private set; } = "1";
        public string Overhead { get; private set; } = "0";

        public TimeFormula(ConfigNode node)
        {
            if (node.HasValue("Work")) Work = node.GetValue("Work").Replace(" ", "");
            if (node.HasValue("Rate")) Rate = node.GetValue("Rate").Replace(" ", "");
            if (node.HasValue("Overhead")) Overhead = node.GetValue("Overhead").Replace(" ", "");
        }
    }
}
