using DotNetMissionSDK;
using DotNetMissionSDK.Json;
using OP2UtilityDotNet;
using OP2UtilityDotNet.OP2Map;
using System.IO;
using TerraNova.Systems;
using TerraNova.Systems.Audio;
using TerraNova.Systems.Constants;
using TerraNova.Systems.GameMap;
using TerraNova.Systems.State;
using TerraNova.UserInterface.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TerraNova.Scenes
{
	/// <summary>
	/// Initializes the game state.
	/// </summary>
	public sealed class GameController : MonoBehaviour
	{
		//[SerializeField] private MapRenderer _MapRenderer	= default;


		private void Awake()
		{
			GameLogger.Begin(SceneParameters.MissionPath);
			Debug.Log("[GameController] Awake. missionPath=" + SceneParameters.MissionPath + " randomSeed=" + SceneParameters.RandomSeed);

			// Stop menu music
			MusicPlayer.StopMusic();
			Debug.Log("[GameController] Menu music stopped.");

			// Load mission
			Debug.Log("[GameController] Loading mission...");
			MissionRoot mission = LoadMission(SceneParameters.MissionPath);
			if (mission == null)
			{
				Debug.LogError("[GameController] Mission load returned null.");
				InfoPopup.Create("Failed to load mission.", OnLoadFailed);
				return;
			}
			LogMissionDetails(mission);

			// Load map
			Debug.Log("[GameController] Loading map '" + mission.levelDetails.mapName + "'...");
			Map map = LoadMap(mission);
			if (map == null)
			{
				Debug.LogError("[GameController] Map load returned null (file '" + mission.levelDetails.mapName + "' not found in CWD or vols).");
				InfoPopup.Create("Failed to load map.", OnLoadFailed);
				return;
			}
			LogMapDetails(map);

			// Load game state
			Debug.Log("[GameController] Initializing GameState (sheets + tech tree + players)...");
			string error;
			if (!GameState.Initialize(mission, SceneParameters.RandomSeed, out error))
			{
				Debug.LogError("[GameController] GameState.Initialize failed: " + error);
				InfoPopup.Create(error, OnLoadFailed);
				return;
			}
			LogGameStateSummary();

			// Setup Game
			//SetDaylightEverywhere(tethysGame.daylightEverywhere);
			//SetDaylightMoves(tethysGame.daylightMoves);
			//SetInitialLightLevel(tethysGame.initialLightLevel);

			// Initialize map renderer
			Debug.Log("[GameController] Spawning MapRenderer...");
			GameObject mapRendererGO = new GameObject("MapRenderer");
			MapRenderer mapRenderer = mapRendererGO.AddComponent<MapRenderer>();
			ConfigureCameraForMap(map);
			mapRenderer.Initialize(mission, map, OnLoadMapComplete);

			Debug.Log("[GameController] Awake complete.");
		}

		/// <summary>
		/// Reposition the main camera as orthographic at native pixel zoom (32 screen px per tile)
		/// with map cell (0,0) anchored to the top-left of the camera view. The OP2 UI frame is a
		/// screen-space overlay, so it draws on top of the rendered map.
		/// </summary>
		private void ConfigureCameraForMap(Map map)
		{
			Camera cam = Camera.main;
			if (cam == null)
			{
				Debug.LogWarning("[GameController] Camera.main is null — creating one.");
				GameObject camGO = new GameObject("Main Camera");
				camGO.tag = "MainCamera";
				cam = camGO.AddComponent<Camera>();
			}

			int h = (int)map.HeightInTiles();

			// Native zoom: one screen pixel per tile pixel. PixelsPerUnit = 32, TileSize = 32 → 1 world unit = 1 tile = 32 px.
			cam.orthographic     = true;
			cam.orthographicSize = Screen.height / (2f * MapRenderer.PixelsPerUnit);

			float halfH  = cam.orthographicSize;
			float aspect = (float)Screen.width / Screen.height;
			float halfW  = halfH * aspect;

			// Sprites are placed with bottom-left pivot at world (x, mapHeight-1-y).
			// Map cell (0,0) sits at world (0, mapHeight-1); its top-left corner is at world (0, mapHeight).
			// We want that point at the top-left of the camera view:
			//   top-left of view = (camX - halfW, camY + halfH) = (0, mapHeight)
			//   → camX = halfW, camY = mapHeight - halfH
			cam.transform.position = new Vector3(halfW, h - halfH, -10f);
			cam.transform.rotation = Quaternion.identity;
			cam.backgroundColor    = Color.black;
			cam.clearFlags         = CameraClearFlags.SolidColor;

			Debug.Log("[GameController] Camera configured: orthoSize=" + cam.orthographicSize +
				" pos=" + cam.transform.position +
				" screen=" + Screen.width + "x" + Screen.height +
				" → visible " + (halfW * 2f).ToString("N1") + "x" + (halfH * 2f).ToString("N1") + " tiles");

			// Scrolling
			MapCameraController scroll = cam.GetComponent<MapCameraController>();
			if (scroll == null)
				scroll = cam.gameObject.AddComponent<MapCameraController>();
			scroll.Initialize(cam, (int)map.WidthInTiles(), (int)map.HeightInTiles());
		}

		private void LogMissionDetails(MissionRoot mission)
		{
			Debug.Log("[GameController] Mission loaded:");
			Debug.Log("  description    = " + mission.levelDetails.description);
			Debug.Log("  mapName        = " + mission.levelDetails.mapName);
			Debug.Log("  techTreeName   = " + mission.levelDetails.techTreeName);
			Debug.Log("  missionType    = " + mission.levelDetails.missionType);
			Debug.Log("  missionVariants= " + mission.missionVariants.Count);
			Debug.Log("  masterVariant.players = " + (mission.masterVariant != null ? mission.masterVariant.players.Count : 0));
		}

		private void LogMapDetails(Map map)
		{
			Debug.Log("[GameController] Map loaded:");
			try { Debug.Log("  widthInTiles   = " + map.WidthInTiles()); } catch { Debug.Log("  widthInTiles   = <n/a>"); }
			try { Debug.Log("  heightInTiles  = " + map.HeightInTiles()); } catch { Debug.Log("  heightInTiles  = <n/a>"); }
			try { Debug.Log("  tilesetCount   = " + map.tilesetSources.Count); } catch { Debug.Log("  tilesetSources = <n/a>"); }

			try
			{
				int populated = 0;
				int empty = 0;
				for (int i = 0; i < map.tilesetSources.Count; ++i)
				{
					var src = map.tilesetSources[i];
					if (src.numTiles > 0)
					{
						Debug.Log("    tileset[" + i + "]: " + src.tilesetFilename + " (numTiles=" + src.numTiles + ")");
						populated++;
					}
					else
					{
						empty++;
					}
				}
				Debug.Log("    (" + populated + " populated tilesets, " + empty + " empty placeholder slots)");
			}
			catch (System.Exception ex)
			{
				Debug.LogWarning("[GameController] Could not enumerate tileset sources: " + ex.Message);
			}
		}

		private void LogGameStateSummary()
		{
			Debug.Log("[GameController] GameState initialized:");
			Debug.Log("  StructureInfo  = " + (GameState.StructureInfo != null ? GameState.StructureInfo.Count.ToString() : "null"));
			Debug.Log("  VehicleInfo    = " + (GameState.VehicleInfo != null ? GameState.VehicleInfo.Count.ToString() : "null"));
			Debug.Log("  WeaponInfo     = " + (GameState.WeaponInfo != null ? GameState.WeaponInfo.Count.ToString() : "null"));
			Debug.Log("  StarshipInfo   = " + (GameState.StarshipInfo != null ? GameState.StarshipInfo.Count.ToString() : "null"));
			Debug.Log("  MineInfo       = " + (GameState.MineInfo != null ? GameState.MineInfo.Count.ToString() : "null"));
			Debug.Log("  TechInfo       = " + (GameState.TechInfo != null ? GameState.TechInfo.Count.ToString() : "null"));
			Debug.Log("  Players        = " + (GameState.Players != null ? GameState.Players.Count.ToString() : "null"));

			if (GameState.Players != null)
			{
				for (int i = 0; i < GameState.Players.Count; ++i)
				{
					Debug.Log("    Player[" + i + "] " + (GameState.Players[i] == null ? "= null" : "instantiated"));
				}
			}

			// SceneParameters.Players carries the per-player info (id + difficulty) the menu selected
			if (SceneParameters.Players != null)
			{
				for (int i = 0; i < SceneParameters.Players.Count; ++i)
				{
					var sp = SceneParameters.Players[i];
					if (sp == null) { Debug.Log("    SceneParameters.Player[" + i + "] = null"); continue; }
					Debug.Log("    SceneParameters.Player[" + i + "] id=" + sp.PlayerID + " difficulty=" + sp.Difficulty);
				}
			}
		}

		private MissionRoot LoadMission(string path)
		{
			MissionRoot missionRoot = null;

			// Load mission
			try
			{
				missionRoot = MissionReader.GetMissionData(path);
			}
			catch (System.Exception ex)
			{
				Debug.LogException(ex);
			}

			return missionRoot;
		}

		private Map LoadMap(MissionRoot mission)
		{
			string mapName = mission.levelDetails.mapName;

			// 1. Look next to the .opm file (custom missions usually ship the map alongside).
			string missionDir = Path.GetDirectoryName(SceneParameters.MissionPath);
			if (!string.IsNullOrEmpty(missionDir))
			{
				string adjacentPath = Path.Combine(missionDir, mapName);
				if (File.Exists(adjacentPath))
				{
					Debug.Log("[GameController] Found map alongside .opm: " + adjacentPath);
					using (Stream fs = File.OpenRead(adjacentPath))
						return Map.ReadMap(fs);
				}
			}

			// 2. Look in the ColonyGames / Tutorials roots (in case the map is at the popup root
			//    but the .opm is in a subfolder).
			foreach (string root in new[] { StartupLogger.ColonyGamesPath, StartupLogger.TutorialsPath })
			{
				if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
					continue;
				string rootPath = Path.Combine(root, mapName);
				if (File.Exists(rootPath))
				{
					Debug.Log("[GameController] Found map in mission root: " + rootPath);
					using (Stream fs = File.OpenRead(rootPath))
						return Map.ReadMap(fs);
				}
			}

			// 3. Fall back to the OP2 data dir via ResourceManager (loose files + every .vol).
			Debug.Log("[GameController] Falling back to ResourceManager search in " + Directory.GetCurrentDirectory());
			using (ResourceManager resourceManager = new ResourceManager("."))
			{
				using (Stream mapStream = resourceManager.GetResourceStream(mapName, true))
				{
					if (mapStream == null)
						return null;

					return Map.ReadMap(mapStream);
				}
			}
		}

		private void OnLoadMapComplete()
		{
			Debug.Log("[GameController] OnLoadMapComplete — map render done, playing CommandControlInitiated voiceover.");
			SoundPlayer.PlaySound(VoicesTable.CommandControlInitiated);
		}

		private void OnLoadFailed()
		{
			Debug.Log("[GameController] OnLoadFailed — returning to MainMenu.");
			GameLogger.End();
			SceneManager.LoadScene("MainMenu");
		}

		private void OnDestroy()
		{
			Debug.Log("[GameController] OnDestroy — closing game log.");
			GameLogger.End();
		}
	}
}
