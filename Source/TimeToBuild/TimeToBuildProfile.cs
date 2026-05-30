using System;
using System.Collections.Generic;
using static TimeToBuild.BuildTime;

namespace TimeToBuild
{
    public class TimeToBuildProfile
    {
        public string Name { get; private set; } = "";
        public string Title { get; private set; } = "";
        public string Description { get; private set; } = "";
        public double MorningTime { get; private set; } = 0;
        public double AlarmWarningBufferTime { get; private set; } = 0;
        public Dictionary<BuildTimeIdentifier, BuildTime> BuildTimes { get; private set; } = new Dictionary<BuildTimeIdentifier, BuildTime>();

        public TimeToBuildProfile(ConfigNode node)
        {
            if (node.HasValue("name")) Name = node.GetValue("name");
            if (node.HasValue("title")) Title = node.GetValue("title");
            if (node.HasValue("description")) Description = node.GetValue("description");
            if (node.HasValue("MorningTime")) MorningTime = double.Parse(node.GetValue("MorningTime"));
            if (node.HasValue("AlarmWarningBufferTime")) MorningTime = double.Parse(node.GetValue("AlarmWarningBufferTime"));
            foreach (var buildTimeNode in node.GetNodes("BuildTime"))
            {
                foreach (var facilityName in buildTimeNode.GetValues("Facility"))
                {
                    foreach (SpaceCenterFacility facility in Enum.GetValues(typeof(SpaceCenterFacility)))
                    {
                        if (facilityName.Replace(" ", "").Equals(facility.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            var buildTime = new BuildTime(buildTimeNode, facility);
                            if (buildTime.Identifier.Name != "") BuildTimes[buildTime.Identifier] = buildTime;
                        }
                    }
                }
            }
        }

        public static TimeToBuildProfile GetTimeToBuildProfile(string name)
        {
            var allTimeToBuildProfileNodes = GameDatabase.Instance.GetConfigNodes("TimeToBuildProfile");
            var allTimeToBuildProfiles = new Dictionary<string, TimeToBuildProfile>();
            foreach (var timeToBuildProfileNode in allTimeToBuildProfileNodes)
            {
                if (timeToBuildProfileNode.HasValue("name") && timeToBuildProfileNode.GetValue("name") == name)
                {
                    return new TimeToBuildProfile(timeToBuildProfileNode);
                }
            }
            return null;
        }
    }
}