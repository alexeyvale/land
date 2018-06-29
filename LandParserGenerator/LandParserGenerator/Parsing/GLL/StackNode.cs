using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.GLL
{
	public class StackNode
	{
		public const string DUMMY_LABEL = "$";
		public const string MAIN_CYCLE_LABEL = "L0";

		public Label Label { get; set; }
		public int Index { get; set; }
		public Dictionary<StackNode, List<ForestNode>> Edges { get; set; } 
			= new Dictionary<StackNode, List<ForestNode>>();
	}
}
