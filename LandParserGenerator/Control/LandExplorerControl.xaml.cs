using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;

using Microsoft.Win32;

using Land.Core;
using Land.Core.Parsing.Tree;
using Land.Markup;
using Land.Markup.Binding;
using Land.Control.Helpers;

namespace Land.Control
{
	/// <summary>
	/// Логика взаимодействия для MarkupControl.xaml
	/// </summary>
	public partial class LandExplorerControl : UserControl
    {
		public enum LandExplorerCommand { AddPoint, Relink }

		public class PendingCommandInfo
		{
			public TreeViewItem Target { get; set; }
			public LandExplorerCommand? Command { get; set; }
			public ParsedFile Document { get; set; }
		}

		public class ControlState
		{
			public TreeViewItem SelectedItem_MarkupTreeView { get; set; }
			public TreeViewItem SelectedItem_MissingTreeView { get; set; }

			public MarkupElement BufferedDataContext { get; set; }

			public Dictionary<ConcernPoint, List<RemapCandidateInfo>> RecentAmbiguities { get; set; } =
				new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();

			public PendingCommandInfo PendingCommand { get; set; }		

			public bool HighlightConcerns { get; set; }
		}

		private static readonly int CACHE_DIRECTORY_DAYS = 30;
		
		private static readonly string APP_DATA_DIRECTORY = 
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\LanD Control";
		private static readonly string CACHE_DIRECTORY =
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\LanD Control\Cache";
		public static readonly string SETTINGS_FILE_NAME = "LandExplorerSettings.xml";

		public static string SETTINGS_DEFAULT_PATH => 
			Path.Combine(APP_DATA_DIRECTORY, SETTINGS_FILE_NAME);

		public static readonly Color HighlightingColor = Color.FromArgb(60, 100, 200, 100);

		/// <summary>
		/// Настройки панели
		/// </summary>
		private LandExplorerSettings SettingsObject { get; set; }

		/// <summary>
		/// Окно настроек
		/// </summary>
		private Window_LandExplorerSettings SettingsWindow { get; set; }

		/// <summary>
		/// Адаптер редактора, с которым взаимодействует панель
		/// </summary>
		private IEditorAdapter Editor { get; set; }

		/// <summary>
		/// Менеджер разметки
		/// </summary>
		private MarkupManager MarkupManager { get; set; }

		/// <summary>
		/// Состояние контрола
		/// </summary>
		private ControlState State { get; set; } = new ControlState();

		/// <summary>
		/// Деревья для файлов, к которым осуществлена привязка
		/// </summary>
		public Dictionary<string, ParsedFile> ParsedFiles { get; set; } = new Dictionary<string, ParsedFile>();

		/// <summary>
		/// Лог панели разметки
		/// </summary>
		public List<Message> Log { get; set; } = new List<Message>();

		private Dispatcher FrontendUpdateDispatcher { get; set; }

		static LandExplorerControl()
		{
			if (!Directory.Exists(APP_DATA_DIRECTORY))
				Directory.CreateDirectory(APP_DATA_DIRECTORY);

			if (!Directory.Exists(CACHE_DIRECTORY))
				Directory.CreateDirectory(CACHE_DIRECTORY);
			else
			{
				if ((DateTime.UtcNow - Directory.GetCreationTimeUtc(CACHE_DIRECTORY)).Days > CACHE_DIRECTORY_DAYS)
				{
					try
					{
						Directory.Delete(CACHE_DIRECTORY, true);
						Directory.CreateDirectory(CACHE_DIRECTORY);
					}
					catch { }
				}
			}
		}

		public LandExplorerControl()
        {
			InitializeComponent();

			MarkupManager = new MarkupManager(GetParsed, new ContextsEqualityHeuristic());
			FrontendUpdateDispatcher = Dispatcher.CurrentDispatcher;
			MarkupManager.OnMarkupChanged += RefreshMissingPointsList;
        }

		#region Public

		public void Initialize(IEditorAdapter adapter)
		{
			Editor = adapter;
			Editor.RegisterOnDocumentChanged(DocumentChangedHandler);
			Editor.ShouldLoadSettings += OnShouldLoadSettings;

			/// Загружаем настройки панели разметки
			LoadSettings();

			/// В TreeView будем показывать текущее дерево разметки
			MarkupTreeView.ItemsSource = MarkupManager.Markup;
		}

		public ObservableCollection<MarkupElement> GetMarkup()
		{
			return MarkupManager.Markup;
		}

		public string GetText(string fileName)
		{
			return Editor.GetDocumentText(fileName) ?? 
				(File.Exists(fileName) ? File.ReadAllText(fileName) : null);
		}

		public List<RemapCandidateInfo> GetMappingCandidates(ConcernPoint point, string fileText, Node root)
		{
			return root != null
				? MarkupManager.Find(point, 
					new ParsedFile()
					{
						Text = fileText,
						Root = root,
						BindingContext = PointContext.GetFileContext(point.Context.FileContext.Name, fileText)
					})
				: new List<RemapCandidateInfo>();
		}

		#endregion

		#region Status

		private Brush LightRed { get; set; } = new SolidColorBrush(Color.FromRgb(255, 200, 200));

		private enum ControlStatus { Ready, Pending, Success, Error }

		private void SetStatus(string text, ControlStatus status)
		{
			switch(status)
			{
				case ControlStatus.Error:
					ControlStatusBar.Background = LightRed;
					break;
				case ControlStatus.Pending:
					ControlStatusBar.Background = Brushes.LightGoldenrodYellow;
					break;
				case ControlStatus.Ready:
					ControlStatusBar.Background = Brushes.LightBlue;
					break;
				case ControlStatus.Success:
					ControlStatusBar.Background = Brushes.LightGreen;
					break;
			}

			ControlStatusLabel.Content = $"{DateTime.Now.ToString("HH:mm:ss")} | {text}";
		}

		#endregion

		#region Controls search

		public TreeViewItem GetTreeViewItemParent(TreeViewItem item)
		{
			DependencyObject parent = VisualTreeHelper.GetParent(item);
			while (!(parent is TreeViewItem || parent is TreeView))
			{
				parent = VisualTreeHelper.GetParent(parent);
			}

			return parent as TreeViewItem;
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

		private static T VisualUpwardSearch<T>(DependencyObject source) where T: class
		{
			while (source != null && !(source is T))
				source = VisualTreeHelper.GetParent(source);

			return source as T;
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

		#endregion

		#region Settings

		private void OnShouldLoadSettings()
		{
			FrontendUpdateDispatcher.Invoke(LoadSettings);
		}

		private void LoadSettings()
		{
			/// Загружаем настройки панели способом, определённым в адаптере
			SettingsObject = Editor.LoadSettings(SETTINGS_DEFAULT_PATH)
				?? new LandExplorerSettings() { Id = Guid.NewGuid() };

			/// Перегенерируем парсеры для зарегистрированных в настройках типов файлов
			LogAction(() => ReloadParsers(), true, true);

			SetStatus("Настройки панели перезагружены", ControlStatus.Success);
		}

		#endregion

		#region Other helpers

		private List<ParsedFile> GetPointSearchArea() =>
			(GetFileSet(Editor.GetWorkingSet()) ?? MarkupManager.GetReferencedFiles())
				.Select(f => TryParse(f, null, out bool success, true))
				.Where(r => r != null)
				.ToList();

		private HashSet<string> GetFileSet(HashSet<string> paths)
		{
			if (paths == null)
				return null;

			var res = new HashSet<string>();

			foreach (var path in paths)
			{
				if (File.Exists(path))
					res.Add(path);
				else if (Directory.Exists(path))
					res.UnionWith(Directory.GetFiles(path, "*.*", SearchOption.AllDirectories));
			}

			return res;
		}

		private void DocumentChangedHandler(string fileName)
		{
			if (ParsedFiles.ContainsKey(fileName))
			{
				ParsedFiles.Remove(fileName);
				MarkupManager.InvalidatePoints(fileName);
			}
		}

		private T LogFunction<T>(Func<T> func, bool resetPrevious, bool skipTrace)
		{
			if (resetPrevious)
				Log.Clear();

			var result = func();
			Editor.ProcessMessages(Log, skipTrace, resetPrevious);

			return result;
		}

		private void LogAction(Action action, bool resetPrevious, bool skipTrace)
		{
			if (resetPrevious)
				Log.Clear();

			action();

			Editor.ProcessMessages(Log, skipTrace, resetPrevious);
		}

		private List<DocumentSegment> GetSegments(MarkupElement elem, bool captureWholeLine)
		{
			var segments = new List<DocumentSegment>();

			if (elem is Concern concern)
			{
				var concernsQueue = new Queue<Concern>();
				concernsQueue.Enqueue(concern);

				/// Для выделения функциональности целиком придётся обходить её и подфункциональности
				while (concernsQueue.Count > 0)
				{
					var currentConcern = concernsQueue.Dequeue();

					foreach (var element in currentConcern.Elements)
					{
						if (element is ConcernPoint cp)
						{
							if (EnsureLocationValid(cp))
							{
								segments.Add(new DocumentSegment()
								{
									FileName = cp.Context.FileContext.Name,
									StartOffset = cp.Location.Start.Offset,
									EndOffset = cp.Location.End.Offset,
									CaptureWholeLine = captureWholeLine
								});
							}
						}
						else
							concernsQueue.Enqueue((Concern)element);
					}
				}
			}
			else
			{
				var concernPoint = (ConcernPoint)elem;

				if (EnsureLocationValid(concernPoint))
				{
					segments.Add(new DocumentSegment()
					{
						FileName = concernPoint.Context.FileContext.Name,
						StartOffset = concernPoint.Location.Start.Offset,
						EndOffset = concernPoint.Location.End.Offset,
						CaptureWholeLine = captureWholeLine
					});
				}
			}

			return segments;
		}

		private bool EnsureLocationValid(ConcernPoint cp)
		{
			if (cp.HasInvalidLocation)
			{
				ProcessAmbiguities(
					MarkupManager.Remap(
						cp.Context.Type, 
						cp.Context.FileContext.Name, 
						GetPointSearchArea()
					),
					false
				);
			}

			return !cp.HasInvalidLocation;
		}

		#endregion
	}
}
