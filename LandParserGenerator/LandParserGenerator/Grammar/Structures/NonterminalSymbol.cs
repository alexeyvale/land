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
		public List<Alternative> Alternatives { get; private set; } = new List<Alternative>();

		public int Count { get { return Alternatives.Count; } }

        public NonterminalSymbol(string name)
        {
            Name = name;
        }

        public NonterminalSymbol(string name, string[][] alts)
		{
			Name = name;

			for(int i=0; i< alts.Length; ++i)
			{
				var newAlt = new Alternative()
				{
					NonterminalSymbolName = name
				};

				for (int j = 0; j < alts[i].Length; ++j)
				{
					newAlt.Add(alts[i][j]);
				}

				Alternatives.Add(newAlt);
			}
		}

		public NonterminalSymbol(string name, List<Alternative> alts)
		{
			Name = name;
			Alternatives = alts;
		}

		public void Add(params string[] altContent)
		{
			var alt = new Alternative()
			{
				NonterminalSymbolName = this.Name
			};

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
			var newLine = System.Environment.NewLine;
			return $"{Name} :{newLine}{String.Join($"{newLine}| ", Alternatives)}{newLine};";
		}
	}
}
