namespace TimeToBuild.Core
{
    public class TimeToBuildSettings : GameParameters.CustomParameterNode
    {
        public override string Title => "Time To Build Options";
        public override string DisplaySection => "Time To Build";
        public override string Section => "Time To Build";
        public override int SectionOrder => 1;
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override bool HasPresets => false;
        public string Profile = "Default";
    }
}