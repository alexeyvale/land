using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;

using Land.Core;

namespace Land.Control
{
	[Serializable]
	[DataContract]
	public class ParserSettingsItem
	{
		[DataMember]
		public Guid? Id { get; set; }

		[DataMember]
		public HashSet<string> Extensions { get; set; } = new HashSet<string>();

		[DataMember]
		public string ParserPath { get; set; }

		[DataMember]
		public HashSet<string> ParserDependencies { get; set; } = new HashSet<string>();

		[DataMember]
		public string PreprocessorPath { get; set; }

		[DataMember]
		public HashSet<string> PreprocessorDependencies { get; set; } = new HashSet<string>();

		[DataMember]
		public List<PreprocessorProperty> PreprocessorProperties { get; set; } = new List<PreprocessorProperty>();

		public string ExtensionsString
		{
			get { return String.Join("; ", Extensions); }

			set
			{
				/// Разбиваем строку на отдельные расширения, добавляем точку, если она отсутствует
				Extensions = new HashSet<string>(
					value.ToLower().Split(new char[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(ext => ext.StartsWith(".") ? ext : '.' + ext)
				);
			}
		}

		public ParserSettingsItem Clone()
		{
			return new ParserSettingsItem()
			{
				Id = Id,
				Extensions = Extensions,
				ParserPath = ParserPath,
                PreprocessorPath = PreprocessorPath,
                PreprocessorProperties = new List<PreprocessorProperty>(PreprocessorProperties),
				ParserDependencies = new HashSet<string>(ParserDependencies),
				PreprocessorDependencies = new HashSet<string>(PreprocessorDependencies)
			};
		}
	}

	[DataContract]
	public class LandExplorerSettings: IExtensibleDataObject
	{
		[DataMember]
		public Guid? Id { get; set; }

		[DataMember]
		public bool EnableAutosave { get; set; }

		[DataMember]
		public bool SaveAbsolutePath { get; set; }

		[DataMember]
		public bool? PreserveIndentation { get; set; }

		[DataMember]
		public ObservableCollection<ParserSettingsItem> Parsers { get; set; } = new ObservableCollection<ParserSettingsItem>();

		public LandExplorerSettings Clone()
		{
			return new LandExplorerSettings()
			{
				Id = Id,
				SaveAbsolutePath = SaveAbsolutePath,
				EnableAutosave = EnableAutosave,
				PreserveIndentation = PreserveIndentation,
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
