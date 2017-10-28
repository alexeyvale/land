using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.LL
{
	public class ParseStack
	{
		/// <summary>
		/// Стек, на который кладутся ожидаемые при разборе символы
		/// </summary>
		private Stack<Node> Stack { get; set; } = new Stack<Node>();

		/// <summary>
		/// Стек действий, которые предпринимаются по мере разбора
		/// </summary>
		private Stack<List<ParseAction>> Actions { get; set; } = new Stack<List<ParseAction>>();

		public void Push(Node node)
		{
			Stack.Push(node);
			Actions.Push(new List<ParseAction>() { new ParseAction()
			{
				Type = ParseAction.ParseActionType.Push,
				Value = node
			}});
		}

		public void PushBatch(params Node[] nodes)
		{
			foreach(var node in nodes)
				Stack.Push(node);

			Actions.Push(nodes.Select(n => new ParseAction()
			{
				Value = n,
				Type = ParseAction.ParseActionType.Push
			}).ToList());
		}

		public Node Pop()
		{
			Actions.Push(new List<ParseAction>() { new ParseAction()
			{
				Type = ParseAction.ParseActionType.Pop,
				Value = Stack.Peek()
			}});

			return Stack.Pop();
		}

		public Node Peek()
		{
			return Stack.Peek();
		}

		public int Count { get { return Stack.Count; } }

		public Stack<Node>.Enumerator GetEnumerator()
		{
			return Stack.GetEnumerator();
		}
	}
}
