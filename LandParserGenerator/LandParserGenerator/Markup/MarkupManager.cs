using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Markup
{
	[DataContract(IsReference = true)]
	public class MarkupManager
	{
		[DataMember]
		public List<MarkupElement> Markup { get; set; } = new List<MarkupElement>();

		[DataMember]
		public Node AstRoot { get; set; } = null;

		public void Clear()
		{
			Markup = new List<MarkupElement>();
			AstRoot = null;
		}

		public void Remove(MarkupElement elem)
		{
			if (elem.Parent != null)
				elem.Parent.Elements.Remove(elem);
			else
				Markup.Remove(elem);
		}

		public void Add(MarkupElement elem)
		{
			if (elem.Parent == null)
				Markup.Add(elem);
			else
				elem.Parent.Elements.Add(elem);
		}

		public void Remap(Node newRoot, Dictionary<Node, Node> mapping)
		{
			AstRoot = newRoot;

			var visitor = new MarkupRemapVisitor(mapping);
			for (int i = 0; i < Markup.Count; ++i)
			{
				if (!visitor.Visit(Markup[i]))
				{
					Markup.RemoveAt(i);
					--i;
				}
			}
		}

		public static void Serialize(string filename, MarkupManager target)
		{
			DataContractSerializer serializer = new DataContractSerializer(typeof(MarkupManager), new Type[] { typeof(Concern), typeof(ConcernPoint) });

			using (FileStream fs = new FileStream(filename, FileMode.Create))
			{
				serializer.WriteObject(fs, target);
			}
		}

		public static MarkupManager Deserialize(string filename)
		{
			DataContractSerializer serializer = new DataContractSerializer(typeof(MarkupManager), new Type[] { typeof(Concern), typeof(ConcernPoint) });

			using (FileStream fs = new FileStream(filename, FileMode.Open))
			{
				return (MarkupManager)serializer.ReadObject(fs);
			}
		}
	}

	public class MarkupRemapVisitor
	{
		public Dictionary<Node, Node> Mapping;

		public MarkupRemapVisitor(Dictionary<Node, Node> mapping)
		{
			Mapping = mapping;
		}

		public bool Visit(MarkupElement element)
		{
			var result = true;

			if (element is Concern)
			{
				var concern = (Concern)element;

				for(var i=0;i<concern.Elements.Count;++i)
					if(!Visit(concern.Elements[i]))
					{
						concern.Elements.RemoveAt(i);
						--i;
					}
			}
			else
			{
				var concernPoint = (ConcernPoint)element;

				if (Mapping.ContainsKey(concernPoint.TreeNode))
					concernPoint.TreeNode = Mapping[concernPoint.TreeNode];
				else
					result = false;
			}

			return result;
		}
	}
}
