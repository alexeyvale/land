using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

using Land.Core.Parsing.Preprocessing;

using sharp_preprocessor;

namespace SharpPreprocessor
{
	[DataContract]
	public class SharpPreprocessorSettings: PreprocessorSettings
	{
		public class PredefinedSymbolsConverter: SettingsPropertyConverter
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

		[DataMember]
		[DisplayedName("Символы условной компиляции")]
		[Converter(typeof(PredefinedSymbolsConverter))]
		public HashSet<string> PredefinedSymbols { get; set; }

        public override object Clone()
        {
            return new SharpPreprocessorSettings()
            {
                PredefinedSymbols = new HashSet<string>(PredefinedSymbols)
            };
        }
    }
}
