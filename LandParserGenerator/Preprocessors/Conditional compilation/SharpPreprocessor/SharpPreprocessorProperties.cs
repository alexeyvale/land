using Land.Core.Parsing.Preprocessing;
using Preprocessor.Core;
using System;
using System.Collections.Generic;

namespace SharpPreprocessing.ConditionalCompilation
{
	public class SharpPreprocessorProperties: PreprocessorSettings
	{
		public class PredefinedSymbolsConverter: PropertyConverter
		{
			public override string ToString(object val)
			{
				return String.Join(" ", ((HashSet<string>)val));
			}

			public override object ToValue(string str)
			{
				return new HashSet<string>(
					str.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
				);
			}
		}

		[PropertyToSet]
		[DisplayedName("Символы условной компиляции")]
		[Converter(typeof(PredefinedSymbolsConverter))]
		public HashSet<string> PredefinedSymbols { get; set; } = new HashSet<string>();

        public override object Clone()
        {
            return new SharpPreprocessorProperties()
            {
                PredefinedSymbols = new HashSet<string>(PredefinedSymbols)
            };
        }
    }
}
