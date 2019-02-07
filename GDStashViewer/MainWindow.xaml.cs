using GDStashLib;
using GDStashViewer.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;

namespace GDStashViewer
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	///

	public partial class MainWindow : Window
	{
		private CollectionViewSource _collectionViewSource;
		private const int maxFilterHistory = 30;
		private const int maxMru = 30;
		private const string DontExportFilename = "DontExport.txt";
		private List<GDStash> _stashes;
		private readonly string _defaultTitle = "GDStashViewer " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
		private ConcurrentDictionary<int, int> _urlCache;
		private ParallelOptions _parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

		public MainWindow()
		{
			InitializeComponent();
			if (Settings.Default.UpgradeRequired)
			{
				Settings.Default.Upgrade();
				Settings.Default.UpgradeRequired = false;
				Settings.Default.Save();
			}

			_dataGrid.AllowDrop = true;
			_dataGrid.DragEnter += new DragEventHandler(Window_DragEnter);
			_dataGrid.Drop += new DragEventHandler(Window_DragDrop);
		}

		private void Window_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				e.Effects = DragDropEffects.Copy;
			}
		}

		private void Window_DragDrop(object sender, DragEventArgs e)
		{
			string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
			Cursor = Cursors.Wait;
			OpenStashFiles(files);
			Cursor = Cursors.Arrow;
		}

		private void OpenStashFiles(IEnumerable<string> filenames, bool includePlayerCharacters = false)
		{
			Cursor = Cursors.Wait;
			bool showErrors = true;
			Stopwatch sw = Stopwatch.StartNew();
			string extractBaseFolder;
			if (string.IsNullOrEmpty(Properties.Settings.Default.ExtractedRootFolder))
			{
				extractBaseFolder = Path.Combine(Environment.CurrentDirectory, @"\Extracted\");
			}
			else
			{
				extractBaseFolder = Properties.Settings.Default.ExtractedRootFolder;
			}

			if (!File.Exists(Path.Combine(extractBaseFolder, @"records\items\gearweapons\axe1h\a01_axe000.dbr")))
			{
				MessageBox.Show("Please Configure Settings to point to a valid root folder where the GD game database is Extracted", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				Cursor = Cursors.Arrow;

				return;
			}

			string resourceFolder;
			if (string.IsNullOrEmpty(Properties.Settings.Default.ExtractedRootFolder))
			{
				resourceFolder = Path.Combine(Environment.CurrentDirectory, @"\Extracted\");
			}
			else
			{
				resourceFolder = Properties.Settings.Default.ResourceFolder;
			}

			if (!File.Exists(Path.Combine(resourceFolder, @"tags_items.txt")))
			{
				MessageBox.Show("Please Configure Settings to point to a valid Resource folder containing 'tag*.txt' files", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				Cursor = Cursors.Arrow;

				return;
			}
			string title = _defaultTitle;
			ConcurrentBag<GDStash> stashes = new ConcurrentBag<GDStash>();
			_stashes = new List<GDStash>(filenames.Count());
			Dictionary<string, string> friendlyNames = null;
			if (File.Exists(Settings.Default.ListCfgFile))
			{
				friendlyNames = GDStash.GetFriendlyNames(Settings.Default.ListCfgFile);
			}
			//Parallel.ForEach(filenames, _parallelOptions, filename =>
			foreach (string filename in filenames)
			{
				if (File.Exists(filename))
				{
					GDStash stash = new GDStash();
					stash.Open(filename);
					stash.UpdateItems(extractBaseFolder, resourceFolder, Settings.Default.ListCfgFile, Settings.Default.ShouldResolveAffixes, Settings.Default.UseAlternateAffixFormat);
					stashes.Add(stash);
				}
				else
				{
					if (showErrors)
					{
						showErrors = MessageBox.Show("Stash File Not Found: " + filename + "\n\nDo you wish to be notified of other files not found?", "File Not Found", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes;
					}
				}
			}
			//);
			_stashes = stashes.ToList();

			List<GDStashItem> itemList = new List<GDStashItem>();
			foreach (GDStash stash in _stashes)
			{
				string filename = stash.FileName;
				string key = Path.GetFileName(filename);
				itemList.AddRange(stash.Items);
				AddFilesToMru(filename);
				if (friendlyNames != null && friendlyNames.ContainsKey(key))
				{
					title += string.Format(" {0}-{1}", friendlyNames[key], filename);
				}
				else
				{
					title += " " + filename;
				}
			}
			Title = title;
			BuildMruMenu(friendlyNames);
			ConcurrentDictionary<string, bool> dontExportDict = ReadDontExportItemsList();
			Parallel.ForEach(itemList, _parallelOptions, item =>
			//foreach(var item in itemList)
			{
				if (!string.IsNullOrEmpty(item.Name))
				{
					int key = item.EmpoweredName.ToLower().GetHashCode();
					int number;
					if (_urlCache.ContainsKey(key))
					{
						number = _urlCache[key];
						item.Url = string.Format("http://www.grimtools.com/db/items/{0}", number);
					}
					else if (item.Tag == "tagMedalD002")//Badge of Mastery edge case, key wont be in dict if affixes are resolved
					{
						item.Url = "http://www.grimtools.com/db/items/794";
					}
					string key2 = item.Tag + item.seed;
					if (dontExportDict != null && dontExportDict.ContainsKey(key2))
					{
						item.DontExport = dontExportDict[key2];
					}
				}
			}
			);

			if (Settings.Default.IncludePlayerCharacters)
			{
				//TODO: Major Cleanup
				string savedGamesFolder = GDPlayer.GetSavedGamesDir();
				string charsFolder = Path.Combine(savedGamesFolder, @"Grim Dawn\save\main\");
				string errorMessage = string.Empty;
				if (Directory.Exists(charsFolder))
				{
					foreach (string folder in Directory.EnumerateDirectories(charsFolder))
					{
						GDPlayer player = new GDPlayer();
						string filename = Path.Combine(folder, @"player.gdc");
						try
						{
							player.read(filename);
						}
						catch (Exception)
						{
							errorMessage += filename + "\r\n";
						}

						GDStash stash = new GDStash
						{
							FriendlyName = player.hdr.name + " (Inventory)",
							FileName = filename,
							Bags = new List<GDStashBag>(player.inv.Bags.Count)
						};
						stash.Bags.AddRange(player.inv.Bags);
						stash.UpdateItems(extractBaseFolder, resourceFolder, Settings.Default.ListCfgFile, Settings.Default.ShouldResolveAffixes, Settings.Default.UseAlternateAffixFormat);
						itemList.AddRange(stash.Items);

						stash = new GDStash
						{
							FriendlyName = player.hdr.name + " (Stash)",
							FileName = filename,
							Bags = new List<GDStashBag>(player.stash.Bags.Count)
						};
						stash.Bags.AddRange(player.stash.Bags);
						stash.UpdateItems(extractBaseFolder, resourceFolder, Settings.Default.ListCfgFile, Settings.Default.ShouldResolveAffixes, Settings.Default.UseAlternateAffixFormat);
						itemList.AddRange(stash.Items);

						stash = new GDStash
						{
							FriendlyName = player.hdr.name + " (Equipped)",
							FileName = filename,
							Bags = new List<GDStashBag>(1)
						};
						stash.Bags.Add(new GDStashBag());
						stash.Bags[0].Items = new List<GDStashItem>(player.inv.equipment.Where(i => i.baseName != null));

						stash.UpdateItems(extractBaseFolder, resourceFolder, Settings.Default.ListCfgFile, Settings.Default.ShouldResolveAffixes, Settings.Default.UseAlternateAffixFormat);
						itemList.AddRange(stash.Items);

						//stash.Items.AddRange(player.inv.Items);

						//stash.Bags.AddRange(player.stash.Bags);
						//stash.Items.AddRange(player.stash.Items);

					}
				}
				if(!string.IsNullOrEmpty(errorMessage))
				MessageBox.Show("Error Reading Character File(s) :\r\n" + errorMessage
							+ "The file(s) may be in the old format, try Loading the character(s) in the game and exit before trying again.\r\n Program will continue", "Error Reading Character File(s)", MessageBoxButton.OK, MessageBoxImage.Warning);
			}

			itemList.Sort((item1, item2) => { return item1.Name.CompareTo(item2.Name); });
			_collectionViewSource = new CollectionViewSource
			{
				Source = itemList
			};
			_dataGrid.ItemsSource = _collectionViewSource.View;
			RefreshColumnLayout();
			sw.Stop();
			TextBlock tb = new TextBlock
			{
				Text = string.Format("{0} Stash File(s) Opened at {1}. Elapsed Time: {2} seconds.", _stashes.Count, DateTime.Now.ToLocalTime().ToShortTimeString(), Math.Round(sw.Elapsed.TotalSeconds, 2))
			};

			//tb.ToolTip = String.Join(" , ",_stashes.Select(s => s.FileName));

			StatusText.Content = tb;
			Cursor = Cursors.Arrow;
		}

		private void ReloadOpenStashFiles()
		{
			if (_stashes != null && _stashes.Any())
			{
				OpenStashFiles(_stashes.Select(s => s.FileName));
			}
		}

		private void AddFilesToMru(string filename)
		{
			StringCollection strings = Settings.Default.FileHistory;
			if (strings.Contains(filename))
			{
				strings.Remove(filename);
			}

			strings.Insert(0, filename);
			if (strings.Count > maxMru)
			{
				strings.RemoveAt(20);
			}

			Settings.Default.Save();
		}

		private void BuildMruMenu(Dictionary<string, string> friendlyNames)
		{
			//Dictionary<string, string> friendlyNames = null;
			//if (File.Exists(Properties.Settings.Default.ListCfgFile))
			//{
			//  friendlyNames = GDStash.GetFriendlyNames(Properties.Settings.Default.ListCfgFile);
			//}
			menuMru.Items.Clear();
			menuMru.Items.Add(App.Current.Resources["infoMenuItem"]);

			foreach (string filename in Settings.Default.FileHistory)
			{
				MenuItem menuItem = new MenuItem();
				string fileNameShort = Path.GetFileName(filename);
				if (friendlyNames != null && friendlyNames.ContainsKey(Path.GetFileName(filename)))
				{
					string friendlyName = friendlyNames[Path.GetFileName(filename)];
					menuItem.Header = friendlyName + " (" + fileNameShort + ")";
					menuItem.ToolTip = filename;
				}
				else
				{
					menuItem.Header = fileNameShort;
					menuItem.ToolTip = filename;
				}
				menuItem.Tag = filename;
				menuItem.Click += MenuItem_Click;
				menuMru.Items.Add(menuItem);
			}
		}

		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{
			MenuItem menuItem = sender as MenuItem;
			OpenStashFiles(new string[] { (string)menuItem.Tag });
		}

		private bool TextFilter(object gdItem)
		{
			if (string.IsNullOrWhiteSpace(filterText.Text))
			{
				return true;
			}

			GDStashItem item = gdItem as GDStashItem;
			if (item == null)
			{
				return false;
			}

			if (string.IsNullOrEmpty(item.Name))
			{
				return false;
			}

			string fieldValue = null;
			if (fieldCombo.SelectedIndex == 0)
			{
				fieldValue = item.Name.ToUpper();
			}
			else if (fieldCombo.SelectedIndex == 1)
			{
				fieldValue = item.Category.ToUpper();
			}
			else if (fieldCombo.SelectedIndex == 2)
			{
				fieldValue = item.SubCategory.ToUpper();
			}

			if (string.IsNullOrEmpty(fieldValue))
			{
				return false;
			}

			string searchTerm = filterText.Text.ToUpper();
			//switch case can suck it
			if ((filterCombo.SelectedIndex == 0) && (fieldValue.Contains(searchTerm)))
			{
				return true;
			}
			else if ((filterCombo.SelectedIndex == 1) && (fieldValue.StartsWith(searchTerm)))
			{
				return true;
			}
			else if ((filterCombo.SelectedIndex == 2) && (fieldValue.EndsWith(searchTerm)))
			{
				return true;
			}
			else if ((filterCombo.SelectedIndex == 3) && (fieldValue.Equals(searchTerm, StringComparison.CurrentCultureIgnoreCase)))
			{
				return true;
			}

			return false;
		}

		private void ShowFilteredItem(object sender, FilterEventArgs e)
		{
			e.Accepted = TextFilter(e.Item as GDStashItem);
		}

		private void filterButton_Click(object sender, RoutedEventArgs e)
		{
			if (_collectionViewSource == null || _collectionViewSource.View == null)
			{
				return;
			}
			if (!string.IsNullOrWhiteSpace(filterText.Text))
			{
				_collectionViewSource.Filter -= ShowFilteredItem;
				//_collectionViewSource.View.Filter = new Predicate<object>(NameFilter);
				_collectionViewSource.Filter += ShowFilteredItem;
				//_collectionViewSource.View.Refresh();
			}
			else
			{
				_collectionViewSource.Filter -= ShowFilteredItem;
			}
			//_collectionViewSource.View.Refresh();

			if (Settings.Default.FilterHistory.Contains(filterText.Text))
			{
				return;
			}

			Settings.Default.FilterHistory.Insert(0, filterText.Text);
			if (Settings.Default.FilterHistory.Count > maxFilterHistory)
			{
				Settings.Default.FilterHistory.RemoveAt(10);
			}

			Settings.Default.Save();
			filterText.Items.Refresh();
		}

		private void filterButtonClear_Click(object sender, RoutedEventArgs e)
		{
			if (_collectionViewSource.View == null)
			{
				return;
			}

			//_collectionViewSource.View.Filter = null;
			//_collectionViewSource.View.Refresh();
			_collectionViewSource.Filter -= ShowFilteredItem;
			filterText.Text = string.Empty;
		}

		private void exportTextButton_Click(object sender, RoutedEventArgs e)
		{
			if (_collectionViewSource == null || _collectionViewSource.View == null || _collectionViewSource.View.IsEmpty)
			{
				MessageBox.Show("No items to export, please load a stash file containing items or change data filters", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}
			SaveFileDialog dlg = new SaveFileDialog
			{
				Filter = "Text Files|*.txt|All Files|*.*",
				Title = "Export (filtered) Data as Text File"
			};
			if (dlg.ShowDialog() != true)
			{
				return;
			}

			GDExporter.ExportToTextFile(dlg.FileName, _collectionViewSource);
			Hyperlink link = new Hyperlink(new Run(dlg.FileName))
			{
				NavigateUri = new System.Uri(dlg.FileName, UriKind.Absolute)
			};
			link.RequestNavigate += (ls, le) =>
			{
				System.Diagnostics.Process.Start(le.Uri.ToString());
			};
			StackPanel sp = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
			sp.Children.Add(new TextBlock() { Text = "Exported to File ", VerticalAlignment = VerticalAlignment.Center });
			sp.Children.Add(new Label() { Content = link, VerticalAlignment = VerticalAlignment.Center });
			sp.Children.Add(new TextBlock() { Text = " At " + DateTime.Now.ToLocalTime().ToShortTimeString(), VerticalAlignment = VerticalAlignment.Center });
			StatusText.Content = sp;
		}


		private void settingsButton_Click(object sender, RoutedEventArgs e)
		{
			SettingsWindow settingsWindow = new SettingsWindow
			{
				Owner = this
			};
			settingsWindow.ShowDialog();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default.Reload();
			Title = _defaultTitle;
			if (Settings.Default.FilterHistory == null)
			{
				Settings.Default.FilterHistory = new System.Collections.Specialized.StringCollection();
			}

			if (Settings.Default.FileHistory == null)
			{
				Settings.Default.FileHistory = new System.Collections.Specialized.StringCollection();
			}

			filterText.ItemsSource = Settings.Default.FilterHistory;

			Settings.Default.FileHistory.Remove("<none>");
			Dictionary<string, string> friendlyNames = null;
			if (File.Exists(Properties.Settings.Default.ListCfgFile))
			{
				friendlyNames = GDStash.GetFriendlyNames(Properties.Settings.Default.ListCfgFile);
			}
			BuildUrlCache(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "ItemLinks.csv"));
			BuildMruMenu(friendlyNames);
			string[] args = Environment.GetCommandLineArgs();
			if (args != null && args.Length > 1)
			{
				//1st arg is self, so skip first
				OpenStashFiles(args.Skip(1).Take(args.Length - 1));
			}

		}

		private void BuildUrlCache(string filename)
		{
			//if (Settings.Default.ExportShouldUseUrl && _urlCache == null)
			//{
			_urlCache = new ConcurrentDictionary<int, int>(Environment.ProcessorCount, 3400);
			using (StreamReader reader = File.OpenText(filename))
			{
				string line;
				string number;
				int nameHash;
				int splitPos;
				while (!reader.EndOfStream)
				{
					line = reader.ReadLine();
					splitPos = line.IndexOf(',');
					number = line.Substring(0, splitPos);
					nameHash = line.Substring(splitPos + 1).ToLower().GetHashCode();
					if (!_urlCache.ContainsKey(nameHash))
					{
						_urlCache.TryAdd(nameHash, int.Parse(number));
					}
				}
			}
			//}
		}

		private void SaveColumnLayout()
		{
			//Properties.Settings.Default.ColumnLayout = System.Net.WebUtility.HtmlEncode(_dataGrid.GetColumnInformation());
			Properties.Settings.Default.ColumnLayout = _dataGrid.GetColumnInformation();
			Properties.Settings.Default.Save();
		}

		private void ResetColumnLayout()
		{
			string columnInfo = Properties.Settings.Default.Properties["ColumnLayout"].DefaultValue.ToString();
			Properties.Settings.Default.ColumnLayout = columnInfo;
			Properties.Settings.Default.Save();
			_dataGrid.SetColumnInformation(columnInfo);
			LinkColumn.DisplayIndex = 0;
			LinkColumn.Width = 40;
		}

		private void RefreshColumnLayout()
		{
			string columnInfo = Properties.Settings.Default.ColumnLayout;
			columnInfo = System.Net.WebUtility.HtmlDecode(columnInfo);
			_dataGrid.SetColumnInformation(columnInfo);
			LinkColumn.DisplayIndex = 0;
			LinkColumn.Width = 40;
		}

		private void exportCsv_Click(object sender, RoutedEventArgs e)
		{
			if (_collectionViewSource.View != null && _collectionViewSource.View.IsEmpty)
			{
				MessageBox.Show("No items to export", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}
			SaveFileDialog dlg = new SaveFileDialog
			{
				Filter = "Csv Files|*.Csv|Text Files|*.txt|All Files|*.*",
				Title = "Export Data as Comma Separated Values"
			};

			if (dlg.ShowDialog() != true)
			{
				return;
			}

			try
			{
				_dataGrid.ExportToCsv("GDStashViewer", dlg.FileName, false);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Error Copying Data to Clipboard\nThis may be a result of a known bug for some users. You can always select all, copy then paste data manually\n" + ex.Message, "Error Copying Data to Clipboard", MessageBoxButton.OK, MessageBoxImage.Error);
				// throw;
			}
		}

		private void OpenCmdExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			OpenFileDialog dlg = new OpenFileDialog
			{
				Title = "Select one or more GD Stash File(s) to load (default is 'Transfer.gst'; GD Stash Manager doesn't use .GST extension for its files)",
				Multiselect = true
			};
			if (dlg.ShowDialog() != true)
			{
				return;
			}

			OpenStashFiles(dlg.FileNames);
		}

		private void SaveGridLayout_Click(object sender, RoutedEventArgs e)
		{
			SaveColumnLayout();
		}

		private void ResetGridLayout_Click(object sender, RoutedEventArgs e)
		{
			ResetColumnLayout();
		}

		public void OpenAllListCfgStashes()
		{
			string gstFolder = Settings.Default.GSTFolder;
			if (!Directory.Exists(gstFolder))
			{
				MessageBox.Show("Please Configure Settings to point to a valid folder containing the *.GST files (or GD Stash Manager's GST folder)", "Error: *.GST Folder not found", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}


			//List<string> fileNames = GDStash.GetFriendlyNames(Settings.Default.ListCfgFile).Keys.ToList();
			//fileNames = fileNames.Select(s => s = Path.Combine(gstFolder, s)).ToList();
			OpenStashFiles(Directory.GetFiles(gstFolder, "*.gst"));
			Title = _defaultTitle + " All GD Stash Manager Stashes in Folder";
		}

		private void menuOpenList_Click(object sender, RoutedEventArgs e)
		{
			//WTB MVVVM
			OpenAllListCfgStashes();
		}

		private void dataGrid_Loaded(object sender, RoutedEventArgs e)
		{
			RefreshColumnLayout();
		}

		private void menuReload_Click(object sender, RoutedEventArgs e)
		{
			ReloadOpenStashFiles();
		}

		private void menuOpenFolder_Click(object sender, RoutedEventArgs e)
		{
			if (_stashes != null && _stashes.Any())
			{
				System.Diagnostics.Process.Start(Path.GetDirectoryName(_stashes[0].FileName));
			}
		}

		protected override void OnPreviewMouseWheel(MouseWheelEventArgs args)

		{
			base.OnPreviewMouseWheel(args);

			if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
			{
				gridZoomSlider.Value += (args.Delta > 0) ? gridZoomSlider.SmallChange : 0 - gridZoomSlider.SmallChange;
			}
		}

		private void ResetGridZoom()
		{
			gridZoomSlider.Value = 1.0;
		}

		protected override void OnPreviewMouseDown(MouseButtonEventArgs args)

		{
			base.OnPreviewMouseDown(args);
			if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
			{
				if (args.MiddleButton == MouseButtonState.Pressed)
				{
					ResetGridZoom();
				}
			}
		}

		private void Label_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			ResetGridZoom();
		}

		private void ResetGridZoom_Click(object sender, RoutedEventArgs e)
		{
			ResetGridZoom();
		}

		private void dataGrid_ColumnLayoutChanged(object sender, DataGridColumnEventArgs e)
		{
			SaveColumnLayout();
		}

		private void Hyperlink_Click(object sender, RoutedEventArgs e)
		{
			Hyperlink link = (Hyperlink)e.OriginalSource;
			if (link.NavigateUri != null)
			{
				Process.Start(link.NavigateUri.AbsoluteUri);
			}
		}

		public void WriteDontExportItemsList()
		{
			using (StreamWriter f = File.CreateText(DontExportFilename))
			{
				foreach (GDStash stash in _stashes)
				{
					foreach (GDStashItem item in stash.Items.Where(i => i.DontExport))
					{
						f.Write("True,");
						f.WriteLine(item.Tag + item.seed);
					}
				}
				f.Close();
			}
		}

		public ConcurrentDictionary<string, bool> ReadDontExportItemsList()
		{
			if (!File.Exists(DontExportFilename))
			{
				return null;
			}

			ConcurrentDictionary<string, bool> dontExportDict = new ConcurrentDictionary<string, bool>();
			string line;

			string[] parts;
			using (StreamReader f = File.OpenText(DontExportFilename))
			{
				while (!f.EndOfStream)
				{
					line = f.ReadLine();
					parts = line.Split(',');
					if (parts != null && parts.Length == 2)
					{
						bool.TryParse(parts[0], out bool dontExport);
						dontExportDict.TryAdd(parts[1], dontExport);
					}
				}
				f.Close();
			}
			return dontExportDict;
		}

		private void menuSaveDontExport_Click(object sender, RoutedEventArgs e)
		{
			WriteDontExportItemsList();
		}

		private void settingsRenameListCfg_Click(object sender, RoutedEventArgs e)
		{
			string listCfgFileContents = File.ReadAllText(Settings.Default.ListCfgFile);
			string[] cfgList = listCfgFileContents.Split('®');
			string filename;
			Dictionary<string, string> listCfgNames = new Dictionary<string, string>(cfgList.Length);
			foreach (string cfgEntry in cfgList)
			{
				string[] cfgEntryRecord = cfgEntry.Split('♂');
				if (cfgEntryRecord.Length > 2)
				{
					filename = cfgEntryRecord[0];
					Array.ForEach(Path.GetInvalidFileNameChars(), c => filename = filename.Replace(c.ToString(), string.Empty));
					if (!listCfgNames.ContainsValue(filename))
					{
						listCfgNames.Add(cfgEntryRecord[2], filename + ".gst");
					}
					else//name collision, unlikely
					{
						filename += cfgEntryRecord[0].GetHashCode();
						listCfgNames.Add(cfgEntryRecord[2], filename + ".gst");
					}
				}
			}
			string gstFolder = Path.Combine(Path.GetDirectoryName(Settings.Default.ListCfgFile), @"GST\");
			//foreach (string key in listCfgNames.Keys)
			//{

			//	filename = listCfgNames[key];
			//	if (key != filename)
			//	{
			//		File.Move(Path.Combine(gstFolder, key), Path.Combine(gstFolder, filename));
			//		listCfgFileContents = listCfgFileContents.Replace(key, filename);
			//	}
			//}
			for (int i = 0; i < cfgList.Length; i++)
			{
				string cfgEntry = cfgList[i];
				string[] cfgEntryRecord = cfgEntry.Split('♂');
				if (cfgEntryRecord.Length > 2)
				{
					string key = cfgEntryRecord[2];
					filename = listCfgNames[key];
					if (key != filename)
					{
						File.Move(Path.Combine(gstFolder, key), Path.Combine(gstFolder, filename));
						cfgList[i] = cfgEntry.Replace(key, filename);
					}
				}
			}
			listCfgFileContents = "";
			foreach (string cfgEntry in cfgList)
			{
				listCfgFileContents += cfgEntry + '®';
			}

			File.WriteAllText(Settings.Default.ListCfgFile, listCfgFileContents);
		}
	}
}