using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.Tree
{
	public class Node
	{
		public string Symbol { get; set; }
		public List<Node> Children { get; private set; }

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
				Anchor = Children[0].Anchor;

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
			Children = new List<Node>();
		}

		public void AddLastChild(Node child)
		{
			Children.Add(child);
		}

		public void AddFirstChild(Node child)
		{
			Children.Insert(0, child);
		}

		public void ResetChildren()
		{
			Children = new List<Node>();
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

		public void Accept(BaseVisitor visitor)
		{
			visitor.Visit(this);
		}
	}
}
