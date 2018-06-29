using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.Earley
{
	public class Marker
	{
		public Alternative Alternative { get; private set; }
		public int Position { get; private set; }

		public Marker(Alternative alt, int pos)
		{
			Alternative = alt;
			Position = pos;
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
				Position < Alternative.Count ? Position + 1 : Position
			);
		}

		public override bool Equals(object obj)
		{
			if (obj is Marker)
			{
				var b = (Marker)obj;
				return b.Alternative.Equals(Alternative)
					&& b.Position == Position;
			}
			else
				return false;
		}

		public override int GetHashCode()
		{
			return this.Alternative.GetHashCode() * 7 + Position;
		}
	}
}
