# https://stackabuse.com/introduction-to-pytorch-for-classification/
# https://habr.com/ru/post/428213/
# https://www.kaggle.com/cast42/lightgbm-model-explained-by-shap

import os
import cloudpickle as cpickle
import pandas as pd
import matplotlib.pyplot as plt
import numpy as np
import torch
import shap
import lightgbm as lgbm
import sklearn as sk
from sklearn.feature_selection import SelectKBest
from sklearn.inspection import permutation_importance
from sklearn.model_selection import train_test_split, StratifiedKFold, GridSearchCV, RandomizedSearchCV

RANDOM_STATE_SEED = 11

def eval_model(model, x_test, y_test):
    y_val = model.predict(x_test)

    print('Confusion matrix:')
    print(sk.metrics.confusion_matrix(y_test, y_val))
    print('Clf report:')
    print(sk.metrics.classification_report(y_test, y_val))
    print('Accuracy:')
    print(sk.metrics.accuracy_score(y_test, y_val))
    print('AUC ROC:')
    print(sk.metrics.roc_auc_score(y_test, y_val))


def fit_grid_lgbm(x, y, verbose):

    model = lgbm.LGBMClassifier(boosting_type='gbdt')
    parameters_dict = {
        'max_depth': np.arange(2, 16),
        'n_estimators': np.arange(50, 121),
        'learning_rate': np.arange(0.1, 0.36, 0.05),
        # 'max_bin': np.arange(18, 21),
        # 'reg_alpha': np.arange(0.1, 0.11, 0.1),
        # 'reg_lambda': np.arange(0.5, 0.65, 0.1),
        # 'num_leaves': np.arange(3, 10),
        # 'subsample': np.arange(0.2, 0.7, 0.1),
        # 'subsample_freq': np.arange(1, 6)
    }

    cv = StratifiedKFold(n_splits=3, shuffle=True, random_state=11)
    clf = RandomizedSearchCV(model, parameters_dict, cv=cv, n_iter=100, scoring='roc_auc', verbose=3)
    clf.fit(x, y)

    if verbose:
        print(clf.best_params_)
        print(clf.best_score_)

        lgbm.plot_importance(clf.best_estimator_)

    return clf


def fit_grid_rf(x, y, verbose):
    union = sk.pipeline.FeatureUnion([
        ('kbest', SelectKBest(k=20)),
        ('nmf', sk.decomposition.NMF(n_components=10, init='random', random_state=RANDOM_STATE_SEED)),
        # ('nmf', sk.decomposition.NMF(n_components=10, init='random', random_state=RANDOM_STATE_SEED))
        # ('pca', sk.decomposition.KernelPCA(n_components=10, kernel='rbf', random_state=RANDOM_STATE_SEED))
        # ('poly', sk.preprocessing.PolynomialFeatures(2, True))])
    ])

    pipeline = sk.pipeline.Pipeline([
        ('union', union)
    ])
    x = pipeline.fit_transform(x, y)

    model = sk.ensemble.RandomForestClassifier()
    parameters_dict = {
        'max_depth': np.arange(7, 10),
        'n_estimators': [80, 90, 100]
    }

    cv = StratifiedKFold(n_splits=3, shuffle=True, random_state=RANDOM_STATE_SEED)
    clf = GridSearchCV(model, parameters_dict, cv=cv, scoring='roc_auc', verbose=3)
    clf.fit(x, y)

    pipeline.steps.append(['step model', clf])

    if verbose:
        print(clf.best_params_)
        print(clf.best_score_)

        """result = permutation_importance(clf, x, y, n_repeats=10, random_state=RANDOM_STATE)
        print(result.importances_mean)
        print(result.importances_std)"""

        importances = clf.best_estimator_.feature_importances_
        std = np.std([tree.feature_importances_ for tree in clf.best_estimator_.estimators_],
                     axis=0)
        indices = np.argsort(importances)[::-1]

        # Print the feature ranking
        print("Feature ranking:")

        for f in range(x.shape[1]):
            print("%d. feature %d (%f)" % (f + 1, indices[f], importances[indices[f]]))

        # Plot the impurity-based feature importances of the forest
        plt.figure()
        plt.title("Feature importances")
        plt.bar(range(x.shape[1]), importances[indices],
                color="r", yerr=std[indices], align="center")
        plt.xticks(range(x.shape[1]), indices)
        plt.xlim([-1, x.shape[1]])
        plt.show()

    return pipeline


def fit_lgbm(x, y, verbose):

    model = lgbm.LGBMClassifier(max_depth=3, n_estimators=50, learning_rate=0.3, boosting_type='gbdt')
    model.fit(x, y)

    # shap_test = shap.TreeExplainer(model).shap_values(X_test)
    # shap.summary_plot(shap_test, X_test, show=True, feature_names=data.columns[:-1])

    return model


def fit_rf(x, y, verbose):
    model = model = sk.ensemble.RandomForestClassifier(n_estimators=500, max_depth=5)
    model.fit(x, y)

    # shap_test = shap.TreeExplainer(model).shap_values(X_test)
    # shap.summary_plot(shap_test[1], X_test, show=True, feature_names=data.columns[:-1])
    # shap.dependence_plot(11, shap_test[1], X_test, feature_names=data.columns[:-1])

    return model


# Описываем класс для нейросети
class NeuralModel(torch.nn.Module):

    # В конструктор передаём количество признаков, количество возможных исходов,
    # список количеств нейронов для слоёв и dropout - долю нейронов, исключаемых
    # из обучения на различных итерациях
    def __init__(self, input_size, output_size, layers, p=0.4):
        super().__init__()

        self.batch_norm_num = torch.nn.BatchNorm1d(input_size)

        all_layers = []

        for i in layers:
            # считает скалярное произведение инпутов на веса
            all_layers.append(torch.nn.Linear(input_size, i))
            # функция активации
            all_layers.append(torch.nn.ReLU(inplace=True))
            # приведение данных к виду, в котором МО=0, дисперсия = 1
            # all_layers.append(torch.nn.BatchNorm1d(i))
            # чтобы избежать переобучения
            all_layers.append(torch.nn.Dropout(p))
            input_size = i

        all_layers.append(torch.nn.Linear(layers[-1], output_size))

        # слои отрабатывают последовательно
        self.layers = torch.nn.Sequential(*all_layers)

    def forward(self, x):
        x = self.batch_norm_num(x)
        x = self.layers(x)
        return x


def fit_neural(x, y, verbose):

    # Разделяем выборку на тренировочную и тестовую
    y_train = torch.tensor(y.values).flatten()
    X_train = torch.tensor(x.values, dtype=torch.float)


    # в первом слое X_train.shape[1] нейронов, в последующих скрытых - задано массивом,
    # в выходном слое 2 нейрона
    model = NeuralModel(X_train.shape[1], 2, [200, 100, 50], p=0.4)
    loss_function = torch.nn.CrossEntropyLoss()
    # adaptive learning rate optimization algorithm
    optimizer = torch.optim.Adam(model.parameters(), lr=0.001)

    epochs = 250
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

    model.eval()

    if verbose:
        print(f'epoch: {i:3} loss: {single_loss.item():10.10f}')

    return model


train_data_directory = 'train_data'
models_directory = '../../LandParserGenerator/Land.Core/Resources/Models'

dataset_files = map(
    lambda f: ('.'.join(sorted(f.split('.')[:-2])), f),
    filter(lambda f: f.endswith('.csv'), os.listdir(train_data_directory))
)

dataset_files_dict = {}
for item in dataset_files:
    dataset_files_dict.setdefault(item[0], []).append(item[1])

for ext, files_list in dataset_files_dict.items():
    print(ext)

    data = pd.concat(
        map(lambda f: pd.read_csv(f'{train_data_directory}/{f}', sep=';', decimal=','), files_list),
        axis=0
    )

    print(data.shape)

    y = data.iloc[:, data.shape[1] - 1]
    x = data.drop(['IsAuto'], axis=1)
    # x_train, x_test, y_train, y_test = train_test_split(x, y, test_size=0.1, random_state=RANDOM_STATE, stratify=y)

    trained_model = fit_grid_rf(x, y, True)
    # eval_model(trained_model, x_test, y_test)

    # shap_test = shap.DeepExplainer(trained_model, x_train).shap_values(x_test)
    # Выводим информацию о влиянии признаков на ответ "1":
    # признаки ранжированы в порядке убывания важности;
    # точки соответствуют объектам датасета, цвет - чем краснее, тем больше значение признака;
    # положение точки по горизонтали определяет величину вклада в ответ нейросети (вклад м.б. + и -)
    # shap.summary_plot(shap_test[1], x_test, show=True, feature_names=data.columns[:-1])

    with open(f'{models_directory}/{ext}.pkl', 'wb') as fid:
        cpickle.dump(trained_model, fid)

