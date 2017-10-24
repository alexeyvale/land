using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	public class NonterminalSymbol: ISymbol
	{
		public string Name { get; private set; }
		private List<Alternative> Alternatives { get; set; } = new List<Alternative>();

		public int Count { get { return Alternatives.Count; } }

		public NonterminalSymbol(string name, string[][] alts)
		{
			Name = name;

			for(int i=0; i< alts.Length; ++i)
			{
				var newAlt = new Alternative();

				for (int j = 0; j < alts[i].Length; ++j)
				{
					newAlt.Add(alts[i][j]);
				}

				Alternatives.Add(newAlt);
			}
		}

		public void Add(string[] altContent)
		{
			var alt = new Alternative();

			foreach (var smb in altContent)
			{
				alt.Add(smb);
			}

			this.Add(alt);
		}

		public void Add(Alternative alt)
		{
			Alternatives.Add(alt);
		} 

		public Alternative this[int i]
		{
			get { return Alternatives[i]; }
		}

		public List<Alternative>.Enumerator GetEnumerator()
		{
			return Alternatives.GetEnumerator();
		}

		public override bool Equals(object obj)
		{
			return obj is NonterminalSymbol && ((NonterminalSymbol)obj).Name == Name;
		}

		public override int GetHashCode()
		{
			return Name.GetHashCode();
		}

		public override string ToString()
		{
			return $"{Name} :\n\r{String.Join("\n\r| ", Alternatives)}\n\r;";
		}
	}
}
