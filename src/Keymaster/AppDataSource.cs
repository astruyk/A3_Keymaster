using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Keymaster
{
	class AppDataSource
	{
		public List<string> ProjectNames { get; private set; }

		public ICommand ExitCommand { get; private set; }
		public ICommand OpenWikiInBrowserCommand { get; private set; }
		public ICommand ShowAboutDialogCommand { get; private set; }

		public AppDataSource()
		{
			ProjectNames = new List<string>();

			ExitCommand = new DelegateCommand(Exit);
			OpenWikiInBrowserCommand = new DelegateCommand(OpenWikiInBrowser);
			ShowAboutDialogCommand = new DelegateCommand(ShowAboutDialog);
		}

		private void Exit(object parameter)
		{
			Application.Current.Shutdown();
		}

		private void OpenWikiInBrowser(object parameter)
		{
			System.Diagnostics.Process.Start("https://github.com/astruyk/A3_Keymaster/wiki");
		}

		private void ShowAboutDialog(object parameter)
		{
			System.Windows.MessageBox.Show("Keymaster Version " + System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion);
		}
	}
}
