__author__ = 'zeny'
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
from pandas.plotting import scatter_matrix

plt.style.use('ggplot')

data = pd.read_csv('train_data/method_train.txt', delimiter=';', decimal=",")
y = data['IsAuto']

data = data.drop(['IsAuto'], axis=1)

"""
data['startLevel'] = data['maxPlayerLevel'] - data['numberOfAttemptedLevels']
"""

print(y.describe())

"""
print(y.shape)
print(data.shape)

print(data.head())
print(data.tail())
print(data['maxPlayerLevel'][0])
print(data.at[0,'maxPlayerLevel'])
"""
print(data.describe())
"""
categorical_columns = [c for c in data.columns if data[c].dtype.name == 'object']
numerical_columns   = [c for c in data.columns if data[c].dtype.name != 'object']

# Определить полный перечень значений категориальных признаков можно, например, так:
for c in categorical_columns:
    print (data[c].unique())
"""

# Функция scatter_matrix позволяет построить для каждой количественной переменной гистограмму,
# а для каждой пары таких переменных – диаграмму рассеяния:
scatter_matrix(data, alpha=0.05, figsize=(15, 15))

# Из построенных диаграмм видно, что признаки не сильно коррелируют между собой,
# что впрочем можно также легко установить, посмотрев на корреляционную матрицу.
# Все ее недиагональные значения по модулю не превосходят 0.4:
print(data.corr())

"""
print(data[(data['numberOfAttemptedLevels'] == data['maxPlayerLevel'])
           & ((data['numberOfAttemptedLevels'] - 1 + data["attemptsOnTheHighestLevel"]) == data['totalNumOfAttempts'])
           & (data['doReturnOnLowerLevels'] == 1)].head())
"""

"""
# Можно выбрать любую пару признаков и нарисовать диаграмму рассеяния
# для этой пары признаков, изображая точки, соответствующие объектам
# из разных классов разным цветом: + – красный, - – синий.
# Например, для пары признаков A2, A11 получаем следующую диаграмму:

col1 = 'fractionOfAttemptedLevels'
col2 = 'numberOfDaysActuallyPlayed'

f2 = plt.figure(figsize=(10, 6))

plt.scatter(data[col1][y[0] == 1],
            data[col2][y[0] == 1],
            alpha=0.75,
            color='red',
            label='1')

plt.scatter(data[col1][y[0] == 0],
            data[col2][y[0] == 0],
            alpha=0.75,
            color='blue',
            label='0')

plt.xlabel(col1)
plt.ylabel(col2)
plt.legend(loc='best')
plt.show()

f2.savefig('corr_diagram')
"""
plt.show()
#f1.savefig('scatter_matrix')
