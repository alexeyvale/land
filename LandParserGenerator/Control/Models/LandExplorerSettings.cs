using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;

using Land.Core;

namespace Land.Control
{
	[DataContract]
	public class ParserSettingsItem
	{
		[DataMember]
		public List<string> Extensions { get; set; } = new List<string>();

		[DataMember]
		public string GrammarPath { get; set; }

		[DataMember]
		public string PreprocessorPath { get; set; }

		[DataMember]
		public List<PreprocessorProperty> PreprocessorProperties { get; set; } = new List<PreprocessorProperty>();

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

		public ParserSettingsItem Clone()
		{
			return new ParserSettingsItem()
			{
				Extensions = Extensions,
				GrammarPath = GrammarPath,
                PreprocessorPath = PreprocessorPath,
                PreprocessorProperties = new List<PreprocessorProperty>(PreprocessorProperties)
			};
		}
	}

	[DataContract]
	public class LandExplorerSettings: IExtensibleDataObject
	{
		[DataMember]
		public bool SaveAbsolutePath { get; set; }

		[DataMember]
		public double? AcceptanceThreshold { get; set; }

		[DataMember]
		public double? DistanceToClosestThreshold { get; set; }

		[DataMember]
		public double? GarbageThreshold { get; set; }

		[DataMember]
		public ObservableCollection<ParserSettingsItem> Parsers { get; set; } = new ObservableCollection<ParserSettingsItem>();

		public LandExplorerSettings Clone()
		{
			return new LandExplorerSettings()
			{
				SaveAbsolutePath = SaveAbsolutePath,
				AcceptanceThreshold = AcceptanceThreshold,
				GarbageThreshold = GarbageThreshold,
				DistanceToClosestThreshold = DistanceToClosestThreshold,
				Parsers = new ObservableCollection<ParserSettingsItem>(Parsers.Select(g => g.Clone()))
			};
		}

		private ExtensionDataObject _extensionData;

		public virtual ExtensionDataObject ExtensionData
		{
			get { return _extensionData; }
			set { _extensionData = value; }
		}
	}
}
