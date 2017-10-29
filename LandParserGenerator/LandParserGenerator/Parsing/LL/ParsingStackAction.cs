using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.LL
{
	public class ParsingStackAction
	{
		public enum ParsingStackActionType { Push, Pop }

		/// <summary>
		/// Тип действия со стеком
		/// </summary>
		public ParsingStackActionType Type { get; set; }
		/// <summary>
		/// Значение, которое помещается на стек или снимается с него
		/// </summary>
		public Node Value { get; set; }
		/// <summary>
		/// Индекс токена, являющегося текущим на момент совершения действия
		/// </summary>
		public int TokenStreamIndex { get; set; }
	}
}
