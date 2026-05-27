using DotNetMissionSDK.Json;
using OP2UtilityDotNet;
using OP2UtilityDotNet.OP2Map;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerraNova.Systems.GameMap
{
	/// <summary>
	/// First-pass tilemap renderer.
	/// - Reads each populated tileset from maps.vol (well0000.bmp ... well0012.bmp via ResourceManager).
	/// - Decodes the indexed BMP via OP2UtilityDotNet.Bitmap.BitmapFile.ReadIndexed.
	/// - Slices the vertical strip into 32x32 sprites, keyed by (tilesetIndex, tileGraphicIndex).
	/// - Instantiates a SpriteRenderer per map cell, parented under this transform.
	///
	/// Map coordinate convention used here:
	///   - Map (x, y): OP2's grid, (0,0) at top-left, y increasing downward.
	///   - World (X, Y): Unity, (0,0) at bottom-left, Y increasing upward.
	///   - We flip y when placing: world.Y = (height - 1) - mapY. So map row 0 ends up at the top of the world.
	/// </summary>
	public class MapRenderer : MonoBehaviour
	{
		public const int TileSize = 32;
		public const float PixelsPerUnit = 32f;

		/// <summary>Keyed by (tilesetIndex &lt;&lt; 16) | tileGraphicIndex.</summary>
		private readonly Dictionary<int, Sprite> _TileSprites = new Dictionary<int, Sprite>();
		private readonly List<Texture2D> _TilesetTextures = new List<Texture2D>();
		private Sprite _MissingTileSprite;

		public int MapWidth  { get; private set; }
		public int MapHeight { get; private set; }

		public void Initialize(MissionRoot mission, Map map, System.Action onComplete)
		{
			StartCoroutine(InitializeRoutine(mission, map, onComplete));
		}

		private IEnumerator InitializeRoutine(MissionRoot mission, Map map, System.Action onComplete)
		{
			float totalStart = Time.realtimeSinceStartup;

			MapWidth  = (int)map.WidthInTiles();
			MapHeight = (int)map.HeightInTiles();
			Debug.Log("[MapRenderer] Initialize. " + MapWidth + " x " + MapHeight + " tiles. " + map.tilesetSources.Count + " tileset slots.");

			yield return LoadTilesetsRoutine(map);
			yield return RenderCellsRoutine(map);

			Debug.Log("[MapRenderer] Total render time: " + (Time.realtimeSinceStartup - totalStart).ToString("N2") + "s");

			onComplete?.Invoke();
		}

		// ----- Tileset loading -----

		private IEnumerator LoadTilesetsRoutine(Map map)
		{
			float start = Time.realtimeSinceStartup;
			_MissingTileSprite = BuildMissingTileSprite();

			using (ResourceManager rm = new ResourceManager("."))
			{
				for (int i = 0; i < map.tilesetSources.Count; i++)
				{
					TilesetSource src = map.tilesetSources[i];
					if (src.numTiles == 0)
						continue;

					string bmpName = src.tilesetFilename + ".bmp";
					Texture2D tex;
					try
					{
						byte[] raw = rm.GetResource(bmpName, true);
						if (raw == null)
						{
							Debug.LogWarning("[MapRenderer] Tileset not found in vols: " + bmpName);
							continue;
						}
						tex = TilesetLoader.Load(raw, bmpName);
					}
					catch (System.Exception ex)
					{
						Debug.LogError("[MapRenderer] Failed to read " + bmpName);
						Debug.LogException(ex);
						continue;
					}

					_TilesetTextures.Add(tex);

					// Slice each tile out of the vertical strip.
					// In the source BMP the tiles are stacked top-to-bottom: tile 0 at the top.
					// After ConvertBitmapToTexture, Unity texture (0,0) is bottom-left and tile 0 sits in the
					// top 32 rows. So tile T's rect.y = textureHeight - (T+1) * TileSize.
					int numTiles = (int)src.numTiles;
					int textureHeight = tex.height;
					for (int t = 0; t < numTiles; t++)
					{
						int rectY = textureHeight - (t + 1) * TileSize;
						if (rectY < 0)
						{
							Debug.LogWarning("[MapRenderer] " + bmpName + " tile " + t + " out of bounds (textureHeight=" + textureHeight + ", numTiles=" + numTiles + ")");
							break;
						}
						UnityEngine.Rect rect = new UnityEngine.Rect(0, rectY, TileSize, TileSize);
						Sprite sprite = Sprite.Create(tex, rect, new Vector2(0f, 0f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
						sprite.name = src.tilesetFilename + "_" + t;
						_TileSprites[(i << 16) | t] = sprite;
					}

					yield return null;
				}
			}

			Debug.Log("[MapRenderer] Tilesets loaded: " + _TilesetTextures.Count + " textures, " + _TileSprites.Count + " tile sprites in " + (Time.realtimeSinceStartup - start).ToString("N2") + "s");
		}

		private static Sprite BuildMissingTileSprite()
		{
			Color32 magenta = new Color32(255, 0, 255, 255);
			Color32[] pixels = new Color32[TileSize * TileSize];
			for (int i = 0; i < pixels.Length; i++) pixels[i] = magenta;
			Texture2D tex = new Texture2D(TileSize, TileSize, TextureFormat.RGBA32, false);
			tex.name = "MissingTile";
			tex.filterMode = FilterMode.Point;
			tex.SetPixels32(pixels);
			tex.Apply(false, false);
			return Sprite.Create(tex, new UnityEngine.Rect(0, 0, TileSize, TileSize), new Vector2(0f, 0f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
		}

		// ----- Cell rendering -----

		private IEnumerator RenderCellsRoutine(Map map)
		{
			float start   = Time.realtimeSinceStartup;
			float yieldAt = start;
			int rendered  = 0;
			int missing   = 0;

			for (int y = 0; y < MapHeight; y++)
			{
				for (int x = 0; x < MapWidth; x++)
				{
					int tilesetIdx;
					int graphicIdx;
					try
					{
						tilesetIdx = map.GetTilesetIndex(x, y);
						graphicIdx = map.GetImageIndex(x, y);
					}
					catch (System.Exception ex)
					{
						Debug.LogWarning("[MapRenderer] GetTilesetIndex/GetImageIndex threw at (" + x + "," + y + "): " + ex.Message);
						continue;
					}

					int key = (tilesetIdx << 16) | graphicIdx;
					Sprite sprite;
					if (!_TileSprites.TryGetValue(key, out sprite))
					{
						sprite = _MissingTileSprite;
						missing++;
					}
					else
					{
						rendered++;
					}

					GameObject go = new GameObject("T_" + x + "_" + y);
					go.transform.SetParent(transform, false);
					go.transform.localPosition = new Vector3(x, (MapHeight - 1) - y, 0);
					SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
					sr.sprite = sprite;
				}

				if (Time.realtimeSinceStartup - yieldAt > 0.05f)
				{
					yield return null;
					yieldAt = Time.realtimeSinceStartup;
				}
			}

			Debug.Log("[MapRenderer] Cells rendered: " + rendered + " known, " + missing + " missing in " + (Time.realtimeSinceStartup - start).ToString("N2") + "s");
		}
	}
}
