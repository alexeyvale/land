//------------------------------------------------------------------------------
// <copyright file="LandExplorerControl.xaml.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Land.VSExtension
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using System.Windows.Media;

	using Microsoft.Win32;

	using Land.Core;
	using Land.Core.Parsing.Tree;
	using Land.Core.Markup;

	/// <summary>
	/// Interaction logic for LandExplorerControl.
	/// </summary>
	public partial class LandExplorerControl : UserControl
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LandExplorerControl"/> class.
		/// </summary>
		public LandExplorerControl()
		{
			this.InitializeComponent();
		}

		/// <summary>
		/// Handles click on the button by displaying a message box.
		/// </summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event args.</param>
		[SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Justification = "Sample code")]
		[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Default event handler naming pattern")]
		private void button1_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show(
				string.Format(System.Globalization.CultureInfo.CurrentUICulture, "Invoked '{0}'", this.ToString()),
				"LandExplorer");
		}

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
			//if (ConcernPointCandidatesList.SelectedItem != null)
			//{
			//	if (Markup.AstRoot == null)
			//	{
			//		Markup.AstRoot = TreeRoot;
			//	}

			//	var concern = MarkupTreeView.SelectedItem as Concern;

			//	Markup.Add(new ConcernPoint((Node)ConcernPointCandidatesList.SelectedItem, concern));
			//}
		}

		private void ConcernPointCandidatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var node = (Node)ConcernPointCandidatesList.SelectedItem;
			//MoveCaretToSource(node, FileEditor, true, 1);
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
			//if (MainTabs.SelectedIndex == 1)
			//{
			//	var offset = FileEditor.TextArea.Caret.Offset;

			//	var pointCandidates = new LinkedList<Node>();
			//	var currentNode = TreeRoot;

			//	/// В качестве кандидатов на роль помечаемого участка рассматриваем узлы от корня,
			//	/// содержащие текущую позицию каретки
			//	while (currentNode != null)
			//	{
			//		if (currentNode.Options.IsLand)
			//			pointCandidates.AddFirst(currentNode);

			//		currentNode = currentNode.Children.Where(c => c.StartOffset.HasValue && c.EndOffset.HasValue
			//			&& c.StartOffset <= offset && c.EndOffset >= offset).FirstOrDefault();
			//	}

			//	ConcernPointCandidatesList.ItemsSource = pointCandidates;
			//}
		}

		private void AddAllLandButton_Click(object sender, RoutedEventArgs e)
		{
			//if (TreeRoot != null)
			//{
			//	if (Markup.AstRoot == null)
			//	{
			//		Markup.AstRoot = TreeRoot;
			//	}

			//	var visitor = new LandExplorerVisitor();
			//	TreeRoot.Accept(visitor);

			//	/// Группируем land-сущности по типу (символу)
			//	foreach (var group in visitor.Land.GroupBy(l => l.Symbol))
			//	{
			//		var concern = new Concern(group.Key);
			//		Markup.Add(concern);

			//		/// В пределах символа группируем по псевдониму
			//		var subgroups = group.GroupBy(g => g.Alias);

			//		/// Для всех точек, для которых указан псевдоним
			//		foreach (var subgroup in subgroups.Where(s => !String.IsNullOrEmpty(s.Key)))
			//		{
			//			/// создаём подфункциональность
			//			var subconcern = new Concern(subgroup.Key, concern);
			//			Markup.Add(subconcern);

			//			foreach (var point in subgroup)
			//				Markup.Add(new ConcernPoint(point, subconcern));
			//		}

			//		/// Остальные добавляются напрямую к функциональности, соответствующей символу
			//		var points = subgroups.Where(s => String.IsNullOrEmpty(s.Key))
			//			.SelectMany(s => s).ToList();

			//		foreach (var point in points)
			//			Markup.Add(new ConcernPoint(point, concern));
			//	}
			//}
		}

		private void ApplyMapping_Click(object sender, RoutedEventArgs e)
		{
			//if (Markup.AstRoot != null && TreeRoot != null)
			//{
			//	Mapper.Remap(Markup.AstRoot, TreeRoot);
			//	Markup.Remap(TreeRoot, Mapper.Mapping);
			//}
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
	}
}