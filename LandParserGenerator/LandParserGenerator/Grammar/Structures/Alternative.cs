using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core
{
	[Serializable]
	public class Alternative
	{
		public string NonterminalSymbolName { get; set; }
		public string Alias { get; set; }

		public List<Entry> Elements { get; set; } = new List<Entry>();

		public int Count { get { return Elements.Count; } }

		public Alternative Add(string elem)
		{
			Elements.Add(new Entry(elem));

			return this;
		}

		public Alternative Add(Entry elem)
		{
			Elements.Add(elem);

			return this;
		}

		public Entry this[int i]
		{
			get { return Elements[i]; }
		}

		public List<Entry>.Enumerator GetEnumerator()
		{
			return Elements.GetEnumerator();
		}

		/// <summary>
		/// Получение подпоследовательности элементов альтернативы
		/// </summary>
		/// <param name="start">Позиция, с которой начинается подпоследовательность</param>
		/// <returns></returns>
		public Alternative Subsequence(int start, int? end = null)
		{
			var length = end.HasValue ? Math.Min(this.Elements.Count - start, end.Value - start + 1) : this.Elements.Count - start;

			return new Alternative()
			{
				Elements = start < Elements.Count ?
					this.Elements.GetRange(start, length) : new List<Entry>()
			};
		}

		public override bool Equals(object obj)
		{
			if (obj is Alternative)
			{
				var b = (Alternative)obj;

				return NonterminalSymbolName == b.NonterminalSymbolName
					&& b.Elements.SequenceEqual(this.Elements);
			}
			else
				return false;
		}

		public override int GetHashCode()
		{
			return Elements.GetHashCode();
		}

		public override string ToString()
		{
			return Count > 0 ? String.Join(" ", Elements) : "eps";
		}

		public bool Contains(string symbol)
		{
			return Elements.Any(e => e.Symbol == symbol);
		}

        public bool ReplaceFirst(string smb1, string smb2)
        {
            var idx = Elements.FindIndex(e => e.Symbol == smb1);

            if (idx > -1)
            {
                Elements[idx] = new Entry(smb2);
                return true;
            }
            else
                return false;
        }
    }
}
