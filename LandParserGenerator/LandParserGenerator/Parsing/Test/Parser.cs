using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.Test
{
	public class Parser : BaseParser
	{
		private const int MAX_RECOVERY_ATTEMPTS = 5;

		private TableLL1 Table { get; set; }
		private TokenStream LexingStream { get; set; }

		public Parser(Grammar g, ILexer lexer) : base(g, lexer)
		{
			Table = new TableLL1(g);

			/// В ходе парсинга потребуется First,
			/// учитывающее возможную пустоту ANY
			g.UseModifiedFirst = true;
		}

		public override Node Parse(string text)
		{
			LexingStream = new TokenStream(Lexer, text);

			/// Создаём набор дескрипторов, 
			/// добавляем дескриптор для фиктивного стартового правила
			var Descriptors = new List<HashSet<Descriptor>>() {
				new HashSet<Descriptor>()
				{
					new Descriptor()
					{
						Alternative = grammar.Rules[grammar.StartSymbol][0],
						ElementIdx = 0
					}
				}
			};

			var i = 0;
			var token = LexingStream.NextToken();

			while (token.Name != Grammar.EOF_TOKEN_NAME)
			{
				Descriptors.Add(new HashSet<Descriptor>());

				while(Descriptors[i].Count > 0)
				{
					var descr = Descriptors[i].First();
					Descriptors[i].Remove(descr);

					/// Если к текущему моменту ещё не закончили сопоставление правила
					if (descr.ElementIdx < descr.Alternative.Count)
					{
						/// Если сопоставляем терминал
						if(grammar.Tokens.ContainsKey(descr.Element))
						{
							if(descr.Element == token.Name)
							{
								descr.ElementIdx += 1;
								Add(descr, Descriptors[i + 1]);
							}
							else
							{
								return null;
							}
						}
						/// Если сопоставляем нетерминал
						else
						{
							var alts = Table[descr.Nonterminal, token.Name];

							foreach(var alt in alts)
							{
								Add(new Descriptor()
								{
									Alternative = alt,
									ElementIdx = 0,
									Predecessors = new HashSet<Descriptor>() { descr }
								}, Descriptors[i]);
							}
						}
					}
					/// Если к моменту, когда достигли i-тый символ,
					/// закончили сопоставление некоторого правила
					else
					{
						foreach(var parent in descr.Predecessors)
						{
							parent.ElementIdx += 1;
							Add(parent, Descriptors[i]);
						}
					}
				}

				token = LexingStream.NextToken();
			}

			return null;
		}

		/// А если добавляем элемент, который раньше уже был рассмотрен и удалён ???????
		private void Add(Descriptor descr, HashSet<Descriptor> set)
		{
			var existing = set.FirstOrDefault(e => e.Alternative == descr.Alternative && e.ElementIdx == e.ElementIdx);

			if(existing != null)
			{
				existing.Predecessors.UnionWith(descr.Predecessors);
			}
			else
			{
				set.Add(descr);
			}
		}
	}
}
