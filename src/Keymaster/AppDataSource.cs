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
		private readonly BackgroundWorker worker = new BackgroundWorker();

		public AppDataSource()
		{
			ExitCommand = new DelegateCommand(Exit);
			OpenWikiInBrowserCommand = new DelegateCommand(OpenWikiInBrowser);
			ShowAboutDialogCommand = new DelegateCommand(ShowAboutDialog);
			StartCommand = new DelegateCommand(Start, CanStart);
			_outputTextBuilder = new StringBuilder();

			worker.DoWork += worker_DoWork;
			worker.RunWorkerCompleted += worker_RunWorkerCompleted;
		}

		void worker_DoWork(object sender, DoWorkEventArgs e)
		{
			_outputTextBuilder.Clear();
			OnPropertyChanged("OutputText");

			var updater = (ServerUpdater) e.Argument;
			updater.OutputGenerated += updater_OutputGenerated;
			updater.DoUpdate();
		}

		void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			// DONE!
		}

		public void SetServerStettings(ServerSettings settings)
		{
			ServerSettings = settings;
			OnPropertyChanged("ServerSettings");
		}

		private void Start(object parameter)
		{
			ServerUpdater updater = new ServerUpdater(ServerSettings, ServerSettings.Configs.ElementAt(SelectedServerSettings));
			worker.RunWorkerAsync(updater);
		}

		private bool CanStart(object parameter)
		{
			return !worker.IsBusy;
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
