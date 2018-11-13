using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;
using Land.Core.Parsing.Tree;
using Land.Core.Parsing.Preprocessing;

namespace Land.Core.Parsing
{
	public abstract class BaseParser: IGrammarProvided
	{
		protected ILexer Lexer { get; set; }
		protected BaseNodeGenerator NodeGenerator { get; set; }
		protected BaseNodeRetypingVisitor NodeRetypingVisitor { get; set; }

		public Grammar GrammarObject { get; protected set; }
		private BasePreprocessor Preproc { get; set; }

		public Statistics Statistics { get; set; }
		public List<Message> Log { get; protected set; }

		public BaseParser(Grammar g, ILexer lexer, BaseNodeGenerator nodeGen = null, BaseNodeRetypingVisitor retypeVisitor = null)
		{
			GrammarObject = g;
			Lexer = lexer;

			NodeGenerator = nodeGen 
				?? new BaseNodeGenerator(g);
			NodeRetypingVisitor = retypeVisitor 
				?? new BaseNodeRetypingVisitor(g);
		}

		public Node Parse(string text, bool enableTracing = false)
		{
			Log = new List<Message>();
			Statistics = new Statistics();

			var parsingStarted = DateTime.Now;
			Node root = null;

			/// Если парсеру передан препроцессор
			if (Preproc != null)
			{
				/// Предобрабатываем текст
				text = Preproc.Preprocess(text, out bool success);

				/// Если препроцессор сработал успешно, можно парсить
				if (success)
				{
					root = ParsingAlgorithm(text, enableTracing);
					Preproc.Postprocess(root, Log);
				}
				else
				{
					Log.AddRange(Preproc.Log);
				}
			}
			else
			{
				root = ParsingAlgorithm(text, enableTracing);
			}

			Statistics.TimeSpent = DateTime.Now - parsingStarted;

			return root;
		}

		protected abstract Node ParsingAlgorithm(string text, bool enableTracing);

		public void SetPreprocessor(BasePreprocessor preproc)
		{
			Preproc = preproc;
		}

		protected void TreePostProcessing(Node root)
		{
			root.Accept(new RemoveAutoVisitor(GrammarObject));
			root.Accept(new GhostListOptionProcessingVisitor(GrammarObject));
			root.Accept(new LeafOptionProcessingVisitor(GrammarObject));
			root.Accept(new MergeAnyVisitor(GrammarObject));
			root.Accept(new MappingOptionsProcessingVisitor(GrammarObject));

			NodeRetypingVisitor.Root = root;
			root.Accept(NodeRetypingVisitor);
			root = NodeRetypingVisitor.Root;

			root.Accept(new UserifyVisitor(GrammarObject));
		}
	}
}
