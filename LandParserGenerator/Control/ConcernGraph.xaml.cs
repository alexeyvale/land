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
		#region Статус

		private Brush LightRed { get; set; } = new SolidColorBrush(Color.FromRgb(255, 200, 200));

		private enum ControlStatus { Ready, Pending, Success, Error }

		private void SetStatus(string text, ControlStatus status)
		{
			switch (status)
			{
				case ControlStatus.Error:
					ConcernGraphStatusBar.Background = LightRed;
					break;
				case ControlStatus.Pending:
					ConcernGraphStatusBar.Background = Brushes.LightGoldenrodYellow;
					break;
				case ControlStatus.Ready:
					ConcernGraphStatusBar.Background = Brushes.LightBlue;
					break;
				case ControlStatus.Success:
					ConcernGraphStatusBar.Background = Brushes.LightGreen;
					break;
			}

			ConcernGraphStatus.Content = text;
		}

		#endregion

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

		public static readonly Brush EdgeStdForeground = new SolidColorBrush(Color.FromArgb(60, 150, 150, 150));
		public static readonly Brush EdgeInForeground = new SolidColorBrush(Color.FromArgb(200, 255, 0, 0));
		public static readonly Brush EdgeOutForeground = new SolidColorBrush(Color.FromArgb(200, 0, 0, 255));

		public static readonly Brush VertexHiddenForeground = new SolidColorBrush(Color.FromArgb(60, 150, 150, 150));
		public static readonly Brush VertexStdForeground = new SolidColorBrush(Color.FromArgb(250, 0, 0, 0));

		public const double EdgeStdWidth = 1.4;
		public const double EdgeSelectedWidth = 2;

		private MarkupElement SelectedVertex { get; set; }

		private List<RelationsTreeNode> RelationsTree { get; set; }
		private MarkupManager MarkupManager { get; set; }
		private BidirectionalGraph<object, IEdge<object>> Graph { get; set; }
		private HashSet<RelationType> RelationsSelected { get; set; } = new HashSet<RelationType>();

		public ConcernGraph(MarkupManager markup)
        {
            InitializeComponent();

			MarkupManager = markup;

			/// Заполняем дерево, в котором можно будет выбрать нужные нам отношения
			RelationsTree = new List<RelationsTreeNode>
			{
				new RelationsTreeNode()
				{
					Text = RelationGroup.Internal.GetDescription(),
					Children = new List<RelationsTreeNode>()
					{
						new RelationsTreeNode(RelationType.Preceeds),
						new RelationsTreeNode(RelationType.Follows),
						new RelationsTreeNode(RelationType.IsLogicalChildOf),
						new RelationsTreeNode(RelationType.IsLogicalDescendantOf),
						new RelationsTreeNode(RelationType.IsLogicalParentOf),
						new RelationsTreeNode(RelationType.IsLogicalAncestorOf),
						new RelationsTreeNode(RelationType.IsPhysicalDescendantOf),
						new RelationsTreeNode(RelationType.IsPhysicalAncestorOf),
					}
				},

				new RelationsTreeNode()
				{
					Text = RelationGroup.External.GetDescription(),
					Children = ((RelationType[])Enum.GetValues(typeof(RelationType)))
						.Where(r=>r.GetGroup() == RelationGroup.External)
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

		private RelationsManager TryGetRelations()
		{
			if (!MarkupManager.IsValid)
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

		private void RebuildGraph()
		{
			var relationsManager = TryGetRelations();

			if (relationsManager != null)
			{
				Graph = new BidirectionalGraph<object, IEdge<object>>(true);

				foreach (var rel in RelationsSelected)
				{
					var relations = new HashSet<RelatedPair<MarkupElement>>(
						relationsManager.InternalRelations.GetRelatedPairs(rel)
					);

					relations.UnionWith(
						relationsManager.ExternalRelations.GetRelatedPairs(rel)
					);

					foreach (var pair in relations)
					{
						Graph.AddVertex(pair.Item0);
						Graph.TryGetEdge(pair.Item0, pair.Item1, out IEdge<object> edge);

						if (edge != null)
						{
							((InfoProvidedEdge)edge).AddRelation(pair.RelationType);
						}
						else
						{
							Graph.AddVertex(pair.Item1);
							Graph.AddEdge(new InfoProvidedEdge(pair.Item0, pair.Item1, rel));
						}
					}
				}

				ConcernGraphLayout.Graph = Graph;

				foreach (var vertex in Graph.Vertices)
				{
					var control = ConcernGraphLayout.GetVertexControl(vertex);
					control.PreviewMouseLeftButtonDown += Vertex_PreviewMouseLeftButtonDown;
				}

				SetStatus(String.Empty, ControlStatus.Success);
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
