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
using Land.Core.Specification;
using Land.Core.Parsing;
using Land.Core.Parsing.Tree;
using Land.Core.Parsing.Preprocessing;
using Land.Markup;
using Land.Control.Helpers;
using System.ComponentModel;

namespace Land.Control
{
	public partial class LandExplorerControl : UserControl, INotifyPropertyChanged
	{
		private void ConcernPointCandidatesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (ConcernPointCandidatesList.SelectedItem is ExistingConcernPointCandidate candidate)
			{
				var node = candidate.Node;

				Editor.SetActiveDocumentAndOffset(
					State.PendingCommand.Document.Name,
					node.Location.Start
				);
			}
		}

		private void ConcernPointNameText_Reset_Click(object sender, RoutedEventArgs e)
		{
			if (ConcernPointCandidatesList.SelectedItem != null)
			{
				ConcernPointNameText.Text = 
					((ConcernPointCandidate)ConcernPointCandidatesList.SelectedItem).ViewHeader;
				CustomConcernPointNameEntered = false;
			}
		}

		private ExistingConcernPointCandidate EnsureCandidateExists(ConcernPointCandidate model, string pointName)
		{
			if (ConcernPointCandidatesList.SelectedItem is ExistingConcernPointCandidate existing)
				return existing;

			/// Если требуется привязка к ещё не существующему пользовательскому блоку,
			var customPoint = (CustomConcernPointCandidate)ConcernPointCandidatesList.SelectedItem;
			var text = Editor.GetDocumentText(State.PendingCommand.Document.Name);

			/// нам понадобится парсер для файла, к содержимому которого хотим привязаться
			var extension = Path.GetExtension(State.PendingCommand.Document.Name);

			if (Parsers[extension] != null)
			{
				var startBorders = Parsers[extension].GrammarObject.Options.GetParams(CustomBlockOption.GROUP_NAME, CustomBlockOption.START)
					.Select(e=>(string)e).ToList();
				var endBorders = Parsers[extension].GrammarObject.Options.GetParams(CustomBlockOption.GROUP_NAME, CustomBlockOption.END)
					.Select(e => (string)e).ToList();

				var indentationString = String.Join("", text.Skip(customPoint.AdjustedSelection.Start.Offset)
					.TakeWhile(c => c == ' ' || c == '\t'));
				var expectedLineEnd = Editor.GetDocumentLineEnd(State.PendingCommand.Document.Name);

				/// Формируем границы блока
				var customBlockStart = $"{indentationString}{startBorders.ElementAtOrDefault(0)} {pointName} {startBorders.ElementAtOrDefault(1)}{expectedLineEnd}";
				var customBlockEnd = $"{expectedLineEnd}{indentationString}{endBorders.ElementAtOrDefault(0)}{endBorders.ElementAtOrDefault(1)}";

				/// Вставляем их в текст
				Editor.InsertText(State.PendingCommand.Document.Name, customBlockStart, 
					customPoint.AdjustedSelection.Start);
				Editor.InsertText(State.PendingCommand.Document.Name, customBlockEnd, 
					new PointLocation(
						customPoint.AdjustedSelection.End.Line + 1,
						customPoint.AdjustedSelection.End.Column,
						customPoint.AdjustedSelection.End.Offset + customBlockStart.Length
					)
				);

				customPoint.RealSelection.Shift(1, 0, customBlockStart.Length);

				/// Переразбираем изменённый текст
				var parsedFile = State.PendingCommand.Document =
					LogFunction(() => GetParsed(State.PendingCommand.Document.Name), true, false);

				/// Теперь в дереве должен появиться узел, соответствующий пользовательскому блоку
				var customBlockNode = MarkupManager
					.GetConcernPointCandidates(parsedFile.Root, customPoint.RealSelection)
					.FirstOrDefault(cand => cand.Type == Grammar.CUSTOM_BLOCK_RULE_NAME);

				return customBlockNode != null
					? new ExistingConcernPointCandidate(customBlockNode) : null;
			}

			return null;
		}

		private void ConcernPointSaveButton_Click(object sender, RoutedEventArgs e)
		{
			if (ConcernPointCandidatesList.SelectedItem != null)
			{
				var selectedCandidate = EnsureCandidateExists(
					(ConcernPointCandidate)ConcernPointCandidatesList.SelectedItem, 
					ConcernPointNameText.Text
				);

				if (selectedCandidate != null)
				{
					if (State.PendingCommand.Command == LandExplorerCommand.Relink
						&& (State.PendingCommand.Target.DataContext is ConcernPoint
							|| State.PendingCommand.Target.DataContext is RemapCandidates))
					{
						var point = (State.PendingCommand.Target.DataContext as ConcernPoint) 
							?? (State.PendingCommand.Target.DataContext as RemapCandidates).Point;

						MarkupManager.RelinkConcernPoint(
							point,
							selectedCandidate.Node,
							State.PendingCommand.Document,
							GetPointSearchArea()
						);

						point.Name = ConcernPointNameText.Text;
						point.Comment = ConcernPointCommentText.Text;
					}
					else
					{
						var point = MarkupManager.AddConcernPoint(
							selectedCandidate.Node,
							State.PendingCommand.Document,
							ConcernPointNameText.Text,
							ConcernPointCommentText.Text,
							State.PendingCommand.Target?.DataContext as Concern
						);

						if (State.PendingCommand.Target != null)
							State.PendingCommand.Target.IsExpanded = true;
					}

					SetStatus($"Привязка завершена", ControlStatus.Success);
				}
				else
				{
					SetStatus("Не удалось произвести привязку", ControlStatus.Error);
				}

				ConcernPointCandidatesList.ItemsSource = null;
				ConfigureMarkupElementTab(false);
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
				ConcernPointNameText.Text = ((ConcernPointCandidate)ConcernPointCandidatesList.SelectedItem).ViewHeader;
		}

		private void ConcernPointNameText_PreviewKeyDown(object sender, KeyboardEventArgs e)
		{
			CustomConcernPointNameEntered = true;
		}
	}
}