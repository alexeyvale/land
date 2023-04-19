using Land.Markup;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Land.Control
{
	public partial class LandExplorerControl : UserControl, INotifyPropertyChanged
	{
		private int MAX_TEXT_SIZE = 30;
		private int MIN_TEXT_SIZE = 9;

		private void MarkupTreeView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			var snd = sender as TreeView;

			if (Keyboard.PrimaryDevice.Modifiers == ModifierKeys.Control)
			{
				e.Handled = true;

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
		private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
		{
			e.Handled = true;
		}

		private void MarkupTreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if(e.ChangedButton == MouseButton.Left)
			{
				/// Двойной клик по элементы дерева всплывает до корня дерева
				var processingItemData = (MarkupElement)((TreeViewItem)sender).DataContext;

				if (processingItemData.Parent == null)
				{
					var clickedItem = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);

					if (clickedItem != null && clickedItem.DataContext is ConcernPoint concernPoint)
					{
						/// При клике по точке переходим к ней
						if (EnsureLocationValid(concernPoint))
						{
							Editor.SetActiveDocumentAndOffset(
								concernPoint.Context.FileName,
								concernPoint.Location.Start
							);
						}

						clickedItem.InvalidateVisual();

						e.Handled = true;
					}
				}
			}
		}

		private void MarkupTreeViewItem_KeyDown(object sender, KeyEventArgs e)
		{
			var item = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);

			if (item != null)
			{
				switch (e.Key)
				{
					case Key.Enter:
						if (item.DataContext is ConcernPoint concernPoint)
						{
							if (EnsureLocationValid(concernPoint))
							{
								Editor.SetActiveDocumentAndOffset(
									concernPoint.Context.FileName,
									concernPoint.Location.Start
								);
							}
						}
						else
							item.IsExpanded = true;

						e.Handled = true;
						break;
					case Key.Escape:
						if (item.DataContext is Concern)
							item.IsExpanded = false;

						e.Handled = true;
						break;
				}
			}
		}

		private void MarkupTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			var item = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);

			if (item == null)
			{
				if (State.SelectedItem_MarkupTreeView != null)
				{
					State.SelectedItem_MarkupTreeView.IsSelected = false;

					MarkupTreeView.Focus();
				}
			}
		}

		private void MarkupTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			var item = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);

			if (item != null)
			{
				item.IsSelected = true;
				e.Handled = true;
			}
			else
			{
				if (State.SelectedItem_MarkupTreeView != null)
				{
					State.SelectedItem_MarkupTreeView.IsSelected = false;
				}
			}

			MarkupTreeView.Focus();
			e.Handled = true;
		}

		private void MarkupTreeViewItem_PreviewMouseLeftButtonDown_Highlight(object sender, MouseButtonEventArgs e)
		{
			try
			{
				var item = (TreeViewItem)sender;

				if (State.HighlightConcerns)
				{
					Editor.ResetSegments();

					Editor.SetSegments(
						GetSegments(
							(MarkupElement)item.DataContext,
							item.DataContext is Concern
						),
						HighlightingColor
					);
				}
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}		
		}

		#region displaying element

		private void MarkupTreeViewItem_Expanded(object sender, RoutedEventArgs e)
		{
			var item = (TreeViewItem)sender;

			if (item.DataContext is Concern)
			{
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
			State.SelectedItem_MarkupTreeView = (TreeViewItem)sender;

			var data = (MarkupElement)State.SelectedItem_MarkupTreeView.DataContext;

			Tabs.SelectedItem = (data is ConcernPoint p && p.HasMissingLocation) 
				? MissingPointsTab : MarkupElementTab;

			MarkupElementNameText.Text = data.Name;
			MarkupElementCommentText.Text = data.Comment;

			e.Handled = true;
		}

		private void MarkupTreeViewItem_Unselected(object sender, RoutedEventArgs e)
		{
			State.SelectedItem_MarkupTreeView = null;

			MarkupElementNameText.Text = null;
			MarkupElementCommentText.Text = null;

			SetMarkupElementNameEditState(false);
			SetMarkupElementCommentEditState(false);

			e.Handled = true;
		}

		#endregion
	}
}