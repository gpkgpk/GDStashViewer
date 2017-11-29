using System;
using System.Windows;
using Microsoft.Win32;

namespace GDStashViewer
{
	/// <summary>
	/// Interaction logic for SettingsWindow.xaml
	/// </summary>
	public partial class SettingsWindow : Window
	{
		public SettingsWindow()
		{
			InitializeComponent();
		}

		private void cancelButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			this.Close();
		}

		private void okButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			this.Close();
		}

		//private void browseItemTagFileButton_Click(object sender, RoutedEventArgs e)
		//{
		//	OpenFileDialog dialog = new OpenFileDialog();
		//	dialog.FileName = Properties.Settings.Default.ItemTagFile;
		//	dialog.Filter = "tags_items.txt file|tags_items.txt|All Files|*.*";
		//	if (dialog.ShowDialog() == true)
		//	{
		//		Properties.Settings.Default.ItemTagFile = dialog.FileName;
		//	}
		//}

		private void browseFolderButton_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new System.Windows.Forms.FolderBrowserDialog();
			dialog.SelectedPath = Properties.Settings.Default.ExtractedRootFolder;
			System.Windows.Forms.DialogResult result = dialog.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				Properties.Settings.Default.ExtractedRootFolder = dialog.SelectedPath;
			}
			this.Focus();
		}

		private void browseResFolderButton_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new System.Windows.Forms.FolderBrowserDialog();
			dialog.SelectedPath = Properties.Settings.Default.ResourceFolder;
			System.Windows.Forms.DialogResult result = dialog.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				Properties.Settings.Default.ResourceFolder = dialog.SelectedPath;
			}
			this.Focus();
		}
		private void browseListCfgFileButton_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog dialog = new OpenFileDialog();
			dialog.FileName = Properties.Settings.Default.ListCfgFile;
			dialog.Filter = "List.cfg file|List.cfg|All Files|*.*";
			if (dialog.ShowDialog() == true)
			{
				Properties.Settings.Default.ListCfgFile = dialog.FileName;
			}
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			if (DialogResult == true)
				Properties.Settings.Default.Save();
			else
				Properties.Settings.Default.Reload();
			//Properties.Settings.Default.Reload();
		}

		private void defaultsButton_Click(object sender, RoutedEventArgs e)
		{
			if (MessageBox.Show("This will revert all settings back to their default values (or empty) and this action cannot be undone.\nAre you sure you want to do this?", "Reset All Settings to Defaults?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
			{
				string extractedRootFolder = Properties.Settings.Default.ExtractedRootFolder;
				//string itemTagFile = Properties.Settings.Default.ItemTagFile;
				string listCfgFile = Properties.Settings.Default.ListCfgFile;
				Properties.Settings.Default.Reset();
				Properties.Settings.Default.ExtractedRootFolder = extractedRootFolder;
				//Properties.Settings.Default.ItemTagFile = itemTagFile;
				Properties.Settings.Default.ListCfgFile = listCfgFile;

			}

		}
	}
}