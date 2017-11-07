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
	public class ParsingStack
	{
		private Stack<int> StatesStack { get; set; } = new Stack<int>();
		private Stack<Node> SymbolsStack { get; set; } = new Stack<Node>();

		/// <summary>
		/// Поток токенов, используемый при разборе
		/// </summary>
		private TokenStream TokenStream { get; set; }

		public ParsingStack(TokenStream stream)
		{
			TokenStream = stream;
		}

		/// <summary>
		/// Стек действий, которые предпринимаются по мере разбора
		/// </summary>
		private Stack<List<ParsingStackAction>> Actions { get; set; } = new Stack<List<ParsingStackAction>>();

		/// <summary>
		/// Признак активного пакетного режима
		/// В пакетном режиме действия со стеком рассматриваются как одна транзакция
		/// </summary>
		private bool BatchMode { get; set; } = false;
		private List<ParsingStackAction> Batch { get; set; }

		/// <summary>
		/// Установка начала пакета
		/// </summary>
		public void InitBatch()
		{
			BatchMode = true;
			Batch = new List<ParsingStackAction>();
		}

		/// <summary>
		/// Завершение пакета
		/// </summary>
		public void FinBatch()
		{
			BatchMode = false;
			Actions.Push(Batch);
		}

		public void Push(Node smb)
		{
			Push(smb, null);
		}

		public void Push(int state)
		{
			Push(null, state);
		}

		public void Push(Node smb, int? state)
		{
			if(state.HasValue)
				StatesStack.Push(state.Value);
			if(smb != null)
				SymbolsStack.Push(smb);

			var action = new ParsingStackAction()
			{
				Type = ParsingStackAction.ParsingStackActionType.Push,
				State = state,
				Symbol = smb,
				TokenStreamIndex = TokenStream.CurrentTokenIndex
			};

			if (BatchMode)
			{
				Batch.Add(action);
			}
			else
			{
				Actions.Push(new List<ParsingStackAction>() { action });
			}
		}

		public void Pop()
		{
			var action = new ParsingStackAction()
			{
				Type = ParsingStackAction.ParsingStackActionType.Pop,
				Symbol = SymbolsStack.Peek(),
				State = StatesStack.Peek(),
				TokenStreamIndex = TokenStream.CurrentTokenIndex
			};

			if (BatchMode)
			{
				Batch.Add(action);
			}
			else
			{
				Actions.Push(new List<ParsingStackAction>() { action });
			}

			SymbolsStack.Pop();
			StatesStack.Pop();
		}

		public Node PeekSymbol()
		{
			return SymbolsStack.Peek();
		}

		public int PeekState()
		{
			return StatesStack.Peek();
		}

		public void Undo()
		{
			///// При отмене действий обратные нужно производить, 
			///// начиная с последнего действия
			var lastActionsBatch = Actions.Pop();
			lastActionsBatch.Reverse();

			foreach (var a in lastActionsBatch)
			{
				switch (a.Type)
				{
					case ParsingStackAction.ParsingStackActionType.Pop:
						if(a.State.HasValue)
							StatesStack.Push(a.State.Value);
						if (a.Symbol != null)
							SymbolsStack.Push(a.Symbol);
						break;
					case ParsingStackAction.ParsingStackActionType.Push:
						if (a.State.HasValue)
							StatesStack.Pop();
						if (a.Symbol != null)
							SymbolsStack.Pop();
						break;
				}

				TokenStream.BackToToken(a.TokenStreamIndex);
			}
		}

		public int CountSymbols { get { return SymbolsStack.Count; } }
		public int CountStates { get { return StatesStack.Count; } }
	}
}
