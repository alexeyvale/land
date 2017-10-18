using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	public class Alternative: IFirstSupporting
	{
		private List<string> Elements { get; set; } = new List<string>();

		public int Count { get { return Elements.Count; } }

		public void Add(string elem)
		{
			Elements.Add(elem);
		}

		public string this[int i]
		{
			get { return Elements[i]; }
		}

		public List<string>.Enumerator GetEnumerator()
		{
			return Elements.GetEnumerator();
		}

		public HashSet<Token> First()
		{
			/// FIRST альтернативы - это либо FIRST для первого символа в альтернативе,
			/// либо, если альтернатива пустая, соответствующий токен
			if (this.Count > 0)
			{
				return Grammar.Instance[this[0]].First();
			}
			else
			{
				return new HashSet<Token>() { Token.Empty };
			}
		}
	}
}
