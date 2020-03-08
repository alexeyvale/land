using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Specification;
using Land.Core.Parsing.Tree;
using Land.Markup.Binding;

namespace Land.Markup.CoreExtension
{
	public class ContextCachingNode : Node
	{
		public List<HeaderContextElement> HeaderContext { get; set; }

		public ContextCachingNode(
			string symbol, 
			SymbolOptionsManager opts = null, 
			SymbolArguments args = null) : base(symbol, opts, args) { }

		public ContextCachingNode(Node node) : base(node) { }
	}
}
