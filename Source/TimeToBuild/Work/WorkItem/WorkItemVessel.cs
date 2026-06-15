using System.Collections.Generic;

namespace TimeToBuild.Work
{
    public class WorkItemVessel : WorkItem
    {
        public struct BuildPart
        {
            public uint ID;
            public bool ReuseFromInventory;
            public double DryMass;
            public double WetMass;
            public double DryCost;
            public double WetCost;
            public int NumBuilds;
        }

        [Persistent]
        public string LaunchSiteName { get; private set; } = "";
        [Persistent]
        public ShipConstruct ShipConstruct { get; private set; }

        public WorkItemVessel(double startTime, List<WorkChunk> workChunks, string launchSiteName, ShipConstruct shipConstruct) : base(startTime, workChunks)
        {
            LaunchSiteName = launchSiteName;
            ShipConstruct = shipConstruct;
        }

        public override ConfigNode Save()
        {
            var node = base.Save();

            node.AddValue("LaunchSiteName", LaunchSiteName);

            if (!(ShipConstruct is null)) node.AddNode("ShipConstruct", ShipConstruct.SaveShip());

            return node;
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);

            if (node.HasValue("LaunchSiteName")) LaunchSiteName = node.GetValue("LaunchSiteName");

            ShipConstruct = new ShipConstruct();
            if (node.HasNode("ShipConstruct")) ShipConstruct.LoadShip(node.GetNode("ShipConstruct"));
        }

        public WorkItemVessel(ConfigNode node)
        {
            Load(node);
        }
    }
}
