import os
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt


def median(lst):
    if len(lst) % 2 == 1:
        return lst[len(lst) // 2]
    else:
        return (lst[len(lst) // 2] + lst[len(lst) // 2 - 1]) / 2


def preprocess_x_ranges_avg(points):
    border = 0
    step = 10
    acc = []
    count = 0
    points_x = []
    points_y = []

    for vals in points:
        x = int(vals[0])
        y = float(vals[1].replace(',', '.'))
        if x <= border:
            acc += y
            ++count
        else:
            if count > 0:
                points_x.append(border)
                points_y.append(acc / count)
            while x > border:
                border += step
            count = 1
            acc = y


def preprocess_x_ranges(points):
    border = 0
    step = 10
    acc = []
    points_x = [0]
    points_y = [0]

    for vals in points:
        x = int(vals[0])
        y = float(vals[1].replace(',', '.'))
        if x <= border:
            acc.append(y)
        else:
            if len(acc) > 0:
                points_x.append(border)
                points_y.append(median(acc))
            while x > border:
                border += step
            acc = [y]

    if len(acc) > 0:
        points_x.append(border)
        points_y.append(median(acc))

    return points_x, points_y


with open('D:/Desktop/Учёба/НИР phd/Репозитории/Land Parser Generator/'
          '_Experiments/Mapping/Comparison/Comparison/bin/_/roslyn simple in algo proc.txt', encoding="utf-8") as f:
    read_data = f.readlines()

with open('D:/Desktop/Учёба/НИР phd/Репозитории/Land Parser Generator/'
          '_Experiments/Mapping/Comparison/Comparison/bin/_/roslyn wo simple in algo fixed 2 parallel proc.txt', encoding="utf-8") as f:
    read_data_wo = f.readlines()

with open('D:/Desktop/Учёба/НИР phd/Репозитории/Land Parser Generator/'
          '_Experiments/Mapping/Comparison/Comparison/bin/_/roslyn wo simple in algo fixed 2 proc.txt', encoding="utf-8") as f:
    read_data_wo_bad = f.readlines()

#with open('D:/Desktop/Учёба/НИР phd/Репозитории/Land Parser Generator/'
#          '_Experiments/Mapping/Comparison/Comparison/bin/_/asp simple in algo proc.txt', encoding="utf-8") as f:
#    read_data_wo = f.readlines()

#with open('D:/Desktop/Учёба/НИР phd/Репозитории/Land Parser Generator/'
#          '_Experiments/Mapping/Comparison/Comparison/bin/_/asp simple release proc.txt', encoding="utf-8") as f:
#    read_data_release = f.readlines()

points = list(map(lambda line: line.strip().split(sep=' '), read_data))
points_wo = list(map(lambda line: line.strip().split(sep=' '), read_data_wo))
points_wo_bad = list(map(lambda line: line.strip().split(sep=' '), read_data_wo_bad))

print(points)
#points_wo = list(map(lambda line: line.strip().split(sep=' '), read_data_wo))
#points_release = list(map(lambda line: line.strip().split(sep=' '), read_data_release))

preprocessed_x, preprocessed_y = preprocess_x_ranges(points_wo)

plt.plot(
    preprocessed_x,
    preprocessed_y,
    linestyle='solid', label=r'Медиана с отсечением', color='black'
)

preprocessed_x, preprocessed_y = preprocess_x_ranges(points_wo_bad)

plt.plot(
    preprocessed_x,
    preprocessed_y,
  linestyle='dashed', label=r'Среднее с отсечением', color='black'
)

plt.scatter(
  list(map(lambda pair: int(pair[0]), points_wo)),
  list(map(lambda pair: float(pair[1].replace(',', '.')), points_wo)),
  color='black'
)

#plt.plot(
#  list(map(lambda pair: int(pair[0]), points_wo_bad)),
#  list(map(lambda pair: float(pair[1].replace(',', '.')), points_wo_bad)),
#  linestyle='dashed', label=r'Среднее с отсечением', color='black'
#)
"""
plt.plot(
  list(map(lambda pair: int(pair[0]), points_wo)),
  list(map(lambda pair: float(pair[1].replace(',', '.')), points_wo)),
  linestyle='dotted', label=r'Медиана без отсечения', color='black'
)
"""
#plt.plot(
#    preproc1[0],
#    preproc1[1],
#    linestyle='solid', label=r'Максимальное', color='black'
#)
#plt.plot(
#    preproc2[0],
#    preproc2[1],
#    linestyle='dashed', label=r'Минимальное', color='black'
#)
#plt.xlim([-20, 1100])
#plt.ylim([-20, 500])
plt.legend()
xticks = np.arange(0, 2000, 50)
plt.xticks(xticks)
yticks = np.arange(0, 120000, 2000)
plt.yticks(yticks)
plt.grid()
plt.show()
