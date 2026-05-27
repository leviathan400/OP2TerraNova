using TerraNova.Systems.AssetManagement;
using TerraNova.Systems.Audio;
using TerraNova.Systems.Rendering.Animations;
using TerraNova.UserInterface.MainMenu;
using UnityEngine;

namespace TerraNova.Scenes
{
	/// <summary>
	/// Controls the main menu scene.
	/// </summary>
	public class MainMenuController : MonoBehaviour
	{
		[SerializeField] private GameObject _LoadingImage	= default;


		private void Awake()
		{
			Debug.Log("[MainMenuController] Awake. Calling AssetManager.Initialize.");
			AssetManager.Initialize(this, new string[0], OnInitialized);
		}

		private void OnInitialized(bool success)
		{
			Debug.Log("[MainMenuController] OnInitialized success=" + success);

			if (!success)
			{
				Debug.LogError("[MainMenuController] Asset load failed — loading screen will remain visible.");
				return;
			}

			// Hide loading image
			Debug.Log("[MainMenuController] Hiding loading image.");
			_LoadingImage.SetActive(false);

			// Open the main menu
			Debug.Log("[MainMenuController] Creating MainMenuPopup.");
			MainMenuPopup.Create().Show();

			// Play menu music
			Debug.Log("[MainMenuController] Starting menu music (Plymth22).");
			MusicPlayer.PlayMusic("Plymth22");

			// TEST: Animations
			Debug.Log("[MainMenuController] Instantiating 2079 test animations...");
			float animStart = Time.realtimeSinceStartup;
			animations = new OP2Animation_UGUI[2079];
			for (int i=0; i < 2079; ++i)
			{
				OP2Animation_UGUI animation = OP2Animation_UGUI.Create(AssetManager.GetAnimation(i));
				animation.gameObject.SetActive(false);
				animation.transform.SetParent(GameObject.Find("CanvasBackground").transform);
				animation.transform.localScale = Vector3.one;
				animation.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;

				animations[i] = animation;
			}
			Debug.Log("[MainMenuController] Animation instantiation done in " + (Time.realtimeSinceStartup - animStart).ToString("N2") + "s.");
		}

		private OP2Animation_UGUI[] animations;
		private int currentAnim = 0;

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.RightArrow))
			{
				animations[currentAnim].gameObject.SetActive(false);
				animations[++currentAnim].gameObject.SetActive(true);
			}
			if (Input.GetKeyDown(KeyCode.LeftArrow))
			{
				animations[currentAnim].gameObject.SetActive(false);
				animations[--currentAnim].gameObject.SetActive(true);
			}
		}
	}
}