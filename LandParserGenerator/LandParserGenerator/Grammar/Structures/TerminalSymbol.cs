using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core
{
	[Serializable]
	public class TerminalSymbol: ISymbol
	{
		public string Name { get; private set; }
		public string Pattern { get; set; }

		/// <summary>
		/// Должен ли токен начинаться с начала строки
		/// </summary>
		public bool LineStart { get; set; }

		public TerminalSymbol(string name, string pattern, bool lineStart = false)
		{
			Name = name;
			Pattern = pattern;
			LineStart = lineStart;
		}

		public override bool Equals(object obj)
		{
			return obj is TerminalSymbol && ((TerminalSymbol)obj).Name == Name;
		}

		public override int GetHashCode()
		{
			return Name.GetHashCode();
		}

		public override string ToString()
		{
			return Name;
		}
	}
}
