using System;
using System.Collections.Generic;
using System.Linq;

namespace Land.Markup.Binding
{
	public class LocationManager
	{
		public class LocationInfo
		{
			public int CountBefore { get; set; }
			public int CountAfter { get; set; }
			public int OffsetBefore { get; set; }
			public int OffsetAfter { get; set; }

			public double LocationSimilarity { get; set; }

			public bool Permutation { get; set; }
		}

		private List<PointContext> Contexts { get; set; }
		private Dictionary<PointContext, LocationInfo> ContextToLocationInfo { get; set; }

		public LocationManager(IEnumerable<PointContext> contexts)
		{
			Contexts = contexts.ToList();

			ContextToLocationInfo = Contexts
				.ToDictionary(e => e, e => new LocationInfo
				{
					OffsetBefore = 0,
					OffsetAfter = int.MaxValue,
					CountBefore = 0,
					CountAfter = 0
				});
		}

		/// <summary>
		/// Фиксация знания о том, что ранее помеченную точка source
		/// в новом файле соответствует точке target
		/// </summary>
		public void Mapped(PointContext source, PointContext target)
		{
			foreach(var elem in Contexts)
			{
				/// Пропускаем сам перепривязанный элемент и элементы, охватывающие его
				if(elem == source
					|| elem.SiblingsContext.CountBefore < source.SiblingsContext.CountBefore
					&& elem.SiblingsContext.CountAfter < source.SiblingsContext.CountAfter)
				{
					continue;
				}

				var isInside = elem.SiblingsContext.CountBefore > source.SiblingsContext.CountBefore
					&& elem.SiblingsContext.CountAfter > source.SiblingsContext.CountAfter;

				if(ContextToLocationInfo[elem].CountBefore <= source.SiblingsContext.CountBefore
					&& elem.SiblingsContext.CountBefore > source.SiblingsContext.CountBefore)
				{
					ContextToLocationInfo[elem].CountBefore = source.SiblingsContext.CountBefore 
						+ (isInside ? 1 : source.SiblingsContext.CountInside);
					ContextToLocationInfo[elem].OffsetBefore = (isInside ? target.StartOffset : target.EndOffset);
				}

				if (ContextToLocationInfo[elem].CountAfter <= source.SiblingsContext.CountAfter
					&& elem.SiblingsContext.CountAfter > source.SiblingsContext.CountAfter)
				{
					ContextToLocationInfo[elem].CountAfter = source.SiblingsContext.CountAfter
						+ (isInside ? 1 : source.SiblingsContext.CountInside);
					ContextToLocationInfo[elem].OffsetAfter = (isInside ? target.EndOffset : target.StartOffset);
				}

				UpdateSimilarity(elem);
			}
		}

		public int GetFrom(PointContext source) =>
			ContextToLocationInfo[source].OffsetBefore;

		public int GetTo(PointContext source) =>
			ContextToLocationInfo[source].OffsetAfter;

		public bool InRange(PointContext source, PointContext target) =>
			GetFrom(source) <= target.StartOffset
			&& GetTo(source) >= target.EndOffset;

		public double GetLocationSimilarity(PointContext source) =>
			ContextToLocationInfo[source].LocationSimilarity;

		public double GetSimilarity(PointContext source, PointContext target) =>
			InRange(source, target) ? ContextToLocationInfo[source].LocationSimilarity : 0;

		private void UpdateSimilarity(PointContext source)
		{
			if (source.SiblingsContext.CountBefore > 0 || source.SiblingsContext.CountAfter > 0)
			{
				ContextToLocationInfo[source].LocationSimilarity =
					(ContextToLocationInfo[source].CountBefore + ContextToLocationInfo[source].CountAfter)
						/ (double)(source.SiblingsContext.CountBefore + source.SiblingsContext.CountAfter);
			}
			else
			{
				ContextToLocationInfo[source].LocationSimilarity = 0;
			}
		}
	}
}
