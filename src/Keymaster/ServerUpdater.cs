using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Keymaster
{
	class ServerUpdater
	{
		public class XmlTags
		{
			public const string RootElementName = "config";
			public const string ServerConfigElementName = "serverConfig";
		}

		public ServerUpdater(XElement configurationXml, string config)
		{
			
		}
	}
}
