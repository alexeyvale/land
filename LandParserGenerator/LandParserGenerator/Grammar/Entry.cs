using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	public class Entry
	{
		public string Value { get; private set; }

		public EntryAdditionalInfo AdditionalInfo { get; private set; }

		public Entry(string val)
		{
			Value = val;
			AdditionalInfo = null;
		}

		public Entry(string val, EntryAdditionalInfo info)
		{
			Value = val;
			AdditionalInfo = info;
		}

		public static implicit operator String(Entry entry)
		{
			return entry.Value;
		}

		public override string ToString()
		{
			return Value;
		}
	}

	public abstract class EntryAdditionalInfo
	{ }

}
