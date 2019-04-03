using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using Antlr4.Runtime;

namespace Land.Core
{
	public class AntlrTokenAdapter : Lexing.IToken
	{
		private IToken Token { get; set; }

		public SegmentLocation Location { get; private set; }
		public string Text => Token.Text;
		public string Name { get; private set; }

		public AntlrTokenAdapter(IToken token, Lexer lexer)
		{
			Token = token;
			Name = lexer.Vocabulary.GetSymbolicName(Token.Type);

			/// Разбиваем текст токена на части, расположенные на разных строках
			var partsOnSeparateLines = Token.Text.Split('\n');
			/// Заканчивается ли последняя часть переходом на новую строке
			var endsWithEol = Token.Text.EndsWith("\n");
			/// Если да, то в нашем разбиении последняя часть - это предпоследний элемент
			var lastPartLength = endsWithEol
				? partsOnSeparateLines[partsOnSeparateLines.Length - 2].Length + 1
				: partsOnSeparateLines[partsOnSeparateLines.Length - 1].Length;
			/// Количество переходов на новую строку, входящих в текст токена
			var eolCount = partsOnSeparateLines.Length - 1;

			Location = new SegmentLocation()
			{
				Start = new PointLocation(Token.Line, Token.Column, Token.StartIndex),
				End = new PointLocation(
					Token.Line + (endsWithEol ? eolCount - 1 : eolCount),
					eolCount == 0 || eolCount == 1 && endsWithEol ? Token.Column + lastPartLength - 1 : lastPartLength - 1,
					Token.StopIndex
				)
			};
		}
	}
}
