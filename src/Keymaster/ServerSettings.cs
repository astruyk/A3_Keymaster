using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Keymaster
{
	class ServerSettings
	{
		public String FtpAddress { get; private set; }
		public String FtpUser { get; private set; }
		public String FtpPassword { get; private set; }
		public String FtpKeysPath { get; private set; }
		public String ParFileFtpPath { get; private set; }
		public String KeystoreUrl { get; private set; }
		public String KeyMappingFileUrl { get; private set; }
		public List<String> ManualKeys { get; private set; }
		public List<String> ManualMods { get; private set; }
		public List<String> ClientOnlyModList { get; private set; }
		public List<Config> Configs { get; private set; }

		public ServerSettings(XElement rootElement)
		{
			ManualKeys = new List<string>();
			ManualMods = new List<string>();
			ClientOnlyModList = new List<string>();
			Configs = new List<Config>();

			// Load the flat strings
			FtpAddress = rootElement.Element("ftpAddress").Value;
			FtpUser = rootElement.Element("ftpUser").Value;
			FtpPassword = rootElement.Element("ftpPassword").Value;
			FtpKeysPath = rootElement.Element("ftpKeysPath").Value;
			ParFileFtpPath = rootElement.Element("parFileFtpPath").Value;
			KeystoreUrl = rootElement.Element("keystoreUrl").Value;
			KeyMappingFileUrl = rootElement.Element("keyMappingFileUrl").Value;

			// Load the manual mods and keys
			ManualKeys.AddRange(rootElement.Element("manualKeys").Elements("key").Select(x => x.Value));
			ManualMods.AddRange(rootElement.Element("manualKeys").Elements("mod").Select(x => x.Value));

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

			public Config(XElement rootElement)
			{
				Name = rootElement.Attribute("name").Value;
				PlayWithSixConfigFileUrl = rootElement.Element("playWithSixConfigFileUrl").Value;
				ParFileSourceUrl = rootElement.Element("parFileSourceUrl").Value;
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
			return new ServerSettings(XElement.Parse(urlContents));
		}
	}
}
 