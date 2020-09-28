import sys
import pandas as pd
import cloudpickle as cpickle
import torch

# Описываем класс для нейросети
class NeuralModel(torch.nn.Module):

    # В конструктор передаём количество признаков, количество возможных исходов,
    # список количеств нейронов для слоёв и dropout - долю нейронов, исключаемых
    # из обучения на различных итерациях
    def __init__(self, input_size, output_size, layers, p=0.1):
        super().__init__()

        # self.batch_norm_num = torch.nn.BatchNorm1d(input_size)

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
        # x = self.batch_norm_num(x)
        x = self.layers(x)
        return x


model_file = sys.argv[1]
x_file = sys.argv[2]
y_file = sys.argv[3]

with open(model_file, 'rb') as fid:
    model = cpickle.load(fid)

data = pd.read_csv(x_file, sep=';', decimal=',')
x = data.drop(['IsAuto'], axis=1)

#y_val = model.predict_proba(x)[:, 1]

#torch.set_default_dtype(torch.double)

x = torch.tensor(x.values, dtype=torch.float)
y_val = model(x)
sm = torch.nn.Softmax()
probs = sm(y_val).detach().numpy()

with open(y_file, 'w') as predictions_file:
    predictions_file.write(';'.join(map(lambda f: str(f).replace('.', ','), probs[:, 1])))
    # predictions_file.write(';'.join(map(lambda f: str(f).replace('.', ','), y_val)))