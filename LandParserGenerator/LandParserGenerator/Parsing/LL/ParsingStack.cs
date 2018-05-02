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
	public class ParsingStack
	{
		/// <summary>
		/// Стек, на который кладутся ожидаемые при разборе символы
		/// </summary>
		private Stack<Node> Stack { get; set; } = new Stack<Node>();

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
		/// Сброс в стек одного пакета и начало другого
		/// </summary>
		public void FlushBatch()
		{
			Actions.Push(Batch);
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

		public void Push(Node node)
		{
			Stack.Push(node);

			var action = new ParsingStackAction()
			{
				Type = ParsingStackAction.ParsingStackActionType.Push,
				Value = node,
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

		public Node Pop()
		{
			var action = new ParsingStackAction()
			{
				Type = ParsingStackAction.ParsingStackActionType.Pop,
				Value = Stack.Peek(),
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

			return Stack.Pop();
		}

		public Node Peek()
		{
			return Stack.Peek();
		}

		public void Undo()
		{
			/// При отмене действий обратные нужно производить, 
			/// начиная с последнего действия
			var lastActionsBatch = Actions.Pop();
			lastActionsBatch.Reverse();
			
			foreach(var a in lastActionsBatch)
			{
				switch(a.Type)
				{
					case ParsingStackAction.ParsingStackActionType.Pop:
						/// Убираем дочерние элементы для заново добавляемого узла
						a.Value.ResetChildren();
						Stack.Push(a.Value);
						break;
					case ParsingStackAction.ParsingStackActionType.Push:
						Stack.Pop();
						break;
				}

				TokenStream.BackToToken(a.TokenStreamIndex);
			}
		}

		public int Count { get { return Stack.Count; } }

		public Stack<Node>.Enumerator GetEnumerator()
		{
			return Stack.GetEnumerator();
		}
	}
}
