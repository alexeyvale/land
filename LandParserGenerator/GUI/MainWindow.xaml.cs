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

			Grammar_Editor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(
				new System.Xml.XmlTextReader(new StreamReader($"../../land.xshd", Encoding.Default)), 
				ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance);

			Grammar_Editor.TextArea.TextView.BackgroundRenderers.Add(new CurrentLineHighlighter(Grammar_Editor.TextArea));
			File_Editor.TextArea.TextView.BackgroundRenderers.Add(new CurrentLineHighlighter(File_Editor.TextArea));
			File_Editor.TextArea.TextView.BackgroundRenderers.Add(CurrentConcernColorizer = new SegmentsHighlighter(File_Editor.TextArea));
			SelectedTextColorizerForGrammar = new SelectedTextColorizer(Grammar_Editor.TextArea);

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

			MarkupTreeView.ItemsSource = Markup.Markup;

			LandExplorer.Initialize(new EditorAdapter(this), new Dictionary<string, string>()
			{
				{ ".cs", "../../../../Land Specifications/sharp/sharp_latest_features.land" },
				{ ".y", "../../../../Land Specifications/yacc/yacc.land" }
			});

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

			if (ParsingLL.IsChecked == true)
			{
				Parser = BuilderLL.BuildParser(Grammar_Editor.Text, messages);
			}
			else if (ParsingLR.IsChecked == true)
			{
				Parser = BuilderLR.BuildParser(Grammar_Editor.Text, messages);
			}

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
			Grammar_Editor.Text = File.ReadAllText(filename);
			Grammar_SaveButton.IsEnabled = false;
			SetAsCurrentGrammar(filename);
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
			SelectedTextColorizerForGrammar.Reset();
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

		private void File_ParseButton_Click(object sender, RoutedEventArgs e)
		{
            if (Parser != null)
            {
				Node root = null;

				try
				{
					root = Parser.Parse(File_Editor.Text);
				}
				catch(Exception ex)
				{
					Parser.Log.Add(Message.Error(ex.ToString(), null));
				}

				File_Statistics.Text = Parser.Statistics?.ToString();

				var noErrors = Parser.Log.All(l => l.Type != MessageType.Error);
				File_StatusBarLabel.Content = noErrors ? "Разбор произведён успешно" : "Ошибки при разборе файла";
                File_StatusBar.Background = noErrors ? Brushes.LightGreen : LightRed;

                if (root != null)
                {
                    TreeRoot = root;
					TreeSource = File_Editor.Text;
                    AstTreeView.ItemsSource = new List<Node>() { root };
                }

                File_LogList.ItemsSource = Parser.Log;
				File_ErrorsList.ItemsSource = Parser.Log.Where(l=>l.Type != MessageType.Trace).ToList();
            }
		}

		private void ParseTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			var treeView = (TreeView)sender;

			MoveCaretToSource((Node)treeView.SelectedItem, File_Editor, true, 1);
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
			var stream = new StreamReader(filename, Encoding.Default, true);

			File_NameLabel.Content = filename;
			File_Editor.Text = stream.ReadToEnd();
			File_Editor.Encoding = stream.CurrentEncoding;

			stream.Close();

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

		void worker_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
		{
			OnPackageFileParsingError = AddRecordToPackageParsingLog;
			OnPackageFileParsed = UpdatePackageParsingStatus;

			var files = (List<string>) e.Argument;
			var errorCounter = 0;
			var counter = 0;
			var errorFiles = new List<string>();

			FrontendUpdateDispatcher.Invoke((Action)(()=>{ Batch_Log.Items.Clear(); }));		
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
				Batch_StatusBarLabel.Content = $"Разобрано: {parsed}; С ошибками: {errorsCount} {Environment.NewLine}";
				Batch_StatusBar.Background = errorsCount == 0 ? Brushes.LightGreen : LightRed;
			}
			else
			{
				Batch_StatusBarLabel.Content = $"Всего: {total}; Разобрано: {parsed}; С ошибками: {errorsCount} {Environment.NewLine}";
			}
		}

		private delegate void AddRecordToPackageParsingLogDelegate(object record);

		private AddRecordToPackageParsingLogDelegate OnPackageFileParsingError { get; set; }

		private void AddRecordToPackageParsingLog(object record)
		{
			Batch_Log.Items.Add(record);
		}

		void worker_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
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
					MoveCaretToSource(concernPoint.TreeNode, File_Editor, HighlightNone.IsChecked == false, 1);

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
				var documentName = (string)File_NameLabel.Content;

				if (!Markup.AstRoots.ContainsKey(documentName))
				{
					Markup.AstRoots[documentName] = TreeRoot;
				}

				var concern = MarkupTreeView.SelectedItem as Concern;
				
				Markup.Add(new ConcernPoint(documentName, (Node)ConcernPointCandidatesList.SelectedItem, concern));
			}
		}

		private void ConcernPointCandidatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var node = (Node)ConcernPointCandidatesList.SelectedItem;
			MoveCaretToSource(node, File_Editor, true, 1);
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
				var offset = File_Editor.TextArea.Caret.Offset;

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
				var documentName = (string)File_NameLabel.Content;

				if (!Markup.AstRoots.ContainsKey(documentName))
				{
					Markup.AstRoots[documentName] = TreeRoot;
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
							Markup.Add(new ConcernPoint(documentName, point, subconcern));
					}

					/// Остальные добавляются напрямую к функциональности, соответствующей символу
					var points = subgroups.Where(s => String.IsNullOrEmpty(s.Key))
						.SelectMany(s => s).ToList();

					foreach (var point in points)
						Markup.Add(new ConcernPoint(documentName, point, concern));
				}
			}
		}

		private void ApplyMapping_Click(object sender, RoutedEventArgs e)
		{
			var documentName = (string)File_NameLabel.Content;

			if (Markup.AstRoots.ContainsKey(documentName) && TreeRoot != null)
			{
				Mapper.Remap(Markup.AstRoots[documentName], TreeRoot);
				Markup.Remap(documentName, TreeRoot, Mapper.Mapping);
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
				NewTextEditor.Text = File_Editor.Text;
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
						Mapper.Remap(Markup.AstRoots.Single().Value, NewTreeRoot);
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

		#region Тестирование панели разметки

		public class DocumentTab
		{
			public TextEditor Editor { get; set; }

			public string DocumentName { get; set; }
		}

		public Dictionary<TabItem, DocumentTab> Documents { get; set; } = new Dictionary<TabItem, DocumentTab>();

		private void NewDocumentButton_Click(object sender, RoutedEventArgs e)
		{
			var tab = new TabItem();

			Documents[tab] = new DocumentTab()
			{
				DocumentName = "Тест",
				Editor = new TextEditor()
				{
					ShowLineNumbers = true,
					FontSize = 16,
					FontFamily = new FontFamily("Consolas")
				}
			};

			DocumentTabs.Items.Add(tab);
			tab.Content = Documents[tab].Editor;
			tab.Header = Documents[tab].DocumentName;

			DocumentTabs.SelectedItem = tab;
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
			}
		}

		private void OpenDocumentButton_Click(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog();
			if (openFileDialog.ShowDialog() == true)
			{
				var stream = new StreamReader(openFileDialog.FileName, Encoding.Default, true);

				var tab = new TabItem();

				Documents[tab] = new DocumentTab()
				{
					DocumentName = openFileDialog.FileName,
					Editor = new TextEditor()
					{
						ShowLineNumbers = true,
						FontSize = 16,
						FontFamily = new FontFamily("Consolas"),
						Text = stream.ReadToEnd(),
						Encoding = stream.CurrentEncoding
					}
				};

				stream.Close();

				DocumentTabs.Items.Add(tab);
				tab.Content = Documents[tab].Editor;
				tab.Header = Path.GetFileName(Documents[tab].DocumentName);

				DocumentTabs.SelectedItem = tab;
			}
		}

		private void DocumentsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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

		#endregion
	}
}
