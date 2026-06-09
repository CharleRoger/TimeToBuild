namespace TimeToBuild
{
    public class BuildVessel
    {
        [Persistent]
        public string LaunchSiteName { get; private set; } = "";
        [Persistent]
        public ShipConstruct ShipConstruct { get; private set; }

        public BuildVessel(string launchSiteName, ShipConstruct shipConstruct)
        {
            LaunchSiteName = launchSiteName;
            ShipConstruct = shipConstruct;
        }

        public BuildVessel(ConfigNode node)
        {
            if (node.HasValue("LaunchSiteName")) LaunchSiteName = node.GetValue("LaunchSiteName");

            ShipConstruct = new ShipConstruct();
            if (node.HasNode("ShipConstruct")) ShipConstruct.LoadShip(node.GetNode("ShipConstruct"));
        }

        public ConfigNode GetConfigNode()
        {
            ConfigNode node = new ConfigNode();

            node.AddValue("LaunchSiteName", LaunchSiteName);

            if (!(ShipConstruct is null)) node.AddNode("ShipConstruct", ShipConstruct.SaveShip());

            return node;
        }
    }
}
