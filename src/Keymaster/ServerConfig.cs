using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Keymaster
{
	class ServerConfig
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

		public ServerConfig(XElement rootElement)
		{
			ManualKeys = new List<string>();
			ManualMods = new List<string>();
			ClientOnlyModList = new List<string>();
			Configs = new List<Config>();

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
		}

		public static ServerConfig LoadFromUrl(string url)
		{
			WebRequest request = HttpWebRequest.Create(url);
			using (var response = request.GetResponse())
			{
				var responseStream = response.GetResponseStream();
				return new ServerConfig(XElement.Load(responseStream));
			}
		}
	}
}
 