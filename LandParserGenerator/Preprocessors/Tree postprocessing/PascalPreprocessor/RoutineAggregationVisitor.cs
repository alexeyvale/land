using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core;
using Land.Core.Parsing.Preprocessing;
using Land.Core.Parsing.Tree;

using pascalabc_declarations;

namespace PascalPreprocessing.TreePostprocessing
{
	public class RoutineAggregationVisitor: BaseTreeVisitor
	{
		private int InClass { get; set; } = 0;
		private bool InInterface { get; set; } = false;
		private routine_node AggregaredRoutine { get; set; } = null;

		public void Visit(class_node node)
		{
			++InClass;
			base.Visit(node);
			--InClass;
		}

		public void Visit(routine_node node)
		{
			if (node.Children.FirstOrDefault() is routine_header_node)
			{
				AggregaredRoutine = node;
			}
			else if (AggregaredRoutine != null)
			{
				node.Parent.Children.Remove(node);
				AggregaredRoutine.Children.AddRange(node.Children);
				foreach (var child in node.Children)
					child.Parent = AggregaredRoutine;
			}

			if(node.Children.LastOrDefault() is routine_init_node
				|| node.Children.FirstOrDefault() is ROUTINE_MODIFIER_node 
				&& (node.Children.First().Value[0] == "forward" || node.Children.First().Value[0] == "extern"))
				AggregaredRoutine = null;
		}

		public void Visit(block_node node)
		{
			if (AggregaredRoutine != null)
			{
				node.Parent.Children.Remove(node);
				AggregaredRoutine.Children.Add(node);
				node.Parent = AggregaredRoutine;

				AggregaredRoutine = null;
			}
		}

		public override void Visit(Node node)
		{
			if (node.Value.Count == 1)
				switch(node.Value[0])
				{
					case "interface":
						InInterface = true;
						break;
					case "implementation":
						AggregaredRoutine = null;
						InInterface = false;
						break;
				}

			if(AggregaredRoutine != null)
			{
				node.Parent.Children.Remove(node);
				AggregaredRoutine.Children.Add(node);
				node.Parent = AggregaredRoutine;
			}
		}
	}
}
