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

		public Alternative Add(string elem)
		{
			Elements.Add(elem);

			return this;
		}

		public string this[int i]
		{
			get { return Elements[i]; }
		}

		public List<string>.Enumerator GetEnumerator()
		{
			return Elements.GetEnumerator();
		}

		public HashSet<Token> First(Grammar g)
		{
			/// FIRST альтернативы - это либо FIRST для первого символа в альтернативе,
			/// либо, если альтернатива пустая, соответствующий токен
			if (this.Count > 0)
			{
				return g[this[0]].First(g);
			}
			else
			{
				return new HashSet<Token>() { Token.Empty };
			}
		}

		/// <summary>
		/// Получение подпоследовательности элементов альтернативы
		/// </summary>
		/// <param name="pos">Позиция, с которой начинается подпоследовательность</param>
		/// <returns></returns>
		public Alternative Subsequence(int pos)
		{
			return new Alternative()
			{
				Elements = pos < Elements.Count ?
					this.Elements.GetRange(pos, this.Elements.Count - pos) :
					new List<string>()
			};
		}
	}
}
