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
		public Dataset DatasetObject { get; set; } = new Dataset();

		public static readonly DependencyProperty IsDatasetLocked = DependencyProperty.Register(
			"IsDatasetLocked",
			typeof(bool),
			typeof(StartWindow),
			new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
		);

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
				LoadDataset(openFileDialog.FileName);
			}
		}

		private void DatasetList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left && DatasetList.SelectedItem != null)
			{
				LoadDataset((string)DatasetList.SelectedItem);
			}
		}

		private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
		{
			var folderDialog = new System.Windows.Forms.FolderBrowserDialog();

			if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				if(sender == SelectSourceFolderButton)
				{
					SourceFolderPath.Text = folderDialog.SelectedPath;
				}
				else
				{
					TargetFolderPath.Text = folderDialog.SelectedPath;
				}
			}
		}

		private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (DatasetList.SelectedItem != null)
			{
				LoadDataset((string)DatasetList.SelectedItem);
			}
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

		private void ResetButton_Click(object sender, RoutedEventArgs e)
		{
			SetValue(IsDatasetLocked, false);
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
		}

		private void OkButton_Click(object sender, RoutedEventArgs e)
		{
			if (!(bool)GetValue(IsDatasetLocked))
			{
				DatasetObject.New();

				DatasetObject.ExtensionsString = Extensions.Text;
				DatasetObject.SourceDirectoryPath = SourceFolderPath.Text;
				DatasetObject.TargetDirectoryPath = TargetFolderPath.Text;
			}

			DialogResult = true;
		}

		#region Helpers

		private void DeleteSelectedRecent()
		{
			if (DatasetList.SelectedItem != null)
			{
				var selected = (string)DatasetList.SelectedItem;

				Settings.Default.RecentDatasets.Remove(selected);
				Settings.Default.Save();

				DatasetList.Items.Refresh();
				DatasetList.SelectedIndex = -1;
			}
		}

		private void LoadDataset(string filePath)
		{
			DatasetObject = Dataset.Load(filePath);
			SetValue(IsDatasetLocked, true);

			SourceFolderPath.Text = DatasetObject.SourceDirectoryPath;
			TargetFolderPath.Text = DatasetObject.TargetDirectoryPath;
			Extensions.Text = DatasetObject.ExtensionsString;

			Settings.Default.RecentDatasets.Remove(filePath);
			Settings.Default.RecentDatasets.Insert(0, filePath);
			Settings.Default.Save();

			DatasetList.Items.Refresh();
		}

		#endregion
	}
}
