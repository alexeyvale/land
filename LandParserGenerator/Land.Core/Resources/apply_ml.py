import sys
import pandas as pd
import cloudpickle as cpickle
# import torch

model_file = sys.argv[1]
x_file = sys.argv[2]
y_file = sys.argv[3]

with open(model_file, 'rb') as fid:
    model = cpickle.load(fid)

data = pd.read_csv(x_file, sep=';', decimal=',')
x = data.drop(['IsAuto'], axis=1)
y_val = model.predict_proba(x)[:, 1]

# x = torch.tensor(x.values, dtype=torch.float)
# y_val = model(x)
# sm = torch.nn.Softmax()
# probs = sm(y_val).detach().numpy()

with open(y_file, 'w') as predictions_file:
    predictions_file.write(';'.join(map(lambda f: str(f).replace('.', ','), y_val)))