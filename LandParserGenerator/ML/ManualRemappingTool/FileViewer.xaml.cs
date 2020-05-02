using ICSharpCode.AvalonEdit;
using Land.Control;
using Land.Core;
using Land.Core.Parsing.Tree;
using Land.Markup.CoreExtension;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
using System.Windows.Navigation;

namespace ManualRemappingTool
{
	/// <summary>
	/// Логика взаимодействия для FileViewer.xaml
	/// </summary>
	public partial class FileViewer : UserControl
	{
		public enum ShiftDirection { Next, Prev }

		public class FileOpenedEventArgs
		{
			public string FileRelativePath { get; set; }
			public ShiftDirection? Direction { get; set; }
			public bool AvailableOnly { get; set; }
		}

		public class EntitySelectedArgs
		{
			public Node EntityNode { get; set; }
			public ShiftDirection? Direction { get; set; }
		}

		private SegmentsBackgroundRenderer SegmentColorizer { get; set; }

		/// <summary>
		/// Панель поиска по тексту
		/// </summary>
		private EditorSearchHandler QuickSearch { get; set; }

		/// <summary>
		/// Корень дерева, соответствующего открытому тексту
		/// </summary>
		private Node TreeRoot { get; set; }

		private List<Node> ExistingEntities { get; set; }

		public Func<Node, bool> AvailableEntitiesFilter { get; set; } = (Node n) => true;

		/// <summary>
		/// Упорядоченный по смещению список сущностей, к которым возможна привязка
		/// </summary>
		public List<Node> AvailableEntities =>
			ExistingEntities.Where(AvailableEntitiesFilter).ToList();

		public HashSet<string> WorkingExtensions { get; private set; }

		/// <summary>
		/// Список файлов, содержащихся в каталоге, 
		/// с которым ведётся работа в рамках данного экземпляра просмотрщика
		/// </summary>
		private List<string> WorkingDirectoryFiles { get; set; }

		/// <summary>
		/// Менеджер, предоставляющий парсеры для разбора
		/// </summary>
		public ParserManager Parsers { get; set; }

		public Node EntityNode => (FileEntitiesList.SelectedItem as ExistingConcernPointCandidate)
			?.Node;

		public SegmentLocation EntityLocation => (FileEntitiesList.SelectedItem as ExistingConcernPointCandidate)
			?.Node?.Location;

		public string EntityType => (FileEntitiesList.SelectedItem as ExistingConcernPointCandidate)
			?.Node?.Type;


		#region Dependency properties

		public static readonly DependencyProperty WorkingDirectoryProperty = DependencyProperty.Register(
			"WorkingDirectory",
			typeof(string),
			typeof(FileViewer),
			new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
		);

		public string WorkingDirectory
		{
			get => (string)GetValue(WorkingDirectoryProperty);

			private set { SetValue(WorkingDirectoryProperty, value); }
		}

		public static readonly DependencyProperty LabelTextProperty = DependencyProperty.Register(
			"LabelText",
			typeof(string),
			typeof(FileViewer),
			new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
		);

		public string LabelText
		{
			get => (string)GetValue(LabelTextProperty);
			set { SetValue(LabelTextProperty, value); }
		}

		public static readonly DependencyProperty FilePathProperty = DependencyProperty.Register(
			"FilePath",
			typeof(string),
			typeof(FileViewer),
			new FrameworkPropertyMetadata(null)
		);

		public string FilePath
		{
			get => (string)GetValue(FilePathProperty);

			private set { SetValue(FilePathProperty, value); }
		}

		public string FileRelativePath =>
			GetRelativePath(FilePath, WorkingDirectory);

		public static readonly DependencyProperty AreNextPrevEnabledProperty = DependencyProperty.Register(
			"AreNextPrevEnabled",
			typeof(bool),
			typeof(FileViewer),
			new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
		);

		public bool AreNextPrevEnabled
		{
			get => (bool)GetValue(AreNextPrevEnabledProperty);
			set { SetValue(AreNextPrevEnabledProperty, value); }
		}

		#endregion

		#region Events

		public event EventHandler<FileOpenedEventArgs> FileOpened;

		public event EventHandler<EntitySelectedArgs> EntitySelected;

		public event EventHandler<MessageSentEventArgs> MessageSent;

		#endregion

		public FileViewer()
		{
			InitializeComponent();

			FontSize = 14;
			QuickSearch = new EditorSearchHandler(FileEditor.TextArea);
			SegmentColorizer = new SegmentsBackgroundRenderer(FileEditor.TextArea);
		}

		private void OpenFileButton_Click(object sender, RoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(WorkingDirectory))
			{
				MessageSent?.Invoke(this, new MessageSentEventArgs
				{
					Message = $"Необходимо сконфигурировать редактор {LabelText}",
					Type = MessageType.Error
				});

				return;
			}

			var openFileDialog = new OpenFileDialog();
			openFileDialog.InitialDirectory = WorkingDirectory;

			if (openFileDialog.ShowDialog() == true
				&& OpenFile(openFileDialog.FileName))
			{
				FileOpened?.Invoke(this,
					new FileOpenedEventArgs { FileRelativePath = FileRelativePath });
			}
		}

		private void OpenPrevFileButton_Click(object sender, RoutedEventArgs e) =>
			ShiftToFile(ShiftDirection.Prev);

		private void OpenNextFileButton_Click(object sender, RoutedEventArgs e) =>
			ShiftToFile(ShiftDirection.Next);

		private void FileEditor_PreviewMouseUp(object sender, MouseButtonEventArgs e)
		{
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			{
				FillEntitiesListAndSelect(FileEditor.CaretOffset, false);

				EntitySelected?.Invoke(this, new EntitySelectedArgs
				{
					EntityNode = EntityNode
				});
			}
		}

		private void FileEntitiesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			SegmentColorizer.ResetSegments();

			if (EntityNode != null
				&& FileEntitiesList.SelectedIndex != FileEntitiesList.Items.Count - 1)
			{
				SegmentColorizer.SetSegments(
					new List<SegmentLocation> { EntityNode.Location },
					Color.FromRgb(72, 72, 72)
				);

				if (!IsInView(EntityNode.Location.Start.Offset))
				{
					FileEditor.ScrollToLine(
						FileEditor.Document.GetLineByOffset(EntityNode.Location.Start.Offset).LineNumber
					);
				}
			}

			EntitySelected?.Invoke(this, new EntitySelectedArgs
			{
				EntityNode = EntityNode
			});
		}

		private void FileEditor_KeyDown(object sender, KeyEventArgs e)
		{
			if (Keyboard.Modifiers == ModifierKeys.None)
			{
				if (Keyboard.IsKeyDown(Key.S))
				{
					ShiftToEntity(ShiftDirection.Next);
					e.Handled = true;
				}
				else if (Keyboard.IsKeyDown(Key.W))
				{
					ShiftToEntity(ShiftDirection.Prev);
					e.Handled = true;
				}
				else if (Keyboard.IsKeyDown(Key.D))
				{
					ShiftToFile(ShiftDirection.Next);
					e.Handled = true;
				}
				else if (Keyboard.IsKeyDown(Key.A))
				{
					ShiftToFile(ShiftDirection.Prev);
					e.Handled = true;
				}
			}
			else if (Keyboard.Modifiers == ModifierKeys.Alt)
			{
				if (Keyboard.IsKeyDown(Key.S))
				{
					ShiftToEntity(ShiftDirection.Next, false);
					e.Handled = true;
				}
				else if (Keyboard.IsKeyDown(Key.W))
				{
					ShiftToEntity(ShiftDirection.Prev, false);
					e.Handled = true;
				}
				else if (Keyboard.IsKeyDown(Key.D))
				{
					ShiftToFile(ShiftDirection.Next, false);
					e.Handled = true;
				}
				else if (Keyboard.IsKeyDown(Key.A))
				{
					ShiftToFile(ShiftDirection.Prev, false);
					e.Handled = true;
				}
			}
		}

		#region API

		public bool OpenFile(string filePath)
		{
			if(!filePath.StartsWith(WorkingDirectory))
			{
				MessageSent?.Invoke(this, new MessageSentEventArgs
				{
					Message = "Попытка открыть файл не из рабочего каталога",
					Type = MessageType.Error
				});

				return false;
			}

			if(!File.Exists(filePath))
			{
				MessageSent?.Invoke(this, new MessageSentEventArgs
				{
					Message = "Попытка открыть несуществующий файл",
					Type = MessageType.Error
				});

				return false;
			}

			FilePath = filePath;

			using (var stream = new StreamReader(filePath, GetEncoding(filePath)))
			{
				FileEditor.Text = stream.ReadToEnd();
				FileEditor.Encoding = stream.CurrentEncoding;
				FileEditor.ScrollToLine(0);

				if (Path.GetExtension(filePath) == ".cs")
				{
					FileEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(
						new System.Xml.XmlTextReader(new StringReader(Properties.Resources.CSharp)),
						ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance
					);
				}
				else
				{
					FileEditor.SyntaxHighlighting = null;
				}

				SegmentColorizer.ResetSegments();
			}

			TreeRoot = Parse(filePath, FileEditor.Text);

			var visitor = new LandExplorerVisitor();
			TreeRoot.Accept(visitor);
			ExistingEntities = visitor.Land;

			return true;
		}

		public void FillEntitiesListAndSelect(int offset, bool availableOnly = true)
		{
			var candidates = GetEntities(TreeRoot, new PointLocation(offset), true, availableOnly);

			candidates.Add(new ExistingConcernPointCandidate() { ViewHeader = "[сбросить выделение]" });

			FileEntitiesList.ItemsSource = candidates;
			FileEntitiesList.SelectedIndex = 0;
		}

		public void ShiftToFile(ShiftDirection direction, bool availableOnly = true, bool raiseEvent = true)
		{
			if (WorkingDirectoryFiles.Count > 0)
			{
				var currentFileIndex = WorkingDirectoryFiles.IndexOf(FilePath);

				switch (direction)
				{
					case ShiftDirection.Next:
						currentFileIndex = (currentFileIndex + 1)
							% WorkingDirectoryFiles.Count;
						break;
					case ShiftDirection.Prev:
						currentFileIndex = (WorkingDirectoryFiles.Count + (currentFileIndex - 1))
							% WorkingDirectoryFiles.Count;
						break;
				}

				OpenFile(WorkingDirectoryFiles[currentFileIndex]);

				if (raiseEvent)
				{
					FileOpened?.Invoke(this, new FileOpenedEventArgs
					{
						FileRelativePath = FileRelativePath,
						Direction = direction,
						AvailableOnly = availableOnly
					});
				}
			}
		}

		public void ShiftToEntity(ShiftDirection direction, bool availableOnly = true, bool raiseEvent = true)
		{
			var entities = availableOnly ? AvailableEntities : ExistingEntities;

			if (entities.Count > 0)
			{
				Node entityNode = null;

				switch (direction)
				{
					case ShiftDirection.Next:
						entityNode = EntityNode != null
							? entities
								.SkipWhile(e => e.Location.Start.Offset < EntityNode.Location.Start.Offset
									|| e.Location.Start.Offset == EntityNode.Location.Start.Offset && e.Location.End.Offset >= EntityNode.Location.End.Offset)
								.FirstOrDefault() ?? entities.First()
							: entities.First();
						break;
					case ShiftDirection.Prev:
						entityNode = EntityNode != null
						   ? entities
							   .TakeWhile(e => e.Location.Start.Offset < EntityNode.Location.Start.Offset
								   || e.Location.Start.Offset == EntityNode.Location.Start.Offset && e.Location.End.Offset > EntityNode.Location.End.Offset)
							   .LastOrDefault() ?? entities.Last()
						   : entities.Last();
						break;
				}

				var candidates = GetEntities(
					entityNode,
					new PointLocation(entityNode.Location.Start.Offset),
					true,
					availableOnly
				);
				candidates.Add(new ExistingConcernPointCandidate() { ViewHeader = "[сбросить выделение]" });

				FileEntitiesList.ItemsSource = candidates;
				FileEntitiesList.SelectedIndex = 0;

				if (raiseEvent)
				{
					EntitySelected?.Invoke(this, new EntitySelectedArgs
					{
						Direction = direction,
						EntityNode = EntityNode
					});
				}
			}
			else
			{
				ResetEntity();
			}
		}

		public void ResetEntity()
		{
			FileEntitiesList.ItemsSource = null;
		}

		public void Configure(string workingDirectory, HashSet<string> workingExtensions)
		{
			if(!Directory.Exists(workingDirectory))
			{
				MessageSent?.Invoke(this, new MessageSentEventArgs
				{
					Message = $"Директория {workingDirectory} не существует",
					Type = MessageType.Error
				});

				return;
			}

			if (workingExtensions == null || workingExtensions.Count == 0)
			{
				MessageSent?.Invoke(this, new MessageSentEventArgs
				{
					Message = $"Необходимо указать расширения файлов, с которыми ведётся работа",
					Type = MessageType.Error
				});

				return;
			}

			WorkingExtensions = workingExtensions;
			WorkingDirectory = workingDirectory;

			WorkingDirectoryFiles = Directory
				.GetFiles(WorkingDirectory, "*", SearchOption.AllDirectories)
				.Where(elem => WorkingExtensions.Contains(Path.GetExtension(elem)))
				.OrderBy(elem => elem)
				.ToList();
		}

		#endregion

		#region Helpers

		private Encoding GetEncoding(string filename)
		{
			using (FileStream fs = File.OpenRead(filename))
			{
				Ude.CharsetDetector cdet = new Ude.CharsetDetector();
				cdet.Feed(fs);
				cdet.DataEnd();
				if (cdet.Charset != null)
				{
					return Encoding.GetEncoding(cdet.Charset);
				}
				else
				{
					return Encoding.Default;
				}
			}
		}

		private Node Parse(string fileName, string text)
		{
			if (!String.IsNullOrEmpty(fileName))
			{
				var extension = Path.GetExtension(fileName);

				if (Parsers[extension] != null)
				{
					Node root = null;

					root = Parsers[extension].Parse(text);
					var success = Parsers[extension].Log.All(l => l.Type != Land.Core.MessageType.Error);

					return success ? root : null;
				}
			}

			return null;
		}

		private static string GetRelativePath(string filePath, string directoryPath)
		{
			var directoryUri = new Uri(directoryPath + "/");

			return Uri.UnescapeDataString(
				directoryUri.MakeRelativeUri(new Uri(filePath)).ToString()
			);
		}

		private List<ConcernPointCandidate> GetEntities(
			Node root, 
			PointLocation point, 
			bool exploreInDepth = true,
			bool availableOnly = true)
		{
			var pseudoSegment = new SegmentLocation
			{
				End = point,
				Start = point
			};

			var pointCandidates = new LinkedList<Node>();
			var currentNode = root;

			/// В качестве кандидатов на роль помечаемого участка рассматриваем узлы от корня,
			/// содержащие текущую позицию каретки
			while (currentNode != null)
			{
				if (currentNode.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
					pointCandidates.AddFirst(currentNode);

				currentNode = currentNode.Children
					.Where(c => c.Location != null && c.Location.Includes(pseudoSegment))
					.FirstOrDefault();
			}

			return pointCandidates
				.Where(c=> !availableOnly || AvailableEntities.Contains(c))
				.Select(c => (ConcernPointCandidate)new ExistingConcernPointCandidate(c))
				.ToList();
		}

		private bool IsInView(int offset)
		{
			var textView = FileEditor.TextArea.TextView;

			var start = textView
				.GetPosition(new Point(0, 0) + textView.ScrollOffset);
			var end = textView
				.GetPosition(new Point(textView.ActualWidth, textView.ActualHeight) + textView.ScrollOffset);

			var startOffset = start != null 
				? FileEditor.Document.GetOffset(start.Value.Location) 
				: FileEditor.Document.TextLength;
			var endOffset = end != null 
				? FileEditor.Document.GetOffset(end.Value.Location) 
				: FileEditor.Document.TextLength;

			return startOffset <= offset && endOffset >= offset;
		}

		#endregion
	}
}
