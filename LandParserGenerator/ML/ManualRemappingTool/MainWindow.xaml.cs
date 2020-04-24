using Land.Control;
using Land.Core;
using Land.Core.Parsing.Tree;
using Land.Markup.Binding;
using ManualRemappingTool.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

		private ParserManager Parsers { get; set; } = new ParserManager();
		private Dataset Dataset { get; set; }

		public List<Tuple<string, List<Tuple<string, List<DatasetRecord>>>>> RecordsToView { get; private set; }

		public MainWindow()
		{
			InitializeComponent();

			SourceFileView.Parsers = Parsers;
			SourceFileView.FileEditor.PreviewMouseWheel += Control_PreviewMouseWheel;
			SourceFileView.FileEditor.TextArea.TextView.ScrollOffsetChanged += FileView_ScrollOffsetChanged;
			SourceFileView.FileEntitiesList.PreviewMouseWheel += Control_PreviewMouseWheel;
			SourceFileView.AvailableEntitiesFilter = IsSourceEntityAvailable;

			TargetFileView.Parsers = Parsers;
			TargetFileView.FileEditor.PreviewMouseWheel += Control_PreviewMouseWheel;
			TargetFileView.FileEditor.TextArea.TextView.ScrollOffsetChanged += FileView_ScrollOffsetChanged;
			TargetFileView.FileEntitiesList.PreviewMouseWheel += Control_PreviewMouseWheel;

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
			DatasetPathLabel.Content = String.Empty;
			UpdateRecordsTree();
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
			if (!String.IsNullOrEmpty(Dataset.SavingPath))
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
				DatasetPathLabel.Content = Path.GetFileName(Dataset.SavingPath);
				SaveDatasetButton_Click(sender, e);

				Settings.Default.RecentDatasets.Insert(0, Dataset.SavingPath);
				Settings.Default.Save();
			}
		}

		private void AddToDatasetButton_Click(object sender, RoutedEventArgs e)
		{
			if (ConfigurationIsRecord)
			{
				Dataset.Add(
					SourceFileView.FileRelativePath,
					TargetFileView.FileRelativePath,
					SourceFileView.EntityLocation.Start.Offset,
					TargetFileView.EntityLocation.Start.Offset,
					SourceFileView.EntityType
				);

				var nestedInSourceEntity = SourceFileView.AvailableEntities
					.Where(el => SourceFileView.EntityLocation.Includes(el.Location)
						&& SourceFileView.EntityLocation != el.Location)
					.ToList();
				var nestedInTargetEntity = TargetFileView.AvailableEntities
					.Where(el => TargetFileView.EntityLocation.Includes(el.Location)
						&& TargetFileView.EntityLocation != el.Location)
					.ToList();

				DoAutoMapping(
					nestedInSourceEntity, 
					nestedInTargetEntity,
					SourceFileView.EntityNode, 
					TargetFileView.EntityNode
				);

				UpdateRecordsTree();

				SourceFileView.ShiftToNextAvailableEntity();
				TargetFileView.ResetEntity();
			}
			else
			{
				Control_MessageSent(null, "Невозможно сохранить текущее соответствие в датасет");
			}
		}

		private void RemoveFromDatasetButton_Click(object sender, RoutedEventArgs e)
		{
			if (ConfigurationIsRecord)
			{
				Dataset.Remove(
					SourceFileView.FileRelativePath,
					TargetFileView.FileRelativePath,
					SourceFileView.EntityLocation.Start.Offset,
					TargetFileView.EntityLocation.Start.Offset,
					SourceFileView.EntityType
				);

				UpdateRecordsTree();
			}
		}

		private void HaveDoubtsButton_Click(object sender, RoutedEventArgs e)
		{
			if (ConfigurationIsRecord)
			{
				Dataset.Add(
					SourceFileView.FileRelativePath,
					TargetFileView.FileRelativePath,
					SourceFileView.EntityLocation.Start.Offset,
					TargetFileView.EntityLocation.Start.Offset,
					SourceFileView.EntityType,
					true
				);

				UpdateRecordsTree();
			}
			else
			{
				Control_MessageSent(null, "Невозможно сохранить текущее соответствие в датасет");
			}
		}

		private void SourceFileView_FileOpened(object sender, FileViewer.FileOpenedEventArgs e)
		{
			if (OpenPairCheckBox.IsChecked ?? false)
			{
				var initialSourceFilePath = e.FileRelativePath;

				do
				{
					var targetPath = Path.Combine(TargetFileView.WorkingDirectory, e.FileRelativePath);

					if (File.Exists(targetPath))
					{
						TargetFileView.OpenFile(targetPath);
					}
					else
					{
						Control_MessageSent(null, "Парный файл отсутствует");
					}

					DoAutoMapping(SourceFileView.AvailableEntities, TargetFileView.AvailableEntities);

					/// Если после автопоиска соответствия не осталось несопоставленных сущностей
					/// и открытие исходного файла было направленным
					if (e.Direction.HasValue && SourceFileView.AvailableEntities.Count == 0)
					{
						/// Открываем новый исходный файл в том же направлении
						switch (e.Direction)
						{
							case FileViewer.ShiftDirection.Next:
								SourceFileView.ShiftToFile(FileViewer.ShiftDirection.Next);
								break;
							case FileViewer.ShiftDirection.Prev:
								SourceFileView.ShiftToFile(FileViewer.ShiftDirection.Prev);
								break;
						}

						if (SourceFileView.FileRelativePath == initialSourceFilePath)
						{
							break;
						}
					}
					else
					{
						break;
					}
				}
				while (true);
			}
			else
			{
				DoAutoMapping(SourceFileView.AvailableEntities, TargetFileView.AvailableEntities);
			}
		}

		private void TargetFileView_FileOpened(object sender, FileViewer.FileOpenedEventArgs e)
		{
			if (OpenPairCheckBox.IsChecked ?? false)
			{
				var sourcePath = Path.Combine(SourceFileView.WorkingDirectory, e.FileRelativePath);

				if (File.Exists(sourcePath))
				{
					SourceFileView.OpenFile(sourcePath);
				}
				else
				{
					Control_MessageSent(null, "Парный файл отсутствует");
				}
			}

			DoAutoMapping(SourceFileView.AvailableEntities, TargetFileView.AvailableEntities);
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

		private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var treeItem = (TreeViewItem)sender;

			if (treeItem.DataContext is
				Tuple<string, List<Tuple<string, List<DatasetRecord>>>> sourceFileData)
			{
				var clickedItem = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);

				if (clickedItem != null && clickedItem.DataContext is DatasetRecord record)
				{
					var sourcePath = Path.Combine(Dataset.SourceDirectoryPath, sourceFileData.Item1);
					var targetPath = Path.Combine(Dataset.TargetDirectoryPath,
						((Tuple<string, List<DatasetRecord>>)VisualUpwardSearch<TreeViewItem>(clickedItem).DataContext).Item1);

					SourceFileView.OpenFile(sourcePath);
					TargetFileView.OpenFile(targetPath);

					DoAutoMapping(SourceFileView.AvailableEntities, TargetFileView.AvailableEntities);

					SourceFileView.FillEntitiesList(record.SourceOffset, true);
					TargetFileView.FillEntitiesList(record.TargetOffset);

					foreach (ExistingConcernPointCandidate item in SourceFileView.FileEntitiesList.Items)
					{
						if (item.Node?.Location.Start.Offset == record.SourceOffset
							&& item.Node?.Type == record.EntityType)
						{
							SourceFileView.FileEntitiesList.SelectedItem = item;
							break;
						}
					}

					foreach (ExistingConcernPointCandidate item in TargetFileView.FileEntitiesList.Items)
					{
						if (item.Node?.Location.Start.Offset == record.TargetOffset
							&& item.Node?.Type == record.EntityType)
						{
							TargetFileView.FileEntitiesList.SelectedItem = item;
							break;
						}
					}

					SourceFileView.FileEditor.ScrollToLine(
						SourceFileView.FileEditor.Document.GetLineByOffset(record.SourceOffset).LineNumber
					);
					TargetFileView.FileEditor.ScrollToLine(
						TargetFileView.FileEditor.Document.GetLineByOffset(record.TargetOffset).LineNumber
					);

					e.Handled = true;
				}
			}
		}

		private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (Dataset?.Records.Count > 0)
			{
				switch (MessageBox.Show(
						"Сохранить текущий датасет?",
						String.Empty,
						MessageBoxButton.YesNoCancel,
						MessageBoxImage.Question))
				{
					case MessageBoxResult.Yes:
						SaveDatasetButton_Click(null, null);
						break;
					case MessageBoxResult.No:
						break;
					case MessageBoxResult.Cancel:
						e.Cancel = true;
						break;
				}
			}
		}

		private void MainWindow_KeyDown(object sender, KeyEventArgs e)
		{
			if (Keyboard.Modifiers == ModifierKeys.Alt)
			{
				if (Keyboard.IsKeyDown(Key.S))
				{
					HaveDoubtsButton_Click(null, null);
					e.Handled = true;
				}
			}
			else if (Keyboard.Modifiers == ModifierKeys.Shift)
			{
				if (Keyboard.IsKeyDown(Key.D) || Keyboard.IsKeyDown(Key.A))
				{
					if (SourceFileView.FileEditor.TextArea.IsFocused)
					{
						TargetFileView.FileEditor.TextArea.Focus();
					}
					else if (TargetFileView.FileEditor.TextArea.IsFocused)
					{
						SourceFileView.FileEditor.TextArea.Focus();
					}

					e.Handled = true;
				}
			}
			else if (Keyboard.Modifiers == ModifierKeys.Control)
			{
				if (Keyboard.IsKeyDown(Key.S))
				{
					AddToDatasetButton_Click(null, null);
					e.Handled = true;
				}
			}
			else if ((Keyboard.Modifiers & ModifierKeys.Shift & ModifierKeys.Control) > 0)
			{
				if (Keyboard.IsKeyDown(Key.S))
				{
					SaveDatasetButton_Click(null, null);
					e.Handled = true;
				}
			}
		}

		#region Helpers

		private void DoAutoMapping(
			List<Node> sourceEntities, 
			List<Node> targetEntities,
			Node sourceParentRestrictor = null,
			Node targetParentRestrictor = null)
		{
			var sourceAncestorRestrictor = sourceParentRestrictor != null
				? (AncestorsContextElement)sourceParentRestrictor : null;
			var targetAncestorRestrictor = targetParentRestrictor != null
				? (AncestorsContextElement)targetParentRestrictor : null;

			var candidates = targetEntities
				.GroupBy(n => n.Type)
				.ToDictionary(g => g.Key, g => g.Select(e => new
				{
					Node = e,
					Header = PointContext.GetHeaderContext(e),		
					Ancestors = PointContext.GetAncestorsContext(e)
						.TakeWhile(el => !el.Equals(targetAncestorRestrictor)).ToList()
				}).ToList());

			var unmapped = sourceEntities
				.Select(e => new
				{
					Node = e,
					Header = PointContext.GetHeaderContext(e),
					Ancestors = PointContext.GetAncestorsContext(e)
						.TakeWhile(el => !el.Equals(sourceAncestorRestrictor)).ToList()
				})
				.Where(e => e.Header.Count > 0 
					&& candidates.ContainsKey(e.Node.Type))
				.ToList();

			foreach (var elem in unmapped)
			{
				var candidateForElem = candidates[elem.Node.Type]
					.FirstOrDefault(c => c.Header.SequenceEqual(elem.Header)
						&& c.Ancestors.SequenceEqual(elem.Ancestors));

				if(candidateForElem != null)
				{
					Dataset.Add(
						SourceFileView.FileRelativePath,
						TargetFileView.FileRelativePath,
						elem.Node.Location.Start.Offset,
						candidateForElem.Node.Location.Start.Offset,
						elem.Node.Type
					);

					candidates[elem.Node.Type].Remove(candidateForElem);
				}
			}

			Control_MessageSent(null, $"Осталось {SourceFileView.AvailableEntities.Count} сущностей без соответствия");
			UpdateRecordsTree();
		}

		private bool IsSourceEntityAvailable(Node node) =>
			!Dataset?[SourceFileView.FileRelativePath]
				.Any(t => t.Value.Any(r => 
					r.SourceOffset == node.Location.Start.Offset 
					&& r.EntityType == node.Type 
					&& !r.HasDoubts)) 
			?? true;

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
				DatasetPathLabel.Content = Path.GetFileName(Dataset.SavingPath);
				UpdateRecordsTree();

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

		private static T VisualUpwardSearch<T>(DependencyObject element) where T : class
		{
			do
			{
				element = VisualTreeHelper.GetParent(element);
			}
			while (element != null && !(element is T));

			return element as T;
		}

		private bool ConfigurationIsRecord => SourceFileView.EntityLocation != null
			&& TargetFileView.EntityLocation != null
			&& SourceFileView.EntityType == TargetFileView.EntityType;

		private void UpdateRecordsTree()
		{
			RecordsToView = Dataset.Records
				.Select(e => new Tuple<string, List<Tuple<string, List<DatasetRecord>>>>(e.Key,
					e.Value.Select(el => new Tuple<string, List<DatasetRecord>>(el.Key, el.Value.OrderBy(e2 => e2.SourceOffset).ToList())).ToList()))
				.ToList();

			DatasetTree.ItemsSource = RecordsToView;
		}

		#endregion
	}
}
