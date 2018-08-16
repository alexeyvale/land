using System;
using System.Collections.Generic;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public class BaseMarkupVisitor
	{
		public virtual void Visit(Concern concern)
		{
			foreach (var child in concern.Elements)
				child.Accept(this);
		}

		public virtual void Visit(ConcernPoint point) { }
	}
}
