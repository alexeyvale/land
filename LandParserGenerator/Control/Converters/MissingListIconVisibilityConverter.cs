using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

using Land.Markup;

namespace Land.Control
{
	public class MissingListIconVisibilityConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			var label = (Label)values[0];

			if (values[1] is RemapCandidates pair)
			{
				switch (label.Name)
				{
					case "MissingIcon":
						return pair.Candidates.Count() == 0
								? Visibility.Visible : Visibility.Hidden;

					case "AmbiguousIcon":
						return pair.Candidates.Count() == 0
								? Visibility.Hidden : Visibility.Visible;
				}
			}

			return Visibility.Hidden;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			return new object[] { };
		}
	}
}
