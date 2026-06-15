namespace TimeToBuild.Work
{
    public abstract class WorkItem
    {
        public abstract ConfigNode Save();
        public abstract void Load(ConfigNode node);
    }
}
