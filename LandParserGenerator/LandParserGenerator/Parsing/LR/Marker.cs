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

		public string Lookahead { get; private set; }

		public Marker(Alternative alt, int pos, string lookahead)
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
		public Marker ShiftNext()
		{
			return new Marker(
				Alternative,
				Position < Alternative.Count ? ++Position : Position,
				Lookahead
			);
		}

		public override bool Equals(object obj)
		{
			if (obj is Marker)
			{
				var b = (Marker)obj;
				return b.Alternative.Equals(Alternative) 
					&& b.Position == Position 
					&& b.Lookahead == Lookahead;
			}
			else
				return false;
		}

		public override int GetHashCode()
		{
			return this.Alternative.GetHashCode();
		}
	}
}
