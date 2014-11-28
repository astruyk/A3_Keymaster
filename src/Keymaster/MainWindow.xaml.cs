using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace Gatekeeper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

			if (System.IO.File.Exists("settings.xml"))
			{
				XElement clientSettings = XElement.Load("settings.xml");
				var configUrl = clientSettings.Descendants().Where(x => x.Name == "configUrl").First().Value;
				ServerSettings serverConfig = ServerSettings.LoadFromUrl(configUrl);
				((AppDataSource)DataContext).SetServerStettings(serverConfig);
			}
        }

		
    }
}
