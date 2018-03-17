using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing
{
	/// <summary>
	/// Таблица парсинга
	/// </summary>
	public abstract class BaseTable
	{
		public BaseTable(Grammar g) { }

		public abstract List<Message> CheckValidity();

		public abstract void ExportToCsv(string filename);
	}
}
