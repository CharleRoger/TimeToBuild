namespace TimeToBuild
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.SPACECENTER, GameScenes.EDITOR, GameScenes.LOADING, GameScenes.LOADINGBUFFER, GameScenes.FLIGHT })]
    public class TimeToBuildScenario : ScenarioModule
    {
        public double EditorStartTime = -1;
        public BuildFacility BuildFacilityVAB { get; private set; } = new BuildFacility();
        public BuildFacility BuildFacilitySPH { get; private set; } = new BuildFacility();

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (node.HasValue("EditorStartTime")) EditorStartTime = double.Parse(node.GetValue("EditorStartTime"));
            if (node.HasNode("BuildFacilityVAB")) BuildFacilityVAB = new BuildFacility(node.GetNode("BuildFacilityVAB"));
            if (node.HasNode("BuildFacilitySPH")) BuildFacilitySPH = new BuildFacility(node.GetNode("BuildFacilitySPH"));
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            node.AddValue("EditorStartTime", EditorStartTime);
            node.AddNode("BuildFacilityVAB", BuildFacilityVAB.GetConfigNode());
            node.AddNode("BuildFacilitySPH", BuildFacilitySPH.GetConfigNode());
        }
    }
}