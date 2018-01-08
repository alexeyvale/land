using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Win32;

using LandParserGenerator.Parsing.Tree;

namespace TestGUI
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private string LAST_GRAMMARS_FILE = "./last_grammars.land.ide";

		private Brush LightRed = new SolidColorBrush(Color.FromRgb(255, 200, 200));

		private SelectedTextColorizer SelectedTextColorizerForGrammar { get; set; }

		private Node TreeRoot { get; set; }
		private LandParserGenerator.Parsing.BaseParser Parser { get; set; }

		public MainWindow()
		{
			InitializeComponent();

			using (var consoleWriter = new ConsoleWriter())
			{
				consoleWriter.WriteEvent += consoleWriter_WriteEvent;
				consoleWriter.WriteLineEvent += consoleWriter_WriteLineEvent;
				Console.SetOut(consoleWriter);
			}

			GrammarEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(
				new System.Xml.XmlTextReader(new StreamReader($"../../land.xshd", Encoding.Default)), 
				ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance);

			GrammarEditor.TextArea.TextView.BackgroundRenderers.Add(new CurrentLineHighlighter(GrammarEditor.TextArea));
			FileEditor.TextArea.TextView.BackgroundRenderers.Add(new CurrentLineHighlighter(FileEditor.TextArea));
			SelectedTextColorizerForGrammar = new SelectedTextColorizer(GrammarEditor.TextArea);

			if (File.Exists(LAST_GRAMMARS_FILE))
			{
				var files = File.ReadAllLines(LAST_GRAMMARS_FILE);
				foreach(var filepath in files)
				{
					if(!String.IsNullOrEmpty(filepath))
					{
						LastGrammarFiles.Items.Add(filepath);
					}
				}
			}

			InitPackageParsing();
		}

		private void consoleWriter_WriteLineEvent(object sender, ConsoleWriterEventArgs e)
		{
			ParserBuidingLog.Items.Add(e.Value);
		}

		private void consoleWriter_WriteEvent(object sender, ConsoleWriterEventArgs e)
		{
			ParserBuidingLog.Items.Add(e.Value);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			e.Cancel = !MayProceedClosingGrammar();
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			var listContent = new List<string>();

			foreach (var item in LastGrammarFiles.Items)
				listContent.Add(item.ToString());

			File.WriteAllLines(LAST_GRAMMARS_FILE, listContent.Take(10));
		}

		#region Генерация парсера

		private string CurrentGrammarFilename { get; set; } = null;

		private bool MayProceedClosingGrammar()
		{
			/// Предлагаем сохранить грамматику, если она новая или если в открытой грамматике произошли изменения или исходный файл был удалён
			if (String.IsNullOrEmpty(CurrentGrammarFilename) && !String.IsNullOrEmpty(GrammarEditor.Text) ||
				!String.IsNullOrEmpty(CurrentGrammarFilename) && (!File.Exists(CurrentGrammarFilename) || File.ReadAllText(CurrentGrammarFilename) != GrammarEditor.Text))
			{
				switch (MessageBox.Show(
					"В грамматике имеются несохранённые изменения. Сохранить текущую версию?",
					"Предупреждение",
					MessageBoxButton.YesNoCancel,
					MessageBoxImage.Question))
				{
					case MessageBoxResult.Yes:
						SaveGrammarButton_Click(null, null);
						return true;
					case MessageBoxResult.No:
						return true;
					case MessageBoxResult.Cancel:
						return false;
				}
			}

			return true;
		}

		private void BuildParserButton_Click(object sender, RoutedEventArgs e)
		{
			Parser = null;
			var errors = new List<LandParserGenerator.Message>();

			if (ParsingLL.IsChecked == true)
			{
				Parser = LandParserGenerator.BuilderLL.BuildParser(GrammarEditor.Text, errors);
			}
			else if (ParsingLR.IsChecked == true)
			{

			}

			//ParserBuidingLog
			ParserBuidingErrors.ItemsSource = errors;

			if (Parser == null || errors.Count > 0)
			{
				ParserStatusLabel.Content = "Обнаружены ошибки в грамматике языка";
				ParserStatus.Background = LightRed;
			}
			else
			{
				ParserStatusLabel.Content = "Парсер успешно сгенерирован";
				ParserStatus.Background = Brushes.LightGreen;
			}
		}

		private void LoadGrammarButton_Click(object sender, RoutedEventArgs e)
		{
			if (MayProceedClosingGrammar())
			{
				var openFileDialog = new OpenFileDialog();
				if (openFileDialog.ShowDialog() == true)
				{
					OpenGrammar(openFileDialog.FileName);
				}
			}
		}

		private void SetAsCurrentGrammar(string filename)
		{
			LastGrammarFiles.SelectionChanged -= LastGrammarFiles_SelectionChanged;

			if (LastGrammarFiles.Items.Contains(filename))
				LastGrammarFiles.Items.Remove(filename);

			LastGrammarFiles.Items.Insert(0, filename);
			LastGrammarFiles.SelectedIndex = 0;

			LastGrammarFiles.SelectionChanged += LastGrammarFiles_SelectionChanged;
		}

		private void OpenGrammar(string filename)
		{
			if(!File.Exists(filename))
			{
				MessageBox.Show("Указанный файл не найден", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

				LastGrammarFiles.Items.Remove(filename);
				LastGrammarFiles.SelectedIndex = -1;

				CurrentGrammarFilename = null;
				GrammarEditor.Text = String.Empty;
				SaveGrammarButton.IsEnabled = true;

				return;
			}

			CurrentGrammarFilename = filename;
			GrammarEditor.Text = File.ReadAllText(filename);
			SaveGrammarButton.IsEnabled = false;
			SetAsCurrentGrammar(filename);
		}

		private void SaveGrammarButton_Click(object sender, RoutedEventArgs e)
		{
			if (!String.IsNullOrEmpty(CurrentGrammarFilename))
			{
				File.WriteAllText(CurrentGrammarFilename, GrammarEditor.Text);
				SaveGrammarButton.IsEnabled = false;
			}
			else
			{
				var saveFileDialog = new SaveFileDialog();
				if (saveFileDialog.ShowDialog() == true)
				{
					File.WriteAllText(saveFileDialog.FileName, GrammarEditor.Text);
					SaveGrammarButton.IsEnabled = false;
					CurrentGrammarFilename = saveFileDialog.FileName;
					SetAsCurrentGrammar(saveFileDialog.FileName);
				}
			}
		}

		private void NewGrammarButton_Click(object sender, RoutedEventArgs e)
		{
			if (MayProceedClosingGrammar())
			{
				LastGrammarFiles.SelectedIndex = -1;
				CurrentGrammarFilename = null;
				GrammarEditor.Text = String.Empty;
			}
		}

		private void GrammarEditor_TextChanged(object sender, EventArgs e)
		{
			ParserStatus.Background = Brushes.Yellow;
			ParserStatusLabel.Content = "Текст грамматики изменился со времени последней генерации парсера";
			SaveGrammarButton.IsEnabled = true;
			SelectedTextColorizerForGrammar.Reset();
		}

		private void GrammarListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var lb = sender as ListBox;

			if (lb.SelectedIndex != -1)
			{
				var msg = (LandParserGenerator.Message)lb.SelectedItem;
				if (msg.Location != null)
				{
					var start = GrammarEditor.Document.GetOffset(msg.Location.Line, msg.Location.Column);
					GrammarEditor.Focus();
					GrammarEditor.Select(start, 0);
					GrammarEditor.ScrollToLine(msg.Location.Line);
				}
			}
		}

		private void LastGrammarFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			/// Если что-то новое было выделено
			if (e.AddedItems.Count > 0)
			{
				/// Нужно предложить сохранение, и откатить смену выбора файла, если пользователь передумал
				if (!MayProceedClosingGrammar())
				{
					LastGrammarFiles.SelectionChanged -= LastGrammarFiles_SelectionChanged;
					ComboBox combo = (ComboBox)sender;
					/// Если до этого был выбран какой-то файл
					if (e.RemovedItems.Count > 0)
						combo.SelectedItem = e.RemovedItems[0];
					else
						combo.SelectedIndex = -1;
					LastGrammarFiles.SelectionChanged += LastGrammarFiles_SelectionChanged;
					return;
				}
				OpenGrammar(e.AddedItems[0].ToString());
			}
		}

		#endregion

		#region Парсинг одиночного файла

		private void ParseButton_Click(object sender, RoutedEventArgs e)
		{
            if (Parser != null)
            {
                var root = Parser.Parse(FileEditor.Text);

                ProgramStatusLabel.Content = Parser.Errors.Count == 0 ? "Разбор произведён успешно" : "Ошибки при разборе файла";
                ProgramStatus.Background = Parser.Errors.Count == 0 ? Brushes.LightGreen : LightRed;

                if (root != null)
                {
                    TreeRoot = root;
                    ParseTreeView.ItemsSource = new[] { (TreeViewAdapter)root };
                }

                FileParsingLog.ItemsSource = Parser.Log;
				FileParsingErrors.ItemsSource = Parser.Errors;
            }
		}

		private void ParseTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			var treeView = (TreeView)sender;
			var node = ((TreeViewAdapter)treeView.SelectedItem).Source;

			if (node.StartOffset.HasValue && node.EndOffset.HasValue)
			{
				var start = node.StartOffset.Value;
				var end = node.EndOffset.Value;
				FileEditor.Select(start, end - start + 1);
				FileEditor.ScrollToLine(FileEditor.Document.GetLocation(start).Line);
				MainTabs.SelectedIndex = 1;
			}
			else
			{
				FileEditor.Select(0, 0);
			}
		}

		private void OpenFileButton_Click(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog();
			if (openFileDialog.ShowDialog() == true)
			{
				OpenFile(openFileDialog.FileName);
			}
		}

		private void OpenFile(string filename)
		{
			FileEditor.Text = File.ReadAllText(filename);
			TestFileName.Content = filename;
		}

		private void ClearFileButton_Click(object sender, RoutedEventArgs e)
		{
			TestFileName.Content = null;
			FileEditor.Text = String.Empty;
		}

		private void TestFileListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var lb = sender as ListBox;

			if (lb.SelectedIndex != -1)
			{
				var msg = (LandParserGenerator.Message)lb.SelectedItem;
				if (msg.Location != null)
				{
					var start = FileEditor.Document.GetOffset(msg.Location.Line, msg.Location.Column);
					FileEditor.Focus();
					FileEditor.Select(start, 0);
					FileEditor.ScrollToLine(FileEditor.Document.GetLocation(start).Line);
				}
			}
		}

		#endregion

		#region Парсинг набора файлов

		private System.ComponentModel.BackgroundWorker PackageParsingWorker;
		private Dispatcher FrontendUpdateDispatcher { get; set; }

		private string PackageSource { get; set; }

		private void InitPackageParsing()
		{
			FrontendUpdateDispatcher = Dispatcher.CurrentDispatcher;

			PackageParsingWorker = new System.ComponentModel.BackgroundWorker();
			PackageParsingWorker.WorkerReportsProgress = true;
			PackageParsingWorker.WorkerSupportsCancellation = true;
			PackageParsingWorker.DoWork += worker_DoWork;
			PackageParsingWorker.ProgressChanged += worker_ProgressChanged;
		}

		private void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
		{
			var folderDialog = new System.Windows.Forms.FolderBrowserDialog();

			/// При выборе каталога запоминаем имя и отображаем его в строке статуса
			if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				PackageSource = folderDialog.SelectedPath;
				PackagePathLabel.Content = $"Выбран каталог {PackageSource}";
			}
		}

		void worker_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
		{
			OnPackageFileParsingError = AddRecordToPackageParsingLog;
			OnPackageFileParsed = UpdatePackageParsingStatus;

			var files = (List<string>) e.Argument;
			var errorCounter = 0;
			var counter = 0;
			var errorFiles = new List<string>();

			FrontendUpdateDispatcher.Invoke((Action)(()=>{ PackageParsingLog.Items.Clear(); }));		

			for (; counter < files.Count; ++counter)
			{
				try
				{
					FrontendUpdateDispatcher.Invoke((Action)(() => { Parser.Parse(File.ReadAllText(files[counter])); }));

					if (Parser.Errors.Count > 0)
					{
						FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, files[counter]);
						foreach (var error in Parser.Errors)
							FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, $"\t{error}");

						++errorCounter;
					}
				}
				catch (Exception ex)
				{
					FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, files[counter]);
					foreach (var error in Parser.Errors)
						FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, $"\t{error}");
					FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, $"\t{ex.ToString()}");

					++errorCounter;
				}

				(sender as System.ComponentModel.BackgroundWorker).ReportProgress((counter + 1) * 100 / files.Count);
				FrontendUpdateDispatcher.Invoke(OnPackageFileParsed, files.Count, counter + 1, errorCounter);

				if(PackageParsingWorker.CancellationPending)
				{
					e.Cancel = true;
					return;
				}
			}

			FrontendUpdateDispatcher.Invoke(OnPackageFileParsed, counter, counter, errorCounter);
		}

		private delegate void UpdatePackageParsingStatusDelegate(int total, int parsed, int errorsCount);

		private UpdatePackageParsingStatusDelegate OnPackageFileParsed { get; set; }
			
		private void UpdatePackageParsingStatus(int total, int parsed, int errorsCount)
		{
			if (total == parsed)
			{
				PackageStatusLabel.Content = $"Разобрано: {parsed}; С ошибками: {errorsCount} {Environment.NewLine}";
				PackageStatus.Background = errorsCount == 0 ? Brushes.LightGreen : LightRed;
			}
			else
			{
				PackageStatusLabel.Content = $"Всего: {total}; Разобрано: {parsed}; С ошибками: {errorsCount} {Environment.NewLine}";
			}
		}

		private delegate void AddRecordToPackageParsingLogDelegate(object record);

		private AddRecordToPackageParsingLogDelegate OnPackageFileParsingError { get; set; }

		private void AddRecordToPackageParsingLog(object record)
		{
			PackageParsingLog.Items.Add(record);
		}

		void worker_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
		{
			PackageParsingProgress.Value = e.ProgressPercentage;
		}

		private void PackageParsingListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var lb = sender as ListBox;

			if (lb.SelectedIndex != -1 && lb.SelectedItem is string)
			{
				/// Открыть файл
				OpenFile(lb.SelectedItem.ToString());
				MainTabs.SelectedIndex = 1;
			}
		}

		private void StartOrStopPackageParsingButton_Click(object sender, RoutedEventArgs e)
		{
			if(!PackageParsingWorker.IsBusy)
			{
				/// Если в настоящий момент парсинг не осуществляется и парсер сгенерирован
				if (Parser != null)
				{
					/// Получаем имена всех файлов с нужным расширением
					var patterns = TargetExtentions.Text.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(ext => $"*.{ext.Trim().Trim('.')}");
					var package = new List<string>();
					/// Возможна ошибка при доступе к определённым директориям
					try
					{			
						foreach (var pattern in patterns)
						{
							package.AddRange(Directory.GetFiles(PackageSource, pattern, SearchOption.AllDirectories));
						}
					}
					catch
					{
						package = new List<string>();
						PackagePathLabel.Content = $"Ошибка при получении содержимого каталога, возможно, отсутствуют права доступа";
					}
					/// Запускаем в отдельном потоке массовый парсинг
					PackageStatus.Background = Brushes.WhiteSmoke;
					PackageParsingWorker.RunWorkerAsync(package);
					PackageParsingProgress.Foreground = Brushes.MediumSeaGreen;
				}
			}
			else
			{
				PackageParsingWorker.CancelAsync();
				PackageParsingProgress.Foreground = Brushes.IndianRed;
			}
		}
		#endregion	
	}
}
