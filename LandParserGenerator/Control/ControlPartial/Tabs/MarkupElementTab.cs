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
	public partial class LandExplorerControl : UserControl
	{
		private void ConcernPointCandidatesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (ConcernPointCandidatesList.SelectedItem != null)
			{
				var node = ((ConcernPointCandidateViewModel)ConcernPointCandidatesList.SelectedItem).Node;

				Editor.SetActiveDocumentAndOffset(
					State.PendingCommand.DocumentName,
					node.Anchor.Start
				);
			}
		}

		private void ConcernPointNameText_Reset_Click(object sender, RoutedEventArgs e)
		{
			if (ConcernPointCandidatesList.SelectedItem != null)
			{
				ConcernPointNameText.Text = 
					((ConcernPointCandidateViewModel)ConcernPointCandidatesList.SelectedItem).ViewHeader;
				CustomConcernPointNameEntered = false;
			}
		}

		private void ConcernPointSaveButton_Click(object sender, RoutedEventArgs e)
		{
			if (ConcernPointCandidatesList.SelectedItem != null)
			{
				if (State.PendingCommand.Command == LandExplorerCommand.Relink)
				{
					var point = State.PendingCommand.Target.DataContext is ConcernPoint cPoint
						? cPoint : (State.PendingCommand.Target.DataContext as PointCandidatesPair).Point;

					MarkupManager.RelinkConcernPoint(
						point,
						new TargetFileInfo()
						{
							FileName = State.PendingCommand.DocumentName,
							FileText = State.PendingCommand.DocumentText,
							TargetNode = ((ConcernPointCandidateViewModel)ConcernPointCandidatesList.SelectedItem).Node
						}
					);

					point.Name = ConcernPointNameText.Text;
					point.Comment = ConcernPointCommentText.Text;
				}
				else
				{
					MarkupManager.AddConcernPoint(
						new TargetFileInfo()
						{
							FileName = State.PendingCommand.DocumentName,
							FileText = State.PendingCommand.DocumentText,
							TargetNode = ((ConcernPointCandidateViewModel)ConcernPointCandidatesList.SelectedItem).Node
						},
						ConcernPointNameText.Text,
						ConcernPointCommentText.Text,
						State.PendingCommand.Target?.DataContext as Concern
					);

					if (State.PendingCommand.Target != null)
						State.PendingCommand.Target.IsExpanded = true;
				}

				ConcernPointCandidatesList.ItemsSource = null;
				ConfigureMarkupElementTab(false);

				SetStatus("Привязка завершена", ControlStatus.Success);
			}
		}

		private void ConcernPointCancelButton_Click(object sender, RoutedEventArgs e)
		{
			ConcernPointCandidatesList.ItemsSource = null;
			ConfigureMarkupElementTab(false);

			SetStatus("Привязка отменена", ControlStatus.Ready);
		}

		private void MarkupElementText_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var textBox = (TextBox)sender;

			if (textBox.IsReadOnly)
			{
				textBox.IsReadOnly = false;
				textBox.Select(0, 0);
			}
		}

		private void MarkupElementText_LostFocus(object sender, RoutedEventArgs e)
		{
			var textBox = (TextBox)sender;

			textBox.IsReadOnly = true;
		}

		private void ConfigureMarkupElementTab(bool mappingMode, ConcernPoint pointToRemap = null)
		{
			Tabs.SelectedItem = MarkupElementTab;

			if (mappingMode)
			{
				ConcernPointPanel.Visibility = Visibility.Visible;
				MarkupElementPanel.Visibility = Visibility.Collapsed;

				ConcernPointNameText.Text = pointToRemap?.Name;
				ConcernPointCommentText.Text = pointToRemap?.Comment;
				CustomConcernPointNameEntered = pointToRemap != null;
			}
			else
			{
				ConcernPointPanel.Visibility = Visibility.Collapsed;
				MarkupElementPanel.Visibility = Visibility.Visible;
			}
		}

		private bool CustomConcernPointNameEntered = false;

		private void ConcernPointCandidatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!CustomConcernPointNameEntered && ConcernPointCandidatesList.SelectedItem != null)
				ConcernPointNameText.Text = ((ConcernPointCandidateViewModel)ConcernPointCandidatesList.SelectedItem).ViewHeader;
		}

		private void ConcernPointNameText_PreviewKeyDown(object sender, KeyboardEventArgs e)
		{
			CustomConcernPointNameEntered = true;
		}
	}
}