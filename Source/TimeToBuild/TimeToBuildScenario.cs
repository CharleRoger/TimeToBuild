namespace TimeToBuild
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.SPACECENTER, GameScenes.EDITOR, GameScenes.LOADING, GameScenes.LOADINGBUFFER, GameScenes.FLIGHT })]
    public class TimeToBuildScenario : ScenarioModule
    {
        [KSPField(isPersistant = true)]
        public double EditorStartTime = -1;
    }
}