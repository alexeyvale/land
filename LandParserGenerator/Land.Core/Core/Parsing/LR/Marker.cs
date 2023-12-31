﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Land.Core.Specification;

namespace Land.Core.Parsing.LR
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
		/// Предшествующий пункту элемент альтернативы
		/// </summary>
		public string Prev => Position > 0 ? Alternative[Position - 1] : String.Empty;

		/// <summary>
		/// Следующий после пункта элемент альтернативы
		/// </summary>
		public string Next => Position < Alternative.Count ? Alternative[Position] : String.Empty;

		/// <summary>
		/// Сдвиг пункта к следующему символу в альтернативе
		/// </summary>
		/// <returns>Признак достижения конца альтернативы</returns>
		public Marker ShiftNext()
		{
			return new Marker(
				Alternative,
				Position < Alternative.Count ? Position + 1 : Position,
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
