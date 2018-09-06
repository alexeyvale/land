using BaseTest;

namespace TestNamespace
{
	public enum TestEnum { Val1=1, Val2=2, Val3=3 }
	
	public class TestClass: BaseTestClass
	{
		public int TestField = 42;
		
		#if DEBUG
		
		public int? TestMethod1(int a, int b)
		{
			return a+b;
		}
		
		#elif RELEASE
		
		public int? TestMethod2(int a, int b)
		{
			return a+b;
		}
		
		#endif
	}
}