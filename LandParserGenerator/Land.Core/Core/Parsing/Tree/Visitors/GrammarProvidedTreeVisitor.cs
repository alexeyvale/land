using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Specification;

namespace Land.Core.Parsing.Tree
{
	public abstract class GrammarProvidedTreeVisitor : BaseTreeVisitor
	{
		protected Grammar GrammarObject { get; set; }

		public GrammarProvidedTreeVisitor(Grammar grammarObject)
		{
			GrammarObject = grammarObject;
		}
	}
}
