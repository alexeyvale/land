using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Land.Core.Specification;
using Land.Core.Lexing;
using Land.Core.Parsing.Tree;

namespace Land.Core.Parsing.LR
{
	public class ParsingStack
	{
		private Stack<int> StatesStack { get; set; } = new Stack<int>();
		private Stack<Node> SymbolsStack { get; set; } = new Stack<Node>();

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
		}

		public void Pop()
		{
			if(SymbolsStack.Count > 0)
				SymbolsStack.Pop();
			if (StatesStack.Count > 0)
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

		public int CountSymbols { get { return SymbolsStack.Count; } }
		public int CountStates { get { return StatesStack.Count; } }

		public string ToString(Grammar grammar)
		{
			return String.Join(" ", SymbolsStack.Reverse().Select(s=>grammar.Userify(s.Symbol)));
		}
	}
}
