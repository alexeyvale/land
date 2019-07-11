import matplotlib.pyplot as plt


class Component:
    def __init__(self, name, weight, s_ab):
        self.name = name
        self.weight = weight
        self.s_ab = s_ab

    def get_weighted_similarity(self):
        return self.weight * self.s_ab

    def to_string(self):
        return '{0}: W = {1}, Sab = {2}'.format(self.name, self.weight, self.s_ab)


def condition(point):
    return point[0] >= 0.1 + point[1]


components = [
    Component('modifiers', 0.1, 0.5),
    Component('type', 0.3, 0.5),
    Component('name', 0.6, 0.4)
]

s_threshold = 0.2

changed_names = ['type', 'modifiers']
changed_components = list(filter(lambda c: c.name in changed_names, components))
unchanged_components = list(filter(lambda c: c.name not in changed_names, components))

print(list(map(lambda c: c.to_string(), unchanged_components)))

s_aa = sum(map(lambda c: c.weight, components))
s_ab = sum(map(lambda c: c.get_weighted_similarity(), components))
max_sum = s_ab + s_aa

min_s_a1a = sum(map(lambda c: c.weight, unchanged_components))
min_s_a1b = sum(map(lambda c: c.get_weighted_similarity(), unchanged_components))
max_s_a1b = sum(map(lambda c: c.get_weighted_similarity(), unchanged_components)) + \
            sum(map(lambda c: c.weight, changed_components))

print('s_aa = {0:0.2f}; s_ab = {1:0.2f}; s_a1a >= {2:0.2f}; s_a1b in [{3:0.2f};{4:0.2f}]'
      .format(s_aa, s_ab, min_s_a1a, min_s_a1b, max_s_a1b))

plt.xlim([0, 1])
plt.ylim([0, 1])

fig, ax = plt.subplots()
ax.set(xlabel=r'$S_{a1a}$', ylabel=r'$S_{a1b}$', title='')
ax.grid()

# Очерчиваем единичный квадрат
ax.plot([0, 1], [1, 1], linestyle='solid', color='black', linewidth=1.5)
ax.plot([0, 1], [0, 0], linestyle='solid', color='black', linewidth=1.5)
ax.plot([1, 1], [1, 0], linestyle='solid', color='black', linewidth=1.5)
ax.plot([0, 0], [1, 0], linestyle='solid', color='black', linewidth=1.5)

# Очерчиваем участок, которому соответствуют изменения указанных компонент
ax.plot([min_s_a1a, min_s_a1a], [0, 1], linestyle='dashed', color='gray')
ax.plot([0, 1], [min_s_a1b, min_s_a1b], linestyle='dashed', color='gray')
ax.plot([0, 1], [max_s_a1b, max_s_a1b], linestyle='dashed', color='gray')

# Отсекаем области, в которых нарушаются ограничения суммы похожестей
changed_weights_sum = sum(map(lambda c: c.weight, changed_components))
unchanged_weights_sum = 1 - changed_weights_sum

search_area = [
    (min_s_a1a, min_s_a1b),
    (min_s_a1a, min_s_a1b + (1 - s_ab) * changed_weights_sum),
    (min_s_a1a + s_ab * changed_weights_sum, max_s_a1b),
    (1, min_s_a1b + s_ab * changed_weights_sum),
    (1, min_s_a1b),
    (min_s_a1a, min_s_a1b)
]
search_area_x, search_area_y = zip(*search_area)
ax.plot(search_area_x, search_area_y, linestyle='solid', color='blue', linewidth=2.0, label=r'search_area')

# отсекаем области, в которых не примем никакого решения
below_threshold_area = [(0, 0), (0, s_threshold), (s_threshold, s_threshold), (s_threshold, 0), (0, 0)]
below_threshold_area_x, below_threshold_area_y = zip(*below_threshold_area)
ax.plot(below_threshold_area_x, below_threshold_area_y,
        linestyle='solid', color='red', linewidth=2.0, label=r'threshold')

# рисуем точки, обозначающие область принятия правильного и неправильного решения
step = 0.03
cur_x = 0
cur_y = 0
points = []

while cur_x <= 1:
    cur_y = 0
    while cur_y <= 1:
        points.append((cur_x, cur_y))
        cur_y += step
    cur_x += step

a_points = filter(condition, points)
b_points = filter(lambda p: p not in a_points, points)

a_x, a_y = zip(*a_points)
b_x, b_y = zip(*b_points)

ax.scatter(a_x, a_y, marker='.', color='blue')

# считаем площади


def in_convex_hull(point, vertices):
    for i in range(1, len(vertices)):
        if (vertices[i][0] - vertices[i-1][0]) * (point[1] - vertices[i-1][1]) - \
                (vertices[i][1] - vertices[i-1][1]) * (point[0] - vertices[i-1][0]) > 0:
            return False
    return True


def can_make_right_decision(point):
    return condition(point) and point[0] > s_threshold


points_in_search_area = list(filter(lambda p: in_convex_hull(p, search_area), points))
whole_area = len(points_in_search_area)
good_area = len(list(filter(condition, points_in_search_area)))
print('{0:0.2f}'.format(good_area/whole_area))

plt.show()



