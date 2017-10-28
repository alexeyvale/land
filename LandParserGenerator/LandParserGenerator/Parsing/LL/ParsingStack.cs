using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.LL
{
	public class ParsingStack
	{
		/// <summary>
		/// Стек, на который кладутся ожидаемые при разборе символы
		/// </summary>
		private Stack<Node> Stack { get; set; } = new Stack<Node>();

		/// <summary>
		/// Стек действий, которые предпринимаются по мере разбора
		/// </summary>
		private Stack<List<ParsingStackAction>> Actions { get; set; } = new Stack<List<ParsingStackAction>>();

		public void Push(Node node)
		{
			Stack.Push(node);
			Actions.Push(new List<ParsingStackAction>() { new ParsingStackAction()
			{
				Type = ParsingStackAction.ParsingStackActionType.Push,
				Value = node
			}});
		}

		public void PushBatch(params Node[] nodes)
		{
			foreach(var node in nodes)
				Stack.Push(node);

			Actions.Push(nodes.Select(n => new ParsingStackAction()
			{
				Value = n,
				Type = ParsingStackAction.ParsingStackActionType.Push
			}).ToList());
		}

		public Node Pop()
		{
			Actions.Push(new List<ParsingStackAction>() { new ParsingStackAction()
			{
				Type = ParsingStackAction.ParsingStackActionType.Pop,
				Value = Stack.Peek()
			}});

			return Stack.Pop();
		}

		public Node Peek()
		{
			return Stack.Peek();
		}

		public void Undo()
		{
			var lastActionsBatch = Actions.Pop();
			
			foreach(var a in lastActionsBatch)
			{
				switch(a.Type)
				{
					case ParsingStackAction.ParsingStackActionType.Pop:
						Stack.Push(a.Value);
						break;
					case ParsingStackAction.ParsingStackActionType.Push:
						Stack.Pop();
						break;
				}
			}
		}

		public int Count { get { return Stack.Count; } }

		public Stack<Node>.Enumerator GetEnumerator()
		{
			return Stack.GetEnumerator();
		}
	}
}
