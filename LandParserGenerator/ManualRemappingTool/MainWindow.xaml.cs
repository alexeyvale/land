using Land.Control;
using Land.Core;
using ManualRemappingTool.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace ManualRemappingTool
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		#region Consts

		private const int MIN_FONT_SIZE = 8;
		private const int MAX_FONT_SIZE = 40;

		private static readonly string APP_DATA_DIRECTORY =
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\LanD Control";
		private static readonly string CACHE_DIRECTORY =
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\LanD Control\Cache";
		public static readonly string SETTINGS_FILE_NAME = "LandExplorerSettings.xml";

		public static string SETTINGS_DEFAULT_PATH =>
			System.IO.Path.Combine(APP_DATA_DIRECTORY, SETTINGS_FILE_NAME);

		#endregion

		private object FileOpeningLock { get; set; } = new object();
		private ParserManager Parsers { get; set; } = new ParserManager();
		private Dataset Dataset { get; set; }
		
		public MainWindow()
		{
			InitializeComponent();

			SourceFileView.Parsers = Parsers;
			SourceFileView.FileEditor.PreviewMouseWheel += Control_PreviewMouseWheel;
			SourceFileView.FileEditor.TextArea.TextView.ScrollOffsetChanged += FileView_ScrollOffsetChanged;
			SourceFileView.FileElementsList.PreviewMouseWheel += Control_PreviewMouseWheel;

			TargetFileView.Parsers = Parsers;
			TargetFileView.FileEditor.PreviewMouseWheel += Control_PreviewMouseWheel;
			TargetFileView.FileEditor.TextArea.TextView.ScrollOffsetChanged += FileView_ScrollOffsetChanged;
			TargetFileView.FileElementsList.PreviewMouseWheel += Control_PreviewMouseWheel;

			Parsers.Load(LoadSettings(SETTINGS_DEFAULT_PATH), CACHE_DIRECTORY, new List<Message>());
		}

		private void MainWindow_ContentRendered(object sender, EventArgs e)
		{
			StartWindowInteraction();
		}

		private void FileView_ScrollOffsetChanged(object sender, EventArgs e)
		{
			if (Keyboard.PrimaryDevice.Modifiers == ModifierKeys.Alt)
			{
				SyncViewsButton_Click(null, null);
			}
		}

		private void Control_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			var controlSender = sender as System.Windows.Controls.Control;

			if (Keyboard.PrimaryDevice.Modifiers == ModifierKeys.Control)
			{
				e.Handled = true;

				if (e.Delta > 0 && controlSender.FontSize < MAX_FONT_SIZE)
					++controlSender.FontSize;
				else if (controlSender.FontSize > MIN_FONT_SIZE)
					--controlSender.FontSize;
			}
		}

		private void NewDatasetButton_Click(object sender, RoutedEventArgs e)
		{
			if (Dataset.Records.Count > 0)
			{
				switch (MessageBox.Show(
						"Сохранить текущий датасет?",
						String.Empty,
						MessageBoxButton.YesNoCancel,
						MessageBoxImage.Question))
				{
					case MessageBoxResult.Yes:
						SaveDatasetButton_Click(sender, e);
						break;
					case MessageBoxResult.No:
						break;
					case MessageBoxResult.Cancel:
						return;
				}
			}

			Dataset.New();
			DatasetTree.ItemsSource = Dataset.Records;
		}

		private void LoadDatasetButton_Click(object sender, RoutedEventArgs e)
		{
			if (Dataset.Records.Count > 0) {
				switch (MessageBox.Show(
						"Сохранить текущий датасет?",
						String.Empty,
						MessageBoxButton.YesNoCancel,
						MessageBoxImage.Question))
				{
					case MessageBoxResult.Yes:
						SaveDatasetButton_Click(sender, e);
						break;
					case MessageBoxResult.No:
						break;
					case MessageBoxResult.Cancel:
						return;
				}
			}

			StartWindowInteraction();
		}

		private void SaveDatasetButton_Click(object sender, RoutedEventArgs e)
		{
			if(!String.IsNullOrEmpty(Dataset.SavingPath))
			{
				Dataset.Save();
			}
			else
			{
				SaveDatasetAsButton_Click(sender, e);
			}
		}

		private void SaveDatasetAsButton_Click(object sender, RoutedEventArgs e)
		{
			var saveFileDialog = new SaveFileDialog()
			{
				AddExtension = true,
				DefaultExt = "ds.txt",
				Filter = "Текстовые файлы (*.ds.txt)|*.ds.txt|Все файлы (*.*)|*.*"
			};

			if (saveFileDialog.ShowDialog() == true)
			{
				Dataset.SavingPath = saveFileDialog.FileName;
				SaveDatasetButton_Click(sender, e);

				Settings.Default.RecentDatasets.Insert(0, Dataset.SavingPath);
				Settings.Default.Save();
			}
		}

		private void AddToDatasetButton_Click(object sender, RoutedEventArgs e)
		{
			Dataset.Add(
				SourceFileView.FilePath,
				TargetFileView.FilePath,
				SourceFileView.EntityStartLine.Value,
				TargetFileView.EntityStartLine.Value,
				SourceFileView.EntityType
			);
		}

		private void RemoveFromDatasetButton_Click(object sender, RoutedEventArgs e)
		{
			Dataset.Remove(
				SourceFileView.FilePath,
				TargetFileView.FilePath,
				SourceFileView.EntityStartLine.Value,
				TargetFileView.EntityStartLine.Value,
				SourceFileView.EntityType
			);
		}

		private void HaveDoubtsButton_Click(object sender, RoutedEventArgs e)
		{
			Dataset.Add(
				SourceFileView.FilePath,
				TargetFileView.FilePath,
				SourceFileView.EntityStartLine.Value,
				TargetFileView.EntityStartLine.Value,
				SourceFileView.EntityType,
				true
			);
		}

		private void SourceFileView_FileOpened(object sender, string e)
		{
			if (OpenPairCheckBox.IsChecked ?? false)
			{
				var targetPath = Path.Combine(TargetFileView.WorkingDirectory, e);

				if (File.Exists(targetPath))
				{
					TargetFileView.OpenFile(targetPath);
				}
				else
				{
					Control_MessageSent(null, "Парный файл отсутствует");
				}
			}
		}

		private void TargetFileView_FileOpened(object sender, string e)
		{
			if (OpenPairCheckBox.IsChecked ?? false)
			{
				var sourcePath = Path.Combine(SourceFileView.WorkingDirectory, e);

				if (File.Exists(sourcePath))
				{
					SourceFileView.OpenFile(sourcePath);
				}
				else
				{
					Control_MessageSent(null, "Парный файл отсутствует");
				}
			}
		}

		private void Control_MessageSent(object sender, string e)
		{
			this.Title = $"{e} - {DateTime.Now}";
		}

		private void SyncViewsButton_Click(object sender, RoutedEventArgs e)
		{
			var offset = SourceFileView.FileEditor.TextArea.TextView.ScrollOffset.Y;

			if (TargetFileView.FileEditor.TextArea.TextView.DocumentHeight > offset)
			{
				TargetFileView.FileEditor.ScrollToVerticalOffset(offset);
			}
		}

		#region Helpers

		private LandExplorerSettings LoadSettings(string path)
		{
			if (File.Exists(path))
			{
				var serializer = new DataContractSerializer(
					typeof(LandExplorerSettings), new Type[] { typeof(ParserSettingsItem) }
				);

				using (FileStream fs = new FileStream(path, FileMode.Open))
				{
					return (LandExplorerSettings)serializer.ReadObject(fs);
				}
			}
			else
			{
				return null;
			}
		}

		private StartWindow CreateStartWindow()
		{
			var startWindow = new StartWindow();

			startWindow.Owner = this;

			startWindow.DatasetObject = this.Dataset ?? new Dataset();
			startWindow.DatasetObject.New();

			return startWindow;
		}

		private void StartWindowInteraction()
		{
			var startWindow = CreateStartWindow();

			if (startWindow.ShowDialog() ?? false)
			{
				Dataset = startWindow.DatasetObject;
				DatasetTree.ItemsSource = Dataset.Records;

				SourceFileView.WorkingDirectory = Dataset.SourceDirectoryPath;
				TargetFileView.WorkingDirectory = Dataset.TargetDirectoryPath;

				SourceFileView.WorkingExtensions = TargetFileView.WorkingExtensions =
					Dataset.Extensions;
			}
			else
			{
				if(Dataset == null)
				{
					this.Close();
				}
			}
		}

		#endregion
	}
}
