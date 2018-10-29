using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Land.Core;
using Land.Core.Parsing.Tree;

namespace SharpPreprocessing.TreePostprocessing
{
	public class EntityIdentificationVisitor : BaseTreeVisitor
	{
		public List<Message> Log { get; set; } = new List<Message>();
		private List<Tuple<Node, List<Node>>> ToAggregate { get; set; } = new List<Tuple<Node, List<Node>>>();

		public override void Visit(Node node)
		{
			switch (node.Type)
			{
				case "class_struct_interface":
					var keywordIdx = node.Children
						.Select((child, index) => new { child, index })
						.First(p=>p.child.Type == "CLASS_STRUCT_INTERFACE").index;

					var className = node.Children[keywordIdx + 1];
					className.Symbol = "name";
					className.Alias = null;

					break;
				case "member":
					var possibleArgs = node.Children
						.Select((child, index) => new { child, index })
						.LastOrDefault(p => p.child.Type == "tuple_or_arguments");

					if(possibleArgs != null 
						&& (node.Children[possibleArgs.index + 1].Type != "header_element" || node.Children[possibleArgs.index + 1].Value[0] == "where"))
					{
						var methodName = node.Children[possibleArgs.index - 1];
						methodName.Symbol = "name";
						methodName.Alias = null;

						node.Symbol = "method";
						node.Alias = null;
					}

					break;
			}

			base.Visit(node);
		}
	}
}
