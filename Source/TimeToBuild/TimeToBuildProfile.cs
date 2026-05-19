using System;
using System.Collections.Generic;

namespace TimeToBuild
{
    public class TimeToBuildProfile
    {
        public class BuildTime
        {
            public string Name { get; private set; } = "";
            public string Title { get; private set; } = "";
            public string Description { get; private set; } = "";
            public string Formula { get; private set; } = "";
            public bool PerNewPart { get; private set; } = false;
            public bool PerReusedPart { get; private set; } = false;
            public bool WholeVessel { get; private set; } = false;
            public List<SpaceCenterFacility> Facilities { get; private set; } = new List<SpaceCenterFacility>();

            public BuildTime(ConfigNode node)
            {
                if (node.HasValue("name")) Name = node.GetValue("name");
                if (node.HasValue("title")) Title = node.GetValue("title");
                if (node.HasValue("description")) Description = node.GetValue("description");
                if (node.HasValue("Formula")) Formula = node.GetValue("Formula").Replace(" ", "");
                if (node.HasValue("PerNewPart")) PerNewPart = bool.Parse(node.GetValue("PerNewPart"));
                if (node.HasValue("PerReusedPart")) PerReusedPart = bool.Parse(node.GetValue("PerReusedPart"));
                if (node.HasValue("WholeVessel")) WholeVessel = bool.Parse(node.GetValue("WholeVessel"));
                foreach (var facilityName in node.GetValues("Facility"))
                {
                    foreach (SpaceCenterFacility facility in Enum.GetValues(typeof(SpaceCenterFacility)))
                    {
                        if (facilityName.Replace(" ", "").Equals(facility.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            Facilities.Add(facility);
                        }
                    }
                }
            }
        }

        public string Name { get; private set; } = "";
        public string Title { get; private set; } = "";
        public string Description { get; private set; } = "";
        public double MorningTime { get; private set; } = 0;
        public double AlarmWarningBufferTime { get; private set; } = 0;
        public List<BuildTime> BuildTimes { get; private set; } = new List<BuildTime>();

        public TimeToBuildProfile(ConfigNode node)
        {
            if (node.HasValue("name")) Name = node.GetValue("name");
            if (node.HasValue("title")) Title = node.GetValue("title");
            if (node.HasValue("description")) Description = node.GetValue("description");
            if (node.HasValue("MorningTime")) MorningTime = double.Parse(node.GetValue("MorningTime"));
            if (node.HasValue("AlarmWarningBufferTime")) MorningTime = double.Parse(node.GetValue("AlarmWarningBufferTime"));
            foreach (var buildTimeNode in node.GetNodes("BuildTime"))
            {
                BuildTimes.Add(new BuildTime(buildTimeNode));
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