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
			MarkupManager.RemoveElement(((PointCandidatesPair)MissingTreeView.SelectedItem).Point);
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
					(parent.DataContext as PointCandidatesPair).Point,
					State.SelectedItem_MissingTreeView.DataContext as CandidateInfo
				);
			}
		}

		private void Command_Relink_Executed(TreeViewItem target)
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
						Target = target,
						DocumentName = fileName,
						Command = LandExplorerCommand.Relink,
						DocumentText = rootTextPair.Item2
					};

					ConcernPointCandidatesList.ItemsSource =
						MarkupManager.GetConcernPointCandidates(rootTextPair.Item1, offset.Value)
							.Select(c => new ConcernPointCandidateViewModel(c));

					ConfigureMarkupElementTab(true, (ConcernPoint)target.DataContext);

					SetStatus("Перепривязка точки", ControlStatus.Pending);
				}
			}
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
						Target = State.SelectedItem_MarkupTreeView,
						DocumentName = fileName,
						Command = LandExplorerCommand.AddPoint,
						DocumentText = rootTextPair.Item2
					};

					ConcernPointCandidatesList.ItemsSource =
						MarkupManager.GetConcernPointCandidates(rootTextPair.Item1, offset.Value)
							.Select(c=>new ConcernPointCandidateViewModel(c));

					ConfigureMarkupElementTab(true);

					SetStatus("Добавление точки", ControlStatus.Pending);
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
				stubNode.SetAnchor(new PointLocation(0, 0, 0), new PointLocation(0, 0, 0));

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
			var graphWindow = new ConcernGraph(MarkupManager);
			graphWindow.Show();
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
				&& MissingTreeView.SelectedItem is PointCandidatesPair;
		}

		private void Command_MissingTree_HasSelectedCandidate_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = MissingTreeView != null
				&& MissingTreeView.SelectedItem != null
				&& MissingTreeView.SelectedItem is CandidateInfo;
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

				SyncMarkupManagerSettings();
				Parsers = LogFunction(() => LoadParsers(), true, true);
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

				ProcessAmbiguities(
					MarkupManager.Remap(forest, sender == ApplyLocalMapping),
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
	}
}