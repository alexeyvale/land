using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Markup
{
	[DataContract]
	public class MarkupManager
	{
		[DataMember]
		public List<MarkupElement> Markup { get; set; } = new List<MarkupElement>();

		public Node AstRoot { get; set; } = null;

		public void Clear()
		{
			Markup = new List<MarkupElement>();
			AstRoot = null;
		}

		public void Remove(MarkupElement elem)
		{
			if (elem.Parent != null)
				elem.Parent.Elements.Remove(elem);
			else
				Markup.Remove(elem);
		}

		public void Add(MarkupElement elem)
		{
			if (elem.Parent == null)
				Markup.Add(elem);
			else
				elem.Parent.Elements.Add(elem);
		}

		public void Remap(Node newRoot, Dictionary<Node, Node> mapping)
		{
			AstRoot = newRoot;
			for (int i = 0; i < Markup.Count; ++i)
			{
				ConcernPoint point = Markup[i] as ConcernPoint;
				if (point != null)
				{
					if (mapping.ContainsKey(point.TreeNode))
						point.TreeNode = mapping[point.TreeNode];
					else
					{
						Markup.RemoveAt(i);
						--i;
					}
				}
			}
		}
	}
}
