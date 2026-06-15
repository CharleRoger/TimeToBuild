using TimeToBuild.Facilities;

namespace TimeToBuild.Core
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
            StartCoroutine(BuildFacilityVAB.UpdateWorkItems_Coroutine());

            BuildFacilitySPH = new BuildFacility(SpaceCenterFacility.SpaceplaneHangar);
            if (node.HasNode("BuildFacilitySPH")) BuildFacilitySPH.Load(node.GetNode("BuildFacilitySPH"));
            StartCoroutine(BuildFacilitySPH.UpdateWorkItems_Coroutine());

            ResearchFacility = new ResearchFacility();
            if (node.HasNode("ResearchFacility")) ResearchFacility.Load(node.GetNode("ResearchFacility"));
            StartCoroutine(ResearchFacility.UpdateWorkItems_Coroutine());
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            node.AddValue("EditorStartTime", EditorStartTime);

            if (!(BuildFacilityVAB is null)) node.AddNode("BuildFacilityVAB", BuildFacilityVAB.Save());
            if (!(BuildFacilitySPH is null)) node.AddNode("BuildFacilitySPH", BuildFacilitySPH.Save());
            if (!(ResearchFacility is null)) node.AddNode("ResearchFacility", ResearchFacility.Save());
        }
    }
}