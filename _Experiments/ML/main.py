# https://stackabuse.com/introduction-to-pytorch-for-classification/
# https://habr.com/ru/post/428213/
# https://www.kaggle.com/cast42/lightgbm-model-explained-by-shap

import os
import cloudpickle as cpickle
import pandas as pd
import numpy as np
import shap
import lightgbm as lgbm
import sklearn as sk
from sklearn.model_selection import train_test_split, StratifiedKFold, GridSearchCV, RandomizedSearchCV


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
        'max_depth': np.arange(2, 10),
        'n_estimators': np.arange(50, 100),
        'learning_rate': np.arange(0.1, 0.36, 0.05),
        # 'max_bin': np.arange(18, 21),
        # 'reg_alpha': np.arange(0.1, 0.11, 0.1),
        # 'reg_lambda': np.arange(0.5, 0.65, 0.1),
        'num_leaves': np.arange(3, 10),
        'subsample': np.arange(0.2, 0.7, 0.1),
        'subsample_freq': np.arange(1, 6)
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
    """
    preprocessors = sk.pipeline.Pipeline([
       ('var_thr', VarianceThreshold()),
       #  ('st_scaler', sk.preprocessing.StandardScaler(with_mean=False))])
    x = preprocessors.fit_transform(x, y)
    """

    model = sk.ensemble.RandomForestClassifier()
    parameters_dict = {
        'max_depth': np.arange(2, 10),
        'n_estimators': np.arange(50, 100)
    }

    cv = StratifiedKFold(n_splits=3, shuffle=True, random_state=11)
    clf = RandomizedSearchCV(model, parameters_dict, cv=cv, n_iter=100, scoring='roc_auc', verbose=3)
    clf.fit(x, y)

    if verbose:
        print(clf.best_params_)
        print(clf.best_score_)

    return clf


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
    x_train, x_test, y_train, y_test = train_test_split(x, y, test_size=0.33, random_state=42, stratify=y)

    trained_model = fit_grid_rf(x_train, y_train, True)
    eval_model(trained_model, x_test, y_test)

    with open(f'{models_directory}/{ext}.pkl', 'wb') as fid:
        cpickle.dump(trained_model, fid)

