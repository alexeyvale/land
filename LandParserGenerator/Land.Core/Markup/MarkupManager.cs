using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Land.Core;
using Land.Core.Parsing.Tree;
using Land.Markup.Binding;
using Land.Markup.Relations;
using Land.Markup.Tree;
using Land.Markup.CoreExtension;

namespace Land.Markup
{
	public class MarkupManager
	{
		public MarkupManager(Func<string, ParsedFile> getParsed)
		{
			ContextFinder.GetParsed = getParsed;
			OnMarkupChanged += InvalidateRelations;

			#region Подключение эвристик

			ContextFinder.SetHeuristic(typeof(EmptyContextHeuristic));
			ContextFinder.SetHeuristic(typeof(PrioritizeByGapHeuristic));
			ContextFinder.SetHeuristic(typeof(LowerChangedInnerPriority));
			ContextFinder.SetHeuristic(typeof(DefaultWeightsHeuristic));
			ContextFinder.SetHeuristic(typeof(SameHeaderAndAncestorsHeuristic));

			#endregion
		}

		private RelationsManager Relations { get; set; } = new RelationsManager();

		public List<RelationNotification> TryGetRelations(out RelationsManager relationsManager)
		{
			if (IsValid)
			{
				if (!Relations.IsValid)
					Relations.RefreshCache(Markup);

				relationsManager = Relations;
				return Relations.CheckConsistency();
			}

			relationsManager = null;
			return new List<RelationNotification>();
		}

		public ContextFinder ContextFinder { get; private set; } = new ContextFinder();

		/// <summary>
		/// Коллекция точек привязки
		/// </summary>
		public ObservableCollection<MarkupElement> Markup = new ObservableCollection<MarkupElement>();

		/// <summary>
		/// Событие изменения разметки
		/// </summary>
		public event Action OnMarkupChanged;

		/// <summary>
		/// Очистка разметки
		/// </summary>
		public void Clear()
		{
			Markup.Clear();

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Проверка того, что вся разметка синхронизирована с кодом
		/// </summary>
		/// <returns></returns>
		public bool IsValid => !GetLinearSequenceVisitor.GetPoints(Markup).Any(p => p.HasInvalidLocation);

		/// <summary>
		/// Помечаем отношения как нерелевантные относительно разметки
		/// </summary>
		public void InvalidateRelations()
		{
			Relations.IsValid = false;
		}

		/// <summary>
		/// Сброс узлов дерева у всех точек, связанных с указанным файлом
		/// </summary>
		public void InvalidatePoints(string fileName)
		{
			var stubNode = new Node("");
			stubNode.SetLocation(new PointLocation(0, 0, 0), new PointLocation(0, 0, 0));

			DoWithMarkup((MarkupElement elem) =>
			{
				if (elem is ConcernPoint concernPoint
					&& concernPoint.Context.FileContext.Name == fileName)
				{
					concernPoint.AstNode = stubNode;
					concernPoint.HasIrrelevantLocation = true;
				}
			});
		}

		/// <summary>
		/// Удаление элемента разметки
		/// </summary>
		public void RemoveElement(MarkupElement elem)
		{
			if (elem.Parent != null)
				elem.Parent.Elements.Remove(elem);
			else
				Markup.Remove(elem);

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Добавление функциональности
		/// </summary>
		public Concern AddConcern(string name, string comment = null, Concern parent = null)
		{
			var concern = new Concern(name, comment, parent);
			AddElement(concern);

			OnMarkupChanged?.Invoke();
			return concern;
		}

		/// <summary>
		/// Добавление точки привязки
		/// </summary>
		public ConcernPoint AddConcernPoint(
			Node node, 
			ParsedFile file, 
			List<ParsedFile> searchArea,
			Func<string, ParsedFile> getParsed,
			string name = null, 
			string comment = null, 
			Concern parent = null)
		{
			var point = new ConcernPoint(
				node, ContextFinder.ContextManager.GetContext(node, file, searchArea, getParsed, ContextFinder), parent
			);

			if (!String.IsNullOrEmpty(name))
				point.Name = name;
			point.Comment = comment;

			AddElement(point);

			OnMarkupChanged?.Invoke();
			return point;
		}

		/// <summary>
		/// Добавление всей "суши", присутствующей в дереве разбора
		/// </summary>
		public void AddLand(
			ParsedFile file,
			List<ParsedFile> searchArea,
			Func<string, ParsedFile> getParsed)
		{
			var visitor = new LandExplorerVisitor();
			file.Root.Accept(visitor);

			/// Группируем land-сущности по типу (символу)
			foreach (var group in visitor.Land.GroupBy(l => l.Symbol))
			{
				var concern = AddConcern(group.Key);

				/// В пределах символа группируем по псевдониму
				var subgroups = group.GroupBy(g => g.Alias);

				/// Для всех точек, для которых указан псевдоним
				foreach (var subgroup in subgroups.Where(s => !String.IsNullOrEmpty(s.Key)))
				{
					/// создаём подфункциональность
					var subconcern = AddConcern(subgroup.Key, null, concern);

					foreach (var node in subgroup)
					{
						AddElement(new ConcernPoint(
							node, ContextFinder.ContextManager.GetContext(node, file, searchArea, getParsed, ContextFinder), subconcern)
						);
					}
				}

				/// Остальные добавляются напрямую к функциональности, соответствующей символу
				var nodes = subgroups.Where(s => String.IsNullOrEmpty(s.Key))
					.SelectMany(s => s).ToList();

				foreach (var node in nodes)
				{
					AddElement(new ConcernPoint(
						node, ContextFinder.ContextManager.GetContext(node, file, searchArea, getParsed, ContextFinder), concern)
					);
				}
			}

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Получение всех узлов, к которым можно привязаться,
		/// если команда привязки была вызвана в позиции offset
		/// </summary>
		public LinkedList<Node> GetConcernPointCandidates(Node root, SegmentLocation selection)
		{
			var pointCandidates = new LinkedList<Node>();
			var currentNode = root;

			/// В качестве кандидатов на роль помечаемого участка рассматриваем узлы от корня,
			/// содержащие текущую позицию каретки
			while (currentNode != null)
			{
				if (currentNode.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
					pointCandidates.AddFirst(currentNode);

				currentNode = currentNode.Children
					.Where(c => c.Location != null && c.Location.Includes(selection))
					.FirstOrDefault();
			}

			return pointCandidates;
		}

		/// <summary>
		/// Смена узла, к которому привязана точка
		/// </summary>
		public void RelinkConcernPoint(
			ConcernPoint point, 
			Node node, 
			ParsedFile file,
			List<ParsedFile> searchArea,
			Func<string, ParsedFile> getParsed)
		{
			point.Relink(node, ContextFinder.ContextManager.GetContext(node, file, searchArea, getParsed, ContextFinder));

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Смена узла, к которому привязана точка
		/// </summary>
		public void RelinkConcernPoint(ConcernPoint point, RemapCandidateInfo candidate)
		{
			point.Relink(candidate);

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Получение списка точек привязки для текущего дерева разметки
		/// </summary>
		public List<ConcernPoint> GetConcernPoints()
		{
			return GetLinearSequenceVisitor.GetPoints(Markup);
		}

		/// <summary>
		/// Перемещение элемента разметки к новому родителю
		/// </summary>
		public void MoveTo(Concern newParent, MarkupElement elem)
		{
			if (elem.Parent != null)
				elem.Parent.Elements.Remove(elem);
			else
				Markup.Remove(elem);

			elem.Parent = newParent;

			if(newParent != null)
				newParent.Elements.Add(elem);
			else
				Markup.Add(elem);

			OnMarkupChanged?.Invoke();
		}

		public void Serialize(string fileName, bool useRelativePaths)
		{
			if (useRelativePaths)
			{
				/// Превращаем указанные в точках привязки абсолютные пути в пути относительно файла разметки
				var directoryUri = new Uri(Path.GetDirectoryName(fileName) + "/");
				DoWithMarkup((MarkupElement elem) =>
				{
					if (elem is ConcernPoint p)
					{
						p.Context.FileContext.Name = Uri.UnescapeDataString(
							directoryUri.MakeRelativeUri(new Uri(p.Context.FileContext.Name)).ToString()
						);
					}
				});
			}

			using (StreamWriter fs = new StreamWriter(fileName, false))
			{
				var unit = new SerializationUnit()
				{
					Markup = Markup,
					ExternalRelatons = Relations.ExternalRelations.GetRelatedPairs()
				};

				fs.Write(JsonConvert.SerializeObject(unit, Formatting.Indented));
			}

			if (useRelativePaths)
			{
				/// Трансформируем пути обратно в абсолютные
				DoWithMarkup((MarkupElement elem) =>
				{
					if (elem is ConcernPoint p)
					{
						p.Context.FileContext.Name = Path.GetFullPath(
							Path.Combine(Path.GetDirectoryName(fileName), p.Context.FileContext.Name)
						);
					}
				});
			}
		}

		public void Deserialize(string fileName)
		{
			Clear();

			using (StreamReader fs = new StreamReader(fileName))
			{
				var unit = JsonConvert.DeserializeObject<SerializationUnit>(fs.ReadToEnd(),
					new JsonSerializerSettings()
					{
						Converters = { new MarkupElementConverter() }
					});

				/// Фиксируем разметку
				Markup = unit.Markup;

				/// Восстанавливаем обратные связи между потомками и предками
				DoWithMarkup(e =>
				{
					if (e is Concern c)
					{
						foreach (var elem in c.Elements)
							elem.Parent = c;
					}
				});

				/// Запоминаем external-отношения между функциональностями
				Relations.RefreshElements(Markup);

				foreach (var pair in unit.ExternalRelatons)
					Relations.AddExternalRelation(pair.RelationType, pair.Item0, pair.Item1);
			}

			DoWithMarkup((MarkupElement elem) =>
			{
				if (elem is ConcernPoint p && !Path.IsPathRooted(p.Context.FileContext.Name))
				{
					p.Context.FileContext.Name = Path.GetFullPath(
						Path.Combine(Path.GetDirectoryName(fileName), p.Context.FileContext.Name)
					);
				}
			});
		}

		/// <summary>
		/// Поиск узла дерева, которому соответствует заданная точка привязки
		/// </summary>
		public List<RemapCandidateInfo> Find(ConcernPoint point, ParsedFile targetInfo)
		{
			return ContextFinder.Find(point, new List<ParsedFile> { targetInfo }, false);
		}

		/// <summary>
		/// Получение списка файлов, в которых есть точки привязки
		/// </summary>
		public HashSet<string> GetReferencedFiles()
		{
			return new HashSet<string>(
				GroupPointsByFileVisitor.GetGroups(Markup).Select(p => p.Key)
			);
		}

		#region Перепривязка

		/// <summary>
		/// Размер топа кандидатов, ранжированных по похожести, возвращаемого при неоднозначности
		/// </summary>
		public int AmbiguityTopCount { get; set; } = 10;

		/// <summary>
		/// Похожесть, ниже которой не рассматриваем элемент как кандидата
		/// </summary>
		public double GarbageThreshold { get; set; } = 0.4;

		public Dictionary<ConcernPoint, List<RemapCandidateInfo>> Remap(
			List<ParsedFile> searchArea,
			bool localOnly,
			bool allowAutoDecisions)
		{
			var ambiguous = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();
			var result = ContextFinder.Find(GetConcernPoints(), searchArea, localOnly);

			foreach (var kvp in result)
			{
				var candidates = kvp.Value
					//.TakeWhile(c=>c.Similarity >= GarbageThreshold)
					.Take(AmbiguityTopCount).ToList();

				if (!allowAutoDecisions || 
					!ApplyCandidate(kvp.Key, candidates, searchArea, ContextFinder.GetParsed))
					ambiguous[kvp.Key] = candidates;
			}

			OnMarkupChanged?.Invoke();

			return ambiguous;
		}

		/// <summary>
		/// Перепривязка точки
		/// </summary>
		public Dictionary<ConcernPoint, List<RemapCandidateInfo>> Remap(
			ConcernPoint point, 
			List<ParsedFile> searchArea,
			bool localOnly)
		{
			var ambiguous = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();
			var candidates = ContextFinder.Find(point, searchArea, localOnly)
				.TakeWhile(c => c.Similarity >= GarbageThreshold)
				.Take(AmbiguityTopCount).ToList();

			if (!ApplyCandidate(point, candidates, searchArea, ContextFinder.GetParsed))
				ambiguous[point] = candidates;

			OnMarkupChanged?.Invoke();

			return ambiguous;
		}

		private bool ApplyCandidate(
			ConcernPoint point, 
			IEnumerable<RemapCandidateInfo> candidates,
			List<ParsedFile> searchArea,
			Func<string, ParsedFile> getParsed)
		{
			var first = candidates.FirstOrDefault();

			if (first?.IsAuto ?? false)
			{
				point.Context = ContextFinder.ContextManager.GetContext(
					first.Node, first.File, searchArea, getParsed, ContextFinder
				);
				point.AstNode = first.Node;
				return true;
			}
			else
			{
				point.AstNode = null;
				return false;
			}
		}

		#endregion

		/// <summary>
		/// Обобщённое добавление элемента разметки
		/// </summary>
		/// <param name="elem"></param>
		private void AddElement(MarkupElement elem)
		{
			if (elem.Parent == null)
				Markup.Add(elem);
			else
				elem.Parent.Elements.Add(elem);
		}

		/// <summary>
		/// Совершение заданного действия со всеми элементами разметки
		/// </summary>
		public void DoWithMarkup(Action<MarkupElement> action)
		{
			foreach (var elem in Markup)
				DoWithMarkupSubtree(action, elem);
		}

		/// <summary>
		/// Совершение заданного действия со всеми элементами поддерева разметки
		/// </summary>
		private void DoWithMarkupSubtree(Action<MarkupElement> action, MarkupElement root)
		{
			var elements = new Queue<MarkupElement>();
			elements.Enqueue(root);

			while (elements.Count > 0)
			{
				var elem = elements.Dequeue();

				if (elem is Concern concern)
					foreach (var child in concern.Elements)
						elements.Enqueue(child);

				action(elem);
			}
		}
	}
}
