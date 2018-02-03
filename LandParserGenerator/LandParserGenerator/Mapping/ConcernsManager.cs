using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Mapping
{
	public class ConcernsManager
	{
		public List<ConcernPoint> Concerns { get; set; } = new List<ConcernPoint>();

		public Node AstRoot { get; set; } = null;

		public void Remap(Node newRoot, Dictionary<Node, Node> mapping)
		{
			AstRoot = newRoot;
			for (int i = 0; i < Concerns.Count; ++i)
			{
				if (mapping.ContainsKey(Concerns[i].TreeNode))
					Concerns[i].TreeNode = mapping[Concerns[i].TreeNode];
				else
				{
					Concerns.RemoveAt(i);
					--i;
				}
			}
		}
	}
}
