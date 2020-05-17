using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Parsing.Tree;

namespace Land.Markup.Binding
{
	public class AncestorCacheElement
	{
		public List<Node> Children { get; set; }
		public ILookup<string, Tuple<int, byte[]>> PreprocessedChildren { get; set; }
	}
}
