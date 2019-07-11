import matplotlib.pyplot as plt
import json

with open('C:/Users/Алексей/Documents/LanD Workspace/last_batch_parsing_stats.json') as f:
    data = json.load(f)

print(data[0]["GeneralTimeSpent"])

sorted = sorted(data, key=lambda k: k['tokens'])

xs = []
ys = []

border = 0
step = 50
sum = 0
accum = []
count = 0

for point in sorted:
    if point['tokens'] < border + step:
        sum += point['time']
        accum.append(point['time'])
        count += 1
    else:
        border += step
        if count > 0:
            xs.append(border / 2)
            ys.append(sum/count)
            #ys.append(accum[count // 2])
        count = 0
        sum = 0
        accum = []

fig, ax = plt.subplots()
ax.set(xlabel='tokens', ylabel='milliseconds', title='')
ax.grid()

# Очерчиваем единичный квадрат
ax.plot(xs, ys, linestyle='solid', color='black', linewidth=1.5)

plt.xlim([0, 3000])
plt.ylim([0, 1000])

plt.show()



