using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.Earley
{
	public class RuleNodePair
	{
		public string NonterminalName { get; set; }
		public ForestNode TreeNode { get; set; }

		public override bool Equals(object obj)
		{
			var pair = obj as RuleNodePair;

			if (pair == null)
				return false;

			return NonterminalName == pair.NonterminalName
				&& TreeNode == pair.TreeNode;
		}

		public override int GetHashCode()
		{
			return NonterminalName.GetHashCode() * 7 + TreeNode.GetHashCode();
		}
	}

	public class Parser : BaseParser
	{
		private const int MAX_RECOVERY_ATTEMPTS = 5;

		private TableLL1 Table { get; set; }
		private TokenStream LexingStream { get; set; }

		public Parser(Grammar g, ILexer lexer) : base(g, lexer)	{}

		/// <summary>
		/// LL(1) разбор
		/// </summary>
		/// <returns>
		/// Корень дерева разбора
		/// </returns>
		public override Node Parse(string text)
		{
			LexingStream = new TokenStream(Lexer, text);

			/// Инициализируем все необходимые множества
			var EarleySets = new List<HashSet<EarleyItem>>();
			var QPrime = new HashSet<EarleyItem>();
			var V = new HashSet<ForestNode>();

			/// Будем проходить по индексам от 0 до длины входного потока
			/// при этом токены в самом алгоритме нумеруются с единицы
			var currentIndex = 0;

			/// Читаем первую лексему из входного потока
			var token = LexingStream.NextToken();

			EarleySets.Add(new HashSet<EarleyItem>());

			foreach (var alt in grammar.Rules[grammar.StartSymbol])
			{
				if (alt.Count == 0 || grammar.Rules.ContainsKey(alt[0].Symbol))
				{
					EarleySets[0].Add(new EarleyItem()
					{
						Marker = new Marker(grammar.Rules[grammar.StartSymbol][0], 0),
						InputIndex = 0,
						TreeNode = null
					});
				}
				else if (alt[0].Symbol == token.Name || alt[0].Symbol == Grammar.ANY_TOKEN_NAME)
				{
					QPrime.Add(new EarleyItem()
					{
						Marker = new Marker(grammar.Rules[grammar.StartSymbol][0], 0),
						InputIndex = 0,
						TreeNode = null
					});
				}
			}

			/// Основной итеративный процесс
			while (true)
			{
				/// Множество, при обработке которого будет расширяться EarleySets[currentIndex]
				var R = EarleySets[currentIndex];
				/// Множество, при обработке которого получим EarleySets[currentIndex + 1]
				var Q = QPrime;
				QPrime = new HashSet<EarleyItem>();
				/// Множество для специальной обработки нетерминалов, из которых выводится пустая строка
				var H = new HashSet<RuleNodePair>();

				/// В это множество попадали ветки, в которых указатель до нетерминала или в конце
				while (R.Count > 0)
				{
					var curItem = R.First();
					R.Remove(curItem);

					/// Если указатель в ветке стоит перед нетерминалом
					if (grammar.Rules.ContainsKey(curItem.Marker.Next))
					{
						/// проходим по веткам для этого нетерминала
						foreach (var alt in grammar.Rules[curItem.Marker.Next])
						{
							/// Если альтернатива пустая или начинается с нетерминала
							if (alt.Count == 0 || grammar.Rules.ContainsKey(alt[0].Symbol))
							{
								/// добавляем элемент одновременно и в Ei, и в R
								EarleySets[currentIndex].Add(new EarleyItem()
								{
									Marker = new Marker(alt, 0),
									InputIndex = currentIndex,
									TreeNode = null
								});
							}
							/// Если альтернатива начинается с терминала
							else if (alt[0].Symbol == token.Name)
							{
								/// добавляем элемент во множество элементов, которые сформируют EarleySet
								/// для следующего токена
								Q.Add(new EarleyItem()
								{
									Marker = new Marker(alt, 0),
									InputIndex = currentIndex,
									TreeNode = null
								});
							}
						}

						/// ????????????????????????
						var hNode = H.FirstOrDefault(e => e.NonterminalName == curItem.Marker.Next
							&& e.TreeNode.Marker.Alternative.NonterminalSymbolName == e.NonterminalName
							&& e.TreeNode.StartIndex == e.TreeNode.EndIndex
							&& e.TreeNode.StartIndex == currentIndex);
						if (hNode != null)
						{
							var y = MakeNode(curItem.Marker.ShiftNext(), curItem.InputIndex, currentIndex, curItem.TreeNode, hNode.TreeNode, V);

						}
					}
					else if (String.IsNullOrEmpty(curItem.Marker.Next))
					{
						if (curItem.TreeNode == null)
						{
							curItem.TreeNode = V.FirstOrDefault(e => e.Symbol == curItem.Marker.Alternative.NonterminalSymbolName
								&& e.StartIndex == e.EndIndex
								&& e.StartIndex == currentIndex) ?? new ForestNode()
								{
									Symbol = curItem.Marker.Alternative.NonterminalSymbolName,
									StartIndex = currentIndex,
									EndIndex = currentIndex
								};
							/// ??????????????????????????????????????????????????????????????
						}

						if(curItem.InputIndex == currentIndex)
						{
							H.Add(new RuleNodePair()
							{
								NonterminalName = curItem.Marker.Alternative.NonterminalSymbolName,
								TreeNode = curItem.TreeNode
							});
						}

						foreach(var item in EarleySets[curItem.InputIndex]
							.Where(e=>e.Marker.Next == curItem.Marker.Alternative.NonterminalSymbolName))
						{
							var y = MakeNode(item.Marker.ShiftNext(), item.InputIndex, currentIndex, item.TreeNode, curItem.TreeNode, V);
							var subAlt = item.Marker.Alternative.Subsequence(item.Marker.Position + 1);
							var newItem = new EarleyItem()
							{
								Marker = item.Marker.ShiftNext(),
								InputIndex = item.InputIndex,
								TreeNode = y
							};

							if (subAlt.Count == 0 || grammar.Rules.ContainsKey(subAlt[0].Symbol))
							{
								EarleySets[currentIndex].Add(newItem);
							}
							else if (subAlt[0].Symbol == token.Name)
							{
								Q.Add(newItem);
							}
						}
					}
				}

				EarleySets[currentIndex + 1] = new HashSet<EarleyItem>();
				V = new HashSet<ForestNode>();
				var v = new ForestNode()
				{
					 Symbol = token.Name,
					 StartIndex = currentIndex,
					 EndIndex = currentIndex + 1
				};
				token = LexingStream.NextToken();

				while(Q.Count > 0)
				{
					var curItem = Q.First();
					Q.Remove(curItem);

					var y = MakeNode(curItem.Marker.ShiftNext(), curItem.InputIndex, currentIndex + 1, curItem.TreeNode, v, V);
					var subAlt = curItem.Marker.Alternative.Subsequence(curItem.Marker.Position + 1);

					var newItem = new EarleyItem()
					{
						Marker = curItem.Marker.ShiftNext(),
						InputIndex = curItem.InputIndex,
						TreeNode = y
					};

					if (subAlt.Count == 0 || grammar.Rules.ContainsKey(subAlt[0].Symbol))
					{
						EarleySets[LexingStream.CurrentTokenIndex + 1].Add(newItem);
					}
					else if (subAlt[0].Symbol == token.Name)
					{
						QPrime.Add(newItem);
					}
				}

				++currentIndex;
			}

			var last = EarleySets[EarleySets.Count].First(e => e.Marker.Alternative.NonterminalSymbolName == grammar.StartSymbol
				&& String.IsNullOrEmpty(e.Marker.Next)
				&& e.InputIndex == 0);

			//return last?.TreeNode; ???????????????????????????????????????????????????
			return null;
		}

		private ForestNode MakeNode(
			Marker curMarker, 
			int itemInputIndex, 
			int curInputIndex,
			ForestNode w,
			ForestNode v, 
			HashSet<ForestNode> nodesSet)
		{
			string symbol = null;
			Marker marker = null;
			ForestNode node = null;

			/// Если маркер стоит в конце альтернативы
			if (String.IsNullOrEmpty(curMarker.Next))
				symbol = curMarker.Alternative.NonterminalSymbolName;
			else
				marker = curMarker.ShiftNext();

			/// Если маркер стоит не в конце и после первого элемента альтернативы
			if (curMarker.Position == 1 && !String.IsNullOrEmpty(curMarker.Next))
				node = v;
			else
			{
				/// ??????????????????????????????????????????????????????????????
				var newNode = new ForestNode()
				{
					Symbol = symbol,
					Marker = marker,
					StartIndex = itemInputIndex,
					EndIndex = curInputIndex
				};
			}
		}
	}
}
