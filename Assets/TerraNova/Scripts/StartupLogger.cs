using System.IO;
using UnityEngine;

namespace TerraNova
{
	/// <summary>
	/// Writes every Debug.Log* call (and uncaught exceptions) to "startup.log"
	/// in the project working directory. Enabled automatically before the first
	/// scene loads. Remove this file (and its .meta) to disable.
	/// </summary>
	public static class StartupLogger
	{
		/// <summary>
		/// Folder containing the running application: the project root in the editor,
		/// the folder containing the .exe in a player build. Used as the base for all
		/// other game-data paths so installations remain portable.
		/// </summary>
		public static string StartupPath { get; private set; }

		/// <summary>
		/// Directory containing the legacy Outpost 2 data files
		/// (op2_art.prt, OP2_ART.BMP, sound.vol, voices.vol, sheets.vol, maps.vol, *.txt tech trees).
		/// Resolves to &lt;StartupPath&gt;\OP2. All ResourceManager("."), VolFile("name.vol"), and
		/// OP2BmpLoader("OP2_ART.BMP", ...) lookups resolve relative to the process current directory,
		/// so we set CWD to this folder before any code runs.
		/// </summary>
		public static string OP2DataPath { get; private set; }

		/// <summary>
		/// Directory containing user-installed colony game .opm files.
		/// Resolves to &lt;StartupPath&gt;\ColonyGames. Used by ColonyGamesPopup.
		/// </summary>
		public static string ColonyGamesPath { get; private set; }

		/// <summary>
		/// Directory containing user-installed tutorial .opm files.
		/// Resolves to &lt;StartupPath&gt;\Tutorials. Used by TutorialsPopup.
		/// </summary>
		public static string TutorialsPath { get; private set; }

		private static string _logPath;
		private static readonly object _lock = new object();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Initialize()
		{
			// Application.dataPath is "<projectRoot>/Assets" in editor and "<exeFolder>/<name>_Data" in builds.
			// Its parent is therefore the project root or the exe folder respectively — our portable startup path.
			StartupPath     = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
			OP2DataPath     = Path.Combine(StartupPath, "OP2");
			ColonyGamesPath = Path.Combine(StartupPath, "ColonyGames");
			TutorialsPath   = Path.Combine(StartupPath, "Tutorials");

			_logPath = Path.Combine(StartupPath, "startup.log");

			string originalCwd = Directory.GetCurrentDirectory();
			string cwdChangeMessage;
			try
			{
				Directory.SetCurrentDirectory(OP2DataPath);
				cwdChangeMessage = "CWD changed to  : " + Directory.GetCurrentDirectory() + " (from " + originalCwd + ")\n";
			}
			catch (System.Exception ex)
			{
				cwdChangeMessage = "CWD CHANGE FAILED to '" + OP2DataPath + "': " + ex.Message + "\n";
			}

			try
			{
				File.WriteAllText(_logPath,
					"=== Startup Log " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===\n" +
					"StartupPath     : " + StartupPath + "\n" +
					"OP2DataPath     : " + OP2DataPath + "\n" +
					"ColonyGamesPath : " + ColonyGamesPath + "\n" +
					"TutorialsPath   : " + TutorialsPath + "\n" +
					"original CWD    : " + originalCwd + "\n" +
					cwdChangeMessage +
					"dataPath        : " + Application.dataPath + "\n" +
					"persistentData  : " + Application.persistentDataPath + "\n" +
					"streamingAssets : " + Application.streamingAssetsPath + "\n" +
					"platform        : " + Application.platform + "\n" +
					"unityVersion    : " + Application.unityVersion + "\n" +
					"=== Log start ===\n");
			}
			catch (System.Exception ex)
			{
				Debug.LogException(ex);
				return;
			}

			Application.logMessageReceivedThreaded += OnLog;
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
