using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.LR
{
	public class Marker
	{
		public Alternative Alternative { get; private set; }
		public int Position { get; private set; }

		public TerminalSymbol Lookahead { get; private set; }

		public Marker(Alternative alt, int pos, TerminalSymbol lookahead)
		{
			Alternative = alt;
			Position = pos;
			Lookahead = lookahead;
		}

		/// <summary>
		/// Следующий после пункта элемент альтернативы
		/// </summary>
		public string Next
		{
			get
			{
				if (Position < Alternative.Count)
				{
					return Alternative[Position];
				}
				else
				{
					/// Случай, когда пункт стоит в конце альтернативы
					return String.Empty;
				}
			}
		}

		/// <summary>
		/// Сдвиг пункта к следующему символу в альтернативе
		/// </summary>
		/// <returns>Признак достижения конца альтернативы</returns>
		public bool ShiftNext()
		{
			/// Если есть куда сдвигаться
			if(Position < Alternative.Count)
			{
				++Position;
			}

			return Position == Alternative.Count;
		}
	}
}
