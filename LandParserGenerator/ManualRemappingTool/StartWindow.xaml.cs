using ManualRemappingTool.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ManualRemappingTool
{
	/// <summary>
	/// Логика взаимодействия для StartWindow.xaml
	/// </summary>
	public partial class StartWindow : Window
	{
		public Dataset Dataset { get; set; } = new Dataset();

		public StartWindow()
		{
			InitializeComponent();

			if (Settings.Default.RecentDatasets == null)
			{
				Settings.Default.RecentDatasets = new System.Collections.Specialized.StringCollection();
			}

			DatasetList.ItemsSource = Settings.Default.RecentDatasets;
		}

		private void LoadDatasetButton_Click(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog
			{
				AddExtension = true,
				DefaultExt = "ds.txt",
				Filter = "Текстовые файлы (*.ds.txt)|*.ds.txt|Все файлы (*.*)|*.*"
			};

			if (openFileDialog.ShowDialog() == true)
			{
				Dataset.Load(openFileDialog.FileName);

				Settings.Default.RecentDatasets.Insert(0, openFileDialog.FileName);
				Settings.Default.Save();
			}
		}

		private void DatasetList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left && DatasetList.SelectedItem != null)
			{
				Dataset.Load((string)DatasetList.SelectedItem);

				var selected = (string)DatasetList.SelectedItem;

				Settings.Default.RecentDatasets.Remove(selected);
				Settings.Default.RecentDatasets.Insert(0, selected);
				Settings.Default.Save();
			}
		}

		private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
		{
			var folderDialog = new System.Windows.Forms.FolderBrowserDialog();

			if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				if(sender == SelectSourceFolderButton)
				{
					Dataset.SourceDirectoryPath = folderDialog.SelectedPath;
				}
				else
				{
					Dataset.TargetDirectoryPath = folderDialog.SelectedPath;
				}

				HandleDatasetUpdated();
			}
		}

		private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
		{

		}

		private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
		{
			DeleteSelectedRecent();
		}

		private void ContextMenu_Loaded(object sender, RoutedEventArgs e)
		{
			foreach (MenuItem item in DatasetList.ContextMenu.Items)
			{
				item.IsEnabled = DatasetList.SelectedItem != null;
			}
		}

		private void DatasetList_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Delete)
			{
				DeleteSelectedRecent();
			}
		}

		#region Helpers

		private void DeleteSelectedRecent()
		{
			if (DatasetList.SelectedItem != null)
			{
				var selected = DatasetList.SelectedIndex;

				Settings.Default.RecentDatasets.RemoveAt(selected);
				Settings.Default.Save();

				DatasetList.SelectedIndex = selected == DatasetList.Items.Count ? -1 : selected;
			}
		}

		private void HandleDatasetUpdated()
		{

		}

		#endregion
	}
}
