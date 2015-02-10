using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Gatekeeper
{
	class AppDataSource : INotifyPropertyChanged
	{
		public static bool ShowDebugOutput
		{
			get { return ServerUpdater.ShowDebugOutput; }
			set { ServerUpdater.ShowDebugOutput = value; }
		}

		public static bool SkipFtpActions
		{
			get { return ServerUpdater.SkipFtpActions; }
			set { ServerUpdater.SkipFtpActions = value; }
		}

		public ICommand ExitCommand { get; private set; }
		public ICommand OpenWikiInBrowserCommand { get; private set; }
		public ICommand ShowAboutDialogCommand { get; private set; }
		public ICommand StartCommand { get; private set; }
		public ServerSettings ServerSettings { get; private set; }
		public int SelectedServerSettings { get; set; }
		public String OutputText
		{
			get { return _outputTextBuilder.ToString(); }
		}

		private StringBuilder _outputTextBuilder;
		private readonly BackgroundWorker changeServerModeWorker = new BackgroundWorker();

		public AppDataSource()
		{
			ExitCommand = new DelegateCommand(Exit);
			OpenWikiInBrowserCommand = new DelegateCommand(OpenWikiInBrowser);
			ShowAboutDialogCommand = new DelegateCommand(ShowAboutDialog);
			StartCommand = new DelegateCommand(StartChangeServerMode, CanStartChangeServerMode);
			_outputTextBuilder = new StringBuilder();

			changeServerModeWorker.DoWork += changeServerModeWorker_DoWork;
			changeServerModeWorker.RunWorkerCompleted += changeServerModeWorker_RunWorkerCompleted;
		}

		void changeServerModeWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			_outputTextBuilder.Clear();
			OnPropertyChanged("OutputText");

			var updater = (ServerUpdater) e.Argument;
			updater.OutputGenerated += updater_OutputGenerated;
			updater.DoUpdate();
		}

		void changeServerModeWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			// DONE!
		}

		public void SetServerSettings(ServerSettings settings)
		{
			ServerSettings = settings;
			OnPropertyChanged("ServerSettings");
		}

		private void StartChangeServerMode(object parameter)
		{
			var needsUpdate = false;
			var assemblyVersionString = System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;
			if (ServerSettings.LatestMajorVersion != 0 || ServerSettings.LatestMinorVersion != 0 || ServerSettings.LatestPatchVersion != 0)
			{
				var versionParts = assemblyVersionString.Split('.').Select(x => int.Parse(x)).ToArray();
				if (versionParts[0] < ServerSettings.LatestMajorVersion || versionParts[1] < ServerSettings.LatestMinorVersion || versionParts[2] < ServerSettings.LatestPatchVersion)
				{
					needsUpdate = true;
				}
			}

			if (needsUpdate)
			{
				var messageString = string.Format("This version ({0}) is out of date. Latest version is {1}.{2}.{3}.", assemblyVersionString, ServerSettings.LatestMajorVersion, ServerSettings.LatestMinorVersion, ServerSettings.LatestPatchVersion);
				MessageBox.Show(messageString, "Update Required", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			else
			{
				ServerUpdater updater = new ServerUpdater(ServerSettings, ServerSettings.Configs.ElementAt(SelectedServerSettings));
				changeServerModeWorker.RunWorkerAsync(updater);
			}
		}

		private bool CanStartChangeServerMode(object parameter)
		{
			return !changeServerModeWorker.IsBusy;
		}

		void updater_OutputGenerated(object sender, string e)
		{
			_outputTextBuilder.AppendLine(e);
			OnPropertyChanged("OutputText");
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
			System.Windows.MessageBox.Show("Gatekeeper Version " + System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion);
		}

		private void OnPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}
}
