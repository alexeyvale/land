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
using Land.Markup;
using Land.Markup.Relations;
using System.ComponentModel;

namespace Land.Control
{
	public partial class LandExplorerControl : UserControl, INotifyPropertyChanged
	{
		private void RelationParticipants_Swap_Click(object sender, RoutedEventArgs e)
		{
			var tmp = RelationSource.Tag;

			RelationSource.Tag = RelationTarget.Tag;
			RelationTarget.Tag = tmp;

			RefreshRelationCandidates();
		}

		private void RelationSource_Reset_Click(object sender, RoutedEventArgs e)
		{
			RelationSource.Tag = null;
			RefreshRelationCandidates();
		}

		private void RelationTarget_Reset_Click(object sender, RoutedEventArgs e)
		{
			RelationTarget.Tag = null;
			RefreshRelationCandidates();
		}

		private void RelationCancelButton_Click(object sender, RoutedEventArgs e)
		{
			RelationSource_Reset_Click(null, null);
			RelationTarget_Reset_Click(null, null);
			RefreshRelationCandidates();

			SetStatus("Добавление отношения отменено", ControlStatus.Ready);
		}

		private void RelationSaveButton_Click(object sender, RoutedEventArgs e)
		{
			if (RelationSource.Tag is MarkupElement source
				&& RelationTarget.Tag is MarkupElement target)
			{
				var relationsManager = TryGetRelations();

				if (relationsManager != null)
				{
					foreach (RelationsTreeNode rel in RelationCandidatesList.ItemsSource)
					{
						if(rel.IsSelected)
							relationsManager.AddExternalRelation(rel.Relation.Value, source, target);
						else
							relationsManager.RemoveExternalRelation(rel.Relation.Value, source, target);
					}

					SetStatus("Отношение добавлено", ControlStatus.Success);
				}
			}
		}

		private void RefreshRelationCandidates()
		{
			if (RelationTarget.Tag == null || RelationSource.Tag == null)
				RelationCandidatesList.ItemsSource = null;
			else
			{
				var relationsManager = TryGetRelations();

				if (relationsManager != null)
				{
					var from = (MarkupElement)RelationSource.Tag;
					var to = (MarkupElement)RelationTarget.Tag;

					RelationCandidatesList.ItemsSource = 
						relationsManager.GetPossibleExternalRelations(from, to)
							.Select(r => new RelationsTreeNode(r, relationsManager.AreRelated(from, to, r)))
							.ToList();
				}
			}
		}

		private RelationsManager TryGetRelations()
		{
			if(!MarkupManager.IsValid)
			{
				SetStatus(
					"Для доступа к информации об отношениях необходимо запустить перепривязку точек, соответствующих изменившемуся тексту",
					ControlStatus.Error
				);
			}
			else
			{
				MarkupManager.TryGetRelations(out RelationsManager manager);

				if (manager != null)
				{
					return manager;
				}
				else
				{
					SetStatus(
						"Не удалось получить информацию об отношениях",
						ControlStatus.Error
					);
				}
			}

			return null;
		}
	}
}