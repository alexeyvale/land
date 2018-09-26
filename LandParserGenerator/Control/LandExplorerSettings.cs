using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;

using Land.Core;
using Land.Core.Parsing.Preprocessing;

namespace Land.Control
{
	[DataContract]
	public class ParserSettingsBlock
	{
		[DataMember]
		public List<string> Extensions { get; set; } = new List<string>();

		[DataMember]
		public string GrammarPath { get; set; }

		[DataMember]
		public string PreprocessorPath { get; set; }

		[DataMember]
		public PreprocessorSettings PreprocessorSettings { get; set; }

		public string ExtensionsString
		{
			get { return String.Join("; ", Extensions); }

			set
			{
				/// Разбиваем строку на отдельные расширения, добавляем точку, если она отсутствует
				Extensions = value.Split(new char[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(ext => ext.StartsWith(".") ? ext : '.' + ext).ToList();
			}
		}

		public ParserSettingsBlock Clone()
		{
			return new ParserSettingsBlock()
			{
				Extensions = Extensions,
				GrammarPath = GrammarPath,
                PreprocessorPath = PreprocessorPath,
                PreprocessorSettings = (PreprocessorSettings)PreprocessorSettings?.Clone()
			};
		}
	}

	[DataContract]
	public class LandExplorerSettings
	{
		[DataMember]
		public bool HighlightSelectedElement { get; set; }

		[DataMember]
		public ObservableCollection<ParserSettingsBlock> Parsers { get; set; } = new ObservableCollection<ParserSettingsBlock>();

		public LandExplorerSettings Clone()
		{
			return new LandExplorerSettings()
			{
				HighlightSelectedElement = HighlightSelectedElement,
				Parsers = new ObservableCollection<ParserSettingsBlock>(Parsers.Select(g => g.Clone()))
			};
		}
	}
}
