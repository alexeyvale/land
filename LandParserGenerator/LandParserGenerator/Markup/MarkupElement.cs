using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Markup
{
	public abstract class MarkupElement
	{
		public string Name { get; set; }

		public Concern Parent { get; set; }
	}
}
