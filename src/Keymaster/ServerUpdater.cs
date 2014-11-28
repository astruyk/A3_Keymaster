using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
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
			dynamic keyMappingJson = JObject.Parse(client.DownloadString(_settings.KeyMappingFileUrl));

			StartNewPhase(Phase.DownloadingPlayWithSixConfig);
			string playWithSixConfigContents = client.DownloadString(_config.PlayWithSixConfigFileUrl);

			StartNewPhase(Phase.DownloadingParFile);
			string parFileContents = client.DownloadString(_config.ParFileSourceUrl);

			StartNewPhase(Phase.ProcessingDownloadedData);
			Log("Done!");
			Log("");
			Log("Successfully updated server!");
		}

		private void StartNewPhase(Phase newPhase)
		{
			Log (LogLevel.Message, "Changing phase from {0} to {1}", CurrentPhase, newPhase);
			CurrentPhase = newPhase;
			
			if (PhaseChanged != null) { PhaseChanged(this, new EventArgs()); }
		}

		private void Log(string format, params object[] args)
		{
			Log(LogLevel.Debug, format, args);
		}

		private void Log(LogLevel level, string format, params object[] args)
		{
			var message = String.Format(format, args);
			switch(level)
			{
				case LogLevel.Debug: { message = "[Debug] " + message; break; }
				case LogLevel.Error: { message = "[ERROR] " + message; break; }
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
