using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Land.Core.Markup;

namespace Land.Control
{
	public class RelationsTreeNode
	{
		public string Text { get; set; }
		public RelationType? Relation { get; set; }
		public List<RelationsTreeNode> Children { get; set; }

		public RelationsTreeNode() { }

		public RelationsTreeNode(RelationType relation)
		{
			Text = relation.GetDescription();
			Relation = relation;
		}
	}
}
