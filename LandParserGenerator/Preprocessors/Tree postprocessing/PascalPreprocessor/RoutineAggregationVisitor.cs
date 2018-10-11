using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Land.Core;
using Land.Core.Parsing.Tree;

namespace PascalPreprocessing.TreePostprocessing
{
	public class RoutineAggregationVisitor : BaseTreeVisitor
	{
		private Dictionary<Node, List<Node>> Aggregated { get; set; } = new Dictionary<Node, List<Node>>();

		private Node CurrentRoutine { get; set; } = null;

		public override void Visit(Node node)
		{
			switch (node.Type)
			{
				case "in_class_routine":
					CurrentRoutine = node;
					Aggregated[node] = new List<Node>();
					break;
				case "in_class_routine_tail":
					Aggregated[CurrentRoutine].Add(node);
					if (node.Children
						.Any(c => c.Type == "block" || c.Type == "routine_init"))
						CurrentRoutine = null;
					break;
				case "file":
					base.Visit(node);

					/// Отложенное выполнение всех перестановок,
					/// связанных со сбором частей описания метода
					foreach (var kvp in Aggregated)
						foreach (var nd in kvp.Value)
							Aggregate(kvp.Key, nd);
					break;
				default:
					if (CurrentRoutine != null && node.Parent == CurrentRoutine.Parent)
						Aggregated[CurrentRoutine].Add(node);
					else
						base.Visit(node);
					break;
			}
		}

		private void Aggregate(Node routine, Node child)
		{
			child.Parent.Children.Remove(child);
			routine.AddLastChild(child);
		}
	}
}
