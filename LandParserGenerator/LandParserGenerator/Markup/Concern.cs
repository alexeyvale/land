using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Markup
{
	public class Concern: MarkupElement
	{
		public List<MarkupElement> Elements { get; set; }

		public Concern(string name, Concern parent = null)
		{
			Name = name;
			Parent = parent;
			Elements = new List<MarkupElement>();
		}
	}
}
