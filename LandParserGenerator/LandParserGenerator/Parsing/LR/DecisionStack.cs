using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;
using LandParserGenerator.Lexing;

namespace LandParserGenerator.Parsing.LR
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
		/// Мемоизация переходов, закончившихся неудачей
		/// </summary>
		private Dictionary<int, HashSet<int>> TransitionFailures { get; set; } = new Dictionary<int, HashSet<int>>();

		public int Count { get { return Decisions.Count; } }

		public DecisionStack(ParsingStack parsingStack, TokenStream tokensStream)
		{
			ParsingStack = parsingStack;
			TokensStream = tokensStream;
		}

		public bool ChooseTransition()
		{
			if (!Decisions.Any(d => d is ChooseTransitionDecision
				 && d.DecisionTokenIndex == TokensStream.CurrentTokenIndex
				 && d.ParsingStackTop.State == ParsingStack.PeekState())
				 && !(TransitionFailures.ContainsKey(ParsingStack.PeekState()) 
				 && TransitionFailures[ParsingStack.PeekState()].Contains(TokensStream.CurrentTokenIndex)))
			{
				Decisions.Push(new ChooseTransitionDecision()
				{
					DecisionTokenIndex = TokensStream.CurrentTokenIndex,
					ParsingStackTop = new StackTop()
					{
						State = ParsingStack.PeekState(),
						Symbol = ParsingStack.PeekSymbol()
					}
				});

				return true;
			}

			return false;
		}

		public void FinishAny(Node anyNode)
		{
			Decisions.Push(new FinishAnyDecision()
			{
				DecisionTokenIndex = TokensStream.CurrentTokenIndex,
				AnyNode = anyNode,
				ParsingStackTop = new StackTop()
				{
					State = ParsingStack.PeekState(),
					Symbol = ParsingStack.PeekSymbol()
				},
				AttemptsCount = 0
			});
		}

		public void Pop()
		{
			if (Decisions.Count > 0)
			{
				if(Decisions.Peek() is ChooseTransitionDecision)
				{
					var decision = (ChooseTransitionDecision)Decisions.Peek();
					if (!TransitionFailures.ContainsKey(decision.ParsingStackTop.State))
						TransitionFailures[decision.ParsingStackTop.State] = new HashSet<int>();
					TransitionFailures[decision.ParsingStackTop.State].Add(decision.DecisionTokenIndex);
				}
				Decisions.Pop();
			}
		}

		public DecisionPoint BacktrackToClosestDecision()
		{
			/// Пока на стеке есть решения
			while(Decisions.Count > 0)
			{
				/// По идее, стек разбора не может закончиться раньше стека решений
				while (Decisions.Peek().ParsingStackTop.State != ParsingStack.PeekState() 
					|| Decisions.Peek().ParsingStackTop.Symbol != ParsingStack.PeekSymbol())
				{
					ParsingStack.Undo();
				}

				/// Возвращаем решение, если его ещё можно изменить
				if (CanBeChanged(Decisions.Peek()))
					return Decisions.Peek();
				/// или отбрасываем
				else
					Decisions.Pop();
			}

			return null;
		}

		private bool CanBeChanged(DecisionPoint decision)
		{
			/// Если решение о выборе перехода присутствует на стеке,
			/// его точно можно поменять
			if (decision is ChooseTransitionDecision)
				return true;

			if (decision is FinishAnyDecision)
			{
				var finish = (FinishAnyDecision)decision;
				return finish.AttemptsCount < MAX_FINISH_ANY_ATTEMPTS;
			}

			return false;
		}
	}
}
