using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing
{
	public abstract class BaseVisitor
	{
		public virtual void Visit(Node node)
		{
			foreach (var child in node.Children)
				Visit(child);
		}
	}
}
