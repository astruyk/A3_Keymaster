using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Gatekeeper
{
	class ServerUpdater
	{
		private ServerSettings _settings;
		private ServerSettings.Config _config;

		public enum Phase
		{
			Idle,
			Starting,
			DownloadingMappingFile,
			DownloadingPlayWithSixConfig,
			DownloadingParFile,
			ProcessingDownloadedData,
			DownloadingKeyFiles,
			ConnectingToFtpServer,
			RemovingStaleKeys,
			UploadingNewKeys,
			RemovingStaleParFile,
			UploadingNewParFile
		}

		public static string GetPhaseName(Phase phase)
		{
			switch (phase)
			{
				case Phase.Idle: return "Idle";
				case Phase.DownloadingMappingFile: return "Downloading Mapping File";
				case Phase.DownloadingPlayWithSixConfig: return "Downloading Play With Six Config File";
				case Phase.DownloadingParFile: return "Downloading Parameters file";
				case Phase.ProcessingDownloadedData: return "Processing Downloaded Data";
				case Phase.DownloadingKeyFiles: return "Downloading Key Files";
				case Phase.ConnectingToFtpServer: return "Connecting to FTP Server";
				case Phase.RemovingStaleKeys: return "Removing Stale Keys";
				case Phase.UploadingNewKeys: return "Uploading New Keys";
				case Phase.RemovingStaleParFile: return "Removing Stale Parameters File";
				case Phase.UploadingNewParFile: return "Uploading New Parameters File";
				default: return "Unknown";
			}
		}

		public ServerUpdater(ServerSettings settings, ServerSettings.Config config)
		{
			_settings = settings;
			_config = config;
			CurrentPhase = Phase.Idle;
		}

		public void StartUpdate()
		{
			Log("Starting...");
			WebClient client = new WebClient();
			StartNewPhase(Phase.DownloadingMappingFile);
			Log(LogLevel.Debug, "Downloading {0}", _settings.KeyMappingFileUrl);
			var keyMappingFileContents = client.DownloadString(_settings.KeyMappingFileUrl);
			var keyMappings = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(keyMappingFileContents);
			keyMappings = new Dictionary<string,string[]>(keyMappings, StringComparer.CurrentCultureIgnoreCase);

			StartNewPhase(Phase.DownloadingPlayWithSixConfig);
			Log(LogLevel.Debug, "Downloading {0}", _config.PlayWithSixConfigFileUrl);
			string playWithSixConfigFileContents = client.DownloadString(_config.PlayWithSixConfigFileUrl);

			StartNewPhase(Phase.DownloadingParFile);
			Log(LogLevel.Debug, "Downloading {0}", _config.ParFileSourceUrl);
			string parFileContents = client.DownloadString(_config.ParFileSourceUrl);

			StartNewPhase(Phase.ProcessingDownloadedData);
			
			// Grab the list of mods in the play with six file.
			var playWithSixMods = GetModsFromPlayWithSixFile(playWithSixConfigFileContents);
			Log("Found {0} keys in play with six mod list.", playWithSixMods.Count);
			foreach (var mod in playWithSixMods)
			{
				Log("\t{0}", mod);
			}

			// Generate the list of mods that we need to install of the server
			var modsForServer = playWithSixMods.Except(_settings.ClientOnlyModList);
			Log("Found {0} mod(s) that need to be running on the server.", modsForServer.Count());
			foreach (var mod in modsForServer)
			{
				Log("\t{0}", mod);
			}

			// Generate the command line we need to run to start the server
			var modCommandLine = string.Format("-mod={0}",  String.Join(";", modsForServer));
			Log("Generated mod command-line args for server.");
			Log(LogLevel.Debug, modCommandLine);

			// Generate a modified version of the par file
			var parFileContentsToUpload = GetModifiedParFileContents(parFileContents, modCommandLine);
			Log(LogLevel.Debug, "Par File Contents:\n\n{0}", parFileContentsToUpload);

			// Generate a mapping of the keys we need to install to the list of mods
			// that require them (for reporting)
			var keysToInstall = new Dictionary<string, List<string>>(StringComparer.CurrentCultureIgnoreCase);
			foreach (var modName in playWithSixMods.Concat(_settings.ManualMods))
			{
				string[] requiredKeys;
				if (keyMappings.TryGetValue(modName, out requiredKeys))
				{
					foreach (var key in requiredKeys)
					{
						List<string> modsRequestingKey;
						if (keysToInstall.TryGetValue(key, out modsRequestingKey))
						{
							modsRequestingKey.Add(modName);
						}
						else
						{
							keysToInstall[key] = new List<string> { modName };
						}
					}
				}
				else
				{
					Log(LogLevel.Error, "Failed to find mod list entry for '{0}'. Unable to continue", modName);
					return;
				}
			}

			// Add the manually required keys	
			foreach (var keyName in _settings.ManualKeys)
			{
				List<string> modsRequestingKey;
				if (keysToInstall.TryGetValue(keyName, out modsRequestingKey))
				{
					modsRequestingKey.Add("MANUAL");
				}
				else
				{
					keysToInstall[keyName] = new List<string> { "MANUAL" };
				}
			}

			Log("Found {0} keys that need to be installed on the server.", keysToInstall.Count());
			foreach(var entry in keysToInstall)
			{
				Log("\t{0} -> {{ {1} }}", entry.Key, String.Join(",", entry.Value));
			}

			// Actually download all the keys we need
			StartNewPhase(Phase.DownloadingKeyFiles);
			Log("Downloading {0} keys from keystore...", keysToInstall.Count());
			var tmpDirectory = "tmp";
			if (!Directory.Exists(tmpDirectory))
			{
				Directory.CreateDirectory(tmpDirectory);
			}
			foreach (var keyName in keysToInstall.Keys)
			{
				var keyUrl = string.Format("{0}/{1}", _settings.KeystoreUrl, keyName);
				var keyFileLoc = Path.Combine(tmpDirectory, keyName);
				Log("{0}", keyName);
				Log(LogLevel.Debug, "\t( {0} -> {1} )", keyUrl, keyFileLoc);
				client.DownloadFile(keyUrl, keyFileLoc);
			}
			Log("Done downloading keys.");

			// Login to the FTP server
			StartNewPhase(Phase.ConnectingToFtpServer);


			Log("Done!");
			Log("");
			Log("Successfully updated server!");
		}

		private string GetModifiedParFileContents(string originalParFileContents, string modCommandLine)
		{
			var parFileModCommand = string.Format(@"mod=""-mod={0}"";", modCommandLine);
			var modifiedFileContents = new StringBuilder();
			foreach (var line in originalParFileContents.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
			{
				string modifiedLine = line;
				if (modifiedLine.Contains("-mod="))
				{
					modifiedLine = "// Disabled by Gatekeeper: " + line;
				}
				if (modifiedLine.Contains("};"))
				{
					modifiedLine = "\t" + parFileModCommand + "\t// Generated command line." + Environment.NewLine + line;
				}
				modifiedFileContents.AppendLine(modifiedLine);
			}
			return modifiedFileContents.ToString();
		}

		private List<string> GetModsFromPlayWithSixFile(string playWithSixConfigFileContents)
		{
			var modList = new List<string>();

			var modRegex = new Regex(@"\s*-\s*""(@\w+)""");
			bool isInModList = false;
			foreach(var line in playWithSixConfigFileContents.Split(new string [] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
			{
				if (isInModList && line.StartsWith(":"))
				{
					isInModList = false;
				}
				if (line.Contains(":required_mods:") || line.Contains(":allowed_mods:"))
				{
					isInModList = true;
				}
				if (isInModList)
				{
					var match = modRegex.Match(line);
					if (match.Success)
					{
						modList.Add(match.Groups[1].Value);
					}
				}
			}

			return modList;
		}

		private void StartNewPhase(Phase newPhase)
		{
			Log("Finished {0} phase.", CurrentPhase);
			Log ("Starting '{0}' phase.", newPhase);
			CurrentPhase = newPhase;	
			if (PhaseChanged != null) { PhaseChanged(this, new EventArgs()); }
		}

		private void Log(string format, params object[] args)
		{
			Log(LogLevel.Message, format, args);
		}

		private void Log(LogLevel level, string format, params object[] args)
		{
			var message = String.Format(format, args);
			switch(level)
			{
				case LogLevel.Debug: { message = "[Debug]\t" + message; break; }
				case LogLevel.Error: { message = "[ERROR]\t" + message; break; }
			}
			if (OutputGenerated != null)
			{
				OutputGenerated(this, message);
			}
		}

		private enum LogLevel
		{
			Debug,
			Message,
			Error
		}

		public bool IsRunning { get; private set; }
		public Phase CurrentPhase { get; private set; }

		public event EventHandler PhaseChanged;
		public event EventHandler<string> OutputGenerated;
	}
}
