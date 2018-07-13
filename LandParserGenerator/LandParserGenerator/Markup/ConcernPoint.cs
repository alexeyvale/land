using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Markup
{
	[DataContract(IsReference=true)]
	public class ConcernPoint: MarkupElement
	{
		[DataMember]
		public Node TreeNode { get; set; }

		[DataMember]
		public int TreeNodeId { get; set; }

		public ConcernPoint(Node node, Concern parent = null)
		{
			TreeNode = node;
			Parent = parent;
			Name = String.IsNullOrEmpty(node.Alias) ? node.Symbol : node.Alias;

			if (node.Value.Count > 0)
				Name += ": " + String.Join(" ", node.Value);
			else
			{
				if (node.Children.Count > 0)
				{
					Name += ": " + String.Join(" ", node.Children.SelectMany(c => c.Value.Count > 0 ? c.Value
						: new List<string>() { '"' + (String.IsNullOrEmpty(c.Alias) ? c.Symbol : c.Alias) + '"' }));
				}
			}
		}

		public ConcernPoint(string name, Node node, Concern parent = null)
		{
			Name = name;
			TreeNode = node;
			Parent = parent;
		}
	}
}
