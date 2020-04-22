using ICSharpCode.AvalonEdit;
using Land.Control;
using Land.Core;
using Land.Core.Parsing.Tree;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
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
		/// Метка с дополнительной информацией, отображаемая в заголовке панели
		/// </summary>
		public string LabelText { get; set; }

		/// <summary>
		/// Каталог, файлы в котором рассматриваем
		/// </summary>
		public string WorkingDirectory
		{
			get { return workingDirectory; }

			set
			{
				workingDirectory = value;

				WorkingDirectoryFiles = Directory
					.GetFiles(WorkingDirectory, $"*.{WorkingExtensions}", SearchOption.AllDirectories)
					.OrderBy(elem => elem)
					.ToList();
			}
		}

		private string workingDirectory;

		/// <summary>
		///  Расширение файлов, которые рассматриваем
		/// </summary>
		public HashSet<string> WorkingExtensions { get; set; }

		/// <summary>
		///  Путь к текущему открытому файлу
		/// </summary>
		public string FilePath 
		{ 
			get { return filePath; }
			
			set
			{ 
				filePath = FilePath;
				FileOpened?.Invoke(this, FilePath);
			} 
		}

		private string filePath;

		public int? EntityStartLine => null;

		public string EntityType { get; set; }

		public bool EntityTypeLocked { get; set; }


		public event EventHandler<string> FileOpened;

		public event EventHandler<string> MessageSent;

		public FileViewer()
		{
			InitializeComponent();

			FontSize = 12;
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
			}

			TreeRoot = Parse(openFileDialog.FileName, FileEditor.Text);
		}

		private void OpenPrevFileButton_Click(object sender, RoutedEventArgs e)
		{
			if (CurrentFileIndex.HasValue && CurrentFileIndex != 0)
			{
				--CurrentFileIndex;

				OpenFile(WorkingDirectoryFiles[CurrentFileIndex.Value]);

				TreeRoot = Parse(WorkingDirectoryFiles[CurrentFileIndex.Value], FileEditor.Text);
			}
		}

		private void OpenNextFileButton_Click(object sender, RoutedEventArgs e)
		{
			if(CurrentFileIndex.HasValue && CurrentFileIndex != WorkingDirectoryFiles.Count - 1)
			{
				++CurrentFileIndex;

				OpenFile(WorkingDirectoryFiles[CurrentFileIndex.Value]);

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

		#endregion
	}
}
