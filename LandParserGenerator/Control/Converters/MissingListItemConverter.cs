using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Land.Core.Markup;

namespace Land.Control
{
	public class MissingListItemConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return value is RemapCandidates pair1
				? pair1.Point?.Name
				: value is IRemapCandidateInfo pair2
					? $"{new ExistingConcernPointCandidate(pair2.Node).ViewHeader}\t{pair2.ToString()}"
					: null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return null;
		}
	}
}
