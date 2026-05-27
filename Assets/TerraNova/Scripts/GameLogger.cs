using System.IO;
using UnityEngine;

namespace TerraNova
{
	/// <summary>
	/// Writes Game-scene activity to "game.log" in the project working directory.
	/// Begin() opens a fresh log and subscribes to Unity's log stream.
	/// End() flushes and detaches.
	/// </summary>
	public static class GameLogger
	{
		public const string LogFileName = "game.log";

		private static string _logPath;
		private static readonly object _lock = new object();
		private static bool _active;

		public static void Begin(string missionPath)
		{
			_logPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", LogFileName));

			try
			{
				File.WriteAllText(_logPath,
					"=== Game Log " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===\n" +
					"missionPath     : " + missionPath + "\n" +
					"CWD             : " + Directory.GetCurrentDirectory() + "\n" +
					"platform        : " + Application.platform + "\n" +
					"unityVersion    : " + Application.unityVersion + "\n" +
					"=== Log start ===\n");
			}
			catch (System.Exception ex)
			{
				Debug.LogException(ex);
				return;
			}

			if (!_active)
			{
				Application.logMessageReceivedThreaded += OnLog;
				_active = true;
			}
		}

		public static void End()
		{
			if (_active)
			{
				Application.logMessageReceivedThreaded -= OnLog;
				_active = false;
			}
		}

		private static void OnLog(string condition, string stackTrace, LogType type)
		{
			try
			{
				lock (_lock)
				{
					string line = "[" + System.DateTime.Now.ToString("HH:mm:ss.fff") + "] " + type + ": " + condition + "\n";
					if (type == LogType.Exception || type == LogType.Error)
						line += stackTrace + "\n";
					File.AppendAllText(_logPath, line);
				}
			}
			catch { /* ignore log-write failures */ }
		}
	}
}
