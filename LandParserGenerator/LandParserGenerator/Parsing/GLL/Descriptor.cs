using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.GLL
{
	public class Descriptor
	{
		public Label Label { get; set; }
		public StackNode StackNode { get; set; }
		public int Index { get; set; }
		public ForestNode ForestNode { get; set; }
	}
}
