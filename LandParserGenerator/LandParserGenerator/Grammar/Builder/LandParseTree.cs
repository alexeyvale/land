using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Builder
{
    public abstract class Node { }

    public abstract class InnerNode<T>: Node 
        where T : Node
    {
        public List<T> Children { get; set; }
		public List<NameComponentLocation> NameComponents { get; set; }
		public InnerNode(params T[] nodes)
        {
            Children = new List<T>(nodes);
			NameComponents = new List<NameComponentLocation>();
        }

    }

    public abstract class LeafNode<T> : Node
    {
        public T Value;
    }

    public class RuleNode : InnerNode<AlternativeNode>
    {
        public RuleNode(params AlternativeNode[] nodes): base(nodes){ }

        public override string ToString()
        {
            return String.Concat(Environment.NewLine, Children.Select(e=>e.ToString()));
        }

        public bool Equals(RuleNode other, string otherName, string thisName)
        {
            if (other.Children.Count == this.Children.Count)
            {
                for (int i = 0; i < other.Children.Count; ++i)
                    if (!other.Children[i].Equals(this.Children[i], thisName, otherName))
                        return false;
                return true;
            }
            else
                return false;
        }
    }

    public class AlternativeNode : InnerNode<EntityNode>
    {
        public AlternativeNode(params EntityNode[] nodes): base(nodes){ }

        public override string ToString()
        {
            return String.Join(" ", Children.Select(c => c.ToString()));
        }

        public bool Equals(AlternativeNode other, string otherName, string thisName)
        {
            if (this.Children.Count == other.Children.Count)
            {
                for (int i = 0; i < other.Children.Count; ++i)
                    //Если элементы ветки не совпадают и не являются ссылкой на сам определяемый элемент
                    if (other.Children[i].Value != this.Children[i].Value &&
                        (other.Children[i].Value != otherName || this.Children[i].Value != thisName))
                        return false;
                return true;
            }
            else
                return false;
        }

		public List<int> IsNested(AlternativeNode inNode)
		{
			var result = new List<int>();

			if (this.Children.Count > inNode.Children.Count ||
				this.Children.Count <= 1)
				return result;

			//Проходим по проверяемой альтернативе
			for (int i = 0; i < inNode.Children.Count - this.Children.Count; ++i)
			{
				var foundEntry = true;
				//Начиная с каждого символа пытаемся сопоставить текущую альтернативу
				for (int j = 0; j < this.Children.Count; ++j)
					if(this.Children[j].Value != inNode.Children[i+j].Value)
					{
						foundEntry = false;
						break;
					}
				//Если нашли совпадение - фиксируем
				if (foundEntry)
					result.Add(i);
			}

			for (var i = 0; i < result.Count - 1; ++i)
			{
				//ABA in ABABA
				if (result[i + 1] - result[i] < this.Children.Count)
				{
					result.RemoveAt(i + 1);
					--i;
				}
			}

			return result;
		}
	}

    public class EntityNode : LeafNode<string>
    {
        public EntityNode(string val) { Value = val; }

        public override string ToString()
        {
            return Value;
        }
    }
}
