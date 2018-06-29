using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.GLL
{
	public class GrammarSlot
	{
		public Alternative Alternative { get; private set; }
		public int Position { get; private set; }

		public GrammarSlot(Alternative alt, int pos)
		{
			Alternative = alt;
			Position = pos;
		}

		/// <summary>
		/// Следующий после указателя элемент альтернативы
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
					/// Случай, когда указатель стоит в конце альтернативы
					return String.Empty;
				}
			}
		}

		/// <summary>
		/// Предшествующий указателю элемент альтернативы
		/// </summary>
		public string Prev
		{
			get
			{
				if (Position > 0)
				{
					return Alternative[Position - 1];
				}
				else
				{
					/// Случай, когда указатель стоит в начале 
					return String.Empty;
				}
			}
		}

		/// <summary>
		/// Сдвиг указателя к следующему символу в альтернативе
		/// </summary>
		/// <returns>Признак достижения конца альтернативы</returns>
		public GrammarSlot ShiftNext()
		{
			return new GrammarSlot(
				Alternative,
				Position < Alternative.Count ? Position + 1 : Position
			);
		}

		public override bool Equals(object obj)
		{
			if (obj is GrammarSlot)
			{
				var b = (GrammarSlot)obj;
				return b.Alternative == Alternative
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
