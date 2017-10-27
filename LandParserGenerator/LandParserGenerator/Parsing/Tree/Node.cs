using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.Tree
{
	public class Node
	{
		public string Symbol { get; private set; }
		public LinkedList<Node> Children { get; private set; }

		private Location Anchor { get; set; }
		private bool AnchorReady { get; set; }

		public int? StartOffset
		{
			get
			{
				if (Anchor == null && !AnchorReady)
					GetAnchorFromChildren();
				return Anchor?.StartOffset;
			}
		}
		public int? EndOffset
		{
			get
			{
				if (Anchor == null && !AnchorReady)
					GetAnchorFromChildren();
				return Anchor?.EndOffset;
			}
		}

		private void GetAnchorFromChildren()
		{
			if (Children.Count > 0)
			{
				Anchor = Children.First.Value.Anchor;

				foreach (var child in Children)
				{
					if (child.Anchor == null)
						child.GetAnchorFromChildren();

					if (Anchor == null)
						Anchor = child.Anchor;
					else
						Anchor = Anchor.Merge(child.Anchor);
				}
			}

			AnchorReady = true;
		}

		public Node(string smb)
		{
			Symbol = smb;
			Children = new LinkedList<Node>();
		}

		public void AddChildLast(Node child)
		{
			Children.AddLast(child);
		}

		public void AddChildFirst(Node child)
		{
			Children.AddFirst(child);
		}

		public void SetAnchor(int start, int end)
		{
			AnchorReady = true;

			Anchor = new Location()
			{
				StartOffset = start,
				EndOffset = end
			};
		}
	}
}
