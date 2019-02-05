using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.ComponentModel;

using QuickGraph;
using GraphSharp.Controls;

using Land.Core.Markup;

namespace Land.Control
{
    /// <summary>
    /// Логика взаимодействия для ConcernGraph.xaml
    /// </summary>
    public partial class ConcernGraph : Window
    {
		public class RelationsTreeNode
		{
			public string Text { get; set; }
			public RelationType? Relation { get; set; }
			public List<RelationsTreeNode> Children { get; set; }
		}

		private List<RelationsTreeNode> RelationsTree { get; set; }
		private RelationsManager RelationsManager { get; set; }
		private BidirectionalGraph<object, IEdge<object>> Graph { get; set; }
		private HashSet<RelationType> RelationsSelected { get; set; } = new HashSet<RelationType>();

		public ConcernGraph(RelationsManager relationsManager)
        {
            InitializeComponent();

			RelationsManager = relationsManager;

			RelationsTree = ((RelationGroup[])Enum.GetValues(typeof(RelationGroup))).Select(g => new RelationsTreeNode()
			{
				Text = g.GetAttribute<DescriptionAttribute>().Description,
				Relation = null,
				Children = ((RelationType[])Enum.GetValues(typeof(RelationType)))
					.Where(r=>r.ToString().Split('_')[0] == g.ToString()).Select(r => new RelationsTreeNode()
					{
						Text = r.GetAttribute<DescriptionAttribute>().Description,
						Relation = r,
						Children = null
					}).ToList()
			}).ToList();

			RelationsTreeView.ItemsSource = RelationsTree;
		}

		private void RelationCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
		{
			var checkBox = (CheckBox)sender;

			if (checkBox.IsChecked ?? false)
				RelationsSelected.Remove((RelationType)checkBox.Tag);
			else
				RelationsSelected.Add((RelationType)checkBox.Tag);

			RebuildGraph();
		}

		private void RebuildGraph()
		{
			Graph = new BidirectionalGraph<object, IEdge<object>>();

			foreach(var rel in RelationsSelected)
			{
				if(RelationsManager[rel] != null)
				{
					foreach(var kvp in RelationsManager[rel])
					{
						Graph.AddVertex(kvp.Key);

						foreach(var to in kvp.Value)
						{
							Graph.AddVertex(to);
							Graph.AddEdge(new Edge<object>(kvp.Key, to));
						}
					}
				}
			}

			ConcernGraphLayout.Graph = Graph;
		}
	}
}
