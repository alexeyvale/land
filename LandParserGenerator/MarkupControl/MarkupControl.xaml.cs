using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using Microsoft.Win32;

using Land.Core;
using Land.Core.Parsing.LL;
using Land.Core.Parsing.Tree;
using Land.Core.Markup;

namespace MarkupControl
{
	/// <summary>
	/// Логика взаимодействия для MarkupControl.xaml
	/// </summary>
	public partial class MarkupControl : UserControl
    {
		/// <summary>
		/// Адаптер редактора, с которым взаимодействует панель
		/// </summary>
		private IEditorAdapter Editor { get; set; }

		/// <summary>
		/// Парсеры, которые необходимо запускать для каждого из расширений файлов
		/// </summary>
		private Dictionary<string, Parser> Parsers { get; set; } = new Dictionary<string, Parser>();

        public MarkupControl()
        {
			InitializeComponent();
        }

		public void Initialize(IEditorAdapter adapter, Dictionary<string, string> parserSources)
		{
			MarkupTreeView.ItemsSource = MarkupManager.Markup;
			Editor = adapter;

			var messages = new List<Message>();

			foreach (var extPathPair in parserSources)
			{
				Parsers[extPathPair.Key] = BuilderLL.BuildParser(
					File.ReadAllText(extPathPair.Value),
					messages
				);
			}

			Editor.ProcessMessages(messages);
		}

		public class MarkupControlState
		{
			public HashSet<MarkupElement> ExpandedItems { get; set; } = new HashSet<MarkupElement>();
			public TreeViewItem EditedItem { get; set; }
			public string EditedItemOldHeader { get; set; }
			public TreeViewItem SelectedItem { get; set; }
			public string DocumentForCurrentCandidates { get; set; }
		}

		private MarkupManager MarkupManager { get; set; } = new MarkupManager();
		private LandMapper Mapper { get; set; } = new LandMapper();
		private MarkupControlState State { get; set; } = new MarkupControlState();


		private void MarkupTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			Editor.ResetSegments();

			if (e.NewValue != null)
			{
				/// При клике по точке переходим к ней и подсвечиваем участок
				if (e.NewValue is ConcernPoint)
				{
					var concernPoint = (ConcernPoint)e.NewValue;
					Editor.SetActiveDocumentAndOffset(concernPoint.FileName, concernPoint.TreeNode.StartOffset.Value);

					Editor.SetSegments(new List<DocumentSegment>(){
						new DocumentSegment()
						{
							StartOffset = concernPoint.TreeNode.StartOffset.Value,
							EndOffset = concernPoint.TreeNode.EndOffset.Value,
							CaptureWholeLine = false
						}
					});

					return;
				}

				/// Если отключен режим постоянной подсветки функциональностей,
				/// подсвечиваем то, что выбрано в данный конкретный момент
				if (e.NewValue is Concern)
				{
					Editor.SetSegments(GetConcernSegments((Concern)e.NewValue).Select(s => new DocumentSegment()
					{
						StartOffset = s.Item1,
						EndOffset = s.Item2,
						CaptureWholeLine = true
					}).ToList());
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
				var concern = MarkupTreeView.SelectedItem as Concern;

				MarkupManager.Add(new ConcernPoint(
					State.DocumentForCurrentCandidates,
					(Node)ConcernPointCandidatesList.SelectedItem,
					concern
				));
			}
		}

		private void ConcernPointCandidatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var node = (Node)ConcernPointCandidatesList.SelectedItem;
			Editor.SetActiveDocumentAndOffset(State.DocumentForCurrentCandidates, node.StartOffset.Value);
		}

		private void DeleteConcernPoint_Click(object sender, RoutedEventArgs e)
		{
			if (MarkupTreeView.SelectedItem != null)
			{
				MarkupManager.Remove((MarkupElement)MarkupTreeView.SelectedItem);
			}
		}

		private void AddConcernPoint_Click(object sender, RoutedEventArgs e)
		{
			var offset = Editor.GetActiveDocumentOffset();
			var documentName = Editor.GetActiveDocumentName();

			if (!offset.HasValue || !EnsureTreeExistence(documentName))
				return;

			State.DocumentForCurrentCandidates = documentName;
			var pointCandidates = new LinkedList<Node>();
			var currentNode = MarkupManager.AstRoots[documentName];

			/// В качестве кандидатов на роль помечаемого участка рассматриваем узлы от корня,
			/// содержащие текущую позицию каретки
			while (currentNode != null)
			{
				if (currentNode.Options.IsLand)
					pointCandidates.AddFirst(currentNode);

				currentNode = currentNode.Children.Where(c => c.StartOffset.HasValue && c.EndOffset.HasValue
					&& c.StartOffset <= offset && c.EndOffset >= offset).FirstOrDefault();
			}

			ConcernPointCandidatesList.ItemsSource = pointCandidates;
		}

		private void AddAllLandButton_Click(object sender, RoutedEventArgs e)
		{
			var documentName = Editor.GetActiveDocumentName();

			if (EnsureTreeExistence(documentName))
			{
				var visitor = new LandExplorerVisitor();
				MarkupManager.AstRoots[documentName].Accept(visitor);

				/// Группируем land-сущности по типу (символу)
				foreach (var group in visitor.Land.GroupBy(l => l.Symbol))
				{
					var concern = new Concern(group.Key);
					MarkupManager.Add(concern);

					/// В пределах символа группируем по псевдониму
					var subgroups = group.GroupBy(g => g.Alias);

					/// Для всех точек, для которых указан псевдоним
					foreach (var subgroup in subgroups.Where(s => !String.IsNullOrEmpty(s.Key)))
					{
						/// создаём подфункциональность
						var subconcern = new Concern(subgroup.Key, concern);
						MarkupManager.Add(subconcern);

						foreach (var point in subgroup)
							MarkupManager.Add(new ConcernPoint(documentName, point, subconcern));
					}

					/// Остальные добавляются напрямую к функциональности, соответствующей символу
					var points = subgroups.Where(s => String.IsNullOrEmpty(s.Key))
						.SelectMany(s => s).ToList();

					foreach (var point in points)
						MarkupManager.Add(new ConcernPoint(documentName, point, concern));
				}
			}
		}

		private void ApplyMapping_Click(object sender, RoutedEventArgs e)
		{
			foreach(var nameRootPair in MarkupManager.AstRoots)
			{
				var newTreeRoot = ParseDocument(nameRootPair.Key);
				Mapper.Remap(nameRootPair.Value, newTreeRoot);
				MarkupManager.Remap(nameRootPair.Key, newTreeRoot, Mapper.Mapping);
			}
		}

		private void AddConcernFolder_Click(object sender, RoutedEventArgs e)
		{
			MarkupManager.Add(new Concern("Новая функциональность"));
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
				MarkupManager.Serialize(saveFileDialog.FileName, MarkupManager);
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
				MarkupManager = MarkupManager.Deserialize(openFileDialog.FileName);
				MarkupTreeView.ItemsSource = MarkupManager.Markup;
			}
		}

		private void NewConcernMarkup_Click(object sender, RoutedEventArgs e)
		{
			MarkupManager.Clear();
		}

		private void MarkupTreeRenameMenuItem_Click(object sender, RoutedEventArgs e)
		{
			TreeViewItem item = State.SelectedItem;

			var textbox = GetMarkupTreeItemTextBox(item);
			textbox.Visibility = Visibility.Visible;
			textbox.Focus();

			State.EditedItemOldHeader = textbox.Text;
			State.EditedItem = item;
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
			State.SelectedItem = (TreeViewItem)e.OriginalSource;

			if (State.EditedItem != null && State.EditedItem != State.SelectedItem)
			{
				var textbox = GetMarkupTreeItemTextBox(State.EditedItem);
				textbox.Visibility = Visibility.Hidden;
				State.EditedItem = null;
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

		private void HighlightConcerns_Click(object sender, RoutedEventArgs e)
		{
			/// Сбрасываем текущее выделение участков текста
			//Editor.ResetSegments();

			///// Обеспечиваем стандартное отображение Concern-ов в панели
			//foreach (var concern in MarkupManager.Markup.OfType<Concern>().Where(c => c.Parent == null))
			//{
			//	var markupTreeItem = MarkupTreeView.ItemContainerGenerator.ContainerFromItem(concern) as TreeViewItem;
			//	if (!markupTreeItem.IsSelected)
			//	{
			//		var label = GetMarkupTreeItemLabel(markupTreeItem, "ConcernIcon");
			//		if (label != null)
			//			label.Foreground = Brushes.DimGray;
			//	}
			//}

			//var concernsAndColors = new Dictionary<Concern, Color>();

			//foreach (var concern in MarkupManager.Markup.OfType<Concern>().Where(c => c.Parent == null))
			//{
			//	concernsAndColors[concern] =
			//		CurrentConcernColorizer.SetSegments(GetConcernSegments(concern).Select(s => new DocumentSegment()
			//		{
			//			StartOffset = s.Item1,
			//			EndOffset = s.Item2,
			//			CaptureWholeLine = true
			//		}).ToList());

			//	var label = GetMarkupTreeItemLabel(MarkupTreeView.ItemContainerGenerator.ContainerFromItem(concern) as TreeViewItem, "ConcernIcon");
			//	if (label != null)
			//		label.Foreground = new SolidColorBrush(concernsAndColors[concern]);
			//}
		}

		#region methods

		private Node ParseDocument(string documentName)
		{
			if (!String.IsNullOrEmpty(documentName))
			{
				var extension = Path.GetExtension(documentName);

				if (Parsers.ContainsKey(extension) && Parsers[extension] != null)
				{
					return Parsers[extension].Parse(Editor.GetDocumentText(documentName));
				}
			}

			return null;
		}

		private bool EnsureTreeExistence(string documentName)
		{
			if (MarkupManager.AstRoots.ContainsKey(documentName))
			{
				return true;
			}
			else
			{
				if (!String.IsNullOrEmpty(documentName))
				{
					var extension = Path.GetExtension(documentName);

					if (Parsers.ContainsKey(extension) && Parsers[extension] != null)
					{
						MarkupManager.AstRoots[documentName] = Parsers[extension].Parse(Editor.GetDocumentText(documentName));

						return Parsers[extension].Log.All(l => l.Type != MessageType.Error) && MarkupManager.AstRoots[documentName] != null;
					}
				}
			}

			return false;
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
				MarkupManager.Remove(source);
				source.Parent = (Concern)target;
				MarkupManager.Add(source);
			}
			else
			{
				if (target != null)
				{
					if (target.Parent != source.Parent)
					{
						MarkupManager.Remove(source);
						source.Parent = target.Parent;
						MarkupManager.Add(source);
					}
				}
				else
				{
					MarkupManager.Remove(source);
					source.Parent = null;
					MarkupManager.Add(source);
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
	}
}
