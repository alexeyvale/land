using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Land.Core.Markup;

namespace Land.Control
{
	/// <summary>
	/// Точка привязки и информация о местах, которые могут ей соответствовать
	/// </summary>
	public class RemapCandidates
	{
		public ConcernPoint Point { get; set; }

		public ObservableCollection<RemapCandidateInfo> Candidates { get; set; } =
			new ObservableCollection<RemapCandidateInfo>();
	}
}
