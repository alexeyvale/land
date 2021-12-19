using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Specification;
using Land.Core.Lexing;
using Land.Core.Parsing.Tree;
using Land.Core.Parsing.Preprocessing;

namespace Land.Core.Parsing
{
	public abstract class BaseParser: MarshalByRefObject, IGrammarProvided
	{
		public Grammar GrammarObject { get; protected set; }

		protected ILexer Lexer { get; set; }
		public ComplexTokenStream LexingStream { get; protected set; }

		protected BaseNodeGenerator NodeGenerator { get; set; }
		protected BaseNodeRetypingVisitor NodeRetypingVisitor { get; set; }
		
		private BasePreprocessor Preproc { get; set; }
		private List<Func<Grammar, GrammarProvidedTreeVisitor>> VisitorConstructors { get; set; } = new List<Func<Grammar, GrammarProvidedTreeVisitor>>();

		public Statistics Statistics { get; set; }
		public List<Message> Log { get; protected set; }
		protected bool EnableTracing { get; set; }

		public BaseParser(
			Grammar g, 
			ILexer lexer, 
			BaseNodeGenerator nodeGen = null, 
			BaseNodeRetypingVisitor retypeVisitor = null)
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
			EnableTracing = enableTracing;

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
					root = ParsingAlgorithm(text);
					Preproc.Postprocess(root, Log);
				}
				else
				{
					Log.AddRange(Preproc.Log);
				}
			}
			else
			{
				root = ParsingAlgorithm(text);
			}

			Statistics.GeneralTimeSpent = DateTime.Now - parsingStarted;
			Statistics.TokensCount = LexingStream.Count;
			Statistics.CharsCount = text.Length;

			return root;
		}

		protected abstract Node ParsingAlgorithm(string text);

		public void SetPreprocessor(BasePreprocessor preproc)
		{
			if (preproc != null)
			{
				Preproc = preproc;
				Preproc.NodeGenerator = NodeGenerator;
			}
		}

		public void SetVisitor(Func<Grammar, GrammarProvidedTreeVisitor> constructor)
		{
			VisitorConstructors.Add(constructor);
		}

		protected Node TreePostProcessing(Node root)
		{
			/// Запускаем стандартные визиторы
			root.Accept(new RemoveAutoVisitor(GrammarObject));
			root.Accept(new GhostListOptionProcessingVisitor(GrammarObject));
			root.Accept(new LeafOptionProcessingVisitor(GrammarObject));
			root.Accept(new MergeAnyVisitor(GrammarObject));
			root.Accept(new UserifyVisitor(GrammarObject));

			/// Формируем узлы для кастомных блоков
			if (LexingStream.CustomBlocks?.Count > 0)
			{
				var visitor = new InsertCustomBlocksVisitor(GrammarObject, NodeGenerator, LexingStream.CustomBlocks);
				root.Accept(visitor);
				root = visitor.Root;

				foreach (var block in visitor.CustomBlocks)
				{
					Log.Add(Message.Error(
						$"Блок \"{block.Start.Value[0]}\" прорезает несколько сущностей программы или находится в области, " +
							$"не учитываемой при синтаксическом анализе",
						block.Start.Location.Start
					));
				}
			}

			/// Типизируем узлы
			NodeRetypingVisitor.Root = root;
			root.Accept(NodeRetypingVisitor);
			root = NodeRetypingVisitor.Root;

			/// Запускаем кастомные сторонние визиторы
			VisitorConstructors.ForEach(c =>
			{
				var visitor = c.Invoke(GrammarObject);
				root.Accept(visitor);
			});

			return root;
		}

		public override object InitializeLifetimeService() => null;
	}
}
