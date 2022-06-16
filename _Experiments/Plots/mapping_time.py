import os
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import pylab
import math


def get_bins(vals):
    bins = [0] * 11
    for val in vals:
        bins[math.floor(float(val[0].replace(',', '.')) * 10)] += 1
    bins[9] += bins[10]
    bins = bins[:-1]

    for i in range(0, len(bins)):
        bins[i] = bins[i]/len(vals) * 100
    print(bins)
    return bins


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

    if count > 0:
        points_x.append(border)
        points_y.append(acc / count)

    return points_x, points_y


def preprocess_x_ranges(points):
    border = 0
    step = 5
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
          '_Experiments/Mapping/Comparison/Comparison/bin/_/roslyn simple in algo fixed 2 proc.txt', encoding="utf-8") as f:
    read_data_wo = f.readlines()

with open('D:/Desktop/Учёба/НИР phd/Репозитории/Land Parser Generator/'
          '_Experiments/Mapping/Comparison/Comparison/bin/_/roslyn wo simple in algo fixed 2 proc.txt', encoding="utf-8") as f:
    read_data_wo_bad = f.readlines()

with open('D:/Desktop/Учёба/НИР phd/Репозитории/Land Parser Generator/'
          '_Experiments/Mapping/Comparison/Comparison/bin/_/roslyn_first_iter_stat 2.txt', encoding="utf-8") as f:
    read_data_first_iter = f.readlines()

#with open('D:/Desktop/Учёба/НИР phd/Репозитории/Land Parser Generator/'
#          '_Experiments/Mapping/Comparison/Comparison/bin/_/asp simple in algo proc.txt', encoding="utf-8") as f:
#    read_data_wo = f.readlines()

#with open('D:/Desktop/Учёба/НИР phd/Репозитории/Land Parser Generator/'
#          '_Experiments/Mapping/Comparison/Comparison/bin/_/asp simple release proc.txt', encoding="utf-8") as f:
#    read_data_release = f.readlines()

points = list(map(lambda line: line.strip().split(sep=' '), read_data))
points_wo = list(map(lambda line: line.strip().split(sep=' '), read_data_wo))
points_wo_bad = list(map(lambda line: line.strip().split(sep=' '), read_data_wo_bad))
points_first_iter = list(map(lambda line: line.strip().split(sep=' '), read_data_first_iter))

#points_wo = list(map(lambda line: line.strip().split(sep=' '), read_data_wo))
#points_release = list(map(lambda line: line.strip().split(sep=' '), read_data_release))

preprocessed_x, preprocessed_y = preprocess_x_ranges(points_wo_bad)
preprocessed_x = preprocessed_x[:-1]
preprocessed_y = preprocessed_y[:-1]

pylab.subplot(2, 1, 1)
pylab.plot(
    preprocessed_x,
    preprocessed_y,
    linestyle='solid', color='black'
)
pylab.xlabel('Количество точек привязки одного типа в файле', fontdict={'size': 14})
pylab.ylabel('Время работы, мс', fontdict={'size': 14, })
#pylab.set_xlabel('Количество точек привязки')
#pylab.set_ylabel('Время работы, мс')
pylab.title("Зависимость времени работы алгоритма от количества точек привязки, без отсечения", fontdict={'size': 14, 'fontweight': 'bold'})

pylab.scatter(
  list(map(lambda pair: int(pair[0]), points_wo_bad)),
  list(map(lambda pair: float(pair[1].replace(',', '.')), points_wo_bad)),
  color='black'
)

pylab.xlim([-50, 1050])
pylab.ylim([-2000, 43000])
xticks = np.arange(0, 1050, 50)
pylab.xticks(xticks)
yticks = np.arange(0, 43000, 5000)
pylab.yticks(yticks)
pylab.tick_params(labelsize=14)
pylab.grid()

preprocessed_x, preprocessed_y = preprocess_x_ranges(points_wo)
preprocessed_x = preprocessed_x[:-1]
preprocessed_y = preprocessed_y[:-1]

pylab.subplot(2, 1, 2)
pylab.plot(
    preprocessed_x,
    preprocessed_y,
    linestyle='solid', color='black'
)
pylab.xlabel('Количество точек привязки одного типа в файле', fontdict={'size': 14})
pylab.ylabel('Время работы, мс', fontdict={'size': 14})
#pylab.set_xlabel('Количество точек привязки')
#pylab.set_ylabel('Время работы, мс')
pylab.title("Зависимость времени работы алгоритма от количества точек привязки, с отсечением", fontdict={'size': 14, 'fontweight': 'bold'})

pylab.scatter(
  list(map(lambda pair: int(pair[0]), points_wo)),
  list(map(lambda pair: float(pair[1].replace(',', '.')), points_wo)),
  color='black'
)

pylab.xlim([-50, 1050])
pylab.ylim([-25, 500])
xticks = np.arange(0, 1050, 50)
pylab.xticks(xticks)
yticks = np.arange(0, 500, 50)
pylab.yticks(yticks)
pylab.tick_params(labelsize=14)
pylab.grid()

pylab.show()

bins = get_bins(points_first_iter)
plt.scatter(
    [0, 10, 20, 30, 40, 50, 60, 70, 80, 90],
    bins,
    color='black'
)

plt.show()

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
#plt.legend()

