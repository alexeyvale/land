import matplotlib.pyplot as plt

file_common_prefix = 'D:/Desktop/Учёба/НИР phd/Репозитории/Land Parser Generator/_Experiments/Mapping/Comparison/Comparison/bin/Debug/'
files = ['class_struct_interface_similarities.txt', 'method_similarities.txt', 'field_similarities.txt', 'property_similarities.txt']

for name in files:
    similarities = [];

    with open(file_common_prefix + name) as f:
        for line in f:
            splitted = list(map(lambda e: float(e), line.strip().replace(",", ".").split(';')))
            similarities.append(splitted)

    fig, axes = plt.subplots(1, 4)
    ((ax1), (ax2), (ax3), (ax4)) = axes

    ax1.set(xlabel='basic', ylabel='modified', title='fst similarity')
    ax1.grid()

    print("Modified similarity lower: "
          + str(len(list(filter(lambda l: l[0] > l[1], similarities))))
          + " out of " + str(len(similarities)))

    print("Distance from second higher: "
          + str(len(list(filter(lambda l: len(l) > 2 and l[1] - l[3] > l[0] - l[2], similarities))))
          + " out of " + str(len(list(filter(lambda l: len(l) > 2, similarities)))))

    ax1.scatter(list(map(lambda l: l[0], similarities)),
            list(map(lambda l: l[1], similarities)),
            marker='.', color='blue', s=30)
    ax1.plot([0, 1], [0, 1], linestyle='solid', color='black', linewidth=0.8)

    longSimilarities = list(filter(lambda l: len(l) > 2, similarities))

    ax2.set(xlabel='basic', ylabel='modified', title='fst minus snd')
    ax2.grid()

    ax2.scatter(list(map(lambda l: l[0] - l[2], longSimilarities)),
                list(map(lambda l: l[1] - l[3], longSimilarities)),
                marker='.', color='blue', s=30)
    ax2.plot([0, 1], [0, 1], linestyle='solid', color='black', linewidth=0.8)

    ax3.set(xlabel='first_dist', ylabel='closest_dist', title='1-fst & fst-snd (basic)')
    ax3.grid()

    #print("Basic auto: "
    #      + str(len(list(filter(lambda l: l[0] >= 0.6 and (len(l) == 2 or 1-l[0]<l[0]-l[2]), similarities))))
    #      + " out of " + str(len(similarities)))

    ax3.scatter(list(map(lambda l: 1 - l[0], longSimilarities)),
                list(map(lambda l: l[0] - l[2], longSimilarities)),
                marker='.', color='blue', s=30)
    ax3.plot([0, 1], [0, 1], linestyle='solid', color='black', linewidth=0.8)

    ax4.set(xlabel='first_dist', ylabel='closest_dist', title='1-fst & fst-snd (modified)')
    ax4.grid()

    #print("Modified auto: "
    #      + str(len(list(filter(lambda l: l[1] >= 0.6 and (len(l) == 2 or 1 - l[1] < l[1] - l[3]), similarities))))
    #      + " out of " + str(len(similarities)))

    ax4.scatter(list(map(lambda l: 1 - l[1], longSimilarities)),
                list(map(lambda l: l[1] - l[3], longSimilarities)),
                marker='.', color='blue', s=30)
    ax4.plot([0, 1], [0, 1], linestyle='solid', color='black', linewidth=0.8)

    plt.xlim([-0.05, 1.05])
    plt.ylim([-0.05, 1.05])

    plt.show()



