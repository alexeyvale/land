using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	[DataContract(IsReference = true)]
	public class ConcernPoint: MarkupElement
	{
		[DataMember]
		public PointContext Context { get; set; }

		public SegmentLocation Location { get; set; }

		public ConcernPoint(string fileName, Node node, Concern parent = null)
		{
			Context = PointContext.Create(fileName, node);

			Location = node.Anchor;
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

		public ConcernPoint(string name, string fileName, Node node, Concern parent = null)
		{
			Name = name;
			Context = PointContext.Create(fileName, node);
			Parent = parent;
		}

		public void Relink(string fileName, Node node)
		{
			Location = node.Anchor;
			Context = PointContext.Create(fileName, node);
		}

		public override void Accept(BaseMarkupVisitor visitor)
		{
			visitor.Visit(this);
		}
	}
}
