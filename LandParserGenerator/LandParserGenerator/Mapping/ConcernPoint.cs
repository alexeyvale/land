using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Mapping
{
	public class ConcernPoint
	{
		public string Name { get; set; }

		public Node TreeNode { get; set; }

		// public List<ConcernPoint> Children { get; set; }

		public ConcernPoint(Node node)
		{
			TreeNode = node;
		}

		public ConcernPoint(string name, Node node)
		{
			Name = name;
			TreeNode = node;
		}
	}
}
