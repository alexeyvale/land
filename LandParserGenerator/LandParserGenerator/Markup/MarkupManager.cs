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
		public event Action OnMarkupChanged;

		/// <summary>
		/// Коллекция точек привязки
		/// </summary>
		public ObservableCollection<MarkupElement> Markup = new ObservableCollection<MarkupElement>();

		/// <summary>
		/// Очистка разметки
		/// </summary>
		public void Clear()
		{
			Markup.Clear();

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Сброс узлов дерева у всех точек, связанных с указанным файлом
		/// </summary>
		public void InvalidatePoints(string fileName)
		{
			DoWithMarkup((MarkupElement elem) =>
			{
				if(elem is ConcernPoint concernPoint 
					&& concernPoint.Context.FileName == fileName)
				{
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
		public ConcernPoint AddConcernPoint(TargetFileInfo sourceInfo, string name = null, string comment = null, Concern parent = null)
		{
			var point = new ConcernPoint(sourceInfo, parent);

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
						AddElement(new ConcernPoint(sourceInfo, subconcern));
					}
				}

				/// Остальные добавляются напрямую к функциональности, соответствующей символу
				var points = subgroups.Where(s => String.IsNullOrEmpty(s.Key))
					.SelectMany(s => s).ToList();

				foreach (var point in points)
				{
					sourceInfo.TargetNode = point;
					AddElement(new ConcernPoint(sourceInfo, concern));
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

				currentNode = currentNode.Children.Where(c => c.Anchor != null && c.Anchor.Start != null && c.Anchor.End != null
					&& c.Anchor.Start.Offset <= offset && c.Anchor.End.Offset >= offset).FirstOrDefault();
			}

			return pointCandidates;
		}

		/// <summary>
		/// Смена узла, к которому привязана точка
		/// </summary>
		public void RelinkConcernPoint(ConcernPoint point, TargetFileInfo targetInfo)
		{
			point.Relink(targetInfo);

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Смена узла, к которому привязана точка
		/// </summary>
		public void RelinkConcernPoint(ConcernPoint point, CandidateInfo candidate)
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
						p.Context.FileName = Uri.UnescapeDataString(
							directoryUri.MakeRelativeUri(new Uri(p.Context.FileName)).ToString()
						);
					}
				});
			}

			using (FileStream fs = new FileStream(fileName, FileMode.Create))
			{
				using (var gZipStream = new GZipStream(fs, CompressionLevel.Optimal))
				{
					DataContractSerializer serializer = new DataContractSerializer(Markup.GetType(), new List<Type>() {
						typeof(Concern), typeof(ConcernPoint), typeof(PointContext), typeof(HeaderContextElement), typeof(AncestorsContextElement)
					});

					serializer.WriteObject(gZipStream, Markup);
				}
			}

			if (useRelativePaths)
			{
				/// Трансформируем пути обратно в абсолютные
				DoWithMarkup((MarkupElement elem) =>
				{
					if (elem is ConcernPoint p)
					{
						p.Context.FileName = Path.GetFullPath(
							Path.Combine(Path.GetDirectoryName(fileName), p.Context.FileName)
						);
					}
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
					DataContractSerializer serializer = new DataContractSerializer(Markup.GetType(), new List<Type>() {
						typeof(Concern), typeof(ConcernPoint), typeof(PointContext), typeof(HeaderContextElement), typeof(AncestorsContextElement)
					});

					this.Markup = (ObservableCollection<MarkupElement>)serializer.ReadObject(gZipStream);
				}
			}

			DoWithMarkup((MarkupElement elem) =>
			{
				if (elem is ConcernPoint p && !Path.IsPathRooted(p.Context.FileName))
				{
					p.Context.FileName = Path.GetFullPath(
						Path.Combine(Path.GetDirectoryName(fileName), p.Context.FileName)
					);
				}
			});
		}

		/// <summary>
		/// Поиск узла дерева, которому соответствует заданная точка привязки
		/// </summary>
		public List<CandidateInfo> Find(ConcernPoint point, TargetFileInfo targetInfo)
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

		public Dictionary<ConcernPoint, List<CandidateInfo>> Remap(List<TargetFileInfo> targetFiles, bool useLocalRemap)
		{
			var ambiguous = useLocalRemap
				? LocalRemap(targetFiles)
				: GlobalRemap(targetFiles);

			OnMarkupChanged?.Invoke();

			return ambiguous;
		}

		private Dictionary<ConcernPoint, List<CandidateInfo>> LocalRemap(List<TargetFileInfo> targetFiles)
		{
			var groupedByFile = GroupPointsByFileVisitor.GetGroups(Markup);
			var ambiguous = new Dictionary<ConcernPoint, List<CandidateInfo>>();

			foreach(var fileGroup in groupedByFile)
			{
				var file = targetFiles.Where(f => f.FileName == fileGroup.Key).FirstOrDefault();

				if(file != null)
				{
					var groupedByType = fileGroup.Value.GroupBy(p => p.Context.NodeType).ToDictionary(g => g.Key, g => g.ToList());
					var groupedFiles = GroupNodesByTypeVisitor.GetGroups(file.TargetNode, groupedByType.Keys);

					var result = ContextFinder.Find(groupedByType, groupedFiles, file);

					foreach (var kvp in result)
					{
						var candidates = kvp.Value.OrderByDescending(c => c.Similarity)
							.TakeWhile(c=>c.Similarity >= GarbageThreshold)
							.Take(AmbiguityTopCount).ToList();

						if (!ApplyCandidate(kvp.Key, candidates))
							ambiguous[kvp.Key] = candidates;
					}
				}
				else
				{
					foreach (var point in fileGroup.Value)
						point.AstNode = null;
				}
			}

			return ambiguous;
		}

		private Dictionary<ConcernPoint, List<CandidateInfo>> GlobalRemap(List<TargetFileInfo> targetFiles)
		{
			var ambiguous = new Dictionary<ConcernPoint, List<CandidateInfo>>();

			/// Группируем точки привязки по типу помеченной сущности 
			var groupedPoints = GroupPointsByTypeVisitor.GetGroups(Markup);
			var accumulator = groupedPoints.SelectMany(e => e.Value).ToDictionary(e => e, e => new List<CandidateInfo>());

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

				if (!ApplyCandidate(kvp.Key, candidates))
					ambiguous[kvp.Key] = candidates;
			}

			return ambiguous;
		}

		/// <summary>
		/// Перепривязка точки
		/// </summary>
		public Dictionary<ConcernPoint, List<CandidateInfo>> Remap(ConcernPoint point, TargetFileInfo targetInfo)
		{
			var ambiguous = new Dictionary<ConcernPoint, List<CandidateInfo>>();
			var candidates = ContextFinder.Find(point, targetInfo).OrderByDescending(c=>c.Similarity)
				.TakeWhile(c => c.Similarity >= GarbageThreshold)
				.Take(AmbiguityTopCount).ToList();

			if (!ApplyCandidate(point, candidates))
				ambiguous[point] = candidates;

			OnMarkupChanged?.Invoke();

			return ambiguous;
		}

		private bool ApplyCandidate(ConcernPoint point, IEnumerable<CandidateInfo> candidates)
		{
			var first = candidates.FirstOrDefault();
			var second = candidates.Skip(1).FirstOrDefault();

			if (first != null && first.Similarity >= AcceptanceThreshold
				&& (second == null || first.Similarity - second.Similarity >= DistanceToClosestThreshold))
			{
				point.Context = first.Context;
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
		private void DoWithMarkup(Action<MarkupElement> action)
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
