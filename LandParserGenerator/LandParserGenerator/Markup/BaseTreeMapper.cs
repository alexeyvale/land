using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public abstract class BaseTreeMapper
	{
		public Dictionary<Node, Node> Mapping { get; protected set; }
		public Dictionary<Node, Dictionary<Node, double>> Similarities { get; protected set; }

		public abstract void Remap(Node oldTree, Node newTree);
	}
}
