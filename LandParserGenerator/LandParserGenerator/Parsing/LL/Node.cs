using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.LL
{
	public class Node
	{
		public string Symbol { get; private set; }
		public LinkedList<Node> Children { get; private set; }

		public int Line { get; private set; }
		public int Column { get; private set; }

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

		public void SetAnchor(int line, int col)
		{
			Line = line;
			Column = col;
		}
	}
}
