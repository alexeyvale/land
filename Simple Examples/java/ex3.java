public class DOMConfigurationImpl extends ParserConfigurationSettings
    implements XMLParserConfiguration, DOMConfiguration {

    protected final static short NAMESPACES          = 0x1<<0;
    protected final static short DTNORMALIZATION     = 0x1<<1;
    protected final static short ENTITIES            = 0x1<<2;

    protected final static short INFOSET_TRUE_PARAMS = NAMESPACES;
    
    private static final long ADD_WORKER = 0x0001L << (TC_SHIFT + 15);
    
    public static <T, K> Collector<T, ?, Map<K, List<T>>>
    groupingBy(Function<? super T, ? extends K> classifier) {
        return groupingBy(classifier, toList());
    }
}