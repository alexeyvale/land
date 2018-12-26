using System.Runtime.Serialization;

namespace Land.Control
{
	[DataContract(IsReference = true)]
	public class PreprocessorProperty
	{
		[DataMember]
		public string DisplayedName { get; set; }
		[DataMember]
		public string PropertyName { get; set; }
		[DataMember]
		public string ValueString { get; set; }

		public PreprocessorProperty Clone()
		{
			return new PreprocessorProperty()
			{
				DisplayedName = DisplayedName,
				PropertyName = PropertyName,
				ValueString = ValueString
			};
		}
	}
}
