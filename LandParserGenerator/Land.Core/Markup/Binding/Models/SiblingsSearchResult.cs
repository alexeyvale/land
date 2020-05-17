using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Parsing.Tree;

namespace Land.Markup.Binding
{
	public class SiblingsSearchResult
	{
		public int? BeforeSiblingOffset { get; set; }
		public int? AfterSiblingOffset { get; set; }
	}
}
