using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.GLL
{
	public class Parser : BaseParser
	{
		private const int MAX_RECOVERY_ATTEMPTS = 5;

		private TableLL1 Table { get; set; }
		private TokenStream LexingStream { get; set; }

		private List<List<Descriptor>> CreatedDescriptors { get; set; }
		private Queue<Descriptor> DescriptorsInWork { get; set; }
		private List<StackNode> CreatedStackNodes { get; set; }
		private List<ForestNode> CreatedForestNodes { get; set; }
		private List<Tuple<StackNode, ForestNode>> PoppedNodes { get; set; }

		public Parser(Grammar g, ILexer lexer) : base(g, lexer)
		{
			Table = new TableLL1(g);

			// g.UseModifiedFirst = true;
		}

		public override Node Parse(string text)
		{
			CreatedDescriptors = new List<List<Descriptor>>();
			DescriptorsInWork = new Queue<Descriptor>();
			CreatedStackNodes = new List<StackNode>();
			CreatedForestNodes = new List<ForestNode>();
			PoppedNodes = new List<Tuple<StackNode, ForestNode>>();

			CreatedStackNodes.Add(new StackNode()
			{
				Label = StackNode.DUMMY_LABEL
			});

			CreatedStackNodes.Add(new StackNode()
			{
				Label = StackNode.MAIN_CYCLE_LABEL,
				Index = 0
			});

			CreatedStackNodes[1].Edges[CreatedStackNodes[0]].Add(null);

			DescriptorsInWork.Enqueue(new Descriptor()
			{
				Label = grammar.StartSymbol,
				StackNode = CreatedStackNodes[1],
				Index = 0,
				ForestNode = null
			});

			while (DescriptorsInWork.Count > 0)
			{
				var curDescr = DescriptorsInWork.Dequeue();

				/// Если метка дескриптора - нетерминальный символ
				if(!String.IsNullOrEmpty(curDescr.Label))
				{
					var alts = Table[curDescr.Label, LexingStream.MoveToToken(curDescr.Index).Name];

					foreach(var alt in alts)
					{
						Add(new GrammarSlot(alt, 0), curDescr.StackNode, curDescr.Index, null);
					}
				}
				/// Если метка - это слот
				else
				{
					if (curDescr.Label.Slot.Position == 0)
					{
						/// Если имеем дело с пустой веткой
						if (curDescr.Label.Slot.Alternative.Count == 0)
						{
							var node1 = GetNodeT(null, curDescr.Index);
							var node2 = GetNodeP(curDescr.Label.Slot, curDescr.ForestNode, node1);
							Pop(curDescr.StackNode, curDescr.Index, node2);
						}
						else if (curDescr.Label.Slot.Alternative.Count == 1 
							&& grammar[curDescr.Label.Slot.Alternative[0]] is TerminalSymbol)
						{
							var node1 = GetNodeT(curDescr.Label.Slot.Alternative[0], curDescr.Index);
							var node2 = GetNodeP(curDescr.Label.Slot, curDescr.ForestNode, node1);
							Pop(curDescr.StackNode, curDescr.Index, node2);
						}
						else if(curDescr.Label.Slot.Alternative.Count >= 2
							&& grammar[curDescr.Label.Slot.Alternative[0]] is TerminalSymbol)
						{
							
						}
					}
					else
					{

					}
				}
			}

			var root = CreatedForestNodes.FirstOrDefault(n=>n.Label.Symbol == grammar.StartSymbol 
				&& n.LeftExtent == 0 && n.RightExtent == LexingStream.CurrentTokenIndex + 1);

			/// Нужно построить по лесу адекватное дерево
			if(root != null)
			{ }

			return null;
		}

		private void Add(Label label, StackNode stackNode, int index, ForestNode forestNode)
		{
			if(CreatedDescriptors.Count == index 
				|| !CreatedDescriptors[index].Any(d=>d.Label.Equals(label) && d.ForestNode == forestNode && d.StackNode == stackNode))
			{
				if (CreatedDescriptors.Count == index)
					CreatedDescriptors.Add(new List<Descriptor>());

				var descriptor = new Descriptor()
				{
					Label = label,
					ForestNode = forestNode,
					Index = index,
					StackNode = stackNode
				};

				CreatedDescriptors[index].Add(descriptor);
				DescriptorsInWork.Enqueue(descriptor);
			}
		}

		private void Pop(StackNode stackNode, int index, ForestNode forestNode)
		{
			if (stackNode != CreatedStackNodes[0])
			{
				PoppedNodes.Add(new Tuple<StackNode, ForestNode>(stackNode, forestNode));

				foreach (var destLabelPair in stackNode.Edges)
					foreach (var edgeLabel in destLabelPair.Value)
					{
						var retNode = GetNodeP(stackNode.Label, edgeLabel, forestNode);
						Add(stackNode.Label, destLabelPair.Key, index, retNode);
					}
			}
		}

		private StackNode Create(Label label, StackNode stackNode, int index, ForestNode forestNode)
		{
			var node = CreatedStackNodes.FirstOrDefault(n => n.Label.Equals(label) && n.Index == index) ?? new StackNode()
			{
				Label = label,
				Index = index
			};

			if (!node.Edges.ContainsKey(stackNode))
				node.Edges[stackNode] = new List<ForestNode>();

			if (!node.Edges[stackNode].Contains(forestNode))
			{
				node.Edges[stackNode].Add(forestNode);

				foreach(var pair in PoppedNodes.Where(p=>p.Item1 == node).ToList())
				{
					var retNode = GetNodeP(label, forestNode, pair.Item2);
					Add(label, stackNode, pair.Item2.RightExtent, retNode);
				}
			}

			return node;
		}

		private ForestNode GetNodeT(string symbol, int index)
		{
			var rIndex = index;
			if (!String.IsNullOrEmpty(symbol))
				rIndex += 1;

			var node = CreatedForestNodes.FirstOrDefault(n => n.Label.Symbol == symbol && n.Pivot == index) ?? new ForestNode()
			{
				Label = symbol,
				Pivot = index
			};

			return node;
		}

		private ForestNode GetNodeP(GrammarSlot slot, ForestNode child1, ForestNode child2)
		{
			if (slot.Position == 1 && !grammar.First(slot.Prev).Contains(null) && !String.IsNullOrEmpty(slot.Next))
				return child2;

			var labelForNewNode = new Label()
			{
				Symbol = String.IsNullOrEmpty(slot.Next) ? slot.Alternative.NonterminalSymbolName : null,
				Slot = !String.IsNullOrEmpty(slot.Next) ? slot : null
			};

			ForestNode node;

			if(child1 != null)
			{
				node = CreatedForestNodes.FirstOrDefault(n => n.Label.Equals(labelForNewNode) 
					&& n.LeftExtent == child1.LeftExtent && n.RightExtent == child2.RightExtent) ?? new ForestNode()
				{
					Label = labelForNewNode,
					LeftExtent = child1.LeftExtent,
					RightExtent = child2.RightExtent
				};

				if (!node.Children.Any(c => c.Label.Slot.Equals(slot) && c.Pivot == child2.LeftExtent))
					node.Children.Add(new ForestNode()
					{
						Label = slot,
						Pivot = child2.LeftExtent,
						Children = new List<ForestNode>() { child1, child2 }
					});
			}
			else
			{
				node = CreatedForestNodes.FirstOrDefault(n => n.Label.Equals(labelForNewNode)
					&& n.LeftExtent == child1.RightExtent && n.RightExtent == child2.RightExtent) ?? new ForestNode()
					{
						Label = labelForNewNode,
						LeftExtent = child1.RightExtent,
						RightExtent = child2.RightExtent
					};

				if (!node.Children.Any(c => c.Label.Slot.Equals(slot) && c.Pivot == child2.LeftExtent))
					node.Children.Add(new ForestNode()
					{
						Label = slot,
						Pivot = child2.LeftExtent,
						Children = new List<ForestNode>() { child2 }
					});
			}

			return node;
		}
	}
}
