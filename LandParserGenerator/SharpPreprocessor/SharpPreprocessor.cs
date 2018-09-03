using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Land.Core;
using Land.Core.Parsing;
using Land.Core.Parsing.Tree;

using sharp_preprocessor;

namespace SharpPreprocessor
{
    public class SharpPreprocessor: BasePreprocessor
    {
		public class DirectivesVisitor: BaseTreeVisitor
		{
			public List<int> LinesToComment = new List<int>();

			public HashSet<string> SymbolsDefined = new HashSet<string>();

			public override void Visit(Node node)
			{
				switch(node.Symbol)
				{
					case "if":
						
						break;
					case "define":
						for (var i = 1; i < node.Children.Count; ++i)
							SymbolsDefined.Add(node.Children[i].Symbol);
						break;
					case "undef":
						for (var i = 1; i < node.Children.Count; ++i)
							SymbolsDefined.Remove(node.Children[i].Symbol);
						break;
				}
			}
		}

		private BaseParser Parser { get; set; }
		public List<Message> Log { get { return Parser?.Log; } }

		public SharpPreprocessor()
		{
			Parser = ParserProvider.GetParser();
		}

		public override string Preprocess(string text)
		{
			var root = Parser.Parse(text);

			foreach (var rec in Log)
				rec.Source = "Preprocessor";

			return text;
		}

		public override void Postprocess(Node root, List<Message> log)
		{
			
		}
	}
}
