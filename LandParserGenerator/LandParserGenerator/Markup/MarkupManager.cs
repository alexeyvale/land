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
					concernPoint.TreeNode = null;
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
		}

		/// <summary>
		/// Добавление функциональности
		/// </summary>
		public Concern AddConcern(string name, Concern parent = null)
		{
			var concern = new Concern(name, parent);
			AddElement(concern);
			return concern;
		}

		/// <summary>
		/// Добавление точки привязки
		/// </summary>
		public ConcernPoint AddConcernPoint(string fileName, Node node, string name = null, Concern parent = null)
		{
			var point = new ConcernPoint(fileName, node, parent);
			AddElement(point);
			return point;
		}

		/// <summary>
		/// Добавление всей "суши", присутствующей в дереве разбора
		/// </summary>
		public void AddLand(string fileName, Node root)
		{
			var visitor = new LandExplorerVisitor();
			root.Accept(visitor);

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
					var subconcern = AddConcern(subgroup.Key, concern);

					foreach (var point in subgroup)
						AddElement(new ConcernPoint(fileName, point, subconcern));
				}

				/// Остальные добавляются напрямую к функциональности, соответствующей символу
				var points = subgroups.Where(s => String.IsNullOrEmpty(s.Key))
					.SelectMany(s => s).ToList();

				foreach (var point in points)
					AddElement(new ConcernPoint(fileName, point, concern));
			}
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
		public void RelinkConcernPoint(ConcernPoint point, string fileName, Node node)
		{
			point.Relink(fileName, node);
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
		}

		public void Serialize(string fileName)
		{
			using (FileStream fs = new FileStream(fileName, FileMode.Create))
			{
				using (var gZipStream = new GZipStream(fs, CompressionLevel.Optimal))
				{
					DataContractSerializer serializer = new DataContractSerializer(Markup.GetType(), new List<Type>() {
						typeof(Concern), typeof(ConcernPoint), typeof(PointContext), typeof(InnerContextElement), typeof(OuterContextElement)
					});

					serializer.WriteObject(gZipStream, Markup);
				}
			}
		}

		public void Deserialize(string filename)
		{
			Clear();

			using (FileStream fs = new FileStream(filename, FileMode.Open))
			{
				using (var gZipStream = new GZipStream(fs, CompressionMode.Decompress))
				{
					DataContractSerializer serializer = new DataContractSerializer(Markup.GetType(), new List<Type>() {
						typeof(Concern), typeof(ConcernPoint), typeof(PointContext), typeof(InnerContextElement), typeof(OuterContextElement)
					});

					this.Markup = (ObservableCollection<MarkupElement>)serializer.ReadObject(gZipStream);
				}
			}
		}

		/// <summary>
		/// Поиск узла дерева, которому соответствует заданная точка привязки
		/// </summary>
		public NodeSimilarityPair Find(ConcernPoint point, string fileName, Node root)
		{
			return ContextFinder.Find(point.Context, fileName, root).FirstOrDefault();
		}

		/// <summary>
		/// Получение списка файлов, в которых есть точки привязки
		/// </summary>
		public List<string> GetReferencedFiles()
		{
			var groupVisitor = new GroupPointsVisitor();

			foreach (var elem in this.Markup)
				elem.Accept(groupVisitor);

			return groupVisitor.Points.Select(p => p.Key).Distinct().ToList();
		}

		/// <summary>
		/// Перепривязка точки
		/// </summary>
		public void Remap(ConcernPoint point, string fileName, Node root)
		{
			var candidate = Find(point, fileName, root);

			point.Context = candidate.Context;
			point.TreeNode = candidate.Node;
		}

		/// <summary>
		/// Перепривязка разметки
		/// </summary>
		public void Remap(Dictionary<string, Node> roots)
		{
			var groupVisitor = new GroupPointsVisitor();

			foreach (var elem in this.Markup)
				elem.Accept(groupVisitor);

			foreach (var group in groupVisitor.Points)
			{
				if (roots.ContainsKey(group.Key) && roots[group.Key] != null)
				{
					var visitor = new RemapVisitor(group.Key, roots[group.Key]);

					foreach (var elem in Markup)
						elem.Accept(visitor);
				}
			}
		}

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
