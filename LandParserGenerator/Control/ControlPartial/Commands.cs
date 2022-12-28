using Land.Control.Helpers;
using Land.Control.Properties;
using Land.Core;
using Land.Core.Parsing.Tree;
using Land.Markup;
using Land.Markup.Binding;
using Land.Markup.Tree;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SWF = System.Windows.Forms;

#pragma warning disable CA1031 // Do not catch general exception types

namespace Land.Control
{
	public partial class LandExplorerControl : UserControl, INotifyPropertyChanged
	{
		private void Command_MarkupTree_Delete_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				MarkupManager.RemoveElement((MarkupElement)MarkupTreeView.SelectedItem);
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_MarkupTree_RelinkSame_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				/// Переходим к точке, которую хотим перепривязать
				var point = (ConcernPoint)State.SelectedItem_MarkupTreeView.DataContext;

				if (EnsureLocationValid(point))
				{
					Editor.SetActiveDocumentAndOffset(
						point.Context.FileName,
						point.Location.Start
					);

					/// Выбираем сущности всех уровней, к которым можно привязаться в данном месте
					Command_Relink_Executed(State.SelectedItem_MarkupTreeView);
				}
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_MarkupTree_RelinkCurrent_Executed(object sender, RoutedEventArgs e)
		{
			Command_Relink_Executed(State.SelectedItem_MarkupTreeView);
		}

		private void Command_MissingTree_Delete_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				MarkupManager.RemoveElement(((RemapCandidates)MissingTreeView.SelectedItem).Point);
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_MarkupTree_DeleteWithSource_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				var points = GetLinearSequenceVisitor.GetPoints(
					new List<MarkupElement> { (MarkupElement)MarkupTreeView.SelectedItem }
				);
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
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
			try
			{
				var parent = GetTreeViewItemParent(State.SelectedItem_MissingTreeView);

				if (parent != null)
				{
					MarkupManager.RelinkConcernPoint(
						(parent.DataContext as RemapCandidates).Point,
						State.SelectedItem_MissingTreeView.DataContext as RemapCandidateInfo
					);

					SetStatus("Точка перепривязана", ControlStatus.Success);
				}
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_Relink_Executed(TreeViewItem target)
		{
			try
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
						GetConcernPointCandidates(
							parsedFile,
							Editor.GetActiveDocumentSelection(false),
							Editor.GetActiveDocumentSelection(true)
						);

					var point = target.DataContext is RemapCandidates pair
						? pair.Point
						: (ConcernPoint)target.DataContext;

					ConfigureMarkupElementTab(true, point);

					SetStatus("Перепривязка точки", ControlStatus.Pending);
				}
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_SelectPoint_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				var fileName = Editor.GetActiveDocumentName();
				var parsedFile = LogFunction(() => GetParsed(fileName), true, false);

				if (parsedFile != null)
				{
					State.PendingCommand = new PendingCommandInfo()
					{
						Target = State.SelectedItem_MarkupTreeView,
						Document = parsedFile,
						Command = LandExplorerCommand.SelectPoint
					};

					ConcernPointCandidatesList.ItemsSource =
						GetConcernPointCandidates(
							parsedFile,
							Editor.GetActiveDocumentSelection(false),
							Editor.GetActiveDocumentSelection(true)
						);

					ConfigureMarkupElementTab(true);

					SetStatus("Добавление точки", ControlStatus.Pending);
				}
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_AddPoint_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				var fileName = Editor.GetActiveDocumentName();
				var parsedFile = LogFunction(() => GetParsed(fileName), true, false);

				if (parsedFile != null)
				{
					var candidate = GetConcernPointCandidates(
						parsedFile,
						Editor.GetActiveDocumentSelection(false),
						Editor.GetActiveDocumentSelection(true)
					)
					.OfType<ExistingConcernPointCandidate>()
					.FirstOrDefault(c => c.Line == null
						&& c.Node.Type != Land.Core.Specification.Grammar.CUSTOM_BLOCK_RULE_NAME
					);

					if (candidate != null)
					{
						MarkupManager.AddConcernPoint(
							candidate.Node,
							null,
							parsedFile,
							candidate.ViewHeader,
							null,
							State.SelectedItem_MarkupTreeView?.DataContext as MarkupElement
						);

						if (State.SelectedItem_MarkupTreeView != null)
						{
							State.SelectedItem_MarkupTreeView.IsExpanded = true;
						}

						SetStatus("Привязка произведена", ControlStatus.Success);

						if (SettingsObject.EnableAutosave)
						{
							Command_Save_Executed(sender, e);
						}
					}
				}
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_AddLand_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				var fileName = Editor.GetActiveDocumentName();
				var parsedFile = LogFunction(() => GetParsed(fileName), true, false);

				if (parsedFile != null)
				{
					MarkupManager.AddLand(parsedFile);

					if (SettingsObject.EnableAutosave)
					{
						Command_Save_Executed(sender, e);
					}
				}
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_AddConcern_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				MarkupManager.AddConcern(
					"Новая функциональность",
					null,
					State.SelectedItem_MarkupTreeView?.DataContext as MarkupElement
				);

				if (State.SelectedItem_MarkupTreeView != null)
				{
					State.SelectedItem_MarkupTreeView.IsExpanded = true;
				}

				if (SettingsObject.EnableAutosave)
				{
					Command_Save_Executed(sender, e);
				}
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_Save_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				if (!String.IsNullOrWhiteSpace(MarkupFilePath))
				{
					MarkupManager.Serialize(MarkupFilePath, !SettingsObject.SaveAbsolutePath);

					SetStatus("Разметка сохранена", ControlStatus.Success);
				}
				else
				{
					Command_SaveAs_Executed(sender, e);
				}
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_SaveAs_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				var saveFileDialog = new SaveFileDialog()
				{
					AddExtension = true,
					DefaultExt = "landmark",
					Filter = "Файлы LANDMARK (*.landmark)|*.landmark|Все файлы (*.*)|*.*",
					InitialDirectory = Path.GetDirectoryName(MarkupFilePath),
					FileName = Path.GetFileName(MarkupFilePath)
				};

				if (saveFileDialog.ShowDialog() == true)
				{
					MarkupFilePath = saveFileDialog.FileName;
					MarkupManager.Serialize(MarkupFilePath, !SettingsObject.SaveAbsolutePath);

					SetStatus("Разметка сохранена", ControlStatus.Success);
				}
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_Open_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				if (HasUnsavedChanges)
				{
					switch (SWF.MessageBox.Show(
						"Сохранить изменения текущей разметки?",
						"Создание новой разметки",
						SWF.MessageBoxButtons.YesNoCancel,
						SWF.MessageBoxIcon.Question))
					{
						case SWF.DialogResult.Yes:
							Command_Save_Executed(sender, e);
							break;
						case SWF.DialogResult.Cancel:
							return;
					}
				}

				var openFileDialog = new OpenFileDialog()
				{
					AddExtension = true,
					DefaultExt = "landmark",
					Filter = "Файлы LANDMARK (*.landmark)|*.landmark|Все файлы (*.*)|*.*"
				};

				if (openFileDialog.ShowDialog() == true)
				{
					LoadFromFile(openFileDialog.FileName);
				}
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_New_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				if (HasUnsavedChanges)
				{
					switch (SWF.MessageBox.Show(
						"Сохранить изменения текущей разметки?",
						"Создание новой разметки",
						SWF.MessageBoxButtons.YesNoCancel,
						SWF.MessageBoxIcon.Question))
					{
						case SWF.DialogResult.Yes:
							Command_Save_Executed(sender, e);
							break;
						case SWF.DialogResult.Cancel:
							return;
					}
				}

				MarkupManager.Clear();
				MarkupFilePath = null;

				SetStatus("Создана новая разметка", ControlStatus.Success);
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_Highlight_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				State.HighlightConcerns = !State.HighlightConcerns;

				if (!State.HighlightConcerns)
					Editor.ResetSegments();
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_OpenConcernGraph_Executed(object sender, RoutedEventArgs e)
		{
			try
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
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_CollapseAll_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				CollapseOrExpand(MarkupTreeView, false);
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_AlwaysEnabled_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}

		private void Command_HasUnsavedChanges_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = MarkupManager?.HasUnsavedChanges ?? false;
		}

		private void Command_MarkupTree_HasSelectedItem_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = MarkupTreeView?.SelectedItem != null;
		}

		private void Command_MarkupTree_HasSelectedConcernPoint_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = MarkupTreeView?.SelectedItem != null
				&& MarkupTreeView.SelectedItem is ConcernPoint;
		}

		private void Command_MissingTree_HasSelectedItem_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = MissingTreeView?.SelectedItem != null;
		}

		private void Command_MissingTree_HasSelectedConcernPoint_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = MissingTreeView?.SelectedItem != null
				&& MissingTreeView.SelectedItem is RemapCandidates;
		}

		private void Command_MissingTree_HasSelectedCandidate_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = MissingTreeView?.SelectedItem != null
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

				if (File.Exists(Settings.Default.SettingsFilePath))
				{
					File.WriteAllText(
						Settings.Default.SettingsFilePath,
						SettingsSerializer.Serialize(SettingsObject)
					);
				}
				else
				{
					Settings.Default.SerializedSettings = SettingsSerializer.Serialize(SettingsObject);
					Settings.Default.Save();
				}
			}
		}

		private void ApplyMapping_Click(object sender, RoutedEventArgs e)
		{
			LogAction(() =>
			{
				var searchType = sender == ApplyMappingLocal
					? ContextFinder.SearchType.Local
					: ContextFinder.SearchType.Global;

				ProcessAmbiguities(
					MarkupManager.Remap(
						GetPointSearchArea(searchType)
							.Select(f => TryParse(f, null, out bool success, true))
							.Where(f => f != null)
							.ToList(),
						true,
						searchType
					),
					true
				);
			}, true, false);
		}

		#region Копирование-вставка

		private void Command_Copy_Executed(object sender, RoutedEventArgs e)
		{
			try
			{
				if (MarkupTreeView.IsKeyboardFocusWithin && State.SelectedItem_MarkupTreeView != null)
				{
					State.BufferedDataContext = (MarkupElement)State.SelectedItem_MarkupTreeView.DataContext;
					SetStatus("Элемент скопирован", ControlStatus.Pending);
				}
				else if (RelationSource.IsKeyboardFocusWithin && RelationSource.Tag != null)
				{
					State.BufferedDataContext = (MarkupElement)RelationSource.Tag;
					SetStatus("Элемент скопирован", ControlStatus.Pending);
				}
				else if (RelationTarget.IsKeyboardFocusWithin && RelationTarget.Tag != null)
				{
					State.BufferedDataContext = (MarkupElement)RelationTarget.Tag;
					SetStatus("Элемент скопирован", ControlStatus.Pending);
				}
			}
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		private void Command_Paste_Executed(object sender, RoutedEventArgs e)
		{
			try
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
			catch (Exception ex)
			{
				ShowExceptionWindow(ex);
			}
		}

		#endregion

		#region Methods

		private void LoadFromFile(string fileName)
		{
			MarkupManager.Deserialize(fileName, Parsers.Grammars);
			MarkupFilePath = fileName;

			var stubNode = new Node("");
			stubNode.SetLocation(new PointLocation(0, 0, 0), new PointLocation(0, 0, 0));

			MarkupManager.DoWithMarkup(elem =>
			{
				if (elem is ConcernPoint p)
				{
					p.NodeLocation = new SegmentLocation
					{
						Start = new PointLocation(0, 0, 0),
						End = new PointLocation(0, 0, 0)
					};

					if (p.LineContext != null)
					{
						p.LineLocation = new SegmentLocation
						{
							Start = new PointLocation(0, 0, 0),
							End = new PointLocation(0, 0, 0)
						};
					}

					p.HasIrrelevantLocation = true;
				}
			});

			MarkupTreeView.ItemsSource = MarkupManager.Markup;

			CollapseOrExpand(MarkupTreeView, true);

			SetStatus("Разметка загружена", ControlStatus.Success);
		}

		private List<ConcernPointCandidate> GetConcernPointCandidates(
			ParsedFile file,
			SegmentLocation realSelection,
			SegmentLocation adjustedSelection)
		{
			/// Для выделения находим сущности, объемлющие его
			var candidates = MarkupManager.GetConcernPointCandidates(file.Root, realSelection)
				.Select(c => (ConcernPointCandidate)new ExistingConcernPointCandidate(c))
				.ToList();

			/// Проверяем, можно ли привязаться к строке
			if (adjustedSelection.Start.Line == adjustedSelection.End.Line)
			{
				var candidate = candidates
					.OfType<ExistingConcernPointCandidate>()
					.FirstOrDefault(c => c.Node.Location.Includes(adjustedSelection));

				if (candidate != null)
				{
					candidates.Insert(0, new StringConcernPointCandidate
					{
						Node = candidate.Node,
						Line = adjustedSelection,
						ViewHeader = "строка: "
							+ file.Text.Substring(adjustedSelection.Start.Offset, adjustedSelection.Length.Value).Trim()
					});
				}
			}

			/// Проверяем, можно ли обрамить его кастомным блоком
			if (CustomBlockValidator.IsValid(file.Root, adjustedSelection))
			{
				candidates.Add(new CustomConcernPointCandidate(
					realSelection, adjustedSelection, "Новый пользовательский блок"
				));
			}

			return candidates;
		}

		private void CollapseOrExpand(ItemsControl control, bool expand)
		{
			for (var i = 0; i < control.Items.Count; ++i)
			{
				var treeViewItem = (TreeViewItem)control.ItemContainerGenerator.ContainerFromIndex(i);

				if (treeViewItem != null)
				{
					treeViewItem.IsExpanded = expand;
					treeViewItem.UpdateLayout();

					CollapseOrExpand(treeViewItem, expand);
				}
			}
		}

		private void ShowExceptionWindow(Exception ex)
		{
			var exceptionWindow = new Window_Exception(ex.ToString());
			exceptionWindow.ShowDialog();
		}

		#endregion
	}
}