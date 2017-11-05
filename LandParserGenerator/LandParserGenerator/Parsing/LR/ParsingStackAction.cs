using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.LR
{
	public class ParsingStackAction
	{
		public enum ParsingStackActionType { Push, Pop }

		/// <summary>
		/// Тип действия со стеком
		/// </summary>
		public ParsingStackActionType Type { get; set; }
		/// <summary>
		/// Значение, которое помещается на стек символов  или снимается с него
		/// </summary>
		public Node Symbol { get; set; }
		/// <summary>
		/// Значение, которое помещается на стек состояний  или снимается с него
		/// </summary>
		public int? State { get; set; }
		/// <summary>
		/// Индекс токена, являющегося текущим на момент совершения действия
		/// </summary>
		public int TokenStreamIndex { get; set; }
	}
}
