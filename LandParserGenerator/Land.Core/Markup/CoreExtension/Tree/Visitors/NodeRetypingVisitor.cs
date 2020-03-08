using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Specification;
using Land.Core.Parsing.Tree;

namespace Land.Markup.CoreExtension
{
	public class NodeRetypingVisitor : BaseNodeRetypingVisitor
	{
		public NodeRetypingVisitor(Grammar grammar) : base(grammar) { }

		public override void Visit(Node node)
		{
			if(node.Parent == null)
			{
				var newNode = new ContextCachingNode(node);
				Root = node = newNode;
			}

			for(var i=0; i<node.Children.Count; ++i)
			{
				var newChild = new ContextCachingNode(node.Children[i]);
				newChild.Parent = node;

				node.Children.RemoveAt(i);
				node.Children.Insert(i, newChild);
			}

			foreach (var child in node.Children)
			{
				child.Accept(this);
			}
		}
	}
}
