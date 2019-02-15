using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

		private Brush StdForeground = new SolidColorBrush(Color.FromArgb(70, 100, 100, 100));
		private Brush InForeground = new SolidColorBrush(Color.FromArgb(200, 255, 0, 0));
		private Brush OutForeground = new SolidColorBrush(Color.FromArgb(200, 0, 0, 255));
		private Brush LinkForeground = new SolidColorBrush(Color.FromArgb(200, 10, 10, 10));
		private const double StdWidth = 1.2;
		private const double SelectedWidth = 2;

		private MarkupElement SelectedVertex { get; set; }

		private List<RelationsTreeNode> RelationsTree { get; set; }
		private RelationsManager RelationsManager { get; set; }
		private BidirectionalGraph<object, IEdge<object>> Graph { get; set; }
		private HashSet<RelationType> RelationsSelected { get; set; } = new HashSet<RelationType>();

		public ConcernGraph(RelationsManager relationsManager)
        {
            InitializeComponent();

			RelationsManager = relationsManager;

			/// Заполняем дерево, в котором можно будет выбрать нужные нам отношения
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

			/// Конфигурируем отображение графа
			var layoutParams = new GraphSharp.Algorithms.Layout.Simple.FDP.KKLayoutParameters();
			ConcernGraphLayout.LayoutAlgorithmType = "CompoundFDP";
			layoutParams.AdjustForGravity = true;
			ConcernGraphLayout.LayoutParameters = layoutParams;
		}

		private void RelationCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
		{
			var checkBox = (CheckBox)sender;

			if (checkBox.IsChecked ?? false)
				RelationsSelected.Add((RelationType)checkBox.Tag);
			else
				RelationsSelected.Remove((RelationType)checkBox.Tag);

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
						if (kvp.Value.Count > 0)
						{
							Graph.AddVertex(kvp.Key);

							foreach (var to in kvp.Value)
							{
								Graph.AddVertex(to);
								Graph.AddEdge(new Edge<object>(kvp.Key, to));
							}
						}
					}
				}
			}

			ConcernGraphLayout.Graph = Graph;

			foreach (var vertex in Graph.Vertices)
			{
				var control = ConcernGraphLayout.GetVertexControl(vertex);
				control.PreviewMouseLeftButtonDown += Vertex_PreviewMouseLeftButtonDown;
			}
		}

		private void Vertex_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			var cur = (MarkupElement)((VertexControl)sender).Vertex;

			/// Чтобы не было конфликтов с операциями масштабирования GraphSharp 
			if (Keyboard.Modifiers != ModifierKeys.Alt)
			{
				UnselectVertex(SelectedVertex);
				SelectedVertex = cur;
				SelectVertex(SelectedVertex);
			}
		}

		private void UnselectVertex(object vc)
		{
			ChangeGroup(vc, StdForeground, StdForeground, StdForeground, StdWidth, false);
		}

		private void SelectVertex(object vc)
		{
			ChangeGroup(vc, InForeground, OutForeground, LinkForeground, SelectedWidth, true);
		}

		private void ChangeGroup(object vertexContext, Brush inColor, Brush outColor, Brush linkColor, double thickness, bool select)
		{
			if (vertexContext != null)
			{
				ConcernGraphLayout.Graph.TryGetInEdges(vertexContext, out IEnumerable<IEdge<object>> edges);

				if (edges != null)
				{
					foreach (IEdge<object> edg in edges)
					{
						var curEdge = ConcernGraphLayout.GetEdgeControl(edg);
						curEdge.Foreground = inColor;
						curEdge.StrokeThickness = thickness;
					}
				}

				ConcernGraphLayout.Graph.TryGetOutEdges(vertexContext, out edges);

				if (edges != null)
				{
					foreach (IEdge<object> edg in edges)
					{
						var curEdge = ConcernGraphLayout.GetEdgeControl(edg);
						curEdge.Foreground = outColor;
						curEdge.StrokeThickness = thickness;
					}
				}
			}
		}

	}
}
