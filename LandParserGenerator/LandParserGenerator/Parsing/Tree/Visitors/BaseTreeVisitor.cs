using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;
using Land.Core.Parsing.Tree;

namespace Land.Core.Parsing
{
	public abstract class BaseTreeVisitor
	{
		public virtual void Visit(Node node)
		{
			foreach (var child in node.Children)
				Visit(child);
		}
	}
}
