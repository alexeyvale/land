using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;
using LandParserGenerator.Lexing;

namespace LandParserGenerator.Parsing.LL
{
	public class DecisionStack
	{
		public const int MAX_FINISH_ANY_ATTEMPTS = 5;

		/// <summary>
		/// Ссылка на стек символов
		/// </summary>
		private ParsingStack ParsingStack { get; set; }

		/// <summary>
		/// Ссылка на входной поток токенов
		/// </summary>
		private TokenStream TokensStream { get; set; }

		/// <summary>
		/// Стек принятых решений
		/// </summary>
		private Stack<DecisionPoint> Decisions { get; set; } = new Stack<DecisionPoint>();

		/// <summary>
		/// Мемоизация неуспешно разобранных правил
		/// </summary>
		private Dictionary<string, List<int>> RuleFailures { get; set; } = new Dictionary<string, List<int>>();

		public int Count { get { return Decisions.Count; } }

		public DecisionStack(ParsingStack parsingStack, TokenStream tokensStream)
		{
			ParsingStack = parsingStack;
			TokensStream = tokensStream;
		}

		public void ChooseAlternative(List<Alternative> alts)
		{
			if (!Decisions.Any(d => d is ChooseAlternativeDecision
				 && d.DecisionTokenIndex == TokensStream.CurrentTokenIndex
				 && ((ChooseAlternativeDecision)d).Alternatives[0].NonterminalSymbolName == ParsingStack.Peek().Symbol))
			{
				Decisions.Push(new ChooseAlternativeDecision()
				{
					Alternatives = alts,
					ChosenIndex = 0,
					DecisionTokenIndex = TokensStream.CurrentTokenIndex,
					ParsingStackTop = ParsingStack.Peek()
				});
			}
		}

		public void FinishAny(Node anyNode)
		{
			Decisions.Push(new FinishAnyDecision()
			{
				DecisionTokenIndex = TokensStream.CurrentTokenIndex,
				AnyNode = anyNode,
				ParsingStackTop = ParsingStack.Peek(),
				AttemptsCount = 0
			});
		}

		public void Pop()
		{
			if (Decisions.Count > 0)
				Decisions.Pop();
		}

		/// <summary>
		/// Была ли уже неуспешная попытка разобрать указанное правило с указанной позиции
		/// </summary>
		public bool RuleFailed(string rule, int tokenIndex)
		{
			return RuleFailures.ContainsKey(rule) && RuleFailures[rule].Contains(tokenIndex);
		}

		private List<Node> FailureCandidates { get; set; } = new List<Node>();

		public DecisionPoint BacktrackToClosestDecision(bool newMemoization = true)
		{
			/// Откат стека разбора до ближайшего решения, которое ещё можно перепринять,
			/// с попутным запоминанием правил, которые не удалось разобрать

			if (newMemoization)
			{
				var pathElement = ParsingStack.Peek().Parent;
				FailureCandidates = new List<Node>();

				while (pathElement != null)
				{
					FailureCandidates.Add(pathElement);
					pathElement = pathElement.Parent;
				}
			}

			/// Пока на стеке есть решения
			while(Decisions.Count > 0)
			{
				if (Decisions.Peek().ParsingStackTop == ParsingStack.Peek()
					&& Decisions.Peek().DecisionTokenIndex >= TokensStream.CurrentTokenIndex)
					if (CanBeChanged(Decisions.Peek()))
						return Decisions.Peek();
					else
						Decisions.Pop();

				if (FailureCandidates.Contains(ParsingStack.Peek()))
				{
					if (!RuleFailures.ContainsKey(ParsingStack.Peek().Symbol))
						RuleFailures[ParsingStack.Peek().Symbol] = new List<int>();

					RuleFailures[ParsingStack.Peek().Symbol].Add(TokensStream.CurrentTokenIndex);
				}

				ParsingStack.Undo();
			}

			return null;
		}

		private bool CanBeChanged(DecisionPoint decision)
		{
			if(decision is ChooseAlternativeDecision)
			{
				var choice = (ChooseAlternativeDecision)decision;
				return choice.ChosenIndex < choice.Alternatives.Count - 1; 
			}

			if(decision is FinishAnyDecision)
			{
				var finish = (FinishAnyDecision)decision;
				return finish.AttemptsCount < MAX_FINISH_ANY_ATTEMPTS; 
			}

			return false;
		}
	}
}
