using System.Collections.Generic;
using System.Linq;
using Google.XR.ARCoreExtensions;
using UnityEngine;

public class VisibleBuildingCandidate
{
    public BuildingData building;
    public Vector3 viewportPoint;
    public float distance;
}

public class AnchorVisibilitySelection
{
    public List<VisibleBuildingCandidate> visibleCandidates = new List<VisibleBuildingCandidate>();
    public List<VisibleBuildingCandidate> topCandidates = new List<VisibleBuildingCandidate>();
    public BuildingData focusedBuilding;
}

public static class AnchorVisibilityPlanner
{
    public static AnchorVisibilitySelection BuildSelection(
        IList<BuildingData> anchorCandidateBuildings,
        GeospatialPose pose,
        bool isEarthTracking,
        float headingAccuracyThreshold,
        float anchorPreviewRadius,
        float detectionAngle,
        Vector3 cameraForward,
        float verticalAngleLimit,
        float detectionRadius,
        float forwardGroupViewportThreshold,
        float groupedCandidateDistanceBias,
        float centerViewportThreshold,
        int maxPreviewAnchors)
    {
        AnchorVisibilitySelection selection = new AnchorVisibilitySelection();
        selection.visibleCandidates = CalculateVisibleCandidates(
            anchorCandidateBuildings,
            pose,
            isEarthTracking,
            headingAccuracyThreshold,
            anchorPreviewRadius,
            detectionAngle);
        selection.topCandidates = GetTopCandidates(
            selection.visibleCandidates,
            detectionRadius,
            forwardGroupViewportThreshold,
            groupedCandidateDistanceBias,
            maxPreviewAnchors);
        selection.focusedBuilding = DetermineFocusedBuilding(
            selection.topCandidates,
            cameraForward,
            verticalAngleLimit,
            centerViewportThreshold);
        return selection;
    }

    public static List<VisibleBuildingCandidate> CalculateVisibleCandidates(
        IList<BuildingData> anchorCandidateBuildings,
        GeospatialPose pose,
        bool isEarthTracking,
        float headingAccuracyThreshold,
        float anchorPreviewRadius,
        float detectionAngle)
    {
        List<VisibleBuildingCandidate> visibleBuildings = new List<VisibleBuildingCandidate>();

        if (anchorCandidateBuildings == null || anchorCandidateBuildings.Count == 0)
        {
            return visibleBuildings;
        }

        if (!isEarthTracking || pose.HeadingAccuracy > headingAccuracyThreshold)
        {
            return visibleBuildings;
        }

        foreach (BuildingData building in anchorCandidateBuildings)
        {
            if (building == null)
            {
                continue;
            }

            float distance = (float)HaversineDistance(
                pose.Latitude,
                pose.Longitude,
                building.latitude,
                building.longitude);

            if (distance > anchorPreviewRadius)
            {
                continue;
            }

            float signedHeadingDelta = Mathf.DeltaAngle(
                (float)pose.Heading,
                (float)CalculateBearingDegrees(pose.Latitude, pose.Longitude, building.latitude, building.longitude));
            float halfDetectionAngle = Mathf.Max(0.5f, detectionAngle * 0.5f);
            if (Mathf.Abs(signedHeadingDelta) > halfDetectionAngle)
            {
                continue;
            }

            float normalizedOffset = Mathf.Clamp(signedHeadingDelta / halfDetectionAngle, -1f, 1f);
            float viewportX = 0.5f + (normalizedOffset * 0.5f);

            if (viewportX < 0f || viewportX > 1f)
            {
                continue;
            }

            visibleBuildings.Add(new VisibleBuildingCandidate
            {
                building = building,
                viewportPoint = new Vector3(viewportX, 0.5f, distance),
                distance = distance
            });
        }

        return visibleBuildings;
    }

    public static BuildingData DetermineFocusedBuilding(
        List<VisibleBuildingCandidate> topCandidates,
        Vector3 cameraForward,
        float verticalAngleLimit,
        float centerViewportThreshold)
    {
        float pitchAngle = 90.0f - Vector3.Angle(cameraForward, Vector3.up);
        if (Mathf.Abs(pitchAngle) > verticalAngleLimit)
        {
            return null;
        }

        if (topCandidates == null || topCandidates.Count == 0)
        {
            return null;
        }

        BuildingData bestTarget = null;
        BuildingData fallbackTarget = null;
        float bestScore = float.MaxValue;
        float fallbackScore = float.MaxValue;
        Vector2 viewportCenter = new Vector2(0.5f, 0.5f);
        float relaxedViewportThreshold = Mathf.Min(0.38f, centerViewportThreshold + 0.1f);

        foreach (VisibleBuildingCandidate candidate in topCandidates)
        {
            Vector2 viewportOffset = new Vector2(
                Mathf.Abs(candidate.viewportPoint.x - viewportCenter.x),
                Mathf.Abs(candidate.viewportPoint.y - viewportCenter.y));
            bool withinPrimaryFocus = viewportOffset.x <= centerViewportThreshold && viewportOffset.y <= centerViewportThreshold;
            bool withinRelaxedFocus = viewportOffset.x <= relaxedViewportThreshold && viewportOffset.y <= relaxedViewportThreshold;
            float focusScore = viewportOffset.x + viewportOffset.y;
            float score = focusScore + candidate.distance * 0.0015f;

            if (withinPrimaryFocus && score < bestScore)
            {
                bestScore = score;
                bestTarget = candidate.building;
            }

            if (withinRelaxedFocus && score < fallbackScore)
            {
                fallbackScore = score;
                fallbackTarget = candidate.building;
            }
        }

        return bestTarget ?? fallbackTarget;
    }

    public static string BuildDetectionSignature(
        AnchorVisibilitySelection selection)
    {
        string focusedKey = selection?.focusedBuilding == null
            ? "none"
            : $"{selection.focusedBuilding.buildingName}|{selection.focusedBuilding.latitude:F5}|{selection.focusedBuilding.longitude:F5}";
        string visibleKeys = selection?.visibleCandidates == null
            ? string.Empty
            : string.Join(";", selection.visibleCandidates.Select(candidate =>
                $"{candidate.building?.buildingName}|{candidate.building?.latitude:F5}|{candidate.building?.longitude:F5}"));
        string topKeys = selection?.topCandidates == null
            ? string.Empty
            : string.Join(";", selection.topCandidates.Select(candidate =>
                $"{candidate.building?.buildingName}|{candidate.building?.latitude:F5}|{candidate.building?.longitude:F5}"));
        return $"{selection?.visibleCandidates?.Count ?? 0}::{selection?.topCandidates?.Count ?? 0}::{focusedKey}::{visibleKeys}::{topKeys}";
    }

    public static string FormatCandidateList(List<VisibleBuildingCandidate> visibleCandidates, int maxCount = 3)
    {
        if (visibleCandidates == null || visibleCandidates.Count == 0)
        {
            return "none";
        }

        List<string> labels = visibleCandidates
            .Take(Mathf.Max(1, maxCount))
            .Select((candidate, index) => $"{index + 1}. {TrimDebugLabel(candidate.building?.buildingName)}")
            .ToList();

        return string.Join("\n", labels);
    }

    public static List<VisibleBuildingCandidate> GetFrontMostCandidates(
        List<VisibleBuildingCandidate> visibleCandidates,
        float detectionRadius,
        float forwardGroupViewportThreshold,
        float groupedCandidateDistanceBias)
    {
        List<VisibleBuildingCandidate> sortedCandidates = visibleCandidates
            .Where(candidate => candidate.distance <= detectionRadius)
            .OrderBy(candidate => candidate.distance)
            .ToList();

        List<VisibleBuildingCandidate> frontMostCandidates = new List<VisibleBuildingCandidate>();

        foreach (VisibleBuildingCandidate candidate in sortedCandidates)
        {
            Vector2 candidateViewport = new Vector2(candidate.viewportPoint.x, candidate.viewportPoint.y);
            bool isRearCandidate = frontMostCandidates.Any(existing =>
            {
                Vector2 existingViewport = new Vector2(existing.viewportPoint.x, existing.viewportPoint.y);
                float viewportDistance = Vector2.Distance(candidateViewport, existingViewport);
                return viewportDistance <= forwardGroupViewportThreshold &&
                       candidate.distance >= existing.distance + groupedCandidateDistanceBias;
            });

            if (!isRearCandidate)
            {
                frontMostCandidates.Add(candidate);
            }
        }

        return frontMostCandidates;
    }

    public static List<VisibleBuildingCandidate> GetTopCandidates(
        List<VisibleBuildingCandidate> visibleCandidates,
        float detectionRadius,
        float forwardGroupViewportThreshold,
        float groupedCandidateDistanceBias,
        int maxPreviewAnchors)
    {
        List<VisibleBuildingCandidate> frontMostCandidates = GetFrontMostCandidates(
            visibleCandidates,
            detectionRadius,
            forwardGroupViewportThreshold,
            groupedCandidateDistanceBias);

        return frontMostCandidates
            .OrderBy(candidate => Mathf.Abs(candidate.viewportPoint.x - 0.5f) + Mathf.Abs(candidate.viewportPoint.y - 0.5f))
            .ThenBy(candidate => candidate.distance)
            .Take(Mathf.Max(1, maxPreviewAnchors))
            .ToList();
    }

    private static double CalculateBearingDegrees(double lat1, double lon1, double lat2, double lon2)
    {
        double phi1 = lat1 * Mathf.Deg2Rad;
        double phi2 = lat2 * Mathf.Deg2Rad;
        double deltaLon = (lon2 - lon1) * Mathf.Deg2Rad;

        double y = System.Math.Sin(deltaLon) * System.Math.Cos(phi2);
        double x = System.Math.Cos(phi1) * System.Math.Sin(phi2) -
                   System.Math.Sin(phi1) * System.Math.Cos(phi2) * System.Math.Cos(deltaLon);
        double bearing = System.Math.Atan2(y, x) * Mathf.Rad2Deg;
        return (bearing + 360.0) % 360.0;
    }

    private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = Mathf.Deg2Rad * (lat2 - lat1);
        double dLon = Mathf.Deg2Rad * (lon2 - lon1);
        double a = System.Math.Sin(dLat / 2) * System.Math.Sin(dLat / 2) +
                   System.Math.Cos(Mathf.Deg2Rad * lat1) * System.Math.Cos(Mathf.Deg2Rad * lat2) *
                   System.Math.Sin(dLon / 2) * System.Math.Sin(dLon / 2);
        double c = 2 * System.Math.Atan2(System.Math.Sqrt(a), System.Math.Sqrt(1 - a));
        return 6371000 * c;
    }

    private static string TrimDebugLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unnamed";
        }

        return value.Length <= 18 ? value : $"{value.Substring(0, 18)}...";
    }
}
