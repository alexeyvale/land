import pandas as pd
from sklearn import metrics
from sklearn.model_selection import GridSearchCV
from sklearn.model_selection import StratifiedKFold


def read_from_file(name_of_file, index_col, y_col, is_train):
    data = pd.read_csv(name_of_file, index_col=index_col)
    if is_train:
        x = data.drop([y_col], axis=1) if y_col else data.iloc[:, 0:data.shape[1] - 1]
        y = data[y_col] if y_col else data.iloc[:, data.shape[1] - 1]
        return x, y, data.index.get_values()
    else:
        return data, data.index.get_values()


def fit(x, y, model, preprocessors):
    x = preprocessors.fit_transform(x, y)
    model.fit(x, y)


def predict_proba(X, model, preprocessors):
    X = preprocessors.transform(X)
    return model.predict_proba(X)[:, 1]


def grid_fit(X, y, model, preprocessors, param_grid):
    cv = StratifiedKFold(y, n_folds=5, shuffle=True)
    clf = GridSearchCV(model, param_grid, cv=cv, scoring='roc_auc',verbose=3)
    fit(X, y, clf, preprocessors)
    return clf


def clf_quality_report(X, expected, estimator, preprocessors):
    # make predictions
    predicted = estimator.predict(preprocessors.transform(X))
    # summarize the fit of the model
    return "Classification matrix: \n%s\nConfusion matrix:\n%s" % \
           (metrics.classification_report(expected, predicted), \
           metrics.confusion_matrix(expected, predicted))
    #print(metrics.classification_report(expected, predicted))
    #print(metrics.confusion_matrix(expected, predicted))


def save_to_file(name_of_file, header, predictions, indices):
    output = open(name_of_file, "w")
    output.write(header)
    for i in range(0, len(predictions)):
        if indices:
            output.write("%f\n" % (predictions[i]))
        else:
            output.write("%d,%f\n" % (indices[i], predictions[i]))