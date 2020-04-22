using ICSharpCode.AvalonEdit;
using Land.Control;
using Land.Core;
using Land.Core.Parsing.Tree;
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
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Панель поиска по тексту
		/// </summary>
		private EditorSearchHandler QuickSearch { get; set; }

		/// <summary>
		/// Корень дерева, соответствующего открытому тексту
		/// </summary>
		private Node TreeRoot { get; set; }

		/// <summary>
		/// Список файлов, содержащихся в каталоге, 
		/// с которым ведётся работа в рамках данного экземпляра просмотрщика
		/// </summary>
		private List<string> WorkingDirectoryFiles { get; set; }

		/// <summary>
		/// Индекс текущего открытого файла в общем списке файлов каталога
		/// </summary>
		private int? CurrentFileIndex { get; set; }

		/// <summary>
		/// Менеджер, предоставляющий парсеры для разбора
		/// </summary>
		public ParserManager Parsers { get; set; }

		/// <summary>
		/// Каталог, файлы в котором рассматриваем
		/// </summary>
		public string WorkingDirectory
		{
			get { return _workingDirectory; }

			set
			{
				_workingDirectory = value;

				if (WorkingExtensions != null && WorkingExtensions.Count > 0)
				{
					WorkingDirectoryFiles = Directory
						.GetFiles(WorkingDirectory, "*", SearchOption.AllDirectories)
						.Where(elem => WorkingExtensions.Contains(Path.GetExtension(elem)))
						.OrderBy(elem => elem)
						.ToList();
				}

				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WorkingDirectory)));
			}
		}

		private string _workingDirectory;

		/// <summary>
		///  Расширение файлов, которые рассматриваем
		/// </summary>
		public HashSet<string> WorkingExtensions
		{
			get { return _workingExtensions; }

			set
			{
				_workingExtensions = value;

				if (!String.IsNullOrEmpty(WorkingDirectory))
				{
					WorkingDirectoryFiles = Directory
						.GetFiles(WorkingDirectory, "*", SearchOption.AllDirectories)
						.Where(elem => WorkingExtensions.Contains(Path.GetExtension(elem)))
						.OrderBy(elem => elem)
						.ToList();
				}
			}
		}

		private HashSet<string> _workingExtensions;

		#region Dependency properties

		public static readonly DependencyProperty EntityStartLineProperty = DependencyProperty.Register(
			"EntityStartLine",
			typeof(int?),
			typeof(FileViewer),
			new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
		);

		public int? EntityStartLine
		{
			get => (int?)GetValue(EntityStartLineProperty);
			set { SetValue(EntityStartLineProperty, value); }
		}

		public static readonly DependencyProperty EntityTypeProperty = DependencyProperty.Register(
			"EntityType",
			typeof(string),
			typeof(FileViewer),
			new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
		);

		public string EntityType
		{
			get => (string)GetValue(EntityTypeProperty);
			set { SetValue(EntityTypeProperty, value); }
		}

		public static readonly DependencyProperty EntityTypeLockedProperty = DependencyProperty.Register(
			"EntityTypeLocked",
			typeof(bool),
			typeof(FileViewer),
			new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
		);

		public bool EntityTypeLocked
		{
			get => (bool)GetValue(EntityTypeLockedProperty);
			set { SetValue(EntityTypeLockedProperty, value); }
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

			private set 
			{
				SetValue(FilePathProperty, 
					GetRelativePath(value, WorkingDirectory)); 
			}
		}

		#endregion

		#region Events

		public event EventHandler<string> FileOpened;

		public event EventHandler<string> MessageSent;

		#endregion

		public FileViewer()
		{
			InitializeComponent();

			FontSize = 14;
			QuickSearch = new EditorSearchHandler(FileEditor.TextArea);
		}

		private void OpenFileButton_Click(object sender, RoutedEventArgs e)
		{
			if(String.IsNullOrEmpty(WorkingDirectory))
			{
				MessageSent?.Invoke(this,
					$"Необходимо указать рабочий каталог для файлов {LabelText}");

				return;
			}

			var openFileDialog = new OpenFileDialog();
			openFileDialog.InitialDirectory = WorkingDirectory;

			if (openFileDialog.ShowDialog() == true 
				&& openFileDialog.FileName.StartsWith(WorkingDirectory))
			{
				OpenFile(openFileDialog.FileName);
				FileOpened?.Invoke(this, FilePath);
			}

			TreeRoot = Parse(openFileDialog.FileName, FileEditor.Text);
		}

		private void OpenPrevFileButton_Click(object sender, RoutedEventArgs e)
		{
			if (!CurrentFileIndex.HasValue && WorkingDirectoryFiles.Count > 0)
			{
				CurrentFileIndex = WorkingDirectoryFiles.Count - 1;
			}

			if (CurrentFileIndex.HasValue && CurrentFileIndex != 0)
			{
				--CurrentFileIndex;

				OpenFile(WorkingDirectoryFiles[CurrentFileIndex.Value]);
				FileOpened?.Invoke(this, FilePath);

				TreeRoot = Parse(WorkingDirectoryFiles[CurrentFileIndex.Value], FileEditor.Text);
			}
		}

		private void OpenNextFileButton_Click(object sender, RoutedEventArgs e)
		{
			if(!CurrentFileIndex.HasValue && WorkingDirectoryFiles.Count > 0)
			{
				CurrentFileIndex = 0;
			}

			if(CurrentFileIndex.HasValue && CurrentFileIndex != WorkingDirectoryFiles.Count - 1)
			{
				++CurrentFileIndex;

				OpenFile(WorkingDirectoryFiles[CurrentFileIndex.Value]);
				FileOpened?.Invoke(this, FilePath);

				TreeRoot = Parse(WorkingDirectoryFiles[CurrentFileIndex.Value], FileEditor.Text);
			}
		}

		private void FileEditor_MouseDown(object sender, MouseButtonEventArgs e)
		{

		}

		#region Helpers

		public void OpenFile(string filePath)
		{
			FilePath = filePath;

			using (var stream = new StreamReader(filePath, GetEncoding(filePath)))
			{
				FileEditor.Text = stream.ReadToEnd();
				FileEditor.Encoding = stream.CurrentEncoding;
				FileEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager
					.Instance.GetDefinitionByExtension(Path.GetExtension(filePath));
			}
		}

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
					var success = Parsers[extension].Log.All(l => l.Type != MessageType.Error);

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

		#endregion
	}
}
