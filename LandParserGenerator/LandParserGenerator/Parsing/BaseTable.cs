using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core.Parsing
{
	/// <summary>
	/// Таблица парсинга
	/// </summary>
	public abstract class BaseTable
	{
		protected Grammar Gram { get; set; }

		public BaseTable(Grammar g)
		{
			Gram = g;
		}

		public abstract List<Message> CheckValidity();

		public abstract void ExportToCsv(string filename);
	}
}
