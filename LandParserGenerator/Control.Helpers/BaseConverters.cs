using System;

namespace Land.Control.Helpers
{
	/// <summary>
	/// Конвертер свойства типа double?
	/// </summary>
	public class DoubleConverter: PropertyConverter
	{
		public override string ToString(object val)
		{
			return val.ToString();
		}

		public override object ToValue(string str)
		{
			return double.TryParse(str, out double doubleNum)
				? doubleNum : (double?)null;
		}
	}

	/// <summary>
	/// Конвертер свойства типа int?
	/// </summary>
	public class IntConverter : PropertyConverter
	{
		public override string ToString(object val)
		{
			return val.ToString();
		}

		public override object ToValue(string str)
		{
			return int.TryParse(str, out int intNum)
				? intNum : (int?)null;
		}
	}
}
