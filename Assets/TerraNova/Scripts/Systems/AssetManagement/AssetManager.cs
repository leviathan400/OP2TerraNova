using OP2UtilityDotNet;
using OP2UtilityDotNet.Archive;
using OP2UtilityDotNet.Sprite;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TerraNova.Systems.AssetManagement
{
	/// <summary>
	/// Loads and manages game assets.
	/// Serves as the primary access point for referencing game assets.
	/// </summary>
	public static class AssetManager
	{
		/// <summary>
		/// Contains the art meta data for the game.
		/// </summary>
		private static ArtFile _ArtFile;

		/// <summary>
		/// Contains the legacy artwork for the game.
		/// </summary>
		private static OP2BmpLoader _LegacyArt;

		/// <summary>
		/// Manages legacy assets for the game.
		/// </summary>
		private static ResourceManager _LegacyAssets;

		/// <summary>
		/// Contains modded assets for the game.
		/// This will override over legacy artwork when possible.
		/// If asset names conflict, the higher indexed bundle will be used.
		/// </summary>
		private static List<AssetBundle> _ModBundles = new List<AssetBundle>();


		private static Dictionary<int, Texture2D> _TextureLookup = new Dictionary<int, Texture2D>();
		private static Dictionary<int, Sprite> _SpriteLookup = new Dictionary<int, Sprite>();
		private static Dictionary<string, AudioClip> _SoundLookup = new Dictionary<string, AudioClip>();


		public delegate void OnInitializeCallback(bool success);


		/// <summary>
		/// Loads required assets for the game.
		/// </summary>
		public static void Initialize(MonoBehaviour coroutineOwner, string[] modBundlePaths, OnInitializeCallback onInitCB)
		{
			Debug.Log("[AssetManager] Initialize start. CWD=" + System.IO.Directory.GetCurrentDirectory());

			// Load art file
			try
			{
				Debug.Log("[AssetManager] Creating ResourceManager(\".\")");
				_LegacyAssets = new ResourceManager(".");

				Debug.Log("[AssetManager] Opening 'op2_art.prt' stream...");
				Stream artPartStream = _LegacyAssets.GetResourceStream("op2_art.prt");
				if (artPartStream == null)
				{
					Debug.LogError("[AssetManager] 'op2_art.prt' stream is null. Is the file in the working directory?");
					onInitCB?.Invoke(false);
					return;
				}

				Debug.Log("[AssetManager] Reading ArtFile...");
				_ArtFile = ArtFile.Read(artPartStream);
				Debug.Log("[AssetManager] ArtFile loaded. imageMetas=" + _ArtFile.imageMetas.Count + " animations=" + _ArtFile.animations.Count);
			}
			catch (System.Exception ex)
			{
				Debug.LogError("[AssetManager] Failed during ArtFile/ResourceManager init.");
				Debug.LogException(ex);
				onInitCB?.Invoke(false);
				return;
			}

			Debug.Log("[AssetManager] Starting LoadAssetsRoutine coroutine.");
			coroutineOwner.StartCoroutine(LoadAssetsRoutine(modBundlePaths, onInitCB));
		}

		private static IEnumerator LoadAssetsRoutine(string[] modBundlePaths, OnInitializeCallback onInitCB)
		{
			// Load mod bundles
			foreach (string bundlePath in modBundlePaths)
			{
				AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(Path.Combine(Application.streamingAssetsPath, bundlePath));
				yield return bundleRequest;
				
				if (bundleRequest.assetBundle != null)
					_ModBundles.Add(bundleRequest.assetBundle);
			}

			// Prepare legacy art loader
			Debug.Log("[AssetManager] Creating OP2BmpLoader('OP2_ART.BMP')...");
			try
			{
				_LegacyArt = new OP2BmpLoader("OP2_ART.BMP", _ArtFile);
				Debug.Log("[AssetManager] OP2BmpLoader created OK.");
			}
			catch (System.Exception ex)
			{
				Debug.LogError("[AssetManager] OP2BmpLoader threw — legacy art will be unavailable.");
				Debug.LogException(ex);
			}

			yield return null;

			float startTime = Time.realtimeSinceStartup;
			float curTime = startTime;

			Debug.Log("[AssetManager] Loading " + _ArtFile.imageMetas.Count + " textures...");

			// Load textures
			for (int i=0; i < _ArtFile.imageMetas.Count; ++i)
			{
				Texture2D texture = GetAssetFromModBundle<Texture2D>("Bitmap" + i);

				if (texture != null)
				{
					_TextureLookup[i] = texture;
					_SpriteLookup[i] = Sprite.Create(texture, new UnityEngine.Rect(0,0, texture.width, texture.height), Vector2.zero, 1);
				}
				else if (_LegacyArt != null)
				{
					// Load legacy texture
					try
					{
						OP2BitmapFile bitmap = _LegacyArt.GetImage(i);
						texture = GetTextureFromBMP(bitmap);
						_TextureLookup[i] = texture;
						_SpriteLookup[i] = Sprite.Create(texture, new UnityEngine.Rect(0,0, texture.width, texture.height), Vector2.zero, 1);
					}
					catch (System.Exception ex)
					{
						Debug.LogException(ex);
						Debug.LogError("BmpIndex: " + i);
					}
				}
				else
				{
					// Could not find texture!
					Debug.LogError("[AssetManager] Could not find texture for index: " + i + " (no mod bundle and _LegacyArt is null). Aborting load.");

					// Inform caller of failure
					onInitCB?.Invoke(false);
					yield break;
				}

				// Every X seconds, take a break to render the screen
				if (curTime + 0.25f < Time.realtimeSinceStartup)
				{
					yield return null;
					curTime = Time.realtimeSinceStartup;
				}
			}

			Debug.Log("[AssetManager] Texture Load Time: " + (Time.realtimeSinceStartup - startTime).ToString("N2") + " seconds (loaded " + _TextureLookup.Count + " textures)");

			Debug.Log("[AssetManager] Loading sound.vol...");
			yield return LoadSoundsRoutine("sound.vol");
			Debug.Log("[AssetManager] Loading voices.vol...");
			yield return LoadSoundsRoutine("voices.vol");

			Debug.Log("[AssetManager] Total Load Time: " + (Time.realtimeSinceStartup - startTime).ToString("N2") + " seconds. Invoking onInitCB(true).");

			// Inform caller of success
			onInitCB?.Invoke(true);
		}

		private static IEnumerator LoadSoundsRoutine(string archivePath)
		{
			float startTime = Time.realtimeSinceStartup;
			float curTime = startTime;

			// Get file names from legacy sound archive.
			// We do this to cache the sounds used by the game engine.
			List<string> soundFileNames = new List<string>();

			try
			{
				VolFile soundArchive = new VolFile(archivePath);
				int count = soundArchive.GetCount();
				for (int i=0; i < count; ++i)
				{
					soundFileNames.Add(Path.GetFileNameWithoutExtension(soundArchive.GetName(i)));
				}
			}
			catch (System.Exception ex)
			{
				Debug.LogWarning(ex);
			}

			// Load sounds
			foreach (string soundFileName in soundFileNames)
			{
				AudioClip soundClip = GetAssetFromModBundle<AudioClip>(soundFileName);

				if (soundClip != null)
				{
					_SoundLookup[soundFileName] = soundClip;
				}
				else if (_LegacyAssets != null)
				{
					// Load legacy sound
					try
					{
						byte[] wavData = _LegacyAssets.GetResource(soundFileName + ".wav");
						if (wavData != null)
						{
							_SoundLookup[soundFileName] = WavUtility.ToAudioClip(wavData, 0, soundFileName);
						}
						else
						{
							Debug.LogWarning("Could not find resource with name: " + soundFileName);
						}
					}
					catch (System.Exception ex)
					{
						Debug.LogException(ex);
						Debug.LogError("Sound File: " + soundFileName);
					}
				}
				else
				{
					// Could not find sound!
					Debug.LogWarning("Could not find sound with name: " + soundFileName);
				}

				// Every X seconds, take a break to render the screen
				if (curTime + 0.25f < Time.realtimeSinceStartup)
				{
					yield return null;
					curTime = Time.realtimeSinceStartup;
				}
			}

			Debug.Log("Sounds Load Time: " + (Time.realtimeSinceStartup - startTime).ToString("N2") + " seconds");
		}

		// Gets an asset from a mod bundle based on priority order, or returns null, if the asset is not in any mod bundle.
		private static T GetAssetFromModBundle<T>(string assetName) where T : UnityEngine.Object
		{
			for (int i=_ModBundles.Count-1; i >= 0; --i)
			{
				T asset = _ModBundles[i].LoadAsset<T>(assetName);
				if (asset != null)
					return asset;
			}

			return null;
		}

		private static Texture2D GetTextureFromBMP(OP2BitmapFile bitmap)
		{
			int width = Mathf.Abs(bitmap.imageHeader.width);
			int height = Mathf.Abs(bitmap.imageHeader.height);

			// Get pixels from bitmap
			Color32[] imageData = new Color32[width * height];

			for (int y = 0; y < height; ++y)
			{
				for (int x = 0; x < width; ++x)
				{
					OP2UtilityDotNet.Bitmap.Color color = bitmap.GetEnginePixel(x, height-1 - y);
					imageData[x + y * width] = new Color32(color.red, color.green, color.blue, color.alpha);
				}
			}

			// Convert to texture
			Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true);
            texture.SetPixels32(imageData);
			texture.wrapMode = TextureWrapMode.Clamp;
			//texture.filterMode = FilterMode.Point;
            texture.Apply();
            return texture;
		}

		public static OP2UtilityDotNet.Sprite.Animation GetAnimation(int index)
		{
			return _ArtFile.animations[index];
		}

		public static Texture2D GetTexture(int index)
		{
			return _TextureLookup[index];
		}

		public static Sprite GetSprite(int index)
		{
			Sprite sprite;
			_SpriteLookup.TryGetValue(index, out sprite);
			return sprite;
		}

		public static AudioClip GetSound(string soundName)
		{
			AudioClip clip;
			_SoundLookup.TryGetValue(soundName, out clip);
			return clip;
		}

		/// <summary>
		/// Gets a music clip by name.
		/// NOTE: This will load the clip. Expect possible performance impact.
		/// </summary>
		public static AudioClip GetMusicClip(string musicName)
		{
			// Try to get from a mod bundle
			AudioClip clip = GetAssetFromModBundle<AudioClip>(musicName);
			if (clip != null)
				return clip;

			// Try to get from legacy
			byte[] data = _LegacyAssets.GetResource(musicName);
			if (data == null)
				return null;

			return WavUtility.ToAudioClip(data, 0, musicName);
		}
	}
}
