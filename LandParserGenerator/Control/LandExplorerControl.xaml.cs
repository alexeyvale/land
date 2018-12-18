using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using Microsoft.Win32;

using Land.Core;
using Land.Core.Parsing;
using Land.Core.Parsing.Tree;
using Land.Core.Parsing.Preprocessing;
using Land.Core.Markup;
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
			public string DocumentName { get; set; }
			public string DocumentText { get; set; }
		}

		public class ControlState
		{
			public HashSet<MarkupElement> ExpandedItems { get; set; } = new HashSet<MarkupElement>();
			public TreeViewItem SelectedItem { get; set; }
			public HashSet<TreeViewItem> InactiveItems { get; set; }

			public TreeViewItem EditedItem { get; set; }
			public string EditedItemOldHeader { get; set; }

			public PendingCommandInfo PendingCommand { get; set; }		

			public bool HighlightConcerns { get; set; }
		}

		private static readonly string APP_DATA_DIRECTORY = 
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\LanD Control";
		public static readonly string SETTINGS_FILE_NAME = "LandExplorerSettings.xml";

		public static string SETTINGS_DEFAULT_PATH => 
			Path.Combine(APP_DATA_DIRECTORY, SETTINGS_FILE_NAME);

		/// <summary>
		/// Настройки панели
		/// </summary>
		private LandExplorerSettings SettingsObject { get; set; }

		/// <summary>
		/// Окно настроек
		/// </summary>
		private LandExplorerSettingsWindow SettingsWindow { get; set; }

		/// <summary>
		/// Адаптер редактора, с которым взаимодействует панель
		/// </summary>
		private IEditorAdapter Editor { get; set; }

		/// <summary>
		/// Менеджер разметки
		/// </summary>
		private MarkupManager MarkupManager { get; set; } = new MarkupManager();

		/// <summary>
		/// Состояние контрола
		/// </summary>
		private ControlState State { get; set; } = new ControlState();

		/// <summary>
		/// Деревья для файлов, к которым осуществлена привязка
		/// </summary>
		public Dictionary<string, Tuple<Node, string>> ParsedFiles { get; set; } = new Dictionary<string, Tuple<Node, string>>();

		/// <summary>
		/// Словарь парсеров, ключ - расширение файла, к которому парсер можно применить
		/// </summary>
		public Dictionary<string, BaseParser> Parsers { get; set; } = new Dictionary<string, BaseParser>();

		/// <summary>
		/// Лог панели разметки
		/// </summary>
		public List<Message> Log { get; set; } = new List<Message>();

		static LandExplorerControl()
		{
			if (!Directory.Exists(APP_DATA_DIRECTORY))
				Directory.CreateDirectory(APP_DATA_DIRECTORY);
		}

		public LandExplorerControl()
        {
			InitializeComponent();
        }

		private void LandExplorer_Loaded(object sender, RoutedEventArgs e)
		{ }

		public void Initialize(IEditorAdapter adapter)
		{
			Editor = adapter;
			Editor.RegisterOnDocumentChanged(DocumentChangedHandler);
			Editor.ShouldLoadSettings += LoadSettings;

			/// Загружаем настройки панели разметки
			LoadSettings();

			/// В TreeView будем показывать текущее дерево разметки
			MarkupTreeView.ItemsSource = MarkupManager.Markup;
		}

		private void LoadSettings()
		{
			/// Загружаем настройки панели способом, определённым в адаптере
			SettingsObject = Editor.LoadSettings(SETTINGS_DEFAULT_PATH) 
				?? new LandExplorerSettings();

			/// Перегенерируем парсеры для зарегистрированных в настройках типов файлов
			Parsers = LogFunction(() => BuildParsers(), true, true);
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

		public BaseParser GetParser(string extension)
		{
			return Parsers.ContainsKey(extension)
				? Parsers[extension] : null;
		}

		public List<NodeSimilarityPair> GetMappingCandidates(ConcernPoint point, string fileText, Node root)
		{
			return root != null
				? MarkupManager.Find(point, 
					new TargetFileInfo() { FileName = point.Context.FileName, FileText = fileText, TargetNode = root })
				: new List<NodeSimilarityPair>();
		}

		#region Commands

		private void Command_Delete_Executed(object sender, RoutedEventArgs e)
		{
			MarkupManager.RemoveElement((MarkupElement)MarkupTreeView.SelectedItem);
		}

		private void Command_AddPoint_Executed(object sender, RoutedEventArgs e)
		{
			var fileName = Editor.GetActiveDocumentName();
			var rootTextPair = LogFunction(() => GetRoot(fileName), true, false);

			if (rootTextPair != null)
			{
				var offset = Editor.GetActiveDocumentOffset();

				if (!String.IsNullOrEmpty(fileName))
				{
					State.PendingCommand = new PendingCommandInfo()
					{
						Target = State.SelectedItem,
						DocumentName = fileName,
						Command = LandExplorerCommand.AddPoint,
						DocumentText = rootTextPair.Item2
					};

					ConcernPointCandidatesList.ItemsSource =
						MarkupManager.GetConcernPointCandidates(rootTextPair.Item1, offset.Value);
				}
			}
		}

		private void Command_AddLand_Executed(object sender, RoutedEventArgs e)
		{
			var fileName = Editor.GetActiveDocumentName();
			var rootTextPair = LogFunction(() => GetRoot(fileName), true, false);

			if (rootTextPair != null)
				MarkupManager.AddLand(new TargetFileInfo()
				{			
					FileName = fileName,
					FileText = rootTextPair.Item2,
					TargetNode = rootTextPair.Item1
				});
		}

		private void Command_AddConcern_Executed(object sender, RoutedEventArgs e)
		{
			var parent = MarkupTreeView.SelectedItem != null 
				&& MarkupTreeView.SelectedItem is Concern
					? (Concern)MarkupTreeView.SelectedItem : null;

			MarkupManager.AddConcern("Новая функциональность", parent);

			if (parent != null)
			{
				State.SelectedItem.IsExpanded = true;
			}
		}

		private void Command_Save_Executed(object sender, RoutedEventArgs e)
		{
			var saveFileDialog = new SaveFileDialog()
			{
				AddExtension = true,
				DefaultExt = "landmark",
				Filter = "Файлы LANDMARK (*.landmark)|*.landmark|Все файлы (*.*)|*.*"
			};

			if (saveFileDialog.ShowDialog() == true)
			{
				MarkupManager.Serialize(saveFileDialog.FileName, !SettingsObject.SaveAbsolutePath);
			}
		}

		private void Command_Open_Executed(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog()
			{
				AddExtension = true,
				DefaultExt = "landmark",
				Filter = "Файлы LANDMARK (*.landmark)|*.landmark|Все файлы (*.*)|*.*"
			};

			if (openFileDialog.ShowDialog() == true)
			{
				MarkupManager.Deserialize(openFileDialog.FileName);
				MarkupTreeView.ItemsSource = MarkupManager.Markup;
			}
		}

		private void Command_New_Executed(object sender, RoutedEventArgs e)
		{
			MarkupManager.Clear();
		}

		private void Command_Relink_Executed(object sender, RoutedEventArgs e)
		{
			var fileName = Editor.GetActiveDocumentName();
			var rootTextPair = LogFunction(() => GetRoot(fileName), true, false);

			if (rootTextPair != null)
			{
				var offset = Editor.GetActiveDocumentOffset();

				if (!String.IsNullOrEmpty(fileName))
				{
					State.PendingCommand = new PendingCommandInfo()
					{
						Target = State.SelectedItem,
						DocumentName = fileName,
						Command = LandExplorerCommand.Relink,
						DocumentText = rootTextPair.Item2
					};

					ConcernPointCandidatesList.ItemsSource =
						MarkupManager.GetConcernPointCandidates(rootTextPair.Item1, offset.Value);
				}
			}
		}

		private void Command_Rename_Executed(object sender, RoutedEventArgs e)
		{
			TreeViewItem item = e.OriginalSource as TreeViewItem;

			var textbox = GetMarkupTreeItemTextBox(item);
			textbox.Visibility = Visibility.Visible;
			textbox.Focus();

			State.EditedItemOldHeader = textbox.Text;
			State.EditedItem = item;
		}

		private void Command_Highlight_Executed(object sender, RoutedEventArgs e)
		{
			State.HighlightConcerns = !State.HighlightConcerns;

			Editor.ResetSegments();

			if (!State.HighlightConcerns)
			{
				/// Обеспечиваем стандартное отображение Concern-ов в панели
				foreach (var concern in MarkupManager.Markup.OfType<Concern>().Where(c => c.Parent == null))
				{
					var markupTreeItem = MarkupTreeView.ItemContainerGenerator.ContainerFromItem(concern) as TreeViewItem;
					if (!markupTreeItem.IsSelected)
					{
						var label = GetMarkupTreeItemLabel(markupTreeItem, "ConcernIcon");
						if (label != null)
							label.Foreground = Brushes.DimGray;
					}
				}
			}
			else
			{
				var concernsAndColors = new Dictionary<Concern, Color>();

				foreach (var concern in MarkupManager.Markup.OfType<Concern>().Where(c => c.Parent == null))
				{
					concernsAndColors[concern] = Editor.SetSegments(GetSegments(concern, true));

					var label = GetMarkupTreeItemLabel(
						MarkupTreeView.ItemContainerGenerator.ContainerFromItem(concern) as TreeViewItem, 
						"ConcernIcon"
					);
					if (label != null)
						label.Foreground = new SolidColorBrush(concernsAndColors[concern]);
				}
			}
		}

		private void Command_AlwaysEnabled_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}

		private void Command_HasSelectedItem_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = MarkupTreeView != null 
				&& MarkupTreeView.SelectedItem != null;
		}

		private void Command_HasSelectedConcernPoint_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = MarkupTreeView != null  
				&& MarkupTreeView.SelectedItem != null 
				&& MarkupTreeView.SelectedItem is ConcernPoint;
		}

		private void Command_ConcernPointIsNotSelected_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = e.CanExecute = MarkupTreeView != null
				&& (MarkupTreeView.SelectedItem == null
				|| MarkupTreeView.SelectedItem is Concern);
		}

		private void Settings_Click(object sender, RoutedEventArgs e)
		{
			SettingsWindow = new LandExplorerSettingsWindow(SettingsObject.Clone());
			SettingsWindow.Owner = Window.GetWindow(this);

			if (SettingsWindow.ShowDialog() ?? false)
			{
				SettingsObject = SettingsWindow.SettingsObject;
				Editor.SaveSettings(
					SettingsObject, SETTINGS_DEFAULT_PATH
				);

				Parsers = LogFunction(() => BuildParsers(), true, true);
			}
		}

		private void ApplyMapping_Click(object sender, RoutedEventArgs e)
		{
			LogAction(() =>
			{
				/// В случае, если запросили перепривязку в пределах
				/// рабочего множества файлов, а это множество не установлено,
				/// проводим перепривязку в пределах файлов, на которые
				/// ссылаются имеющиеся точки
				var forest = (sender == ApplyLocalMapping
					? MarkupManager.GetReferencedFiles()
					: GetFileSet(Editor.GetWorkingSet()) ?? MarkupManager.GetReferencedFiles()
				).Select(f =>
				{
					var parsed = TryParse(f, out bool success);

					return success
						? new TargetFileInfo()
						{
							FileName = f,
							FileText = parsed.Item2,
							TargetNode = parsed.Item1
						}
						: null;
				}).Where(r => r != null).ToList();

				MarkupManager.Remap(forest, sender == ApplyLocalMapping);
			}, true, false);
		}

		private void ConcernPointCandidatesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (ConcernPointCandidatesList.SelectedItem != null)
			{
				if (State.PendingCommand.Command == LandExplorerCommand.Relink)
				{
					MarkupManager.RelinkConcernPoint(
						(ConcernPoint)State.SelectedItem.DataContext,
						new TargetFileInfo()
						{
							FileName = State.PendingCommand.DocumentName,
							FileText = State.PendingCommand.DocumentText,
							TargetNode = (Node)ConcernPointCandidatesList.SelectedItem
						}
					);
				}
				else
				{
					MarkupManager.AddConcernPoint(
						new TargetFileInfo()
						{
							FileName = State.PendingCommand.DocumentName,
							FileText = State.PendingCommand.DocumentText,
							TargetNode = (Node)ConcernPointCandidatesList.SelectedItem
						},
						null,
						State.PendingCommand.Target != null 
							? (Concern)State.PendingCommand.Target.DataContext : null
					);

					if (State.PendingCommand.Target != null)
						State.PendingCommand.Target.IsExpanded = true;
				}

				ConcernPointCandidatesList.ItemsSource = null;
			}
		}

		private void ConcernPointCandidatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (ConcernPointCandidatesList.SelectedItem != null)
			{
				var node = (Node)ConcernPointCandidatesList.SelectedItem;

				Editor.SetActiveDocumentAndOffset(
					State.PendingCommand.DocumentName, 
					node.Anchor.Start
				);
			}
		}

		#endregion

		#region MarkupTreeView manipulations

		private int MAX_TEXT_SIZE = 30;
		private int MIN_TEXT_SIZE = 9;

		private void MarkupTreeView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			var snd = sender as TreeView;

			if (Keyboard.PrimaryDevice.Modifiers == ModifierKeys.Control)
			{
				e.Handled = true;
				var oldSize = snd.FontSize;
				if (e.Delta > 0)
				{
					if (snd.FontSize < MAX_TEXT_SIZE)
					{
						++snd.FontSize;
					}
				}
				else if (snd.FontSize > MIN_TEXT_SIZE)
				{
					--snd.FontSize;
				}
			}
		}

		/// Убираем горизонтальную прокрутку при выборе элемента
		private void MarkupTreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
		{
			e.Handled = true;
		}

		private void MarkupTreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			TreeViewItem item = VisualUpwardSearch(e.OriginalSource as DependencyObject);

			if (item != null && State.EditedItem != item && e.ChangedButton == MouseButton.Left)
			{
				/// При клике по точке переходим к ней
				if (item.DataContext is ConcernPoint concernPoint)
				{
					if(EnsureLocationValid(concernPoint))
					{
						Editor.SetActiveDocumentAndOffset(
							concernPoint.Context.FileName,
							concernPoint.Location.Start
						);
					}

					e.Handled = true;
				}
			}
		}

		private void MarkupTreeViewItem_KeyDown(object sender, KeyEventArgs e)
		{
			TreeViewItem item = VisualUpwardSearch(e.OriginalSource as DependencyObject);

			if (item != null)
			{
				switch (e.Key)
				{
					case Key.Enter:
						if (State.EditedItem == item)
						{
							var textbox = GetMarkupTreeItemTextBox(State.EditedItem);
							textbox.Visibility = Visibility.Hidden;
							State.EditedItem = null;
						}
						else
						{
							if (item.DataContext is ConcernPoint concernPoint)
							{
								Editor.SetActiveDocumentAndOffset(
									concernPoint.Context.FileName,
									concernPoint.Location.Start
								);
							}
						}
						break;
					case Key.Escape:
						if (State.EditedItem == item)
						{
							var textbox = GetMarkupTreeItemTextBox(State.EditedItem);
							textbox.Text = State.EditedItemOldHeader;
							textbox.Visibility = Visibility.Hidden;						

							State.EditedItem = null;				
						}
						break;
				}
			}
		}

		private void MarkupTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			TreeViewItem item = VisualUpwardSearch(e.OriginalSource as DependencyObject);

			if (item == null)
			{
				if (State.SelectedItem != null)
				{
					State.SelectedItem.IsSelected = false;

					MarkupTreeView.Focus();
				}
			}
		}

		private void MarkupTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			TreeViewItem item = VisualUpwardSearch(e.OriginalSource as DependencyObject);

			if (item != null)
			{
				item.IsSelected = true;
				e.Handled = true;
			}
			else
			{
				if (State.SelectedItem != null)
				{
					State.SelectedItem.IsSelected = false;
				}
			}
		}

		private void MarkupTreeViewItem_PreviewMouseLeftButtonDown_Highlight(object sender, MouseButtonEventArgs e)
		{
			var item = (TreeViewItem)sender;

			if (SettingsObject.HighlightSelectedElement && !State.HighlightConcerns)
			{
				Editor.ResetSegments();

				Editor.SetSegments(GetSegments(
					(MarkupElement)item.DataContext,
					item.DataContext is Concern
				));
			}
		}

		#region displaying element

		private void MarkupTreeViewItem_Expanded(object sender, RoutedEventArgs e)
		{
			var item = (TreeViewItem)sender;

			if (item.DataContext is Concern)
			{
				State.ExpandedItems.Add((MarkupElement)item.DataContext);

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
				State.ExpandedItems.Remove((MarkupElement)item.DataContext);

				var label = GetMarkupTreeItemLabel(item, "ConcernIcon");
				if (label != null)
					label.Content = "\xf07b";
			}

			e.Handled = true;
		}

		private void MarkupTreeViewItem_GotFocus(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
		}

		private void MarkupTreeViewItem_LostFocus(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
		}

		private void MarkupTreeViewItem_Selected(object sender, RoutedEventArgs e)
		{
			State.SelectedItem = (TreeViewItem)sender;

			if (State.EditedItem != null 
				&& State.EditedItem != (TreeViewItem)sender)
			{
				var textbox = GetMarkupTreeItemTextBox(State.EditedItem);
				textbox.Visibility = Visibility.Hidden;
				State.EditedItem = null;
			}

			e.Handled = true;
		}

		private void MarkupTreeViewItem_Unselected(object sender, RoutedEventArgs e)
		{
			State.SelectedItem = null;

			e.Handled = true;
		}

		#endregion

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

		private void MarkupTreeViewItem_PreviewMouseLeftButtonDown_DragDrop(object sender, MouseButtonEventArgs e)
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
				MarkupManager.MoveTo((Concern)target, source);
			}
			else
			{
				if (target != null)
				{
					if (target.Parent != source.Parent)
					{
						MarkupManager.MoveTo(target.Parent, source);
					}
				}
				else
				{
					MarkupManager.MoveTo(null, source);
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

		#region Helpers

		private HashSet<string> GetFileSet(HashSet<string> paths)
		{
			if (paths == null)
				return null;

			var res = new HashSet<string>();

			foreach(var path in paths)
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

		private Tuple<Node, string> GetRoot(string documentName)
		{
			return !String.IsNullOrEmpty(documentName)
				/// Если связанный с точкой файл разбирали и он не изменился с прошлого разбора,
				? ParsedFiles.ContainsKey(documentName) && ParsedFiles[documentName] != null
					/// возвращаем сохранённый ранее результат
					? ParsedFiles[documentName]
					/// иначе пытаемся переразобрать файл
					: ParsedFiles[documentName] = TryParse(documentName, out bool success)
				: null;
		}

		private Tuple<Node, string> TryParse(string fileName, out bool success, string text = null)
		{
			if (!String.IsNullOrEmpty(fileName))
			{
				var extension = Path.GetExtension(fileName);

				if (Parsers.ContainsKey(extension) && Parsers[extension] != null)
				{
					if (String.IsNullOrEmpty(text))
						text = GetText(fileName);

					var root = Parsers[extension].Parse(text);
					success = Parsers[extension].Log.All(l => l.Type != MessageType.Error);

					Parsers[extension].Log.ForEach(l => l.FileName = fileName);
					Log.AddRange(Parsers[extension].Log);

					return success ? new Tuple<Node, string>(root, text) : null;
				}
				else
				{
					Log.Add(Message.Error($"Отсутствует парсер для файлов с расширением '{extension}'", null));
				}
			}

			success = false;
			return null;
		}

		private T LogFunction<T>(Func<T> func, bool resetPrevious, bool skipTrace)
		{
			if(resetPrevious)
			{
				Log.Clear();
			}

			var result = func();
			Editor.ProcessMessages(Log, skipTrace, resetPrevious);
			return result;
		}

		private void LogAction(Action action, bool resetPrevious, bool skipTrace)
		{
			if (resetPrevious)
			{
				Log.Clear();
			}

			action();
			Editor.ProcessMessages(Log, skipTrace, resetPrevious);
		}

		private Dictionary<string, BaseParser> BuildParsers()
		{
			var parsers = new Dictionary<string, BaseParser>();

			/// Генерируем парсер и связываем его с каждым из расширений, 
			/// указанных для грамматики
			foreach (var item in SettingsObject.Parsers)
			{
				if(!File.Exists(item.GrammarPath))
				{
					Log.Add(Message.Error(
						$"Файл {item.GrammarPath} не существует, невозможно загрузить парсер для расширения {item.ExtensionsString}",
						null
					));

					continue;
				}

				var parser = BuilderBase.BuildParser(
					GrammarType.LL,
					File.ReadAllText(item.GrammarPath),
					Log
				);

                foreach (var key in item.Extensions)
					parsers[key] = parser;

				if (!String.IsNullOrEmpty(item.PreprocessorPath))
				{
					if (!File.Exists(item.PreprocessorPath))
					{
						Log.Add(Message.Error(
							$"Файл {item.PreprocessorPath} не существует, невозможно загрузить препроцессор для расширения {item.ExtensionsString}",
							null
						));
					}
					else
					{
						var preprocessor = (BasePreprocessor)Assembly.LoadFrom(item.PreprocessorPath)
							.GetTypes().FirstOrDefault(t => t.BaseType.Equals(typeof(BasePreprocessor)))
							?.GetConstructor(Type.EmptyTypes).Invoke(null);

						if (preprocessor != null)
						{
							if (item.PreprocessorProperties != null
								&& item.PreprocessorProperties.Count > 0)
							{
								/// Получаем тип препроцессора из библиотеки
								var propertiesObjectType = Assembly.LoadFrom(item.PreprocessorPath)
									.GetTypes().FirstOrDefault(t => t.BaseType.Equals(typeof(PreprocessorSettings)));

								/// Для каждой настройки препроцессора
								foreach (var property in item.PreprocessorProperties)
								{
									/// проверяем, есть ли такое свойство у объекта
									var propertyInfo = propertiesObjectType.GetProperty(property.PropertyName);

									if (propertyInfo != null)
									{
										var converter = (PropertyConverter)(((ConverterAttribute)propertyInfo
											.GetCustomAttribute(typeof(ConverterAttribute))).ConverterType)
											.GetConstructor(Type.EmptyTypes).Invoke(null);

										try
										{
											propertyInfo.SetValue(preprocessor.Properties, converter.ToValue(property.ValueString));
										}
										catch
										{
											Log.Add(Message.Error(
												$"Не удаётся конвертировать строку '{property.ValueString}' в свойство '{property.DisplayedName}' препроцессора для расширения {item.ExtensionsString}",
												null
											));
										}
									}
								}
							}

							parser.SetPreprocessor(preprocessor);
						}
						else
						{
							Log.Add(Message.Error(
								$"Библиотека {item.PreprocessorPath} не содержит описание препроцессора для расширения {item.ExtensionsString}",
								null
							));
						}
					}
				}
            }

			return parsers;
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
									FileName = cp.Context.FileName,
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
						FileName = concernPoint.Context.FileName,
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
				var rootTextPair = GetRoot(cp.Context.FileName);

				if (rootTextPair != null)
				{
					MarkupManager.Remap(cp, new TargetFileInfo()
					{
						FileName = cp.Context.FileName,
						FileText = rootTextPair.Item2,
						TargetNode = rootTextPair.Item1
					});
				}
			}

			return !cp.HasInvalidLocation;
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

		private static TreeViewItem VisualUpwardSearch(DependencyObject source)
		{
			while (source != null && !(source is TreeViewItem))
				source = VisualTreeHelper.GetParent(source);

			return source as TreeViewItem;
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
	}
}
