using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	public class Alternative
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

		public override string ToString()
		{
			return Count > 0 ? String.Join(" ", Elements) : "\u03b5";
		}
	}
}
