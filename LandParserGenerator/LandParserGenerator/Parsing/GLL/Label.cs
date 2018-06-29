using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.GLL
{
	public class Label
	{
		public string Symbol { get; set; }
		public GrammarSlot Slot { get; set; }

		public static implicit operator Label(string val)
		{
			return new Label()
			{
				Symbol = val
			};
		}

		public static implicit operator Label(GrammarSlot val)
		{
			return new Label()
			{
				Slot = val
			};
		}

		public static implicit operator GrammarSlot(Label val)
		{
			return val.Slot;
		}

		public static implicit operator string(Label val)
		{
			return val.Symbol;
		}

		public override bool Equals(object obj)
		{
			if (obj is Label)
			{
				var b = (Label)obj;
				return b.Symbol == Symbol
					&& (b.Slot == Slot
					|| Slot != null && Slot.Equals(b.Slot));
			}
			else
				return false;
		}

		public override int GetHashCode()
		{
			return Symbol.GetHashCode() * 7 + Slot.GetHashCode();
		}
	}
}
