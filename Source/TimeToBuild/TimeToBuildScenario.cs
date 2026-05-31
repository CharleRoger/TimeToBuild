namespace TimeToBuild
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.SPACECENTER, GameScenes.EDITOR, GameScenes.LOADING, GameScenes.LOADINGBUFFER, GameScenes.FLIGHT })]
    public class TimeToBuildScenario : ScenarioModule
    {
        public double EditorStartTime = -1;
        public BuildFacility BuildFacilityVAB { get; private set; }
        public BuildFacility BuildFacilitySPH { get; private set; }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            BuildFacilityVAB = new BuildFacility(SpaceCenterFacility.VehicleAssemblyBuilding);
            if (node.HasValue("EditorStartTime")) EditorStartTime = double.Parse(node.GetValue("EditorStartTime"));

            BuildFacilitySPH = new BuildFacility(SpaceCenterFacility.SpaceplaneHangar);
            if (node.HasNode("BuildFacilityVAB")) BuildFacilityVAB.Load(node.GetNode("BuildFacilityVAB"));
            StartCoroutine(BuildFacilityVAB.UpdateBuildVessels_Coroutine());

            if (node.HasNode("BuildFacilitySPH")) BuildFacilitySPH.Load(node.GetNode("BuildFacilitySPH"));
            StartCoroutine(BuildFacilitySPH.UpdateBuildVessels_Coroutine());
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            node.AddValue("EditorStartTime", EditorStartTime);

            var buildFacilityVABNode = new ConfigNode();
            BuildFacilityVAB.Save(buildFacilityVABNode);
            node.AddNode("BuildFacilityVAB", buildFacilityVABNode);

            var buildFacilitySPHNode = new ConfigNode();
            BuildFacilitySPH.Save(buildFacilitySPHNode);
            node.AddNode("BuildFacilitySPH", buildFacilitySPHNode);
        }
    }
}