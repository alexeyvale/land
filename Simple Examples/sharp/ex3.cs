using BaseTest;

namespace Unexpected
{
	public class TestClass: BaseTestClass
	{
		private const Word ZeroWord = 0, TestVal = 4;

        public const int BitsPerWord = 1 << Log2BitsPerWord;
        
        public Dictionary<string, int> TestDict = new Dictionary<string, int>();
		
		public static bool operator ==(CompilationOptions left, CompilationOptions right)
        {
            return object.Equals(left, right);
        }
        
        public bool HasLeadingTrivia => this.GetLeadingTrivia().Count < 0;       
	}
}