/// land start Весь класс
public class DOMConfigurationImpl extends ParserConfigurationSettings
    implements XMLParserConfiguration, DOMConfiguration {
    
	/// land start Поля
    protected final static short NAMESPACES          = 0x1<<0;
    protected final static short DTNORMALIZATION     = 0x1<<1;
    protected final static short ENTITIES            = 0x1<<2;
	/// land end
	
    protected final static short INFOSET_TRUE_PARAMS = NAMESPACES;
    
    public static <T, K> Collector<T, ?, Map<K, List<T>>> groupingBy(Function<? super T, ? extends K> classifier) 
    {
    	/// land start
    	TestCall();
    	/// land end
        return groupingBy(classifier, toList());   
    }
}
/// land end