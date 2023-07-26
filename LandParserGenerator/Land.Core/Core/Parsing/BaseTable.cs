using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Specification;

namespace Land.Core.Parsing
{
	/// <summary>
	/// Таблица парсинга
	/// </summary>
	public abstract class BaseTable
	{
		protected Grammar GrammarObject { get; set; }

		public BaseTable(Grammar g)
		{
			GrammarObject = g;
		}

		public abstract List<Message> CheckValidity();

		public abstract void ExportToCsv(string filename);
	}
}
