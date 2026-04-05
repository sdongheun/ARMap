# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Geospatial AR mobile app (Unity 6) that lets users explore nearby buildings through their phone camera. Uses GPS + ARCore Geospatial API to place AR markers at real-world building positions, and Kakao Local API to fetch place/facility data. Primary target is iOS (16.0+), with Android (API 23+) as secondary.

**Language**: All project documentation and code comments are in Korean.

## Key Technical Stack

- **Unity 6** (6000.2.9f1) with Universal Render Pipeline (URP 17.2.0)
- **AR Foundation 6.2.0** + ARCore Extensions (Geospatial API) + ARKit 6.2.0
- **Kakao Local API** for nearby place search (categories: supermarkets, convenience stores, cafes, hospitals, pharmacies, restaurants, schools)
- **Kakao Mobility API** for walking directions (route vertexes + turn-by-turn guides)
- **TextMesh Pro** for UI text rendering
- **Input System** 1.14.2 (New Input System)

## Build & Run

This is a Unity project — open with Unity 6 (6000.2.9f1). There is no CLI build pipeline; builds are done through the Unity Editor.

- **Single scene**: `Assets/Scenes/GeospatialTestScene.unity` (the only build scene)
- **iOS build**: After Unity build, `IOSPostBuildFix.cs` automatically removes the `-ld64` linker flag from the Xcode project
- **No automated tests** exist in this project

## API Key Setup

API keys are stored in `Assets/StreamingAssets/LocalApiKeys.json` (gitignored). On editor load, `Assets/Editor/LocalApiKeySync.cs` reads this file and syncs keys to ARCore Extensions settings automatically.

Required keys:
- `kakaoApiKey` — Kakao REST API key for place search
- `androidApiKey` — ARCore Cloud Services key for Android
- `iosApiKey` — ARCore Cloud Services key for iOS

## Architecture

Manager-based pattern with seven core scripts in `Assets/Scripts/`:

### GeospatialManager.cs (~860 lines) — Central controller
- Initializes AR session and geospatial tracking (AREarthManager, ARAnchorManager)
- Fetches nearby places from Kakao API (coroutine-based HTTP calls)
- Clusters places into buildings by distance (≤20m) + matching road address
- Creates ARGeospatialAnchors with BuildingMarker prefabs
- Runs viewport-based building detection each frame: filters by detection radius (100m), eliminates rear/occluded buildings, selects center-viewport candidate (threshold: 0.24)
- Triggers data reload when user moves >100m (Haversine distance)

### ARUIManager.cs (~600 lines) — UI controller
- Three card states: Scanning → Detected → QuickInfo
- Manages detail view panel (full-screen sheet with facility list)
- Creates/updates 2D screen-space markers dynamically
- Handles user actions: copy address, phone call, open Kakao Map link
- Toast notification system

### BuildingMarker.cs (~230 lines) — 3D world marker
- Three visual states: Hidden, Preview (cyan), Selected (orange)
- Billboard behavior (always faces camera)
- Animated scale transitions
- World-space canvas with title/subtitle labels
- Dynamic height offset: 2.5m (near, 15m) to 7.0m (far, 120m), linearly interpolated

### BuildingData.cs (~30 lines) — Data models
- `BuildingData`: building name, address, phone, URL, lat/lon/alt, facility list
- `FacilityInfo`: individual tenant/facility data within a building

### NavigationManager.cs (~400 lines) — Navigation controller
- AR walking navigation using Kakao Mobility Directions API
- State machine: Idle → Searching → Routing → Navigating → Arrived
- Object-pooled arrow placement (30 arrows, 8m spacing, 80m visible range)
- GPS→world coordinate conversion using camera's Geospatial Pose + heading rotation
- Reroute detection (30m threshold), arrival detection (15m threshold)
- Destination search via Kakao keyword search API

### NavigationArrow.cs (~140 lines) — AR arrow component
- Smooth position/rotation interpolation (Lerp-based, Y-axis only rotation)
- Fade in/out alpha control via MaterialPropertyBlock
- Pool-friendly activate/deactivate lifecycle

### NavigationData.cs (~110 lines) — Navigation data models
- `NavigationState` enum, `RoutePoint`, `RouteGuide`, `DestinationResult`, `NavigationRoute`
- Kakao Mobility API response parsing: `KakaoDirectionsResponse`, `KakaoRoute`, `KakaoRouteSection`, `KakaoRoad`, `KakaoGuide`

### Dependency flow
```
GeospatialManager    → creates BuildingMarker instances
                     → calls ARUIManager for UI updates
                     → uses BuildingData as data model
ARUIManager          → displays BuildingData fields
                     → creates screen-space marker UI elements
                     → navigation UI (search panel, HUD, off-screen indicator)
BuildingMarker       → attached to ARGeospatialAnchor GameObjects
NavigationManager    → uses GeospatialManager (API key, HaversineDistance, nav flag)
                     → calls ARUIManager for navigation UI
                     → creates NavigationArrow instances (object pool)
                     → uses NavigationData models
NavigationArrow      → attached to pooled arrow GameObjects
```

## Editor Scripts (Assets/Editor/)

- **LocalApiKeySync.cs**: Auto-loads API keys from StreamingAssets on editor startup
- **IOSPostBuildFix.cs**: Post-build processor that fixes iOS linker flags

## Prefabs (Assets/Prefabs/)

- **BuildingMarker.prefab**: 3D AR marker with sprite renderer + world-space canvas
- **FacilityItem.prefab**: UI list item for facility details in the detail view
- **NavigationArrow.prefab**: Flat chevron arrow for AR walking navigation (URP Unlit, cyan)
- **DestinationMarker.prefab**: Large destination marker with pulse glow (orange/gold, 2.5x scale)
- **SearchResultItem.prefab**: UI list item for destination search results

## Development Context

Refer to `.docs/PROJECT_MASTER.md` for the full project specification and `.docs/DEV_THREAD_TASKS.md` for current sprint tasks (TASK-A1 through TASK-A7). Key active work areas:

- Front-most building detection (eliminate rear buildings on same sight line)
- Center-viewport-only info cards (non-center buildings show as markers only)
- Map-style pin markers for non-selected buildings
- Marker → label animation when building enters screen center
- Full-screen detail page (replacing AR overlay sheet)
- Location reload stabilization
