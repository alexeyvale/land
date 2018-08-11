﻿using System;
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

using Land.Core;
using Land.Core.Parsing.Tree;
using Land.Core.Markup;

namespace Land.GUI
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private string LAST_GRAMMARS_FILE = "./last_grammars.land.ide";

		private Brush LightRed = new SolidColorBrush(Color.FromRgb(255, 200, 200));

		private SelectedTextColorizer SelectedTextColorizerForGrammar { get; set; }
		private SegmentsHighlighter CurrentConcernColorizer { get; set; }

		private Land.Core.Parsing.BaseParser Parser { get; set; }

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
			FileEditor.TextArea.TextView.BackgroundRenderers.Add(CurrentConcernColorizer = new SegmentsHighlighter(FileEditor.TextArea));
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

			MarkupTreeView.ItemsSource = Markup.Markup;

			InitPackageParsing();
		}

		private void consoleWriter_WriteLineEvent(object sender, ConsoleWriterEventArgs e)
		{
			ParserBuidingLog.Text += e.Value + Environment.NewLine;
		}

		private void consoleWriter_WriteEvent(object sender, ConsoleWriterEventArgs e)
		{
			ParserBuidingLog.Text += e.Value + Environment.NewLine;
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

		private void MoveCaretToSource(Node node, ICSharpCode.AvalonEdit.TextEditor editor, bool selectText = true, int? tabToSelect = null)
		{
			if (node != null && node.StartOffset.HasValue && node.EndOffset.HasValue)
			{
				var start = node.StartOffset.Value;
				var end = node.EndOffset.Value;
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
			var messages = new List<Message>();

			if (ParsingLL.IsChecked == true)
			{
				Parser = BuilderLL.BuildParser(GrammarEditor.Text, messages);
			}
			else if (ParsingLR.IsChecked == true)
			{
				Parser = BuilderLR.BuildParser(GrammarEditor.Text, messages);
			}

			ParserBuidingErrors.ItemsSource = messages;

			if (Parser == null || messages.Count(m=>m.Type == MessageType.Error) > 0)
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
				var msg = lb.SelectedItem as Land.Core.Message;
				if (msg != null && msg.Location != null)
				{
					/// Если координаты не выходят за пределы файла, устанавливаем курсор в соответствии с ними, 
					/// иначе ставим курсор в позицию после последнего элемента последней строки
					int start = 0;
					if(msg.Location.Line <= GrammarEditor.Document.LineCount)
						start = GrammarEditor.Document.GetOffset(msg.Location.Line, msg.Location.Column);
					else
						start = GrammarEditor.Document.GetOffset(GrammarEditor.Document.LineCount, GrammarEditor.Document.Lines[GrammarEditor.Document.LineCount-1].Length + 1);

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

		private Node TreeRoot { get; set; }
		private string TreeSource { get; set; }

		private void ParseButton_Click(object sender, RoutedEventArgs e)
		{
            if (Parser != null)
            {
				Node root = null;

				try
				{
					root = Parser.Parse(FileEditor.Text);
				}
				catch(Exception ex)
				{
					Parser.Log.Add(Message.Error(ex.ToString(), null));
				}

				FileStatistics.Text = Parser.Statistics?.ToString();

				var noErrors = Parser.Log.All(l => l.Type != MessageType.Error);
				ProgramStatusLabel.Content = noErrors ? "Разбор произведён успешно" : "Ошибки при разборе файла";
                ProgramStatus.Background = noErrors ? Brushes.LightGreen : LightRed;

                if (root != null)
                {
                    TreeRoot = root;
					TreeSource = FileEditor.Text;
                    AstTreeView.ItemsSource = new List<Node>() { root };
                }

                FileParsingLog.ItemsSource = Parser.Log;
				FileParsingErrors.ItemsSource = Parser.Log.Where(l=>l.Type != MessageType.Trace).ToList();
            }
		}

		private void ParseTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			var treeView = (TreeView)sender;

			MoveCaretToSource((Node)treeView.SelectedItem, FileEditor, true, 1);
		}

		private void OpenFileButton_Click(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog();
			if (openFileDialog.ShowDialog() == true)
			{
				OpenFile(openFileDialog.FileName);
			}
		}

		private void SaveFileButton_Click(object sender, RoutedEventArgs e)
		{
			/// По нажатию на "Сохранить" всегда предлагаем выбрать, куда,
			/// чтобы не перезатереть файлы разбираемых проектов 
			var saveFileDialog = new SaveFileDialog();
			if (saveFileDialog.ShowDialog() == true)
			{
				File.WriteAllText(saveFileDialog.FileName, FileEditor.Text);
				TestFileName.Content = saveFileDialog.FileName;
			}
		}


		private void OpenFile(string filename)
		{
			var stream = new StreamReader(filename, Encoding.Default, true);

			TestFileName.Content = filename;
			FileEditor.Text = stream.ReadToEnd();
			FileEditor.Encoding = stream.CurrentEncoding;

			stream.Close();

			ParseButton_Click(null, null);
		}

		private void ClearFileButton_Click(object sender, RoutedEventArgs e)
		{
			TestFileName.Content = null;

			FileEditor.Text = String.Empty;
			FileEditor.Encoding = Encoding.UTF8;
		}

		private void TestFileListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var lb = sender as ListBox;

			if (lb.SelectedIndex != -1)
			{
				var msg = (Land.Core.Message)lb.SelectedItem;
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
			var timePerFile = new Dictionary<string, TimeSpan>();

			for (; counter < files.Count; ++counter)
			{
				try
				{
					FrontendUpdateDispatcher.Invoke((Action)(() => { Parser.Parse(File.ReadAllText(files[counter])); }));

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
				FrontendUpdateDispatcher.Invoke(OnPackageFileParsed, files.Count, counter + 1, errorCounter);

				if(PackageParsingWorker.CancellationPending)
				{
					e.Cancel = true;
					return;
				}
			}

			/// Выводим предупреждения о наиболее долго разбираемых файлах
			foreach(var file in timePerFile.OrderByDescending(f=>f.Value).Take(10))
			{
				FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, $"{Message.Warning(file.Key, null)}");
				FrontendUpdateDispatcher.Invoke(OnPackageFileParsingError, $"\t{Message.Warning(file.Value.ToString(@"hh\:mm\:ss\:ff"), null)}");
			}

			//visitor.Finish();

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
				if (File.Exists(lb.SelectedItem.ToString()))
				{
					OpenFile(lb.SelectedItem.ToString());
					MainTabs.SelectedIndex = 1;
				}
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

		#region Работа с точками привязки

		public class MarkupPanelState
		{
			public HashSet<MarkupElement> ExpandedItems { get; set; } = new HashSet<MarkupElement>();
			public TreeViewItem EditedItem { get; set; }
			public string EditedItemOldHeader { get; set; }
			public TreeViewItem SelectedItem { get; set; }
		}

		private MarkupManager Markup { get; set; } = new MarkupManager();
		private LandMapper Mapper { get; set; } = new LandMapper();
		private MarkupPanelState MarkupState { get; set; } = new MarkupPanelState();	

		private void MarkupTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			/// Если не включено постоянное выделение чего-либо,
			/// сбрасываем текущее выделение
			if (HighlightNone.IsChecked == true)
				CurrentConcernColorizer.ResetSegments();

			if (e.NewValue != null)
			{
				/// При клике по точке переходим к ней и подсвечиваем участок
				if (e.NewValue is ConcernPoint)
				{
					var concernPoint = (ConcernPoint)e.NewValue;
					MoveCaretToSource(concernPoint.TreeNode, FileEditor, HighlightNone.IsChecked == false, 1);

					if (HighlightNone.IsChecked == true)
					{
						CurrentConcernColorizer.SetSegments(new List<SegmentToHighlight>(){
							new SegmentToHighlight()
							{
								StartOffset = concernPoint.TreeNode.StartOffset.Value,
								EndOffset = concernPoint.TreeNode.EndOffset.Value,
								HighlightWholeLine = false
							}
						});
					}

					return;
				}

				/// Если отключен режим постоянной подсветки функциональностей,
				/// подсвечиваем то, что выбрано в данный конкретный момент
				if (e.NewValue is Concern)
					if (HighlightNone.IsChecked == true)
					{
						CurrentConcernColorizer.SetSegments(GetConcernSegments((Concern)e.NewValue).Select(s=> new SegmentToHighlight()
							{
								StartOffset = s.Item1,
								EndOffset = s.Item2,
								HighlightWholeLine = true
							}
						).ToList());
					}
			}
		}

		private List<Tuple<int, int>> GetConcernSegments(Concern concern)
		{
			var concernsQueue = new Queue<Concern>();
			concernsQueue.Enqueue(concern);

			var segments = new List<Tuple<int, int>>();

			/// Для выделения функциональности целиком придётся обходить её и подфункциональности
			while (concernsQueue.Count > 0)
			{
				var currentConcern = concernsQueue.Dequeue();

				foreach (var element in currentConcern.Elements)
				{
					if (element is ConcernPoint)
					{
						var cp = (ConcernPoint)element;
						segments.Add(new Tuple<int, int>(cp.TreeNode.StartOffset.Value, cp.TreeNode.EndOffset.Value));
					}
					else
						concernsQueue.Enqueue((Concern)element);
				}
			}

			return segments;
		}

		private void MarkupTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			TreeViewItem item = VisualUpwardSearch(e.OriginalSource as DependencyObject);
			if (item != null)
			{
				item.IsSelected = true;
				e.Handled = true;
			}
		}

		private static TreeViewItem VisualUpwardSearch(DependencyObject source)
		{
			while (source != null && !(source is TreeViewItem))
				source = VisualTreeHelper.GetParent(source);

			return source as TreeViewItem;
		}

		private void ConcernPointCandidatesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (ConcernPointCandidatesList.SelectedItem != null)
			{
				if (Markup.AstRoot == null)
				{
					Markup.AstRoot = TreeRoot;
				}

				var concern = MarkupTreeView.SelectedItem as Concern;
				
				Markup.Add(new ConcernPoint((Node)ConcernPointCandidatesList.SelectedItem, concern));
			}
		}

		private void ConcernPointCandidatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var node = (Node)ConcernPointCandidatesList.SelectedItem;
			MoveCaretToSource(node, FileEditor, true, 1);
		}

		private void DeleteConcernPoint_Click(object sender, RoutedEventArgs e)
		{
			if (MarkupTreeView.SelectedItem != null)
			{
				Markup.Remove((MarkupElement)MarkupTreeView.SelectedItem);
			}
		}

		private void AddConcernPoint_Click(object sender, RoutedEventArgs e)
		{
			/// Если открыта вкладка с тестовым файлом
			if(MainTabs.SelectedIndex == 1)
			{
				var offset = FileEditor.TextArea.Caret.Offset;

				var pointCandidates = new LinkedList<Node>();
				var currentNode = TreeRoot;

				/// В качестве кандидатов на роль помечаемого участка рассматриваем узлы от корня,
				/// содержащие текущую позицию каретки
				while (currentNode!=null)
				{
					if(currentNode.Options.IsLand)
						pointCandidates.AddFirst(currentNode);

					currentNode = currentNode.Children.Where(c => c.StartOffset.HasValue && c.EndOffset.HasValue
						&& c.StartOffset <= offset && c.EndOffset >= offset).FirstOrDefault();
				}

				ConcernPointCandidatesList.ItemsSource = pointCandidates;
			}
		}

		private void AddAllLandButton_Click(object sender, RoutedEventArgs e)
		{
			if (TreeRoot != null)
			{
				if (Markup.AstRoot == null)
				{
					Markup.AstRoot = TreeRoot;
				}

				var visitor = new LandExplorerVisitor();
				TreeRoot.Accept(visitor);

				/// Группируем land-сущности по типу (символу)
				foreach (var group in visitor.Land.GroupBy(l => l.Symbol))
				{
					var concern = new Concern(group.Key);
					Markup.Add(concern);

					/// В пределах символа группируем по псевдониму
					var subgroups = group.GroupBy(g => g.Alias);

					/// Для всех точек, для которых указан псевдоним
					foreach (var subgroup in subgroups.Where(s => !String.IsNullOrEmpty(s.Key)))
					{
						/// создаём подфункциональность
						var subconcern = new Concern(subgroup.Key, concern);
						Markup.Add(subconcern);

						foreach (var point in subgroup)
							Markup.Add(new ConcernPoint(point, subconcern));
					}

					/// Остальные добавляются напрямую к функциональности, соответствующей символу
					var points = subgroups.Where(s => String.IsNullOrEmpty(s.Key))
						.SelectMany(s => s).ToList();

					foreach (var point in points)
						Markup.Add(new ConcernPoint(point, concern));
				}
			}
		}

		private void ApplyMapping_Click(object sender, RoutedEventArgs e)
		{
			if (Markup.AstRoot != null && TreeRoot != null)
			{
				Mapper.Remap(Markup.AstRoot, TreeRoot);
				Markup.Remap(TreeRoot, Mapper.Mapping);
			}
		}

		private void AddConcernFolder_Click(object sender, RoutedEventArgs e)
		{
			Markup.Add(new Concern("Новая функциональность"));
		}

		private void SaveConcernMarkup_Click(object sender, RoutedEventArgs e)
		{
			var saveFileDialog = new SaveFileDialog()
			{
				AddExtension = true,
				DefaultExt = "landmark",
				Filter = "Файлы LANDMARK (*.landmark)|*.landmark|Все файлы (*.*)|*.*"
			};

			if (saveFileDialog.ShowDialog() == true)
			{
				MarkupManager.Serialize(saveFileDialog.FileName, Markup);
			}
		}

		private void LoadConcernMarkup_Click(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog()
			{
				AddExtension = true,
				DefaultExt = "landmark",
				Filter = "Файлы LANDMARK (*.landmark)|*.landmark|Все файлы (*.*)|*.*"
			};

			if (openFileDialog.ShowDialog() == true)
			{
				Markup = MarkupManager.Deserialize(openFileDialog.FileName);
				MarkupTreeView.ItemsSource = Markup.Markup;
			}
		}

		private void NewConcernMarkup_Click(object sender, RoutedEventArgs e)
		{
			Markup.Clear();
		}

		private void MarkupTreeRenameMenuItem_Click(object sender, RoutedEventArgs e)
		{
			TreeViewItem item = MarkupState.SelectedItem;

			var textbox = GetMarkupTreeItemTextBox(item);
			textbox.Visibility = Visibility.Visible;
			textbox.Focus();

			MarkupState.EditedItemOldHeader = textbox.Text;
			MarkupState.EditedItem = item;
		}

		private void MarkupTreeDeleteMenuItem_Click(object sender, RoutedEventArgs e)
		{

		}

		private void MarkupTreeDisableMenuItem_Click(object sender, RoutedEventArgs e)
		{

		}

		private void MarkupTreeViewItem_Expanded(object sender, RoutedEventArgs e)
		{
			var item = (TreeViewItem)sender;

			if (item.DataContext is Concern)
			{
				MarkupState.ExpandedItems.Add((MarkupElement)item.DataContext);

				var label = GetMarkupTreeItemLabel(item, "ConcernIcon");
				if (label != null)
					label.Content = "\xf07c";
			}

			e.Handled = true;
		}

		private void MarkupTreeViewItem_Collapsed(object sender, RoutedEventArgs e)
		{
			var item = (TreeViewItem)sender;

			if (item.DataContext is Concern)
			{
				MarkupState.ExpandedItems.Remove((MarkupElement)item.DataContext);

				var label = GetMarkupTreeItemLabel(item,"ConcernIcon");
				if (label != null)
					label.Content = "\xf07b";
			}

			e.Handled = true;
		}

		private void MarkupTreeViewItem_GotFocus(object sender, RoutedEventArgs e)
		{
			var item = (TreeViewItem)sender;

			var label = GetMarkupTreeItemLabel(item, "ConcernIcon");
			if (label != null)
				label.Foreground = Brushes.WhiteSmoke;
			label = GetMarkupTreeItemLabel(item, "PointIcon");
			if (label != null)
				label.Foreground = Brushes.WhiteSmoke;

			e.Handled = true;
		}

		private void MarkupTreeViewItem_LostFocus(object sender, RoutedEventArgs e)
		{
			var item = (TreeViewItem)sender;

			var label = GetMarkupTreeItemLabel(item, "ConcernIcon");
			if (label != null)
				label.Foreground = Brushes.DimGray;
			label = GetMarkupTreeItemLabel(item, "PointIcon");
			if (label != null)
				label.Foreground = Brushes.DimGray;

			e.Handled = true;
		}

		private void MarkupTreeViewItem_Selected(object sender, RoutedEventArgs e)
		{
			MarkupState.SelectedItem = (TreeViewItem)e.OriginalSource;

			if (MarkupState.EditedItem != null && MarkupState.EditedItem != MarkupState.SelectedItem)
			{
				var textbox = GetMarkupTreeItemTextBox(MarkupState.EditedItem);
				textbox.Visibility = Visibility.Hidden;
				MarkupState.EditedItem = null;
			}

			e.Handled = true;
		}

		private void MarkupTreeViewItem_Unselected(object sender, RoutedEventArgs e)
		{
			var item = (TreeViewItem)sender;

			var label = GetMarkupTreeItemLabel(item, "ConcernIcon");
			if (label != null)
				label.Foreground = Brushes.DimGray;

			e.Handled = true;
		}

		private void MarkupTreeViewItem_Loaded(object sender, RoutedEventArgs e)
		{
			var item = (TreeViewItem)sender;

			var label = GetMarkupTreeItemLabel(item, item.DataContext is Concern ? "PointIcon" : "ConcernIcon");
			if (label != null)
				label.Visibility = Visibility.Hidden;

			e.Handled = true;
		}

		private Label GetMarkupTreeItemLabel(TreeViewItem item, string labelName)
		{
			ContentPresenter templateParent = GetFrameworkElementByName<ContentPresenter>(item);
			HierarchicalDataTemplate dataTemplate = MarkupTreeView.ItemTemplate as HierarchicalDataTemplate;

			try
			{
				if (dataTemplate != null && templateParent != null)
				{
					return dataTemplate.FindName(labelName, templateParent) as Label;
				}
			}
			catch
			{
			}

			return null;
		}

		private TextBox GetMarkupTreeItemTextBox(TreeViewItem item)
		{
			ContentPresenter templateParent = GetFrameworkElementByName<ContentPresenter>(item);
			HierarchicalDataTemplate dataTemplate = MarkupTreeView.ItemTemplate as HierarchicalDataTemplate;

			if (dataTemplate != null && templateParent != null)
			{
				return dataTemplate.FindName("ConcernNameEditor", templateParent) as TextBox;
			}

			return null;
		}

		private static T GetFrameworkElementByName<T>(FrameworkElement referenceElement) where T : FrameworkElement
		{
			FrameworkElement child = null;

			//travel the visualtree by VisualTreeHelper to get the template
			for (Int32 i = 0; i < VisualTreeHelper.GetChildrenCount(referenceElement); i++)
			{
				child = VisualTreeHelper.GetChild(referenceElement, i) as FrameworkElement;

				if (child != null && child.GetType() == typeof(T)) { break; }
				else if (child != null)
				{
					child = GetFrameworkElementByName<T>(child);
					if (child != null && child.GetType() == typeof(T))
					{
						break;
					}
				}
			}

			return child as T;
		}

		private void HighlightingRadioButton_Checked(object sender, RoutedEventArgs e)
		{
			if (CurrentConcernColorizer != null)
			{
				/// Сбрасываем текущее выделение участков текста
				CurrentConcernColorizer.ResetSegments();

				/// Обеспечиваем стандартное отображение Concern-ов в панели
				foreach (var concern in Markup.Markup.OfType<Concern>().Where(c => c.Parent == null))
				{
					var markupTreeItem = MarkupTreeView.ItemContainerGenerator.ContainerFromItem(concern) as TreeViewItem;
					if (!markupTreeItem.IsSelected)
					{
						var label = GetMarkupTreeItemLabel(markupTreeItem, "ConcernIcon");
						if (label != null)
							label.Foreground = Brushes.DimGray;
					}
				}

				if (HighlightNone.IsChecked == true)
				{
					return;
				}

				if (HighlightWater.IsChecked == true)
				{
					if (TreeRoot != null)
					{
						var waterVisitor = new GetWaterSegmentsVisitor();
						waterVisitor.Visit(TreeRoot);
						CurrentConcernColorizer.SetSegments(waterVisitor.AnySegments.Select(s => new SegmentToHighlight()
							{
								StartOffset = s.Item1,
								EndOffset = s.Item2,
								HighlightWholeLine = false
							}).ToList(), Color.FromArgb(60, 150, 150, 200));
					}
					return;
				}

				if (HighlightConcerns.IsChecked == true)
				{
					var concernsAndColors = new Dictionary<Concern, Color>();

					foreach (var concern in Markup.Markup.OfType<Concern>().Where(c=>c.Parent == null))
					{
						concernsAndColors[concern] = 
							CurrentConcernColorizer.SetSegments(GetConcernSegments(concern).Select(s => new SegmentToHighlight()
							{
								StartOffset = s.Item1,
								EndOffset = s.Item2,
								HighlightWholeLine = true
							}).ToList());

						var label = GetMarkupTreeItemLabel(MarkupTreeView.ItemContainerGenerator.ContainerFromItem(concern) as TreeViewItem, "ConcernIcon");
						if (label != null)
							label.Foreground = new SolidColorBrush(concernsAndColors[concern]);
					}

					return;
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
						if (child.StartOffset.HasValue)
							AnySegments.Add(new Tuple<int, int>(child.StartOffset.Value, child.EndOffset.Value));
					}
					else
					{
						if (!child.Options.IsLand)
							TypedWaterSegments.Add(new Tuple<int, int>(child.StartOffset.Value, child.EndOffset.Value));
					}

					Visit(child);
				}
			}
		}

		#region drag and drop

		/// Точка, в которой была нажата левая кнопка мыши
		private Point LastMouseDown { get; set; }

		/// Перетаскиваемый и целевой элементы
		private MarkupElement DraggedItem { get; set; }

		private const int DRAG_START_TOLERANCE = 20;
		private const int SCROLL_START_TOLERANCE = 20;
		private const int SCROLL_BASE_OFFSET = 6;

		/// Элементы, развёрнутые автоматически в ходе перетаскивания
		private List<TreeViewItem> ExpandedWhileDrag = new List<TreeViewItem>();

		private void MarkupTreeView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
			{
				TreeViewItem treeItem = GetNearestContainer(e.OriginalSource as UIElement);

				DraggedItem = treeItem != null ? (MarkupElement)treeItem.DataContext : null;
				LastMouseDown = e.GetPosition(MarkupTreeView);
			}
		}

		private void MarkupTreeView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
			{
				DraggedItem = null;
			}
		}

		private void MarkupTreeView_MouseMove(object sender, MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed && DraggedItem != null)
			{
				Point currentPosition = e.GetPosition(MarkupTreeView);

				/// Если сдвинули элемент достаточно далеко
				if (Math.Abs(currentPosition.Y - LastMouseDown.Y) > DRAG_START_TOLERANCE)
				{
					/// Инициируем перемещение
					ExpandedWhileDrag.Clear();

					DragDrop.DoDragDrop(MarkupTreeView, DraggedItem, DragDropEffects.Move);
				}
			}
		}

		private void MarkupTreeView_DragOver(object sender, DragEventArgs e)
		{
			Point currentPosition = e.GetPosition(MarkupTreeView);

			/// Прокручиваем дерево, если слишком приблизились к краю
			ScrollViewer sv = FindVisualChild<ScrollViewer>(MarkupTreeView);

			double verticalPos = currentPosition.Y;

			if (verticalPos < SCROLL_START_TOLERANCE)
			{
				sv.ScrollToVerticalOffset(sv.VerticalOffset - SCROLL_BASE_OFFSET);
			}
			else if (verticalPos > MarkupTreeView.ActualHeight - SCROLL_START_TOLERANCE)
			{
				sv.ScrollToVerticalOffset(sv.VerticalOffset + SCROLL_BASE_OFFSET);   
			}

			/// Ищем элемент дерева, над которым происходит перетаскивание,
			/// чтобы выделить его и развернуть
			TreeViewItem treeItem = GetNearestContainer(e.OriginalSource as UIElement);

			if (treeItem != null)
			{
				treeItem.IsSelected = true;

				if (!treeItem.IsExpanded && treeItem.DataContext is Concern)
				{
					treeItem.IsExpanded = true;
					ExpandedWhileDrag.Add(treeItem);
				}

				var item = (MarkupElement)treeItem.DataContext;

				/// Запрещаем перенос элемента во вложенный элемент
				var canMove = true;
				while (item != null)
				{
					if (item == DraggedItem)
					{
						canMove = false;
						break;
					}
					item = item.Parent;
				}

				e.Effects = canMove ? DragDropEffects.Move : DragDropEffects.None;
			}

			e.Handled = true;
		}

		private void MarkupTreeView_Drop(object sender, DragEventArgs e)
		{
			e.Effects = DragDropEffects.None;
			e.Handled = true;

			/// Если закончили перетаскивание над элементом treeview, нужно осуществить перемещение
			TreeViewItem target = GetNearestContainer(e.OriginalSource as UIElement);

			if (DraggedItem != null)
			{
				var targetItem = target != null ? (MarkupElement)target.DataContext : null;

				DropItem(DraggedItem, targetItem);
				DraggedItem = null;
			}

			if (target != null)
			{
				var dataElement = (MarkupElement)target.DataContext;

				while (dataElement != null)
				{
					ExpandedWhileDrag.RemoveAll(elem => elem.DataContext == dataElement);
					dataElement = dataElement.Parent;
				}
			}

			foreach (var item in ExpandedWhileDrag)
				item.IsExpanded = false;
		}

		public void DropItem(MarkupElement source, MarkupElement target)
		{
			/// Если перетащили элемент на функциональность, добавляем его внутрь функциональности
			if (target is Concern)
			{
				Markup.Remove(source);
				source.Parent = (Concern)target;
				Markup.Add(source);	
			}
			else
			{
				if (target != null)
				{
					if (target.Parent != source.Parent)
					{
						Markup.Remove(source);
						source.Parent = target.Parent;
						Markup.Add(source);
					}
				}
				else
				{
					Markup.Remove(source);
					source.Parent = null;
					Markup.Add(source);
				}
			}
		}

		private TreeViewItem GetNearestContainer(UIElement element)
		{
			// Поднимаемся по визуальному дереву до TreeViewItem-а
			TreeViewItem container = element as TreeViewItem;
			while ((container == null) && (element != null))
			{
				element = VisualTreeHelper.GetParent(element) as UIElement;
				container = element as TreeViewItem;
			}
			return container;
		}

		/// <summary>
		/// Search for an element of a certain type in the visual tree.
		/// </summary>
		/// <typeparam name="T">The type of element to find.</typeparam>
		/// <param name="visual">The parent element.</param>
		/// <returns></returns>
		private T FindVisualChild<T>(Visual visual) where T : Visual
		{
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visual); i++)
			{
				Visual child = (Visual)VisualTreeHelper.GetChild(visual, i);
				if (child != null)
				{
					T correctlyTyped = child as T;
					if (correctlyTyped != null)
					{
						return correctlyTyped;
					}

					T descendent = FindVisualChild<T>(child);
					if (descendent != null)
					{
						return descendent;
					}
				}
			}

			return null;
		}

		#endregion

		#endregion

		#region Отладка перепривязки

		private Node NewTreeRoot { get; set; }
		private bool NewTextChanged { get; set; }

		private void ReplaceNewWithOldButton_Click(object sender, RoutedEventArgs e)
		{
			NewTextEditor.Text = OldTextEditor.Text;
		}

		private void AddAllLandDebugButton_Click(object sender, RoutedEventArgs e)
		{
			AddAllLandButton_Click(null, null);
			MarkupDebugTreeView.ItemsSource = MarkupTreeView.ItemsSource;
		}

		private void MainPerspectiveTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (sender == e.Source && MappingDebugTab.IsSelected)
			{
				NewTextEditor.Text = FileEditor.Text;
				OldTextEditor.Text = TreeSource;
				MarkupDebugTreeView.ItemsSource = MarkupTreeView.ItemsSource;
				AstDebugTreeView.ItemsSource = AstTreeView.ItemsSource;
			}
		}

		private void AstDebugTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if(AstDebugTreeView.SelectedItem != null)
			{
				var node = (Node)AstDebugTreeView.SelectedItem;
				ParseNewTextAndSelectMostSimilar(node);
			}
		}

		private void MarkupDebugTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if (MarkupDebugTreeView.SelectedItem is ConcernPoint)
			{
				var node = ((ConcernPoint)MarkupDebugTreeView.SelectedItem).TreeNode;
				ParseNewTextAndSelectMostSimilar(node);
			}
		}

		private void ParseNewTextAndSelectMostSimilar(Node node)
		{
			/// Если текст, к которому пытаемся перепривязаться, изменился
			if (NewTextChanged)
			{
				/// и при этом парсер сгенерирован
				if (Parser != null)
				{
					/// пытаемся распарсить текст
					NewTreeRoot = Parser.Parse(NewTextEditor.Text);
					var noErrors = Parser.Log.All(l => l.Type != MessageType.Error);

					NewFileParsingStatus.Background = noErrors ? Brushes.LightGreen : LightRed;

					/// Если текст распарсился, ищем отображение из старого текста в новый
					if (noErrors)
					{
						Mapper.Remap(Markup.AstRoot, NewTreeRoot);
						NewTextChanged = false;
					}
				}
			}

			/// Если для текущего нового текста построено дерево и просчитано отображение
			if (!NewTextChanged)
			{
				/// Заполняем список похожестей похожестями узлов нового дерева на выбранный узел старого дерева
				SimilaritiesList.ItemsSource = Mapper.Similarities.ContainsKey(node) ? Mapper.Similarities[node] : null;
				MoveCaretToSource(node, OldTextEditor);

				/// Если есть узлы в новом дереве, с которыми мы сравнивали выбранный узел старого дерева
				if (SimilaritiesList.ItemsSource != null && Mapper.Mapping.ContainsKey(node))
				{
					/// значит, в какой-то новый узел мы отобразили старый
					SimilaritiesList.SelectedItem = Mapper.Similarities[node].FirstOrDefault(p => p.Key == Mapper.Mapping[node]);
				}
			}
		}

		private void SimilaritiesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if(SimilaritiesList.SelectedItem != null)
			{
				var node = ((KeyValuePair<Node,double>)SimilaritiesList.SelectedItem).Key;
				MoveCaretToSource(node, NewTextEditor);
			}
		}

		private void NewTextEditor_TextChanged(object sender, EventArgs e)
		{
			NewTextChanged = true;
		}

		#endregion

	}
}