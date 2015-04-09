using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Gatekeeper
{
	class ServerSettings
	{
		public String FtpAddress { get; private set; }
		public String FtpUser { get; private set; }
		public String FtpPassword { get; private set; }
		public String FtpArmaPath { get; private set; }
		public String FtpParFileName { get; private set; }
		public String KeystoreUrl { get; private set; }
		public String KeyMappingFileUrl { get; private set; }
		public List<String> ManualKeys { get; private set; }
		public List<String> BlacklistedKeys { get; private set; }
		public List<String> ManualKeyMods { get; private set; }
		public List<String> ClientOnlyModList { get; private set; }
		public List<Config> Configs { get; private set; }

		public int LatestMajorVersion { get; private set; }
		public int LatestMinorVersion { get; private set; }
		public int LatestPatchVersion { get; private set; }

		public ServerSettings(XElement rootElement)
		{
			ManualKeys = new List<string>();
			BlacklistedKeys = new List<string>();
			ManualKeyMods = new List<string>();
			ClientOnlyModList = new List<string>();
			Configs = new List<Config>();

			// Load version info (for forced updating)
			var versionElement = rootElement.Element("latestVersion");
			if (versionElement != null)
			{
				LatestMajorVersion = int.Parse(versionElement.Element("majorVersion").Value);
				LatestMinorVersion = int.Parse(versionElement.Element("minorVersion").Value);
				LatestPatchVersion = int.Parse(versionElement.Element("patchVersion").Value);
			}

			// Load the flat strings
			FtpAddress = rootElement.Element("ftpAddress").Value;
			FtpUser = rootElement.Element("ftpUser").Value;
			FtpPassword = rootElement.Element("ftpPassword").Value;
			FtpArmaPath = rootElement.Element("ftpArmaPath").Value;
			FtpParFileName = rootElement.Element("ftpParFileName").Value;
			KeystoreUrl = rootElement.Element("keystoreUrl").Value;
			KeyMappingFileUrl = rootElement.Element("keyMappingFileUrl").Value;

			// Fixup the path to look like a dir if it doesn't already
			if (!FtpArmaPath.EndsWith("/")) { FtpArmaPath += "/"; }

			// Load the manual mods and keys
			ManualKeys.AddRange(rootElement.Element("manualKeys").Elements("key").Select(x => x.Value));
			ManualKeyMods.AddRange(rootElement.Element("manualKeys").Elements("mod").Select(x => x.Value));

			// Load the blacklist for keys
			var blacklistElement = rootElement.Element("blacklistKeys");
			if (blacklistElement != null)
			{
				BlacklistedKeys.AddRange(blacklistElement.Elements("key").Select(x => x.Value));
			}

			// Load the client only mod list
			ClientOnlyModList.AddRange(rootElement.Element("clientOnlyModList").Elements("mod").Select(x => x.Value));

			// Load the specific project configs
			foreach (var configElement in rootElement.Elements("serverConfig"))
			{
				Configs.Add(new Config(configElement));
			}
		}

		public class Config
		{
			public String Name { get; private set; }
			public String PlayWithSixConfigFileUrl { get; private set; }
			public String ParFileSourceUrl { get; private set; }
			public List<String> ServerOnlyMods { get; private set; }
			public Dictionary<String, String> FilesToCopy { get; private set; }

			public Config(XElement rootElement)
			{
				ServerOnlyMods = new List<string>();
				FilesToCopy = new Dictionary<String, String>();
				
				Name = rootElement.Attribute("name").Value;
				PlayWithSixConfigFileUrl = rootElement.Element("playWithSixConfigFileUrl").Value;
				ParFileSourceUrl = rootElement.Element("parFileSourceUrl").Value;
				var serverOnlyModElement = rootElement.Element("serverOnlyMods");
				if (serverOnlyModElement != null)
				{
					ServerOnlyMods.AddRange(serverOnlyModElement.Elements("mod").Select(x => x.Value));
				}
				foreach (var fileToCopyElement in rootElement.Elements("copyFile"))
				{
					if (fileToCopyElement.Attribute("source") == null)
					{
						// Ignore malformed XML element.
					}
					else if (fileToCopyElement.Attribute("destination") == null)
					{
						// Ignore malformed XML element.
					}
					else
					{
						FilesToCopy.Add(fileToCopyElement.Attribute("source").Value, fileToCopyElement.Attribute("destination").Value);
					}
				}
			}

			public override string ToString()
			{
				return Name;
			}
		}

		public static ServerSettings LoadFromUrl(string url)
		{
			WebClient client = new WebClient();
			var urlContents = client.DownloadString(url);
			var rootElement = XElement.Parse(urlContents);
			return new ServerSettings(rootElement);
		}
	}
}
 