using Land.Control;
using Land.Core;
using Land.Core.Parsing.Tree;
using Land.Markup.Binding;
using Land.Markup.CoreExtension;
using ManualRemappingTool.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
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

		private const int RECORDS_TREE_PAGE_SIZE = 10;
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
		private MappingHelper Mapper { get; set; } = new MappingHelper();

		public List<Tuple<string, List<Tuple<string, List<DatasetRecord>>>>> RecordsToView { get; private set; }

		public static readonly DependencyProperty RecordsPageIdxProperty = DependencyProperty.Register(
			"RecordsPageIdx",
			typeof(int),
			typeof(MainWindow),
			new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
		);
		public int RecordsPageIdx
		{
			get => (int)GetValue(RecordsPageIdxProperty);
			set { SetValue(RecordsPageIdxProperty, value); }
		}

		public MainWindow()
		{
			InitializeComponent();

			SourceFileView.Parsers = Parsers;
			SourceFileView.FileEditor.MouseDown += FileView_MouseDown;
			SourceFileView.FileEditor.PreviewMouseWheel += Control_PreviewMouseWheel;
			SourceFileView.FileEditor.TextArea.TextView.ScrollOffsetChanged += FileView_ScrollOffsetChanged;
			SourceFileView.FileEntitiesList.PreviewMouseWheel += Control_PreviewMouseWheel;
			SourceFileView.AvailableEntitiesFilter = IsSourceEntityAvailable;

			TargetFileView.Parsers = Parsers;
			TargetFileView.FileEditor.MouseDown += FileView_MouseDown;
			TargetFileView.FileEditor.PreviewMouseWheel += Control_PreviewMouseWheel;
			TargetFileView.FileEditor.TextArea.TextView.ScrollOffsetChanged += FileView_ScrollOffsetChanged;
			TargetFileView.FileEntitiesList.PreviewMouseWheel += Control_PreviewMouseWheel;

			Parsers.Load(LoadSettings(SETTINGS_DEFAULT_PATH), CACHE_DIRECTORY, new List<Message>());
		}

		private void MainWindow_ContentRendered(object sender, EventArgs e)
		{
			StartWindowInteraction();
		}

		private void FileView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if(e.ChangedButton == MouseButton.Middle)
			{
				SyncViewsButton_Click(sender, null);
			}
		}

		private void FileView_ScrollOffsetChanged(object sender, EventArgs e)
		{
			if ((Keyboard.Modifiers & ModifierKeys.Shift & ModifierKeys.Control) > 0)
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

				DoAutoMapping(
					Path.GetExtension(SourceFileView.FilePath),				
					SourceFileView.EntityNode, 
					TargetFileView.EntityNode,
					null, null, false
				);

				if (SourceFileView.AvailableEntities.Count == 0)
				{
					Dataset.FinalizedFiles.Add(SourceFileView.FileRelativePath);
				}

				Control_MessageSent(null, new MessageSentEventArgs
				{
					Message = $"Осталось {SourceFileView.AvailableEntities.Count} сущностей без соответствия",
					Type = MessageType.Info
				});

				UpdateIsFinalizedCheckBox();
				UpdateRecordsTree();

				SourceFileView.ShiftToEntity(FileViewer.ShiftDirection.Next, true, false);
				TargetFileView.ResetEntity();
			}
			else
			{
				Control_MessageSent(null, new MessageSentEventArgs
				{
					Message = "Невозможно сохранить текущее соответствие в датасет",
					Type = MessageType.Error
				});
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
				Control_MessageSent(null, new MessageSentEventArgs
				{
					Message = "Невозможно сохранить текущее соответствие в датасет",
					Type = MessageType.Error
				});
			}
		}

		private void SourceFileView_FileOpened(object sender, FileViewer.FileOpenedEventArgs e)
		{
			/// Если известно, в каком направлении открываем, и нужно открывать только файлы, 
			/// в которых осталось что-то не перепривязанное, ищем ближайший не финализированный файл
			if (e.AvailableOnly && e.Direction.HasValue
				&& Dataset.FinalizedFiles.Contains(SourceFileView.FileRelativePath))
			{
				var currentFileIdx = SourceFileView.WorkingDirectoryFiles.IndexOf(SourceFileView.FilePath)
					+ SourceFileView.WorkingDirectoryFiles.Count;
				var directionCoeff = e.Direction == FileViewer.ShiftDirection.Next ? 1 : -1;

				for (var i = 0; i < SourceFileView.WorkingDirectoryFiles.Count; ++i)
				{
					var nextFilePath = SourceFileView.WorkingDirectoryFiles[
						(currentFileIdx + (directionCoeff * i)) % SourceFileView.WorkingDirectoryFiles.Count
					];

					if (!Dataset.FinalizedFiles.Contains(
						FileViewer.GetRelativePath(nextFilePath, SourceFileView.WorkingDirectory)))
					{
						SourceFileView.OpenFile(nextFilePath);
						break;
					}
				}
			}

			Control_MessageSent(null, new MessageSentEventArgs
			{
				Message = $"Осталось {SourceFileView.AvailableEntities.Count} сущностей без соответствия",
				Type = MessageType.Info
			});

			UpdateIsFinalizedCheckBox();

			if (OpenPairCheckBox.IsChecked ?? false)
			{
				var targetPath = Path.Combine(
					TargetFileView.WorkingDirectory,
					SourceFileView.FileRelativePath
				);

				if (File.Exists(targetPath))
				{
					TargetFileView.OpenFile(targetPath);
				}
				else
				{
					Control_MessageSent(null, new MessageSentEventArgs
					{
						Message = "Парный файл отсутствует",
						Type = MessageType.Error
					});
				}
			}
		}

		private void TargetFileView_FileOpened(object sender, FileViewer.FileOpenedEventArgs e)
		{
			if (OpenPairCheckBox.IsChecked ?? false && e.Direction.HasValue)
			{
				var sourcePath = Path.Combine(SourceFileView.WorkingDirectory, e.FileRelativePath);

				if (File.Exists(sourcePath))
				{
					SourceFileView.OpenFile(sourcePath);
				}
				else
				{
					Control_MessageSent(null, new MessageSentEventArgs
					{
						Message = "Парный файл отсутствует",
						Type = MessageType.Error
					});
				}
			}
		}

		private void Control_MessageSent(object sender, MessageSentEventArgs e)
		{
			this.AppStatusText.Content = $"{e.Message} - {e.Stamp}";

			switch(e.Type)
			{
				case MessageType.Error:
					this.AppStatus.Background = Brushes.LightPink;
					break;
				case MessageType.Info:
					this.AppStatus.Background = Brushes.LightBlue;
					break;
				case MessageType.Success:
					this.AppStatus.Background = Brushes.LightGreen;
					break;
			}
		}

		private void SyncViewsButton_Click(object sender, RoutedEventArgs e)
		{
			var sourceView = (sender as ICSharpCode.AvalonEdit.TextEditor) ?? SourceFileView.FileEditor;
			var targetView = sourceView == SourceFileView.FileEditor
				? TargetFileView.FileEditor : SourceFileView.FileEditor;

			var offset = sourceView.TextArea.TextView.ScrollOffset.Y;

			if (targetView.TextArea.TextView.DocumentHeight > offset)
			{
				targetView.ScrollToVerticalOffset(offset);
			}
		}

		private void RunAutoRemapForPair_Click(object sender, RoutedEventArgs e)
		{
			DoAutoMapping(
				Path.GetExtension(SourceFileView.FilePath),
				SourceFileView.TreeRoot,
				TargetFileView.TreeRoot,
				null, null, true
			);

			if (SourceFileView.AvailableEntities.Count == 0)
			{
				Dataset.FinalizedFiles.Add(SourceFileView.FileRelativePath);
			}

			Control_MessageSent(null, new MessageSentEventArgs
			{
				Message = $"Осталось {SourceFileView.AvailableEntities.Count} сущностей без соответствия",
				Type = MessageType.Info
			});

			UpdateIsFinalizedCheckBox();

			UpdateRecordsTree();
		}

		private void RunAutoRemapForAll_Click(object sender, RoutedEventArgs e)
		{
			for(var i=0; i<SourceFileView.WorkingDirectoryFiles.Count; ++i)
			{
				SourceFileView.OpenFile(SourceFileView.WorkingDirectoryFiles[i]);

				var targetPath = Path.Combine(
					TargetFileView.WorkingDirectory,
					SourceFileView.FileRelativePath
				);

				TargetFileView.OpenFile(targetPath);

				if (File.Exists(targetPath))
				{
					DoAutoMapping(
						Path.GetExtension(SourceFileView.FilePath),
						SourceFileView.TreeRoot,
						TargetFileView.TreeRoot,
						null, null, true
					);

					if (SourceFileView.AvailableEntities.Count == 0)
					{
						Dataset.FinalizedFiles.Add(SourceFileView.FileRelativePath);
					}
				}
			}

			Control_MessageSent(null, new MessageSentEventArgs
			{
				Type = MessageType.Info,
				Message = $"Проверено {SourceFileView.WorkingDirectoryFiles.Count} файлов, требуют ручной проверки {SourceFileView.WorkingDirectoryFiles.Count - Dataset.FinalizedFiles.Count}"
			});

			UpdateIsFinalizedCheckBox();
			UpdateRecordsTree();
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

					SyncEntitiesListAndEditor(SourceFileView, record.SourceOffset, record.EntityType);
					SyncEntitiesListAndEditor(TargetFileView, record.TargetOffset, record.EntityType);

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
		}

		private void SourceFileView_EntitySelected(object sender, FileViewer.EntitySelectedArgs e)
		{
			if(e.EntityNode != null)
			{
				var pair = Dataset[SourceFileView.FileRelativePath, TargetFileView.FileRelativePath]
					.FirstOrDefault(el => el.SourceOffset == e.EntityNode.Location.Start.Offset
						&& el.EntityType == e.EntityNode.Type);

				if(pair != null)
				{
					SyncEntitiesListAndEditor(TargetFileView, pair.TargetOffset, pair.EntityType);
				}
			}
		}

		private void TreeFilters_Changed(object sender, RoutedEventArgs e)
		{
			RecordsPageIdx = 0;
			UpdateRecordsTree();
		}

		private void FinalizeFileButton_CheckedChanged(object sender, RoutedEventArgs e)
		{
			if (!String.IsNullOrEmpty(SourceFileView.FileRelativePath))
			{
				if (FinalizeFileButton.IsChecked ?? false)
				{
					if(Dataset[SourceFileView.FileRelativePath].Any(f=>f.Value.Any(r=>r.HasDoubts)))
					{
						Control_MessageSent(null, new MessageSentEventArgs
						{
							Message = "Чтобы финализировать файл, необходимо убрать сомнительные привязки",
							Type = MessageType.Error
						});

						FinalizeFileButton.IsChecked = false;
						return;
					}

					Dataset.FinalizedFiles.Add(SourceFileView.FileRelativePath);
				}
				else
				{
					Dataset.FinalizedFiles.Remove(SourceFileView.FileRelativePath);
				}
			}
		}

		private void PrevTreePageButton_Click(object sender, RoutedEventArgs e)
		{
			var oldIdx = RecordsPageIdx;
			RecordsPageIdx = Math.Max(0, RecordsPageIdx - 1);

			if (RecordsPageIdx != oldIdx)
			{
				UpdateRecordsTree();
			}
		}

		private void NextTreePageButton_Click(object sender, RoutedEventArgs e)
		{
			var oldIdx = RecordsPageIdx;
			RecordsPageIdx = Math.Min(RecordsPageIdx + 1,(int)Math.Floor(GetRecordsTreeCount() / (double)RECORDS_TREE_PAGE_SIZE));

			if (RecordsPageIdx != oldIdx)
			{
				UpdateRecordsTree();
			}
		}

		#region Helpers

		private void SyncEntitiesListAndEditor(FileViewer fileViewer, int pffset, string type)
		{
			fileViewer.FillEntitiesListAndSelect(pffset, false);

			foreach (ExistingConcernPointCandidate item in fileViewer.FileEntitiesList.Items)
			{
				if (item.Node?.Location.Start.Offset == pffset
					&& item.Node?.Type == type)
				{
					fileViewer.FileEntitiesList.SelectedItem = item;
					break;
				}
			}

			fileViewer.FileEditor.ScrollToLine(
				fileViewer.FileEditor.Document.GetLineByOffset(pffset).LineNumber
			);
		}

		/// <summary>
		/// Автоматическое сопоставление всего, что можно автоматически сопоставить
		/// </summary>
		/// <param name="extension">Расширение файла, элементы из которого сопоставляются</param>
		/// <param name="sourceRoot">Корень поддерева, которое рассматриваем в исходном файле</param>
		/// <param name="targetRoot">Корень поддерева, которое рассматриваем в модифицированном файле</param>
		/// <param name="sourceCandidates">Островные элементы из исходного файла</param>
		/// <param name="targetCandidates">Островные элементы из целевого файла</param>
		/// <param name="checkRoot"></param>
		/// <param name="visualUpdate"></param>
		private void DoAutoMapping(
			string extension,
			Node sourceRoot,
			Node targetRoot,
			List<MappingElement> sourceCandidates = null,
			List<MappingElement> targetCandidates = null,
			bool checkRoot = true)
		{
			var sourceAncestorRestrictor = (AncestorsContextElement)sourceRoot;
			var targetAncestorRestrictor = (AncestorsContextElement)targetRoot;

			if (sourceCandidates == null)
			{
				var visitor = new LandExplorerVisitor();
				sourceRoot.Accept(visitor);
				var sourceLand = visitor.Land;

				/// В качестве элементов, которые нужно смаппить, рассматриваем ещё не смапленные элементы
				sourceCandidates = sourceLand
					.Select(e => new MappingElement
					{
						Node = e,
						Header = PointContext.GetHeaderContext(e),
						Ancestors = PointContext.GetAncestorsContext(e)
							.TakeWhile(el => !el.Equals(sourceAncestorRestrictor)).ToList()
					})
					.ToList();
			}
			else
			{
				foreach(var elem in sourceCandidates)
				{
					elem.Ancestors = elem.Ancestors
						.TakeWhile(el => !el.Equals(sourceAncestorRestrictor)).ToList();
				}
			}

			if (!checkRoot)
			{
				var rootElement = sourceCandidates.FirstOrDefault(c => c.Node == sourceRoot);
				sourceCandidates.Remove(rootElement);
			}

			if (targetCandidates == null)
			{
				var visitor = new LandExplorerVisitor();
				targetRoot.Accept(visitor);
				var targetLand = visitor.Land;

				targetCandidates = targetLand
					.Select(e => new MappingElement
					{
						Node = e,
						Header = PointContext.GetHeaderContext(e),
						Ancestors = PointContext.GetAncestorsContext(e)
							.TakeWhile(el => !el.Equals(targetAncestorRestrictor)).ToList()
					})
					.ToList();
			}
			else
			{
				foreach (var elem in targetCandidates)
				{
					elem.Ancestors = elem.Ancestors
						.TakeWhile(el => !el.Equals(targetAncestorRestrictor)).ToList();
				}
			}

			for (var i = 0; i < sourceCandidates.Count; ++i)
			{
				var targetElement = Mapper[extension].GetSameElement(
					sourceCandidates[i], 
					sourceCandidates, 
					targetCandidates
				);

				if (targetElement != null)
				{
					Dataset.Add(
						SourceFileView.FileRelativePath,
						TargetFileView.FileRelativePath,
						sourceCandidates[i].Node.Location.Start.Offset,
						targetElement.Node.Location.Start.Offset,
						sourceCandidates[i].Node.Type
					);

					var inner = sourceCandidates.Skip(i + 1)
						.TakeWhile(c => sourceCandidates[i].Node.Location.Includes(c.Node.Location)).ToList();						

					DoAutoMapping(
						extension,
						sourceCandidates[i].Node,
						targetElement.Node,
						inner,
						targetCandidates.SkipWhile(e=>e != targetElement).Skip(1)
							.TakeWhile(c => targetElement.Node.Location.Includes(c.Node.Location)).ToList(),
						false
					);

					i += inner.Count;
				}
			}
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

				SourceFileView.Configure(Dataset.SourceDirectoryPath, Dataset.Extensions);
				TargetFileView.Configure(Dataset.TargetDirectoryPath, Dataset.Extensions);

				SourceWorkingDirectory.Content = Dataset.SourceDirectoryPath;
				TargetWorkingDirectory.Content = Dataset.TargetDirectoryPath;
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
			RecordsToView = Dataset?.Records
				.Where(e => (!(ShowNotFinalizedOnlyCheckBox.IsChecked ?? false) || !Dataset.FinalizedFiles.Contains(e.Key))
					&& (String.IsNullOrEmpty(FileNameFilter.Text) || e.Key.ToLower().Contains(FileNameFilter.Text.ToLower())))
				.Skip(RECORDS_TREE_PAGE_SIZE * RecordsPageIdx)
				.Take(RECORDS_TREE_PAGE_SIZE)
				.Select(e => new Tuple<string, List<Tuple<string, List<DatasetRecord>>>>(
					e.Key,
					e.Value
						.Select(e1 => new Tuple<string, List<DatasetRecord>>(
							e1.Key,
							e1.Value
								.Where(e2 => !(ShowDoubtsOnlyCheckBox.IsChecked ?? false) || e2.HasDoubts)
								.OrderBy(e2 => e2.SourceOffset)
								.ToList()
						))
						.Where(e1 => e1.Item2.Count > 0)
						.ToList()
				))
				.Where(e => e.Item2.Count > 0)
				.ToList();

			DatasetTree.ItemsSource = RecordsToView;
		}

		private int GetRecordsTreeCount()
		{
			return Dataset?.Records
				.Where(e => (!(ShowNotFinalizedOnlyCheckBox.IsChecked ?? false) || !Dataset.FinalizedFiles.Contains(e.Key))
					&& (String.IsNullOrEmpty(FileNameFilter.Text) || e.Key.ToLower().Contains(FileNameFilter.Text.ToLower()))
					&& (!(ShowDoubtsOnlyCheckBox.IsChecked ?? false) || e.Value.Any(tf => tf.Value.Any(r => r.HasDoubts))))
				.Count() ?? 0;
		}

		private void UpdateIsFinalizedCheckBox()
		{
			FinalizeFileButton.IsChecked =
				Dataset?.FinalizedFiles?.Contains(SourceFileView.FileRelativePath) ?? false;
		}

		#endregion
	}
}
