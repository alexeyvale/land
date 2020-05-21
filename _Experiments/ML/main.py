# https://stackabuse.com/introduction-to-pytorch-for-classification/
# https://habr.com/ru/post/428213/
# https://www.kaggle.com/cast42/lightgbm-model-explained-by-shap

import os
import pandas as pd
import numpy as np
import torch
import shap
import torch.nn as nn
import lightgbm as lgbm
from sklearn.model_selection import train_test_split, StratifiedKFold, GridSearchCV
from sklearn.metrics import classification_report, confusion_matrix, accuracy_score
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler


# Описываем класс для нейросети
class Model(nn.Module):

    # В конструктор передаём количество признаков, количество возможных исходов,
    # список количеств нейронов для слоёв и dropout - долю нейронов, исключаемых
    # из обучения на различных итерациях
    def __init__(self, input_size, output_size, layers, p=0.4):
        super().__init__()

        self.batch_norm_num = nn.BatchNorm1d(input_size)

        all_layers = []

        for i in layers:
            # считает скалярное произведение инпутов на веса
            all_layers.append(nn.Linear(input_size, i))
            # функция активации
            all_layers.append(nn.ReLU(inplace=True))
            # приведение данных к виду, в котором МО=0, дисперсия = 1
            all_layers.append(nn.BatchNorm1d(i))
            # чтобы избежать переобучения
            all_layers.append(nn.Dropout(p))
            input_size = i

        all_layers.append(nn.Linear(layers[-1], output_size))

        # слои отрабатывают последовательно
        self.layers = nn.Sequential(*all_layers)

    def forward(self, x):
        x = self.batch_norm_num(x)
        x = self.layers(x)
        return x


def fit_neural(data, verbose):
    if verbose:
        print(data.shape)
        print(data.columns)
        print(data.dtypes)

    # Разделяем фичи и предсказываемое значение
    y = data.iloc[:, data.shape[1] - 1]
    X = data.drop(['IsAuto'], axis=1)

    # Разделяем выборку на тренировочную и тестовую
    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.33, random_state=42)

    y_train = torch.tensor(y_train.values).flatten()
    X_train = torch.tensor(X_train.values, dtype=torch.float)

    y_test = torch.tensor(y_test.values).flatten()
    X_test = torch.tensor(X_test.values, dtype=torch.float)

    # в первом слое X_train.shape[1] нейронов, в последующих скрытых - задано массивом,
    # в выходном слое 2 нейрона
    model = Model(X_train.shape[1], 2, [200, 100, 50], p=0.4)
    loss_function = nn.CrossEntropyLoss()
    # adaptive learning rate optimization algorithm
    optimizer = torch.optim.Adam(model.parameters(), lr=0.001)

    epochs = 300
    aggregated_losses = []

    # датасет прогоняется через нейросеть epochs раз
    for i in range(epochs):
        i += 1
        y_pred = model(X_train)
        single_loss = loss_function(y_pred, y_train)
        aggregated_losses.append(single_loss)

        if verbose and i % 25 == 1:
            print(f'epoch: {i:3} loss: {single_loss.item():10.8f}')

        optimizer.zero_grad()
        # обновление весов в нейросети
        single_loss.backward()
        # обновление градиента
        optimizer.step()

    if verbose:
        print(f'epoch: {i:3} loss: {single_loss.item():10.10f}')

    with torch.no_grad():
        y_val = model(X_test)
        loss = loss_function(y_val, y_test)

    if verbose:
        print(f'Loss on test: {loss:.8f}')

        y_val = np.argmax(y_val, axis=1)
        print(y_val[:5])

        print(confusion_matrix(y_test, y_val))
        print(classification_report(y_test, y_val))
        print(accuracy_score(y_test, y_val))

    shap_test = shap.DeepExplainer(model, X_train).shap_values(X_test)
    # Выводим информацию о влиянии признаков на ответ "1":
    # признаки ранжированы в порядке убывания важности;
    # точки соответствуют объектам датасета, цвет - чем краснее, тем больше значение признака;
    # положение точки по горизонтали определяет величину вклада в ответ нейросети (вклад м.б. + и -)
    shap.summary_plot(shap_test[1], X_test, show=True, feature_names=data.columns[:-1])

    return model


def fit_grid_lgbm(data, verbose):
    # Разделяем фичи и предсказываемое значение
    y = data.iloc[:, data.shape[1] - 1]
    x = data.drop(['IsAuto'], axis=1)

    """
    preprocessors = Pipeline([
       ('var_thr', VarianceThreshold()),
       #  ('st_scaler', StandardScaler(with_mean=False))])
    x = preprocessors.fit_transform(x, y)
    """

    model = lgbm.LGBMClassifier(boosting_type='gbdt')
    grid = {
        'max_depth': np.arange(3, 5),
        'n_estimators': np.arange(50, 51),
        'learning_rate': np.arange(0.3, 0.31, 0.05),
        # 'max_bin': np.arange(18, 21),
        # 'reg_alpha': np.arange(0.1, 0.11, 0.1),
        # 'reg_lambda': np.arange(0.5, 0.65, 0.1),
        'num_leaves': np.arange(5, 6),
        'subsample': np.arange(0.4, 0.5, 0.1),
        'subsample_freq': np.arange(3, 4)
    }

    cv = StratifiedKFold(n_splits=2, shuffle=True, random_state=11)
    clf = GridSearchCV(model, grid, cv=cv, scoring='neg_log_loss', verbose=3)
    clf.fit(x, y)

    if verbose:
        print(clf.best_params_)
        print(clf.best_score_)

    lgbm.plot_importance(clf.best_estimator_)

    return clf


def fit_lgbm(data, verbose):
    # Разделяем фичи и предсказываемое значение
    y = data.iloc[:, data.shape[1] - 1]
    X = data.drop(['IsAuto'], axis=1)

    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.33, random_state=42)

    model = lgbm.LGBMClassifier(max_depth=3, n_estimators=50, learning_rate=0.3, boosting_type='gbdt')
    model.fit(X_train, y_train)

    shap_test = shap.TreeExplainer(model, X_train).shap_values(X_test)
    shap.summary_plot(shap_test, X_test, show=True, feature_names=data.columns[:-1])

    return model


directory = 'train_data'

for filename in os.listdir(directory):
    if filename.endswith('.csv'):
        fileName = f'{directory}/{filename}'

        print(fileName)

        data = pd.read_csv(fileName, sep=';', decimal=',')
        trained_model = fit_lgbm(data, True)

        # torch.save(trained_model.state_dict(), f'models/{filename}')

