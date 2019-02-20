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
		public class InfoProvidedEdge : IEdge<object>
		{
			public object Source { get; set; }

			public object Target { get; set; }

			private HashSet<RelationType> Relations { get; set; }

			public string Info => String.Join($";{Environment.NewLine}", Relations
				.Select(r => r.GetAttribute<DescriptionAttribute>().Description));

			public InfoProvidedEdge(MarkupElement source, MarkupElement target, RelationType relation)
			{
				Source = source;
				Target = target;

				Relations = new HashSet<RelationType>{ relation };
			}

			public void AddRelation(RelationType relation)
			{
				Relations.Add(relation);
			}

			public void RemoveRelation(RelationType relation)
			{
				Relations.Remove(relation);
			}

			public int Count => Relations.Count;

			public override string ToString()
			{
				return String.Empty;
			}
		}

		public static readonly Brush EdgeStdForeground = new SolidColorBrush(Color.FromArgb(60, 170, 170, 170));
		public static readonly Brush EdgeInForeground = new SolidColorBrush(Color.FromArgb(200, 255, 0, 0));
		public static readonly Brush EdgeOutForeground = new SolidColorBrush(Color.FromArgb(200, 0, 0, 255));

		public static readonly Brush VertexHiddenForeground = new SolidColorBrush(Color.FromArgb(150, 10, 10, 10));
		public static readonly Brush VertexStdForeground = new SolidColorBrush(Color.FromArgb(250, 0, 0, 0));

		public const double EdgeStdWidth = 1.4;
		public const double EdgeSelectedWidth = 2;

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
			RelationsTree = new List<RelationsTreeNode>
			{
				new RelationsTreeNode()
				{
					Text = RelationGroup.Internal.GetDescription(),
					Children = new List<RelationsTreeNode>()
					{
						new RelationsTreeNode(RelationType.Internal_Preceeds),
						new RelationsTreeNode(RelationType.Internal_Follows),
						new RelationsTreeNode(RelationType.Internal_IsLogicalChildOf),
						new RelationsTreeNode(RelationType.Internal_IsLogicalDescendantOf),
						new RelationsTreeNode(RelationType.Internal_IsLogicalParentOf),
						new RelationsTreeNode(RelationType.Internal_IsLogicalAncestorOf),
						new RelationsTreeNode(RelationType.Internal_IsPhysicalDescendantOf),
						new RelationsTreeNode(RelationType.Internal_IsPhysicalAncestorOf),
					}
				},

				new RelationsTreeNode()
				{
					Text = RelationGroup.External.GetDescription(),
					Children = ((RelationType[])Enum.GetValues(typeof(RelationType)))
						.Where(r=>r.ToString().Split('_')[0] == RelationGroup.External.ToString())
						.Select(r => new RelationsTreeNode()
						{
							Text = r.GetAttribute<DescriptionAttribute>().Description,
							Relation = r,
							Children = null
						}).ToList()
				}
			};

			RelationsTreeView.ItemsSource = RelationsTree;

			/// Конфигурируем отображение графа
			var layoutParams = new GraphSharp.Algorithms.Layout.Simple.FDP.KKLayoutParameters
			{
				AdjustForGravity = true
			};

			ConcernGraphLayout.LayoutAlgorithmType = "KK";
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
			Graph = new BidirectionalGraph<object, IEdge<object>>(true);

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
								Graph.TryGetEdge(kvp.Key, to, out IEdge<object> edge);

								if (edge != null)
								{
									((InfoProvidedEdge)edge).AddRelation(rel);
								}
								else
								{
									Graph.AddVertex(to);
									Graph.AddEdge(new InfoProvidedEdge(kvp.Key, to, rel));
								}
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
			ChangeGroup(vc, false);
		}

		private void SelectVertex(object vc)
		{
			ChangeGroup(vc, true);
		}

		private void ChangeGroup(object vertexContext, bool select)
		{
			if (vertexContext != null)
			{
				if(!select)
				{
					foreach (var vertex in Graph.Vertices)
					{
						var control = ConcernGraphLayout.GetVertexControl(vertex);
						control.Foreground = VertexStdForeground;
					}
				}

				var groupVertices = new HashSet<object> { vertexContext };

				ConcernGraphLayout.Graph.TryGetInEdges(vertexContext, out IEnumerable<IEdge<object>> edges);

				if (edges != null)
				{
					foreach (IEdge<object> edg in edges)
					{
						var curEdge = ConcernGraphLayout.GetEdgeControl(edg);
						curEdge.Foreground = select ? EdgeInForeground : EdgeStdForeground;
						curEdge.StrokeThickness = select ? EdgeSelectedWidth : EdgeStdWidth;

						groupVertices.Add(edg.Source);
					}
				}

				ConcernGraphLayout.Graph.TryGetOutEdges(vertexContext, out edges);

				if (edges != null)
				{
					foreach (IEdge<object> edg in edges)
					{
						var curEdge = ConcernGraphLayout.GetEdgeControl(edg);
						curEdge.Foreground = select ? EdgeOutForeground : EdgeStdForeground;
						curEdge.StrokeThickness = select ? EdgeSelectedWidth : EdgeStdWidth;

						groupVertices.Add(edg.Target);
					}
				}

				if (select)
				{
					foreach (var vertex in Graph.Vertices.Except(groupVertices))
					{
						var control = ConcernGraphLayout.GetVertexControl(vertex);
						control.Foreground = VertexHiddenForeground;
					}
				}
			}
		}

		private void ConcernGraphZoom_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left && SelectedVertex != null)
				UnselectVertex(SelectedVertex);
		}
	}
}
