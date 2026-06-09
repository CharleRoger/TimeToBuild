using TimeToBuild.Facilities;

namespace TimeToBuild
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.SPACECENTER, GameScenes.EDITOR, GameScenes.LOADING, GameScenes.LOADINGBUFFER, GameScenes.FLIGHT })]
    public class TimeToBuildScenario : ScenarioModule
    {
        public double EditorStartTime = -1;
        public BuildFacility BuildFacilityVAB { get; private set; }
        public BuildFacility BuildFacilitySPH { get; private set; }
        public ResearchFacility ResearchFacility { get; private set; }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (node.HasValue("EditorStartTime")) EditorStartTime = double.Parse(node.GetValue("EditorStartTime"));

            BuildFacilityVAB = new BuildFacility(SpaceCenterFacility.VehicleAssemblyBuilding);
            if (node.HasNode("BuildFacilityVAB")) BuildFacilityVAB.Load(node.GetNode("BuildFacilityVAB"));
            StartCoroutine(BuildFacilityVAB.UpdateWorkLoads_Coroutine());

            BuildFacilitySPH = new BuildFacility(SpaceCenterFacility.SpaceplaneHangar);
            if (node.HasNode("BuildFacilitySPH")) BuildFacilitySPH.Load(node.GetNode("BuildFacilitySPH"));
            StartCoroutine(BuildFacilitySPH.UpdateWorkLoads_Coroutine());

            ResearchFacility = new ResearchFacility();
            if (node.HasNode("ResearchFacility")) ResearchFacility.Load(node.GetNode("ResearchFacility"));
            StartCoroutine(ResearchFacility.UpdateWorkLoads_Coroutine());
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            node.AddValue("EditorStartTime", EditorStartTime);

            if (!(BuildFacilityVAB is null))
            {
                var buildFacilityVABNode = new ConfigNode();
                BuildFacilityVAB.Save(buildFacilityVABNode);
                node.AddNode("BuildFacilityVAB", buildFacilityVABNode);
            }

            if (!(BuildFacilitySPH is null))
            {
                var buildFacilitySPHNode = new ConfigNode();
                BuildFacilitySPH.Save(buildFacilitySPHNode);
                node.AddNode("BuildFacilitySPH", buildFacilitySPHNode);
            }

            if (!(ResearchFacility is null))
            {
                var researchFacilityNode = new ConfigNode();
                ResearchFacility.Save(researchFacilityNode);
                node.AddNode("ResearchFacility", researchFacilityNode);
            }
        }
    }
}