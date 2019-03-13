using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public class MarkupManager
	{
		public MarkupManager()
		{
			OnMarkupChanged += InvalidateRelations;
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

		/// <summary>
		/// Коллекция точек привязки
		/// </summary>
		public ObservableCollection<MarkupElement> Markup = new ObservableCollection<MarkupElement>();

		/// <summary>
		/// Якоря, на которые могут ссылаться точки привязки
		/// </summary>
		public List<AnchorPoint> Anchors = new List<AnchorPoint>();

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
			Anchors.Clear();

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Проверка того, что вся разметка синхронизирована с кодом
		/// </summary>
		/// <returns></returns>
		public bool IsValid => !Anchors.Any(a => a.HasInvalidLocation);

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
			stubNode.SetAnchor(new PointLocation(0, 0, 0), new PointLocation(0, 0, 0));

			Anchors.ForEach(a =>
			{
				if (a.Context.FileName == fileName)
				{
					a.HasIrrelevantLocation = true;
					a.AstNode = stubNode;
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

			foreach(var point in GetLinearSequenceVisitor.GetPoints(new List<MarkupElement> { elem }))
				point.Anchor = null;

			Anchors.RemoveAll(a => a.Links.Count == 0);

			OnMarkupChanged?.Invoke();
		}

		public AnchorPoint GetAnchor(PointContext context, Node astNode)
		{
			var anchor = GetExistingAnchor(astNode);

			if(anchor == null)
			{
				anchor = new AnchorPoint(context, astNode);
				Anchors.Add(anchor);
			}

			return anchor;
		}

		public AnchorPoint GetExistingAnchor(Node astNode)
		{
			return Anchors.FirstOrDefault(a => a.AstNode == astNode);
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
		public ConcernPoint AddConcernPoint(TargetFileInfo sourceInfo, string name = null, string comment = null, Concern parent = null)
		{
			var point = new ConcernPoint(
				GetAnchor(PointContext.Create(sourceInfo), sourceInfo.TargetNode), parent
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
		public void AddLand(TargetFileInfo sourceInfo)
		{
			var visitor = new LandExplorerVisitor();
			/// При добавлении всей суши к разметке, в качестве целевого узла передаётся корень дерева
			sourceInfo.TargetNode.Accept(visitor);

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

					foreach (var point in subgroup)
					{
						sourceInfo.TargetNode = point;
						AddElement(new ConcernPoint(
							GetAnchor(PointContext.Create(sourceInfo), sourceInfo.TargetNode), 
							subconcern
						));
					}
				}

				/// Остальные добавляются напрямую к функциональности, соответствующей символу
				var points = subgroups.Where(s => String.IsNullOrEmpty(s.Key))
					.SelectMany(s => s).ToList();

				foreach (var point in points)
				{
					sourceInfo.TargetNode = point;
					AddElement(new ConcernPoint(
						GetAnchor(PointContext.Create(sourceInfo), sourceInfo.TargetNode),
						concern
					));
				}
			}

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Получение всех узлов, к которым можно привязаться,
		/// если команда привязки была вызвана в позиции offset
		/// </summary>
		public LinkedList<Node> GetConcernPointCandidates(Node root, int offset)
		{
			var pointCandidates = new LinkedList<Node>();
			var currentNode = root;

			/// В качестве кандидатов на роль помечаемого участка рассматриваем узлы от корня,
			/// содержащие текущую позицию каретки
			while (currentNode != null)
			{
				if (currentNode.Options.IsLand)
					pointCandidates.AddFirst(currentNode);

				currentNode = currentNode.Children.Where(c => c.Location != null && c.Location.Start != null && c.Location.End != null
					&& c.Location.Start.Offset <= offset && c.Location.End.Offset >= offset).FirstOrDefault();
			}

			return pointCandidates;
		}

		/// <summary>
		/// Смена узла, к которому привязана точка
		/// </summary>
		public void RelinkPoint(ConcernPoint point, PointContext context, Node astNode)
		{
			if (point.Anchor.Links.Count == 1)
				Anchors.Remove(point.Anchor);

			point.Anchor = GetAnchor(context, astNode);

			Anchors.Add(point.Anchor);

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Сдвиг узла, к которому привязана точка
		/// </summary>
		public void ShiftAnchor(AnchorPoint anchor, PointContext context, Node astNode)
		{
			var existing = GetExistingAnchor(astNode);

			if (existing == null)
			{
				anchor.AstNode = astNode;
				anchor.Context = context;
			}
			else
			{
				Anchors.Remove(anchor);

				foreach (var p in GetLinearSequenceVisitor.GetPoints(Markup).Where(p => p.Anchor == anchor))
					p.Anchor = existing;
			}

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

				Anchors.ForEach(a=>
				{
					a.Context.FileName = Uri.UnescapeDataString(
						directoryUri.MakeRelativeUri(new Uri(a.Context.FileName)).ToString());
				});
			}

			using (FileStream fs = new FileStream(fileName, FileMode.Create))
			{
				using (var gZipStream = new GZipStream(fs, CompressionLevel.Optimal))
				{
					var unit = new SerializationUnit()
					{
						Markup = Markup,
						ExternalRelatons = Relations.ExternalRelations.GetRelatedPairs()
					};

					DataContractSerializer serializer = new DataContractSerializer(typeof(SerializationUnit), new List<Type>() {
						typeof(Concern),
						typeof(ConcernPoint),
						typeof(AnchorPoint),
						typeof(PointContext),
						typeof(HeaderContextElement),
						typeof(AncestorsContextElement),
						typeof(ObservableCollection<MarkupElement>),
						typeof(List<RelatedPair<MarkupElement>>),
						typeof(RelatedPair<MarkupElement>)
					});

					serializer.WriteObject(gZipStream, unit);
				}
			}

			if (useRelativePaths)
			{
				/// Трансформируем пути обратно в абсолютные
				Anchors.ForEach(a =>
				{
					a.Context.FileName = Path.GetFullPath(
						Path.Combine(Path.GetDirectoryName(fileName), a.Context.FileName)
					);
				});
			}
		}

		public void Deserialize(string fileName)
		{
			Clear();

			using (FileStream fs = new FileStream(fileName, FileMode.Open))
			{
				using (var gZipStream = new GZipStream(fs, CompressionMode.Decompress))
				{
					DataContractSerializer serializer = new DataContractSerializer(typeof(SerializationUnit), new List<Type>() {
						typeof(Concern),
						typeof(ConcernPoint),
						typeof(AnchorPoint),
						typeof(PointContext),
						typeof(HeaderContextElement),
						typeof(AncestorsContextElement),
						typeof(ObservableCollection<MarkupElement>),
						typeof(List<RelatedPair<MarkupElement>>),
						typeof(RelatedPair<MarkupElement>)
					});

					var unit = (SerializationUnit)serializer.ReadObject(gZipStream);

					/// Фиксируем разметку
					Markup = unit.Markup;

					/// Вытаскиваем якоря
					Anchors = GetConcernPoints().Select(p => p.Anchor).Distinct().ToList();
					
					/// Запоминаем external-отношения между функциональностями
					Relations.RefreshElements(Markup);

					foreach (var pair in unit.ExternalRelatons)
						Relations.AddExternalRelation(pair.RelationType, pair.Item0, pair.Item1);
				}
			}

			Anchors.ForEach(a =>
			{
				if(!Path.IsPathRooted(a.Context.FileName))
				{
					a.Context.FileName = Path.GetFullPath(
						Path.Combine(Path.GetDirectoryName(fileName), a.Context.FileName)
					);
				}
			});
		}

		/// <summary>
		/// Поиск узла дерева, которому соответствует заданная точка привязки
		/// </summary>
		public List<CandidateInfo> Find(AnchorPoint point, TargetFileInfo targetInfo)
		{
			return ContextFinder.Find(point, targetInfo);
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
		/// Значение похожести, ниже которого нельзя выполнять автоматическую перепривязку
		/// </summary>
		public double AcceptanceThreshold { get; set; } = 0.8;

		/// <summary>
		/// Дельта похожести между лучшим кандидатом и следующим, при которой можно автоматически
		/// перепривязаться к лучшему кандидату
		/// </summary>
		public double DistanceToClosestThreshold { get; set; } = 0.05;

		/// <summary>
		/// Размер топа кандидатов, ранжированных по похожести, возвращаемого при неоднозначности
		/// </summary>
		public int AmbiguityTopCount { get; set; } = 10;

		/// <summary>
		/// Похожесть, ниже которой не рассматриваем элемент как кандидата
		/// </summary>
		public double GarbageThreshold { get; set; } = 0.4;

		public Dictionary<AnchorPoint, List<CandidateInfo>> Remap(List<TargetFileInfo> targetFiles, bool useLocalRemap)
		{
			var ambiguous = useLocalRemap
				? LocalRemap(targetFiles)
				: GlobalRemap(targetFiles);

			OnMarkupChanged?.Invoke();

			return ambiguous;
		}

		private Dictionary<AnchorPoint, List<CandidateInfo>> LocalRemap(List<TargetFileInfo> targetFiles)
		{
			var groupedByFile = Anchors.GroupBy(a=>a.Context.FileName).ToList();
			var ambiguous = new Dictionary<AnchorPoint, List<CandidateInfo>>();

			foreach(var fileGroup in groupedByFile)
			{
				var file = targetFiles.Where(f => f.FileName == fileGroup.Key).FirstOrDefault();

				if(file != null)
				{
					var groupedByType = fileGroup.GroupBy(p => p.Context.NodeType).ToDictionary(g => g.Key, g => g.ToList());
					var groupedFiles = GroupNodesByTypeVisitor.GetGroups(file.TargetNode, groupedByType.Keys);

					var result = ContextFinder.Find(groupedByType, groupedFiles, file);

					foreach (var kvp in result)
					{
						var candidates = kvp.Value.OrderByDescending(c => c.Similarity)
							.TakeWhile(c=>c.Similarity >= GarbageThreshold)
							.Take(AmbiguityTopCount).ToList();

						if (!TryApplyCandidate(kvp.Key, candidates))
							ambiguous[kvp.Key] = candidates;
					}
				}
				else
				{
					foreach (var point in fileGroup)
						point.AstNode = null;
				}
			}

			return ambiguous;
		}

		private Dictionary<AnchorPoint, List<CandidateInfo>> GlobalRemap(List<TargetFileInfo> targetFiles)
		{
			var ambiguous = new Dictionary<AnchorPoint, List<CandidateInfo>>();

			/// Группируем точки привязки по типу помеченной сущности 
			var groupedPoints = Anchors.GroupBy(a => a.Context.NodeType).ToDictionary(g=>g.Key, g=>g.ToList());
			var accumulator = Anchors.ToDictionary(e => e, e => new List<CandidateInfo>());

			foreach (var file in targetFiles)
			{
				/// Группируем узлы AST файла, к которому попытаемся перепривязаться,
				/// по типам точек, к которым требуется перепривязка
				var groupedFiles = GroupNodesByTypeVisitor.GetGroups(file.TargetNode, groupedPoints.Keys);

				/// Похожести, посчитанные для сущностей из текущего файла
				var currentRes = ContextFinder.Find(groupedPoints, groupedFiles, file);

				foreach (var kvp in currentRes)
					accumulator[kvp.Key].AddRange(kvp.Value);
			}

			foreach (var kvp in accumulator)
			{
				var candidates = kvp.Value.OrderByDescending(c => c.Similarity)
					.TakeWhile(c => c.Similarity >= GarbageThreshold)
					.Take(AmbiguityTopCount).ToList();

				if (!TryApplyCandidate(kvp.Key, candidates))
					ambiguous[kvp.Key] = candidates;
			}

			return ambiguous;
		}

		public Dictionary<ConcernPoint, List<CandidateInfo>> Remap(ConcernPoint point, TargetFileInfo targetInfo)
		{
			var ambiguous = new Dictionary<ConcernPoint, List<CandidateInfo>>();

			if (point.HasInvalidLocation)
			{
				var candidates = GetCandidates(point.Anchor, targetInfo);

				if (!TryApplyCandidate(point, candidates))
					ambiguous[point] = candidates;

				OnMarkupChanged?.Invoke();
			}

			return ambiguous;
		}

		public Dictionary<AnchorPoint, List<CandidateInfo>> Remap(AnchorPoint anchor, TargetFileInfo targetInfo)
		{
			var ambiguous = new Dictionary<AnchorPoint, List<CandidateInfo>>();

			if (anchor.HasInvalidLocation)
			{
				var candidates = GetCandidates(anchor, targetInfo);

				if (!TryApplyCandidate(anchor, candidates))
					ambiguous[anchor] = candidates;

				OnMarkupChanged?.Invoke();
			}

			return ambiguous;
		}

		private bool TryApplyCandidate(ConcernPoint point, IEnumerable<CandidateInfo> candidates)
		{
			var best = ChooseCandidateToApply(candidates);

			if (best != null)
			{
				RelinkPoint(point, best.Context, best.Node);
				return true;
			}
			else
			{
				point.Anchor.AstNode = null;
				return false;
			}
		}

		private bool TryApplyCandidate(AnchorPoint anchor, IEnumerable<CandidateInfo> candidates)
		{
			var best = ChooseCandidateToApply(candidates);

			if (best != null)
			{
				ShiftAnchor(anchor, best.Context, best.Node);
				return true;
			}
			else
			{
				anchor.AstNode = null;
				return false;
			}
		}

		private List<CandidateInfo> GetCandidates(AnchorPoint point, TargetFileInfo targetInfo)
		{
			return ContextFinder.Find(point, targetInfo).OrderByDescending(c => c.Similarity)
				.TakeWhile(c => c.Similarity >= GarbageThreshold)
				.Take(AmbiguityTopCount).ToList();
		}

		private CandidateInfo ChooseCandidateToApply(IEnumerable<CandidateInfo> candidates)
		{
			var first = candidates.FirstOrDefault();
			var second = candidates.Skip(1).FirstOrDefault();

			if (first != null && first.Similarity >= AcceptanceThreshold
				&& (second == null || first.Similarity - second.Similarity >= DistanceToClosestThreshold))
			{
				return first;
			}
			else
			{
				return null;
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
