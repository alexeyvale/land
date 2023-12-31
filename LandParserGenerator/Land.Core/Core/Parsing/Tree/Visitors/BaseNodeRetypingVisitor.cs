﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Land.Core.Specification;
using Land.Core.Lexing;

namespace Land.Core.Parsing.Tree
{
	public class BaseNodeRetypingVisitor: GrammarProvidedTreeVisitor
	{
		public Node Root { get; set; }

		public BaseNodeRetypingVisitor(Grammar grammar): base(grammar) { }

		public override void Visit(Node node)
		{ }
	}
}
