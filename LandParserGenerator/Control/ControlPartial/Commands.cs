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
using Land.Core.Parsing.Tree;
using Land.Markup;
using Land.Markup.Binding;
using Land.Markup.Tree;
using Land.Control.Helpers;

namespace Land.Control
{
	public partial class LandExplorerControl : UserControl
	{
		private void Command_MarkupTree_Delete_Executed(object sender, RoutedEventArgs e)
		{
			MarkupManager.RemoveElement((MarkupElement)MarkupTreeView.SelectedItem);
		}

		private void Command_MarkupTree_Relink_Executed(object sender, RoutedEventArgs e)
		{
			Command_Relink_Executed(State.SelectedItem_MarkupTreeView);
		}

		private void Command_MissingTree_Delete_Executed(object sender, RoutedEventArgs e)
		{
			MarkupManager.RemoveElement(((RemapCandidates)MissingTreeView.SelectedItem).Point);
		}

		private void Command_MarkupTree_DeleteWithSource_Executed(object sender, RoutedEventArgs e)
		{
			var points = GetLinearSequenceVisitor.GetPoints(
				new List<MarkupElement> { (MarkupElement)MarkupTreeView.SelectedItem }
			);

		}

		private void Command_MarkupTree_TurnOn_Executed(object sender, RoutedEventArgs e)
		{
			
		}

		private void Command_MarkupTree_TurnOff_Executed(object sender, RoutedEventArgs e)
		{
			
		}

		private void Command_MissingTree_Relink_Executed(object sender, RoutedEventArgs e)
		{
			Command_Relink_Executed(State.SelectedItem_MissingTreeView);
		}

		private void Command_MissingTree_Accept_Executed(object sender, RoutedEventArgs e)
		{
			var parent = GetTreeViewItemParent(State.SelectedItem_MissingTreeView);

			if (parent != null)
			{
				MarkupManager.RelinkConcernPoint(
					(parent.DataContext as RemapCandidates).Point,
					State.SelectedItem_MissingTreeView.DataContext as RemapCandidateInfo
				);
			}
		}

		private void Command_Relink_Executed(TreeViewItem target)
		{
			var fileName = Editor.GetActiveDocumentName();
			var parsedFile = LogFunction(() => GetParsed(fileName), true, false);

			if (parsedFile != null)
			{
				State.PendingCommand = new PendingCommandInfo()
				{
					Target = target,
					Command = LandExplorerCommand.Relink,
					Document = parsedFile
				};

				ConcernPointCandidatesList.ItemsSource =
					GetConcernPointCandidates(parsedFile.Root, 
						Editor.GetActiveDocumentSelection(false), Editor.GetActiveDocumentSelection(true));

				var point = target.DataContext is RemapCandidates pair
					? pair.Point
					: (ConcernPoint)target.DataContext;

				ConfigureMarkupElementTab(true, point);

				SetStatus("Перепривязка точки", ControlStatus.Pending);
			}
		}

		private void Command_AddPoint_Executed(object sender, RoutedEventArgs e)
		{
			var fileName = Editor.GetActiveDocumentName();
			var parsedFile = LogFunction(() => GetParsed(fileName), true, false);

			if (parsedFile != null)
			{
				State.PendingCommand = new PendingCommandInfo()
				{
					Target = State.SelectedItem_MarkupTreeView,
					Document = parsedFile,
					Command = LandExplorerCommand.AddPoint
				};

				ConcernPointCandidatesList.ItemsSource =
					GetConcernPointCandidates(parsedFile.Root,
						Editor.GetActiveDocumentSelection(false), Editor.GetActiveDocumentSelection(true));

				ConfigureMarkupElementTab(true);

				SetStatus("Добавление точки", ControlStatus.Pending);
			}
		}

		private void Command_AddLand_Executed(object sender, RoutedEventArgs e)
		{
			var fileName = Editor.GetActiveDocumentName();
			var parsedFile = LogFunction(() => GetParsed(fileName), true, false);

			if (parsedFile != null)
				MarkupManager.AddLand(parsedFile, GetPointSearchArea(), GetParsed);
		}

		private void Command_AddConcern_Executed(object sender, RoutedEventArgs e)
		{
			var parent = MarkupTreeView.SelectedItem != null 
				&& MarkupTreeView.SelectedItem is Concern
					? (Concern)MarkupTreeView.SelectedItem : null;

			MarkupManager.AddConcern("Новая функциональность", null, parent);

			if (parent != null)
			{
				State.SelectedItem_MarkupTreeView.IsExpanded = true;
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

				var stubNode = new Node("");
				stubNode.SetLocation(new PointLocation(0, 0, 0), new PointLocation(0, 0, 0));

				MarkupManager.DoWithMarkup(elem =>
				{
					if(elem is ConcernPoint p)
					{
						p.AstNode = stubNode;
						p.HasIrrelevantLocation = true;
					}
				});

				MarkupTreeView.ItemsSource = MarkupManager.Markup;
			}
		}

		private void Command_New_Executed(object sender, RoutedEventArgs e)
		{
			MarkupManager.Clear();
		}

		private void Command_Highlight_Executed(object sender, RoutedEventArgs e)
		{
			State.HighlightConcerns = !State.HighlightConcerns;

			if(!State.HighlightConcerns)
				Editor.ResetSegments();		
		}

		private void Command_OpenConcernGraph_Executed(object sender, RoutedEventArgs e)
		{
			if (MarkupManager.IsValid)
			{
				var graphWindow = new Window_ConcernGraph(MarkupManager);
				graphWindow.Show();
			}
			else
			{
				SetStatus(
					"Для работы с отношениями необходимо синхронизировать разметку с кодом", 
					ControlStatus.Error
				);
			}
		}

		private void Command_AlwaysEnabled_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}

		private void Command_MarkupTree_HasSelectedItem_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = MarkupTreeView != null 
				&& MarkupTreeView.SelectedItem != null;
		}

		private void Command_MarkupTree_HasSelectedConcernPoint_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = MarkupTreeView != null
				&& MarkupTreeView.SelectedItem != null
				&& MarkupTreeView.SelectedItem is ConcernPoint;
		}

		private void Command_MissingTree_HasSelectedItem_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = MissingTreeView != null
				&& MissingTreeView.SelectedItem != null;
		}

		private void Command_MissingTree_HasSelectedConcernPoint_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = MissingTreeView != null
				&& MissingTreeView.SelectedItem != null
				&& MissingTreeView.SelectedItem is RemapCandidates;
		}

		private void Command_MissingTree_HasSelectedCandidate_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = MissingTreeView != null
				&& MissingTreeView.SelectedItem != null
				&& MissingTreeView.SelectedItem is RemapCandidateInfo;
		}

		private void Settings_Click(object sender, RoutedEventArgs e)
		{
			SettingsWindow = new Window_LandExplorerSettings(SettingsObject.Clone());
			SettingsWindow.Owner = Window.GetWindow(this);

			if (SettingsWindow.ShowDialog() ?? false)
			{
				SettingsObject = SettingsWindow.SettingsObject;

				LogAction(() => ReloadParsers(), true, true);

				Editor.SaveSettings(
					SettingsObject, SETTINGS_DEFAULT_PATH
				);
			}
		}

		private void ApplyMapping_Click(object sender, RoutedEventArgs e)
		{
			LogAction(() =>
			{
				ProcessAmbiguities(
					MarkupManager.Remap(
						GetPointSearchArea(),
						true
					),
					true
				);
			}, true, false);
		}

		#region Копирование-вставка

		private void Command_Copy_Executed(object sender, RoutedEventArgs e)
		{
			if (MarkupTreeView.IsKeyboardFocusWithin && State.SelectedItem_MarkupTreeView != null)
			{
				State.BufferedDataContext = (MarkupElement)State.SelectedItem_MarkupTreeView.DataContext;
				SetStatus("Элемент скопирован", ControlStatus.Pending);
			}
			else if(RelationSource.IsKeyboardFocusWithin && RelationSource.Tag != null)
			{
				State.BufferedDataContext = (MarkupElement)RelationSource.Tag;
				SetStatus("Элемент скопирован", ControlStatus.Pending);
			}
			else if(RelationTarget.IsKeyboardFocusWithin && RelationTarget.Tag != null)
			{
				State.BufferedDataContext = (MarkupElement)RelationTarget.Tag;
				SetStatus("Элемент скопирован", ControlStatus.Pending);
			}
		}

		private void Command_Paste_Executed(object sender, RoutedEventArgs e)
		{
			if (State.BufferedDataContext != null)
			{
				if (RelationSource.IsKeyboardFocusWithin)
				{
					RelationSource.Tag = State.BufferedDataContext;
					RefreshRelationCandidates();
					State.BufferedDataContext = null;

					SetStatus("Элемент вставлен", ControlStatus.Ready);
				}
				else if (RelationTarget.IsKeyboardFocusWithin)
				{
					RelationTarget.Tag = State.BufferedDataContext;
					RefreshRelationCandidates();
					State.BufferedDataContext = null;

					SetStatus("Элемент вставлен", ControlStatus.Ready);
				}
			}
		}

		#endregion

		#region Methods

		private List<ConcernPointCandidate> GetConcernPointCandidates(Node root, 
			SegmentLocation realSelection, SegmentLocation adjustedSelection)
		{
			/// Для выделения находим сущности, объемлющие его
			var candidates = MarkupManager.GetConcernPointCandidates(root, realSelection)
					.Select(c => (ConcernPointCandidate)new ExistingConcernPointCandidate(c)).ToList();

			/// Проверяем, можно ли обрамить его кастомным блоком
			if (CustomBlockValidator.IsValid(root, adjustedSelection))
			{
				candidates.Insert(0, new CustomConcernPointCandidate(
					realSelection, adjustedSelection, "Новый пользовательский блок"
				));
			}

			return candidates;
		}

		#endregion
	}
}