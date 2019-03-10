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
		private void MissingTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			TreeViewItem item = VisualUpwardSearch(e.OriginalSource as DependencyObject);

			if (item == null)
			{
				if (State.SelectedItem_MissingTreeView != null)
				{
					State.SelectedItem_MissingTreeView.IsSelected = false;

					MissingTreeView.Focus();
				}
			}
		}

		private void RefreshMissingPointsList()
		{
			var missingPoints = MarkupManager.GetConcernPoints()
				.Where(p => p.HasMissingLocation)
				.Select(p => new PointCandidatesPair() { Point = p })
				.ToList();

			foreach (PointCandidatesPair pair in missingPoints)
			{
				if (State.RecentAmbiguities.ContainsKey(pair.Point))
					State.RecentAmbiguities[pair.Point].ForEach(a => pair.Candidates.Add(a));
			}

			MissingTreeView.ItemsSource = missingPoints;
		}

		private void ProcessAmbiguities(Dictionary<ConcernPoint, List<CandidateInfo>> recentAmbiguities, bool globalRemap)
		{
			if (globalRemap)
				State.RecentAmbiguities = recentAmbiguities;
			else
			{
				foreach (var kvp in recentAmbiguities)
					State.RecentAmbiguities[kvp.Key] = kvp.Value;
			}

			foreach (PointCandidatesPair existingAmbiguityInfo in MissingTreeView.ItemsSource)
			{
				if (recentAmbiguities.ContainsKey(existingAmbiguityInfo.Point))
				{
					existingAmbiguityInfo.Candidates.Clear();
					recentAmbiguities[existingAmbiguityInfo.Point].ForEach(a => existingAmbiguityInfo.Candidates.Add(a));
				}
			}

			if (MissingTreeView.Items.Count > 0)
			{
				Tabs.SelectedItem = MissingPointsTab;
				SetStatus("Не удалось перепривязать некоторые точки", ControlStatus.Error);
			}
		}

		private void MissingTreeViewItem_Selected(object sender, RoutedEventArgs e)
		{
			State.SelectedItem_MissingTreeView = (TreeViewItem)sender;

			e.Handled = true;
		}

		private void MissingTreeViewItem_Unselected(object sender, RoutedEventArgs e)
		{
			State.SelectedItem_MissingTreeView = null;

			e.Handled = true;
		}

		private void MissingTreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			TreeViewItem item = VisualUpwardSearch(e.OriginalSource as DependencyObject);

			if (item != null && e.ChangedButton == MouseButton.Left)
			{
				if (item.DataContext is CandidateInfo pair)
				{
					Editor.SetActiveDocumentAndOffset(
						pair.Context.FileName,
						pair.Node.Anchor.Start
					);

					e.Handled = true;
				}
			}
		}
	}
}