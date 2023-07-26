# LanD parser generator
Development repository for LanD parser generator.
## Prerequisites
[Java Runtime Environment](http://www.oracle.com/technetwork/java/javase/downloads/jre8-downloads-2133155.html) must be installed for correct lexical analyzer generation and work.

Missing NuGet packages will be automatically restored when the solution is built for the first time.
## Author's research on tolerant parsing and binding to code
Tolerant parsing with a special kind of «Any» symbol: the algorithm and practical application, 2018.

[paper](https://www.ispras.ru/proceedings/docs/2018/30/4/isp_30_2018_4_7.pdf) & [particular LanD version used](https://github.com/alexeyvale/SYRCoSE-2018)

Tolerant parsing using modified LR(1) and LL(1) algorithms with embedded “Any” symbol, 2019.

[paper](https://www.ispras.ru/proceedings/docs/2019/31/3/isp_31_2019_3_7.pdf) & [particular LanD version used](https://github.com/alexeyvale/SYRCoSE-2019)
  
Using improved context-based code description for robust algorithmic binding to changing code, 2021.

[paper](https://www.sciencedirect.com/science/article/pii/S1877050921020652) & [particular LanD version used](https://github.com/alexeyvale/YSC-2021) (with the Land Explorer tool)

Robust algorithmic binding to arbitrary fragment of program code, 2022.

[paper](https://psta.psiras.ru/read/psta2022_1_35-62.pdf)
