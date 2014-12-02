using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.FtpClient;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Gatekeeper
{
	class ServerUpdater
	{
		public static bool ShowDebugOutput { get; set; }
		public static bool SkipFtpActions { get; set; }

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

		private ServerSettings _settings;
		private ServerSettings.Config _config;

		public ServerUpdater(ServerSettings settings, ServerSettings.Config config)
		{
			_settings = settings;
			_config = config;
			CurrentPhase = Phase.Idle;
		}

		public void DoUpdate()
		{
			try
			{
				DoUpdateInternal();
			}
			catch(Exception ex)
			{
				Log(LogLevel.Error, "Something went wrong!!");
				Log(LogLevel.Error, " -- Writing out Exception Log - Send the following to your server admin to help diagnose the problem:");
				Log(ex.ToString());
			}
		}

		private void DoUpdateInternal()
		{
			Log("Starting...");
			WebClient client = new WebClient();
			StartNewPhase(Phase.DownloadingMappingFile);
			Log("Downloading {0}", _settings.KeyMappingFileUrl);
			var keyMappingFileContents = client.DownloadString(_settings.KeyMappingFileUrl);
			var keyMappings = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(keyMappingFileContents);
			keyMappings = new Dictionary<string, string[]>(keyMappings, StringComparer.CurrentCultureIgnoreCase);

			StartNewPhase(Phase.DownloadingPlayWithSixConfig);
			Log("Downloading {0}", _config.PlayWithSixConfigFileUrl);
			string playWithSixConfigFileContents = client.DownloadString(_config.PlayWithSixConfigFileUrl);

			StartNewPhase(Phase.DownloadingParFile);
			Log("Downloading {0}", _config.ParFileSourceUrl);
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
			var modsForServer = playWithSixMods.Except(_settings.ClientOnlyModList).Concat(_config.ServerOnlyMods);
			Log("Found {0} mod(s) that need to be running on the server.", modsForServer.Count());
			foreach (var mod in modsForServer)
			{
				Log("\t{0}", mod);
			}

			// Generate the command line we need to run to start the server
			var modCommandLine = string.Format("-mod={0}", String.Join(";", modsForServer));
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
			foreach (var entry in keysToInstall)
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
				Log("\t{0}", keyName);
				Log(LogLevel.Debug, "\t( {0} -> {1} )", keyUrl, keyFileLoc);
				client.DownloadFile(keyUrl, keyFileLoc);
			}
			Log("Done downloading keys.");

			// Login to the FTP server
			StartNewPhase(Phase.ConnectingToFtpServer);
			var ftpKeyDirectoryUri = new UriBuilder();
			ftpKeyDirectoryUri.Scheme = "ftp";
			ftpKeyDirectoryUri.Path = "";
			ftpKeyDirectoryUri.Host = _settings.FtpAddress;
			ftpKeyDirectoryUri.UserName = _settings.FtpUser;
			ftpKeyDirectoryUri.Password = _settings.FtpPassword;

			Log("Connecting to {0}", ftpKeyDirectoryUri.Uri);
			using (var ftpClient = FtpClient.Connect(ftpKeyDirectoryUri.Uri))
			{
				Log("Connected.");
				var files = ftpClient.GetListing(_settings.FtpArmaPath + "keys/");
				Log("Got list of {0} existing keys from server.", files.Count());

				// Remove stale key files
				StartNewPhase(Phase.RemovingStaleKeys);
				Log("Deleting {0} stale keys from server.", files.Count());
				foreach (var file in files)
				{
					Log("\tDeleting stale key: {0}", file.FullName);
					if (SkipFtpActions)
					{
						Log("\tSkipped uploading because 'Local Actions Only' was selected");
					}
					else
					{
						ftpClient.DeleteFile(file.FullName);
					}
				}
				Log("Done.");

				// Upload new key files
				StartNewPhase(Phase.UploadingNewKeys);
				Log("Uploading {0} new keys to server.", keysToInstall.Count());
				foreach (var keyName in keysToInstall.Keys)
				{
					var localPath = Path.Combine(tmpDirectory, keyName);
					var remotePath = _settings.FtpArmaPath + "keys/" + keyName;
					Log("\tUploading: {0} -> {1}", localPath, remotePath);
					if (SkipFtpActions)
					{
						Log("\tSkipped uploading because 'Local Actions Only' was selected");
					}
					else
					{
						using (var remoteFile = ftpClient.OpenWrite(remotePath, FtpDataType.Binary))
						using (var localFile = File.OpenRead(localPath))
						{
							localFile.CopyTo(remoteFile);
						}
					}
				}
				Log("Done.");

				// Remove old PAR file
				StartNewPhase(Phase.RemovingStaleParFile);
				files = ftpClient.GetListing(_settings.FtpArmaPath);
				Log("Checking for stale PAR file");
				foreach (var file in files)
				{
					if (file.Name.Equals(_settings.FtpParFileName))
					{
						Log("\tFound stale PAR file.. Deleting.");
						Log(LogLevel.Debug, "Stale par file at: " + file.FullName);
						if (SkipFtpActions)
						{
							Log("\tSkipped uploading because 'Local Actions Only' was selected");
						}
						else
						{
							ftpClient.DeleteFile(file.FullName);
						}
					}
				}
				Log("Done.");

				// Upload new PAR file
				StartNewPhase(Phase.UploadingNewParFile);
				var parFileRemotePath = _settings.FtpArmaPath + _settings.FtpParFileName;
				Log("Uploading par file to {0}", parFileRemotePath);
				if (SkipFtpActions)
				{
					Log("\tSkipped uploading because 'Local Actions Only' was selected");
				}
				else
				{
					using (var remoteFile = ftpClient.OpenWrite(_settings.FtpArmaPath + _settings.FtpParFileName))
					using (var parFileMemoryStream = new MemoryStream())
					using (var parFileStreamWriter = new StreamWriter(parFileMemoryStream))
					{
						parFileStreamWriter.Write(parFileContentsToUpload);
						parFileStreamWriter.Flush();
						parFileMemoryStream.Position = 0;

						parFileMemoryStream.CopyTo(remoteFile);
					}
				}
				Log("Done.");
			}
			Log("");
			Log("All Done!");
			Log("");
			Log("Successfully updated server!");
		}

		private string GetModifiedParFileContents(string originalParFileContents, string modCommandLine)
		{
			var parFileModCommand = string.Format(@"mod=""{0}"";", modCommandLine);
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
			Log(LogLevel.Debug, "Finished {0} phase.", CurrentPhase);
			Log(LogLevel.Debug, "Starting '{0}' phase.", newPhase);
			CurrentPhase = newPhase;	
			if (PhaseChanged != null) { PhaseChanged(this, new EventArgs()); }
		}

		private void Log(string format, params object[] args)
		{
			Log(LogLevel.Message, format, args);
		}

		private void Log(LogLevel level, string format, params object[] args)
		{
			if (level == LogLevel.Debug && !ShowDebugOutput) { return; }
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
