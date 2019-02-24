using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ComponentModel;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	[DataContract]
	public struct RelatedPair<T>
	{
		[DataMember]
		public RelationType RelationType { get; set; }

		[DataMember]
		public T Item0 { get; set; }

		[DataMember]
		public T Item1 { get; set; }
	}

	public class RelationsCache
	{
		/// <summary>
		/// Закешированные отношения
		/// </summary>
		private Dictionary<RelationType, Dictionary<MarkupElement, HashSet<MarkupElement>>> Cache { get; set; } =
			new Dictionary<RelationType, Dictionary<MarkupElement, HashSet<MarkupElement>>>();

		/// <summary>
		/// Множество элементов разметки, для которого кешируются отношения
		/// </summary>
		private HashSet<MarkupElement> KnownElements { get; set; } = new HashSet<MarkupElement>();

		public RelationsCache(RelationGroup group)
		{
			foreach (var type in ((RelationType[])Enum.GetValues(typeof(RelationType))).Where(r => r.GetGroup() == group))
				Cache[type] = new Dictionary<MarkupElement, HashSet<MarkupElement>>();
		}

		/// <summary>
		/// Получение списка всех связанных отношениями элементов
		/// </summary>
		public List<RelatedPair<MarkupElement>> GetRelatedPairs(params RelationType[] relationTypes)
		{
			var res = new List<RelatedPair<MarkupElement>>();

			if (relationTypes.Length == 0)
				relationTypes = Cache.Keys.ToArray();

			foreach (var rel in relationTypes)
				if(Cache.ContainsKey(rel))
					foreach (var item0 in Cache[rel].Keys)
						res.AddRange(Cache[rel][item0].Select(e => new RelatedPair<MarkupElement>()
						{
							RelationType = rel,
							Item0 = item0,
							Item1 = e
						}));

			return res;
		}

		/// <summary>
		/// Проверка того, что элементы состоят в указанном отношении
		/// </summary>
		public bool AreRelated(MarkupElement a, MarkupElement b, RelationType relation) =>
			Cache.ContainsKey(relation) && Cache[relation].ContainsKey(a) && Cache[relation][a].Contains(b);

		/// <summary>
		/// Получение всех элементов, связанных отношением с указанным
		/// </summary>
		public HashSet<MarkupElement> GetRelated(MarkupElement from, RelationType relation) =>
			Cache.ContainsKey(relation) && Cache[relation].ContainsKey(from) ? Cache[relation][from] : null;

		/// <summary>
		/// Убираем из кеша информацию об элементах, связанных отношениями
		/// </summary>
		public void Clear()
		{
			foreach (RelationType relType in Cache.Keys.ToList())
				Cache[relType] = new Dictionary<MarkupElement, HashSet<MarkupElement>>();

			KnownElements = new HashSet<MarkupElement>();
		}

		/// <summary>
		/// Инициализируем все отношения списком элементов,
		/// для которых впоследствии зададим связанные с ними элементы
		/// </summary>
		public void SetElements(IEnumerable<MarkupElement> elements)
		{
			Clear();

			foreach(var key in Cache.Keys.ToList())
				Cache[key] = elements.ToDictionary(c => c, c => new HashSet<MarkupElement>());

			KnownElements.UnionWith(elements);
		}

		/// <summary>
		/// Обновляем список элементов и удаляем отношения,
		/// в которых участвуют отсутствующие в списке элементы
		/// </summary>
		public void RefreshElements(IEnumerable<MarkupElement> elements)
		{
			foreach(var rel in Cache.Keys)
			{
				foreach (var key in Cache[rel].Keys.Where(k => !elements.Contains(k)).ToList())
					Cache[rel].Remove(key);

				foreach (var key in Cache[rel].Keys)
					Cache[rel][key].IntersectWith(elements);
			}

			KnownElements.Clear();
			KnownElements.UnionWith(elements);
		}

		/// <summary>
		/// Добавление связи между элементами
		/// </summary>
		public bool AddRelation(RelationType type, MarkupElement from, MarkupElement to)
		{
			if (!Cache.ContainsKey(type) || !KnownElements.Contains(from) || !KnownElements.Contains(to))
				return false;

			Cache[type][from].Add(to);

			if (type.GetAttribute<RelationPropertiesAttribute>().IsSymmetric)
			{
				Cache[type][to].Add(from);
			}

			if (type.GetAttribute<RelationPropertiesAttribute>().IsTransitive)
			{
				FillAsClosure(type, type);
			}

			return true;
		}

		/// <summary>
		/// Массовое добавление связи между элементами
		/// </summary>
		public bool AddRelation(RelationType type, MarkupElement from, IEnumerable<MarkupElement> to)
		{
			if (!Cache.ContainsKey(type) || !KnownElements.Contains(from) || to.Any(e=>!KnownElements.Contains(e)))
				return false;

			Cache[type][from] = new HashSet<MarkupElement>(to);

			if (type.GetAttribute<RelationPropertiesAttribute>().IsSymmetric)
			{
				foreach(var toElem in to)
					Cache[type][toElem].Add(from);
			}

			if (type.GetAttribute<RelationPropertiesAttribute>().IsTransitive)
			{
				FillAsClosure(type, type);
			}

			return true;
		}

		public void FillAsTransposition(RelationType target, RelationType source)
		{
			if (Cache.ContainsKey(source))
			{
				var result = Cache[source].SelectMany(kvp => kvp.Value).Distinct()
					.ToDictionary(v => v, v => new HashSet<MarkupElement>());

				foreach (var leftPart in Cache[source].Keys)
					foreach (var rightPart in Cache[source][leftPart])
						result[rightPart].Add(leftPart);

				Cache[target] = result;
			}
		}

		public void FillAsClosure(RelationType target, RelationType source)
		{
			if (Cache.ContainsKey(source))
			{
				/// Копируем исходное отношение в целевое
				var sourceCopy = new Dictionary<MarkupElement, HashSet<MarkupElement>>();

				foreach (var leftPart in Cache[source].Keys)
					sourceCopy[leftPart] = new HashSet<MarkupElement>(Cache[source][leftPart]);

				Cache[target] = sourceCopy;

				/// Итеративно строим замыкание
				var changed = true;

				while (changed)
				{
					changed = false;

					foreach (var leftPart in Cache[target].Keys)
					{
						var toAdd = new HashSet<MarkupElement>();
						var oldCount = Cache[target][leftPart].Count;

						foreach (var rightPart in Cache[target][leftPart])
							toAdd.UnionWith(Cache[target][rightPart]);

						Cache[target][leftPart].UnionWith(toAdd);

						if (Cache[target][leftPart].Count > oldCount)
							changed = true;
					}
				}
			}
		}		
	}
}
