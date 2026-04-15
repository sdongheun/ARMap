using System.Collections.Generic;
using System.Linq;

public static class AnchorPoolPlanner
{
    public static List<BuildingData> BuildDesiredBuildings(List<VisibleBuildingCandidate> topCandidates)
    {
        if (topCandidates == null || topCandidates.Count == 0)
        {
            return new List<BuildingData>();
        }

        return topCandidates
            .Where(candidate => candidate?.building != null)
            .GroupBy(candidate => BuildBuildingKey(candidate.building))
            .Select(group => group.First().building)
            .ToList();
    }

    public static string BuildPoolSignature(List<BuildingData> desiredBuildings)
    {
        if (desiredBuildings == null || desiredBuildings.Count == 0)
        {
            return "none";
        }

        return string.Join(";",
            desiredBuildings
                .Select(BuildBuildingKey)
                .OrderBy(key => key));
    }

    private static string BuildBuildingKey(BuildingData building)
    {
        if (building == null)
        {
            return string.Empty;
        }

        return $"{building.buildingName}|{building.latitude:F5}|{building.longitude:F5}";
    }
}
