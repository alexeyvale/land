using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;

using Microsoft.Win32;

using ICSharpCode.AvalonEdit;

using Land.Core;
using Land.Core.Parsing.Preprocessing;
using Land.Core.Parsing.Tree;
using Land.Core.Markup;
using Land.Control;

namespace Land.GUI
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private string LAST_GRAMMARS_FILE = "./last_grammars.land.ide";

		private Brush LightRed = new SolidColorBrush(Color.FromRgb(255, 200, 200));

		private SelectedTextColorizer Grammar_SelectedTextColorizer { get; set; }
		private SegmentsBackgroundRenderer File_SegmentColorizer { get; set; }

		private Land.Core.Parsing.BaseParser Parser { get; set; }

		public MainWindow()
		{
			InitializeComponent();

			Grammar_Editor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(
				new System.Xml.XmlTextReader(new StreamReader($"../../land.xshd", Encoding.Default)), 
				ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance);

			Grammar_Editor.TextArea.TextView.BackgroundRenderers.Add(new CurrentLineBackgroundRenderer(Grammar_Editor.TextArea));
			Grammar_SelectedTextColorizer = new SelectedTextColorizer(Grammar_Editor.TextArea);

			File_Editor.TextArea.TextView.BackgroundRenderers.Add(new CurrentLineBackgroundRenderer(File_Editor.TextArea));
			File_Editor.TextArea.TextView.BackgroundRenderers.Add(File_SegmentColorizer = new SegmentsBackgroundRenderer(File_Editor.TextArea));


			if (File.Exists(LAST_GRAMMARS_FILE))
			{
				var files = File.ReadAllLines(LAST_GRAMMARS_FILE);
				foreach(var filepath in files)
				{
					if(!String.IsNullOrEmpty(filepath))
					{
						Grammar_RecentFiles.Items.Add(filepath);
					}
				}
			}

			EditorAdapter = new EditorAdapter(this, "./land_explorer_settings.xml");
			LandExplorer.Initialize(EditorAdapter);

			InitPackageParsing();
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			e.Cancel = !MayProceedClosingGrammar();
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			var listContent = new List<string>();

			foreach (var item in Grammar_RecentFiles.Items)
				listContent.Add(item.ToString());

			File.WriteAllLines(LAST_GRAMMARS_FILE, listContent.Take(10));
		}

		private void MoveCaretToSource(SegmentLocation loc, ICSharpCode.AvalonEdit.TextEditor editor, bool selectText = true, int? tabToSelect = null)
		{
			if (loc != null)
			{
				var start = loc.Start.Offset;
				var end = loc.End.Offset;
				editor.ScrollToLine(editor.Document.GetLocation(start).Line);

				if (selectText)
					editor.Select(start, end - start + 1);

				if(tabToSelect.HasValue)
					MainTabs.SelectedIndex = tabToSelect.Value;
			}
			else
			{
				editor.Select(0, 0);
			}
		}

		#region Генерация парсера

		private string CurrentGrammarFilename { get; set; } = null;

		private bool MayProceedClosingGrammar()
		{
			/// Предлагаем сохранить грамматику, если она новая или если в открытой грамматике произошли изменения или исходный файл был удалён
			if (String.IsNullOrEmpty(CurrentGrammarFilename) && !String.IsNullOrEmpty(Grammar_Editor.Text) ||
				!String.IsNullOrEmpty(CurrentGrammarFilename) && (!File.Exists(CurrentGrammarFilename) || File.ReadAllText(CurrentGrammarFilename) != Grammar_Editor.Text))
			{
				switch (MessageBox.Show(
					"В грамматике имеются несохранённые изменения. Сохранить текущую версию?",
					"Предупреждение",
					MessageBoxButton.YesNoCancel,
					MessageBoxImage.Question))
				{
					case MessageBoxResult.Yes:
						Grammar_SaveButton_Click(null, null);
						return true;
					case MessageBoxResult.No:
						return true;
					case MessageBoxResult.Cancel:
						return false;
				}
			}

			return true;
		}

		private void Grammar_BuildButton_Click(object sender, RoutedEventArgs e)
		{
			Parser = null;
			var messages = new List<Message>();

			Parser = BuilderBase.BuildParser(GrammarType.LL, Grammar_Editor.Text, messages);

			Grammar_LogList.Text = String.Join(Environment.NewLine, messages.Where(m=>m.Type == MessageType.Trace).Select(m=>m.Text));
			Grammar_ErrorsList.ItemsSource = messages.Where(m=>m.Type == MessageType.Error || m.Type == MessageType.Warning);

			if (Parser == null || messages.Count(m=>m.Type == MessageType.Error) > 0)
			{
				Grammar_StatusBarLabel.Content = "Обнаружены ошибки в грамматике языка";
				Grammar_StatusBar.Background = LightRed;
			}
			else
			{
				Grammar_StatusBarLabel.Content = "Парсер успешно сгенерирован";
				Grammar_StatusBar.Background = Brushes.LightGreen;
			}
		}

		private void Grammar_LibraryButton_Click(object sender, RoutedEventArgs e)
		{
			var librarySettings = new LibrarySettingsWindow();

			librarySettings.Input_Namespace.Text = !String.IsNullOrEmpty(CurrentGrammarFilename) 
				? Path.GetFileNameWithoutExtension(CurrentGrammarFilename)
				: null;
			librarySettings.Input_OutputDirectory.Text = Path.GetDirectoryName(CurrentGrammarFilename);

			if (librarySettings.ShowDialog() == true)
			{
				var messages = new List<Message>();
				var success = BuilderBase.GenerateLibrary(
					GrammarType.LL, 
					Grammar_Editor.Text, 
					librarySettings.Input_Namespace.Text, 
					librarySettings.Input_OutputDirectory.Text,
					librarySettings.Input_IsSignedAssembly.IsChecked == true 
						? String.IsNullOrWhiteSpace(librarySettings.Input_KeysFile.Text)
							? Path.Combine(librarySettings.Input_OutputDirectory.Text, $"{librarySettings.Input_Namespace.Text}.snk")
							: librarySettings.Input_KeysFile.Text
						: null,
					messages
				);			

				Grammar_LogList.Text = String.Join(Environment.NewLine, messages.Where(m => m.Type == MessageType.Trace).Select(m => m.Text));
				Grammar_ErrorsList.ItemsSource = messages.Where(m => m.Type == MessageType.Error || m.Type == MessageType.Warning);

				if (!success)
				{
					Grammar_StatusBarLabel.Content = "Не удалось сгенерировать библиотеку";
					Grammar_StatusBar.Background = LightRed;
				}
				else
				{
					Grammar_StatusBarLabel.Content = "Библиотека успешно сгенерирована";
					Grammar_StatusBar.Background = Brushes.LightGreen;
				}
			}
		}

		private void Grammar_LoadGrammarButton_Click(object sender, RoutedEventArgs e)
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
			Grammar_RecentFiles.SelectionChanged -= Grammar_RecentFiles_SelectionChanged;

			if (Grammar_RecentFiles.Items.Contains(filename))
				Grammar_RecentFiles.Items.Remove(filename);

			Grammar_RecentFiles.Items.Insert(0, filename);
			Grammar_RecentFiles.SelectedIndex = 0;

			Grammar_RecentFiles.SelectionChanged += Grammar_RecentFiles_SelectionChanged;
		}

		private void OpenGrammar(string filename)
		{
			if(!File.Exists(filename))
			{
				MessageBox.Show("Указанный файл не найден", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

				Grammar_RecentFiles.Items.Remove(filename);
				Grammar_RecentFiles.SelectedIndex = -1;

				CurrentGrammarFilename = null;
				Grammar_Editor.Text = String.Empty;
				Grammar_SaveButton.IsEnabled = true;

				return;
			}

			CurrentGrammarFilename = filename;

			using (var stream = new StreamReader(filename, GetEncoding(filename)))
			{
				Grammar_Editor.Text = stream.ReadToEnd();
				Grammar_Editor.Encoding = stream.CurrentEncoding;
			}

			Grammar_SaveButton.IsEnabled = false;
			SetAsCurrentGrammar(filename);
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

		private void Grammar_SaveButton_Click(object sender, RoutedEventArgs e)
		{
			if (!String.IsNullOrEmpty(CurrentGrammarFilename))
			{
				File.WriteAllText(CurrentGrammarFilename, Grammar_Editor.Text);
				Grammar_SaveButton.IsEnabled = false;
			}
			else
			{
				var saveFileDialog = new SaveFileDialog();
				if (saveFileDialog.ShowDialog() == true)
				{
					File.WriteAllText(saveFileDialog.FileName, Grammar_Editor.Text);
					Grammar_SaveButton.IsEnabled = false;
					CurrentGrammarFilename = saveFileDialog.FileName;
					SetAsCurrentGrammar(saveFileDialog.FileName);
				}
			}
		}

		private void Grammar_NewGrammarButton_Click(object sender, RoutedEventArgs e)
		{
			if (MayProceedClosingGrammar())
			{
				Grammar_RecentFiles.SelectedIndex = -1;
				CurrentGrammarFilename = null;
				Grammar_Editor.Text = String.Empty;
			}
		}

		private void Grammar_Editor_TextChanged(object sender, EventArgs e)
		{
			Grammar_StatusBar.Background = Brushes.Yellow;
			Grammar_StatusBarLabel.Content = "Текст грамматики изменился со времени последней генерации парсера";
			Grammar_SaveButton.IsEnabled = true;
			Grammar_SelectedTextColorizer.Reset();
		}

		private void Grammar_ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var lb = sender as ListBox;

			if (lb.SelectedIndex != -1)
			{
				var msg = lb.SelectedItem as Land.Core.Message;
				if (msg != null && msg.Location != null)
				{
					/// Если координаты не выходят за пределы файла, устанавливаем курсор в соответствии с ними, 
					/// иначе ставим курсор в позицию после последнего элемента последней строки
					int start = 0;
					if(msg.Location.Line <= Grammar_Editor.Document.LineCount)
						start = Grammar_Editor.Document.GetOffset(msg.Location.Line, msg.Location.Column);
					else
						start = Grammar_Editor.Document.GetOffset(Grammar_Editor.Document.LineCount, Grammar_Editor.Document.Lines[Grammar_Editor.Document.LineCount-1].Length + 1);

					Grammar_Editor.Focus();
					Grammar_Editor.Select(start, 0);
					Grammar_Editor.ScrollToLine(msg.Location.Line);
				}
			}
		}

		private void Grammar_RecentFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			/// Если что-то новое было выделено
			if (e.AddedItems.Count > 0)
			{
				/// Нужно предложить сохранение, и откатить смену выбора файла, если пользователь передумал
				if (!MayProceedClosingGrammar())
				{
					Grammar_RecentFiles.SelectionChanged -= Grammar_RecentFiles_SelectionChanged;
					ComboBox combo = (ComboBox)sender;
					/// Если до этого был выбран какой-то файл
					if (e.RemovedItems.Count > 0)
						combo.SelectedItem = e.RemovedItems[0];
					else
						combo.SelectedIndex = -1;
					Grammar_RecentFiles.SelectionChanged += Grammar_RecentFiles_SelectionChanged;
					return;
				}
				OpenGrammar(e.AddedItems[0].ToString());
			}
		}

		#endregion

		#region Парсинг одиночного файла

		private Node TreeRoot { get; set; }
		private string TreeSource { get; set; }

		private Node File_Parse(string fileName, string text, bool enableTracing = false)
		{
			BasePreprocessor preproc = null;
			
			/// Если известно имя файла, по расширению можем выбрать препроцессор
			if ((File_PreprocessToggle.IsChecked ?? false) && !String.IsNullOrEmpty(fileName))
			{
				switch (Path.GetExtension(fileName))
				{
					case ".cs":
						preproc = new SharpPreprocessor.SharpPreprocessor();
						break;
				}
			}

			Parser?.SetPreprocessor(preproc);
			return Parser?.Parse(text, enableTracing);
		}

		private void File_ParseButton_Click(object sender, RoutedEventArgs e)
		{
            if (Parser != null)
            {
				Node root = null;

				try
				{
					root = File_Parse((string)File_NameLabel.Content, File_Editor.Text, true);
				}
				catch(Exception ex)
				{
					Parser.Log?.Add(Message.Error(ex.ToString(), null));
				}

				File_Statistics.Text = Parser.Statistics?.ToString();

				var noErrors = Parser.Log.All(l => l.Type != MessageType.Error);
				File_StatusBarLabel.Content = noErrors ? "Разбор произведён успешно" : "Ошибки при разборе файла";
                File_StatusBar.Background = noErrors ? Brushes.LightGreen : LightRed;

                if (root != null)
                {
                    TreeRoot = root;
					TreeSource = File_Editor.Text;
                    AstView.ItemsSource = new List<Node>() { root };
                }

                File_LogList.ItemsSource = Parser.Log;
				File_ErrorsList.ItemsSource = Parser.Log.Where(l=>l.Type != MessageType.Trace).ToList();
            }
		}

		private void AstView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			var treeView = (TreeView)sender;

			MoveCaretToSource(((Node)treeView.SelectedItem).Anchor, File_Editor, true, 1);
		}

		private void File_OpenButton_Click(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog();
			if (openFileDialog.ShowDialog() == true)
			{
				OpenFile(openFileDialog.FileName);
			}
		}

		private void File_SaveButton_Click(object sender, RoutedEventArgs e)
		{
			/// По нажатию на "Сохранить" всегда предлагаем выбрать, куда,
			/// чтобы не перезатереть файлы разбираемых проектов 
			var saveFileDialog = new SaveFileDialog();
			if (saveFileDialog.ShowDialog() == true)
			{
				File.WriteAllText(saveFileDialog.FileName, File_Editor.Text);
				File_NameLabel.Content = saveFileDialog.FileName;
			}
		}


		private void OpenFile(string filename)
		{
			File_NameLabel.Content = filename;

			using (var stream = new StreamReader(filename, GetEncoding(filename)))
			{
				File_Editor.Text = stream.ReadToEnd();
				File_Editor.Encoding = stream.CurrentEncoding;
			}

			File_ParseButton_Click(null, null);
		}

		private void File_ClearButton_Click(object sender, RoutedEventArgs e)
		{
			File_NameLabel.Content = null;

			File_Editor.Text = String.Empty;
			File_Editor.Encoding = Encoding.UTF8;
		}

		private void File_ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var lb = sender as ListBox;

			if (lb.SelectedIndex != -1)
			{
				var msg = (Land.Core.Message)lb.SelectedItem;
				if (msg.Location != null)
				{
					var start = File_Editor.Document.GetOffset(msg.Location.Line, msg.Location.Column);
					File_Editor.Focus();
					File_Editor.Select(start, 0);
					File_Editor.ScrollToLine(File_Editor.Document.GetLocation(start).Line);
				}
			}
		}

		public class GetWaterSegmentsVisitor
		{
			public List<Tuple<int, int>> AnySegments { get; set; } = new List<Tuple<int, int>>();
			public List<Tuple<int, int>> TypedWaterSegments { get; set; } = new List<Tuple<int, int>>();

			public void Visit(Node node)
			{
				foreach (var child in node.Children)
				{
					if (child.Symbol == Grammar.ANY_TOKEN_NAME)
					{
						if (child.Anchor != null)
							AnySegments.Add(new Tuple<int, int>(child.Anchor.Start.Offset, child.Anchor.End.Offset));
					}
					else
					{
						if (!child.Options.IsLand && child.Anchor != null)
							TypedWaterSegments.Add(new Tuple<int, int>(child.Anchor.Start.Offset, child.Anchor.End.Offset));
					}

					Visit(child);
				}
			}
		}

		private void AstView_SelectAny_StateChanged(object sender, RoutedEventArgs e)
		{
			var source = (CheckBox)sender;

			if (source.IsChecked == true)
			{
				if (TreeRoot != null)
				{
					var waterVisitor = new GetWaterSegmentsVisitor();
					waterVisitor.Visit(TreeRoot);
					File_SegmentColorizer.SetSegments(waterVisitor.AnySegments.Select(s => new DocumentSegment()
					{
						StartOffset = s.Item1,
						EndOffset = s.Item2,
						CaptureWholeLine = false
					}).ToList(), Color.FromArgb(60, 150, 150, 200));
				}
			}
			else
			{
				File_SegmentColorizer.ResetSegments();
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
			PackageParsingWorker.DoWork += Worker_DoWork;
			PackageParsingWorker.ProgressChanged += Worker_ProgressChanged;
		}

		private void Batch_ChooseFolderButton_Click(object sender, RoutedEventArgs e)
		{
			var folderDialog = new System.Windows.Forms.FolderBrowserDialog();

			/// При выборе каталога запоминаем имя и отображаем его в строке статуса
			if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				PackageSource = folderDialog.SelectedPath;
				Batch_PathLabel.Content = $"Выбран каталог {PackageSource}";
			}
		}

		void Worker_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
		{
			OnPackageFileParsingError = AddRecordToPackageParsingLog;
			OnPackageFileParsed = UpdatePackageParsingStatus;

			var files = (List<string>) e.Argument;
			var errorCounter = 0;
			var counter = 0;
			var timeSpent = new TimeSpan();
			var errorFiles = new List<string>();

			FrontendUpdateDispatcher.Invoke((Action)(()=>{ Batch_Log.Items.Clear(); }));		
			var timePerFile = new Dictionary<string, TimeSpan>();
			var landCounts = new Dictionary<string, int>();
			var landValues = new Dictionary<string, Dictionary<string, List<string>>>();

			for (; counter < files.Count; ++counter)
			{
				try
				{
					Node root = null;
					FrontendUpdateDispatcher.Invoke((Action)(() => { root = File_Parse(files[counter], File.ReadAllText(files[counter])); }));

					timeSpent += Parser.Statistics.TimeSpent;

					if (Parser.Log.Any(l=>l.Type == MessageType.Error))
					{
						FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, files[counter]);
						foreach (var error in Parser.Log.Where(l=>l.Type != MessageType.Trace))
							FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, $"\t{error}");

						++errorCounter;
					}
					else
					{
						timePerFile[files[counter]] = Parser.Statistics.TimeSpent;

						var visitor = new CountLandNodesVisitor("name");
						root.Accept(visitor);

						foreach (var pair in visitor.Counts)
						{
							if (!landCounts.ContainsKey(pair.Key))
							{
								landCounts[pair.Key] = 0;
								landValues[pair.Key] = new Dictionary<string, List<string>>();
							}

							landCounts[pair.Key] += pair.Value;
							landValues[pair.Key][files[counter]] = new List<string>(visitor.Values[pair.Key]);
						}

					}
				}
				catch (Exception ex)
				{
					FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, files[counter]);
					foreach (var error in Parser.Log.Where(l => l.Type != MessageType.Trace))
						FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, $"\t{error}");
					FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, $"\t{ex.ToString()}");

					++errorCounter;
				}

				(sender as System.ComponentModel.BackgroundWorker).ReportProgress((counter + 1) * 100 / files.Count);
				FrontendUpdateDispatcher.Invoke(OnPackageFileParsed, files.Count, counter + 1, errorCounter, timeSpent);

				if(PackageParsingWorker.CancellationPending)
				{
					e.Cancel = true;
					return;
				}
			}

			FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, "");

			/// Выводим предупреждения о наиболее долго разбираемых файлах
			foreach (var file in timePerFile.OrderByDescending(f=>f.Value).Take(10))
			{
				FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, $"{Message.Warning(file.Key, null)}");
				FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, $"\t{Message.Warning(file.Value.ToString(@"hh\:mm\:ss\:ff"), null)}");
			}

			FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, "");

			foreach (var pair in landCounts)
			{
				FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, $"{Message.Warning($"{pair.Key}:\t{pair.Value}", null)}");

				using (var fs = new StreamWriter($"{pair.Key}.txt", false))
				{
					foreach(var fileData in landValues[pair.Key].Where(p=>p.Value.Count > 0))
					{
						fs.WriteLine(fileData.Key);

						foreach(var line in fileData.Value.SelectMany(str => str.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)))
							fs.WriteLine(line);
					}
				}
			}

			FrontendUpdateDispatcher.Invoke(OnPackageFileParsed, counter, counter, errorCounter, timeSpent);
		}

		private delegate void UpdatePackageParsingStatusDelegate(int total, int parsed, int errorsCount, TimeSpan tipeSpent);

		private UpdatePackageParsingStatusDelegate OnPackageFileParsed { get; set; }
			
		private void UpdatePackageParsingStatus(int total, int parsed, int errorsCount, TimeSpan timeSpent)
		{
			if (total == parsed)
			{
				Batch_StatusBarLabel.Content = $"Разобрано: {parsed}; С ошибками: {errorsCount}; Время: {timeSpent.ToString(@"hh\:mm\:ss\:ff")}{Environment.NewLine}";
				Batch_StatusBar.Background = errorsCount == 0 ? Brushes.LightGreen : LightRed;
			}
			else
			{
				Batch_StatusBarLabel.Content = $"Всего: {total}; Разобрано: {parsed}; С ошибками: {errorsCount}; Время: {timeSpent.ToString(@"hh\:mm\:ss\:ff")}{Environment.NewLine}";
			}
		}

		private delegate void AddRecordToPackageParsingLogDelegate(object record);

		private AddRecordToPackageParsingLogDelegate OnPackageFileParsingError { get; set; }

		private void AddRecordToPackageParsingLog(object record)
		{
			Batch_Log.Items.Add(record);
		}

		void Worker_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
		{
			Batch_ParsingProgress.Value = e.ProgressPercentage;
		}

		private void Batch_ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var lb = sender as ListBox;

			if (lb.SelectedIndex != -1 && lb.SelectedItem is string)
			{
				/// Открыть файл
				if (File.Exists(lb.SelectedItem.ToString()))
				{
					OpenFile(lb.SelectedItem.ToString());
					MainTabs.SelectedIndex = 1;
				}
			}
		}

		private void Batch_StartOrStopPackageParsingButton_Click(object sender, RoutedEventArgs e)
		{
			if(!PackageParsingWorker.IsBusy)
			{
				/// Если в настоящий момент парсинг не осуществляется и парсер сгенерирован
				if (Parser != null)
				{
					/// Получаем имена всех файлов с нужным расширением
					var patterns = Batch_TargetExtentions.Text.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(ext => $"*.{ext.Trim().Trim('.')}");
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
						Batch_PathLabel.Content = $"Ошибка при получении содержимого каталога, возможно, отсутствуют права доступа";
					}
					/// Запускаем в отдельном потоке массовый парсинг
					Batch_StatusBar.Background = Brushes.WhiteSmoke;
					PackageParsingWorker.RunWorkerAsync(package);
					Batch_ParsingProgress.Foreground = Brushes.MediumSeaGreen;
				}
			}
			else
			{
				PackageParsingWorker.CancelAsync();
				Batch_ParsingProgress.Foreground = Brushes.IndianRed;
			}
		}
		#endregion

		#region Отладка перепривязки

		private Node NewTreeRoot { get; set; }
		private bool NewTextChanged { get; set; }

		private void ReplaceNewWithOldButton_Click(object sender, RoutedEventArgs e)
		{
			MappingDebug_NewTextEditor.Text = MappingDebug_OldTextEditor.Text;
		}

		private void MappingDebug_MarkupTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (MappingDebug_MarkupTreeView.SelectedItem is ConcernPoint point)
			{
				if (point.Location != null)
				{
					MappingDebug_OldTextEditor.Text = LandExplorer.GetText(point.Context.FileName);

					if (String.IsNullOrEmpty(MappingDebug_NewTextEditor.Text))
					{
						MappingDebug_NewTextEditor.Text = LandExplorer.GetText(point.Context.FileName);
					}

					MoveCaretToSource(point.Location, MappingDebug_OldTextEditor, true);
				}
			}
		}

		private void MainPerspectiveTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			MappingDebug_MarkupTreeView.ItemsSource = LandExplorer.GetMarkup();
		}

		private void MappingDebug_MarkupTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if(MappingDebug_MarkupTreeView.SelectedItem is ConcernPoint)
			{
				MapPoint((ConcernPoint)MappingDebug_MarkupTreeView.SelectedItem);
			}
		}

		private void MapPoint(ConcernPoint point)
		{
			/// Если текст, к которому пытаемся перепривязаться, изменился
			if (NewTextChanged)
			{
				var parser = LandExplorer.GetParser(Path.GetExtension(point.Context.FileName));

				/// и при этом парсер сгенерирован
				if (parser != null)
				{
					/// пытаемся распарсить текст
					NewTreeRoot = parser.Parse(MappingDebug_NewTextEditor.Text);
					var noErrors = parser.Log.All(l => l.Type != MessageType.Error);

					MappingDebug_StatusBar.Background = noErrors ? Brushes.LightGreen : LightRed;
					MappingDebug_StatusLabel.Content = noErrors ? String.Empty : "Ошибка при разборе нового текста";

					/// Если текст распарсился, ищем отображение из старого текста в новый
					if (noErrors)
					{
						NewTextChanged = false;
					}
				}
			}

			/// Если для текущего нового текста построено дерево и просчитано отображение
			if (!NewTextChanged)
			{
				var similarities = LandExplorer.GetMappingCandidates(point, NewTreeRoot).ToDictionary(e=>e.Node, e=>e.Similarity);
				MappingDebug_SimilaritiesList.ItemsSource = similarities;

				MoveCaretToSource(point.Location, MappingDebug_OldTextEditor);

				/// Если есть узлы в новом дереве, с которыми мы сравнивали выбранный узел старого дерева
				if (MappingDebug_SimilaritiesList.ItemsSource != null)
				{
					/// значит, в какой-то новый узел мы отобразили старый
					MappingDebug_SimilaritiesList.SelectedItem = similarities.FirstOrDefault(e=>e.Value == similarities.Max(el=>el.Value));
					MoveCaretToSource(((KeyValuePair<Node, double>)MappingDebug_SimilaritiesList.SelectedItem).Key.Anchor, MappingDebug_NewTextEditor);
				}
			}
		}

		private void MappingDebug_SimilaritiesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if(MappingDebug_SimilaritiesList.SelectedItem != null)
			{
				var node = ((KeyValuePair<Node,double>)MappingDebug_SimilaritiesList.SelectedItem).Key;
				MoveCaretToSource(node.Anchor, MappingDebug_NewTextEditor);
			}
		}

		private void NewTextEditor_TextChanged(object sender, EventArgs e)
		{
			NewTextChanged = true;
		}

		#endregion

		#region Тестирование панели разметки

		//public delegate void DocumentChangedHandler(string documentName);
		public Action<string> DocumentChangedCallback;

		public class DocumentTab
		{
			public TextEditor Editor { get; set; }

			public string DocumentName { get; set; }

			public SegmentsBackgroundRenderer SegmentsColorizer { get; set; }
		}

		public EditorAdapter EditorAdapter { get; set; }

		public Dictionary<TabItem, DocumentTab> Documents { get; set; } = new Dictionary<TabItem, DocumentTab>();

		private int NewDocumentCounter { get; set; } = 1;

		public DocumentTab CreateDocument(string documentName)
		{
			var tab = new TabItem();
			DocumentTabs.Items.Add(tab);

			Documents[tab] = new DocumentTab()
			{
				DocumentName = documentName,
				Editor = new TextEditor()
				{
					ShowLineNumbers = true,
					FontSize = 16,
					FontFamily = new FontFamily("Consolas")
				}
			};

			Documents[tab].Editor.TextArea.TextView.BackgroundRenderers
				.Add(Documents[tab].SegmentsColorizer = new SegmentsBackgroundRenderer(Documents[tab].Editor.TextArea));
			Documents[tab].Editor.TextChanged += Editor_TextChanged;

			tab.Content = Documents[tab].Editor;
			tab.Header = Path.GetFileName(Documents[tab].DocumentName);

			DocumentTabs.SelectedItem = tab;

			return Documents[tab];
		}

		private void Editor_TextChanged(object sender, EventArgs e)
		{
			var document = Documents.Values.FirstOrDefault(d => d.Editor == sender);

			if (document != null && !String.IsNullOrEmpty(document.DocumentName) 
				&& DocumentChangedCallback != null)
				DocumentChangedCallback(document.DocumentName);
		}

		public DocumentTab OpenDocument(string documentName)
		{
			if (File.Exists(documentName))
			{
				var stream = new StreamReader(documentName, Encoding.Default, true);
				var document = CreateDocument(documentName);

				document.Editor.Text = stream.ReadToEnd();
				stream.Close();

				return document;
			}

			return null;
		}

		private void NewDocumentButton_Click(object sender, RoutedEventArgs e)
		{
			CreateDocument($"Новый документ {NewDocumentCounter++}");
		}

		private void SaveDocumentButton_Click(object sender, RoutedEventArgs e)
		{
			var activeTab = (TabItem)DocumentTabs.SelectedItem;

			if (activeTab != null)
			{
				if (!File.Exists(Documents[activeTab].DocumentName))
				{
					var saveFileDialog = new SaveFileDialog();
					if (saveFileDialog.ShowDialog() == true)
					{
						File.WriteAllText(saveFileDialog.FileName, Documents[activeTab].Editor.Text);
						Documents[activeTab].DocumentName = saveFileDialog.FileName;
						activeTab.Header = Path.GetFileName(saveFileDialog.FileName);
					}
				}
				else
				{
					File.WriteAllText(
						Documents[activeTab].DocumentName, 
						Documents[activeTab].Editor.Text
					);
				}
			}
		}

		private void CloseDocumentButton_Click(object sender, RoutedEventArgs e)
		{
			var activeTab = (TabItem)DocumentTabs.SelectedItem;

			if (activeTab != null)
			{
				/// Если файла для закрываемого таба не существует, и закрываемый текст непуст
				if ((String.IsNullOrEmpty(Documents[activeTab].DocumentName) 
					|| !File.Exists(Documents[activeTab].DocumentName)) 
					&& !String.IsNullOrEmpty(Documents[activeTab].Editor.Text)
					/// или если файл существует и его текст не совпадает с текстом в табе
					|| !String.IsNullOrEmpty(Documents[activeTab].DocumentName) 
					&& File.Exists(Documents[activeTab].DocumentName) 
					&& File.ReadAllText(Documents[activeTab].DocumentName) != Documents[activeTab].Editor.Text)
				{
					switch (MessageBox.Show(
						"В файле имеются несохранённые изменения. Сохранить текущую версию?",
						"Предупреждение",
						MessageBoxButton.YesNoCancel,
						MessageBoxImage.Question))
					{
						case MessageBoxResult.Yes:
							SaveDocumentButton_Click(null, null);
							break;
						case MessageBoxResult.No:
							break;
						case MessageBoxResult.Cancel:
							return;
					}
				}

				DocumentTabs.Items.Remove(activeTab);
				Documents.Remove(activeTab);
			}
		}

		private void OpenDocumentButton_Click(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog();
			if (openFileDialog.ShowDialog() == true)
			{
				OpenDocument(openFileDialog.FileName);
			}
		}

		private void DocumentsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var lb = sender as ListBox;

			if (lb.SelectedIndex != -1)
			{
				var msg = (Message)lb.SelectedItem;

				if (!String.IsNullOrEmpty(msg.FileName))
				{
					EditorAdapter.SetActiveDocumentAndOffset(msg.FileName, msg.Location);
				}
			}
		}

		#endregion
	}
}
