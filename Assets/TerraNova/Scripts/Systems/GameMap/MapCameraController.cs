using UnityEngine;

namespace TerraNova.Systems.GameMap
{
	/// <summary>
	/// Pans the map camera with WASD / arrow keys and (optionally) edge-scrolling, clamping
	/// the view inside the map bounds. Attach to the same GameObject as the Camera and call
	/// Initialize() after the camera has been positioned by GameController.
	/// </summary>
	public class MapCameraController : MonoBehaviour
	{
		/// <summary>Tiles per second at full deflection.</summary>
		public float ScrollSpeed = 20f;

		/// <summary>How close to the screen edge (in pixels) the mouse must be to trigger edge scrolling.</summary>
		public float EdgeScrollPx = 6f;

		public bool EnableKeyboardScroll = true;

		/// <summary>OP2-authentic edge scrolling. Off by default — it conflicts with mouse use of the
		/// right-side toolbar / minimap. Toggle on to test the classic feel.</summary>
		public bool EnableEdgeScroll = false;

		/// <summary>Hold Shift to scroll at 2x speed.</summary>
		public KeyCode FastScrollModifier = KeyCode.LeftShift;
		public float FastScrollMultiplier = 2f;

		private Camera _Camera;
		private int _MapWidth;
		private int _MapHeight;

		public void Initialize(Camera cam, int mapWidth, int mapHeight)
		{
			_Camera    = cam;
			_MapWidth  = mapWidth;
			_MapHeight = mapHeight;
			Debug.Log("[MapCameraController] Initialized. Map " + mapWidth + "x" + mapHeight + ". Scroll: WASD/arrows.");
		}

		private void Update()
		{
			if (_Camera == null)
				return;

			Vector2 delta = Vector2.zero;

			if (EnableKeyboardScroll)
			{
				if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  delta.x -= 1f;
				if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) delta.x += 1f;
				if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    delta.y += 1f;
				if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  delta.y -= 1f;
			}

			if (EnableEdgeScroll && Application.isFocused)
			{
				Vector3 m = Input.mousePosition;
				if (m.x >= 0f && m.x <  EdgeScrollPx)                    delta.x -= 1f;
				if (m.x <= Screen.width  && m.x > Screen.width  - EdgeScrollPx) delta.x += 1f;
				if (m.y >= 0f && m.y <  EdgeScrollPx)                    delta.y -= 1f;
				if (m.y <= Screen.height && m.y > Screen.height - EdgeScrollPx) delta.y += 1f;
			}

			if (delta.sqrMagnitude <= 0f)
				return;

			float speed = ScrollSpeed;
			if (Input.GetKey(FastScrollModifier))
				speed *= FastScrollMultiplier;

			if (delta.sqrMagnitude > 1f)
				delta = delta.normalized;

			Vector3 pos = _Camera.transform.position;
			pos.x += delta.x * speed * Time.unscaledDeltaTime;
			pos.y += delta.y * speed * Time.unscaledDeltaTime;
			ClampToMap(ref pos);
			_Camera.transform.position = pos;
		}

		private void ClampToMap(ref Vector3 pos)
		{
			float halfH = _Camera.orthographicSize;
			float halfW = halfH * _Camera.aspect;

			// If the map is wider than the viewport, clamp so the viewport stays inside the map.
			// If the map is narrower, lock the camera to the map's centre.
			if (_MapWidth >= halfW * 2f)
				pos.x = Mathf.Clamp(pos.x, halfW, _MapWidth - halfW);
			else
				pos.x = _MapWidth * 0.5f;

			if (_MapHeight >= halfH * 2f)
				pos.y = Mathf.Clamp(pos.y, halfH, _MapHeight - halfH);
			else
				pos.y = _MapHeight * 0.5f;
		}
	}
}
