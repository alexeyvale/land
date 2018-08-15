using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	[Serializable]
	public abstract class MarkupElement
	{
		public string Name { get; set; }

		public Concern Parent { get; set; }
	}
}
