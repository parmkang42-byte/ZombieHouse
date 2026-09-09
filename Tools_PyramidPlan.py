# -*- coding: utf-8 -*-
"""Generates the pyramid's floor plan as a MAZE and proves it before it reaches Unity.

The first pyramid was a spine with chambers off it — legible, and therefore not a
tomb. This one is a proper maze carved by recursive backtracking, with a handful of
chambers knocked through it and a few extra links added so it is not a pure tree:
a tree maze has exactly one route between any two points, which reads as a puzzle,
while a maze with loops reads as a place that was built and then got lost.

Everything is still flood-filled with solid props treated as solid, and the script
refuses to write anything out if one cell is unreachable.
"""
import io
import random
import os

random.seed(20260821)

# Maze cells are on odd coordinates; walls are on even ones. A CELLS x CELLS maze
# becomes a (2*CELLS+1) grid.
CELLS_X = 22
CELLS_Y = 10

W = CELLS_X * 2 + 1
ROWS = CELLS_Y * 2 + 1

WALL = '#'
FLOOR = '.'
SOLID_PROPS = 'CQO'
BLOCKED = set(WALL + SOLID_PROPS)

grid = [[WALL] * W for _ in range(ROWS)]


def cell_to_grid(cx, cy):
    return cy * 2 + 1, cx * 2 + 1


# --- carve the maze -----------------------------------------------------------
visited = [[False] * CELLS_X for _ in range(CELLS_Y)]
stack = [(0, CELLS_Y // 2)]
visited[CELLS_Y // 2][0] = True
r, c = cell_to_grid(0, CELLS_Y // 2)
grid[r][c] = FLOOR

while stack:
    cx, cy = stack[-1]

    neighbours = []
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        nx, ny = cx + dx, cy + dy
        if 0 <= nx < CELLS_X and 0 <= ny < CELLS_Y and not visited[ny][nx]:
            neighbours.append((nx, ny, dx, dy))

    if not neighbours:
        stack.pop()
        continue

    nx, ny, dx, dy = random.choice(neighbours)
    visited[ny][nx] = True

    r, c = cell_to_grid(cx, cy)
    grid[r][c] = FLOOR
    grid[r + dy][c + dx] = FLOOR          # knock through the wall between them
    nr, nc = cell_to_grid(nx, ny)
    grid[nr][nc] = FLOOR

    stack.append((nx, ny))

# --- add loops ----------------------------------------------------------------
# A perfect maze is a tree: one route between any two points, and it plays like a
# puzzle to be solved rather than a place to be lost in. Knocking out a fraction of
# the remaining walls gives alternative routes and dead ends that reconnect, which
# is what makes it feel like architecture.
loops = 0
for _ in range(220):
    r = random.randrange(1, ROWS - 1)
    c = random.randrange(1, W - 1)

    if grid[r][c] != WALL:
        continue

    # Only knock out a wall with open floor on exactly two opposite sides.
    horizontal = grid[r][c - 1] == FLOOR and grid[r][c + 1] == FLOOR
    vertical = grid[r - 1][c] == FLOOR and grid[r + 1][c] == FLOOR
    if not (horizontal or vertical):
        continue

    grid[r][c] = FLOOR
    loops += 1

# --- chambers -----------------------------------------------------------------
# Rooms knocked through the maze. They are what stop it being a uniform grid of
# corridors, and they are where everything worth finding lives.
CHAMBERS = [
    (2, 3, 6, 10, 'antechamber'),
    (12, 2, 17, 8, 'hall of columns'),
    (2, 13, 6, 18, 'the shaft room'),
    (13, 13, 18, 19, 'treasury'),
    (7, 30, 13, 41, 'burial chamber'),
]

for (r0, c0, r1, c1, _name) in CHAMBERS:
    for r in range(r0, r1 + 1):
        for c in range(c0, c1 + 1):
            grid[r][c] = FLOOR

# --- props --------------------------------------------------------------------
def scatter(char, cells):
    for (r, c) in cells:
        if grid[r][c] == FLOOR:
            grid[r][c] = char


scatter('C', [(9, 32), (9, 36), (9, 40), (12, 32), (12, 36), (12, 40),
              (13, 4), (13, 7), (16, 4), (16, 7)])
scatter('Q', [(10, 39), (4, 5), (15, 16), (17, 15), (11, 34)])
scatter('O', [(3, 9), (5, 16), (16, 18), (8, 34), (12, 38)])

# --- keep every prop out of a doorway -----------------------------------------
# A prop in a one-cell corridor is a wall. In a maze that is fatal in a way it never
# was in the school: it does not seal a room, it silently removes a route, and the
# flood fill is the only thing that would ever notice.
for r in range(1, ROWS - 1):
    for c in range(1, W - 1):
        if grid[r][c] not in SOLID_PROPS:
            continue

        open_sides = sum(1 for (dr, dc) in ((1, 0), (-1, 0), (0, 1), (0, -1))
                         if grid[r + dr][c + dc] != WALL)

        # Two open sides means it is standing in a corridor rather than in a room.
        if open_sides <= 2:
            grid[r][c] = FLOOR

# --- contents -----------------------------------------------------------------
PLACEABLE = FLOOR + SOLID_PROPS


def put(r, c, ch):
    assert grid[r][c] in PLACEABLE, 'cell %d,%d holds %r' % (r, c, grid[r][c])
    grid[r][c] = ch


start_r, start_c = cell_to_grid(0, CELLS_Y // 2)
put(start_r, start_c, 'P')

# Out through the far side of the burial chamber.
grid[10][W - 1] = 'E'
put(10, W - 2, 'G')

put(4, 4, 'W')
put(16, 5, 'W')
put(4, 17, 'W')
put(17, 18, 'W')
put(3, 7, 'W')

put(14, 3, 'U')
put(5, 14, 'U')

put(5, 8, 'R')
put(17, 16, 'R')
put(8, 38, 'R')

for (r, c) in [(3, 5), (15, 14), (6, 9), (11, 31), (13, 40), (16, 17)]:
    put(r, c, 'N')          # belt crates

for (r, c) in [(2, 8), (14, 17), (6, 4), (12, 35), (17, 13)]:
    put(r, c, 'A')
for (r, c) in [(5, 4), (3, 16), (16, 14), (9, 37)]:
    put(r, c, 'M')
for (r, c) in [(2, 5), (18, 15)]:
    put(r, c, 'X')
for (r, c) in [(6, 6), (15, 18), (10, 33), (13, 3)]:
    put(r, c, 'V')

# Torches: at junctions through the maze, and round the burial chamber.
torches = 0
for r in range(1, ROWS - 1, 2):
    for c in range(1, W - 1, 4):
        if grid[r][c] != FLOOR:
            continue

        exits = sum(1 for (dr, dc) in ((1, 0), (-1, 0), (0, 1), (0, -1))
                    if grid[r + dr][c + dc] != WALL)

        # Junctions only, and only one in four of those. A loopy maze has junctions
        # everywhere, and lighting all of them turned the tomb into a lit corridor
        # system — 64 torches on the first pass. The dark between them is the level.
        if exits < 3 or random.random() > 0.26:
            continue

        grid[r][c] = 'T'
        torches += 1

for (r, c) in [(8, 31), (8, 39), (13, 31), (13, 39), (10, 30)]:
    if grid[r][c] == FLOOR:
        grid[r][c] = 'T'
        torches += 1

# Enemy spawns, spread through the chambers and the maze.
spawn_cells = [(4, 8), (5, 5), (14, 5), (17, 4), (4, 15), (6, 17), (15, 17), (18, 17),
               (8, 32), (11, 37), (13, 34), (8, 41), (12, 41), (9, 34),
               (3, 21), (7, 23), (11, 21), (15, 23), (5, 27), (17, 27), (9, 25), (13, 27)]
for (r, c) in spawn_cells:
    if grid[r][c] == FLOOR:
        grid[r][c] = 'Z'

rows = [''.join(r) for r in grid]

# --- prove it -----------------------------------------------------------------
assert all(len(r) == W for r in rows), 'ragged rows'

start = next(((r, c) for r, row in enumerate(rows)
              for c, ch in enumerate(row) if ch == 'P'), None)
assert start, 'no player start'

seen = {start}
stack = [start]
while stack:
    r, c = stack.pop()
    for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        nr, nc = r + dr, c + dc
        if not (0 <= nr < ROWS and 0 <= nc < W):
            continue
        if (nr, nc) in seen or rows[nr][nc] in BLOCKED:
            continue
        seen.add((nr, nc))
        stack.append((nr, nc))

unreached = [(r, c, ch) for r, row in enumerate(rows)
             for c, ch in enumerate(row)
             if ch not in BLOCKED and (r, c) not in seen]

if unreached:
    print('UNREACHABLE CELLS:', unreached[:24])
    raise SystemExit('layout is not fully connected')

counts = {}
for row in rows:
    for ch in row:
        counts[ch] = counts.get(ch, 0) + 1

floor_cells = sum(v for k, v in counts.items() if k not in BLOCKED)
print('maze is %d x %d, %d walkable cells, %d extra loops knocked through'
      % (ROWS, W, floor_cells, loops))
print('markers:', {k: v for k, v in sorted(counts.items()) if k in 'PEGWUNRAMXVZTCQO'})
for i, row in enumerate(rows):
    print('%2d %s' % (i, row))

# --- write it into the generator ------------------------------------------------
# Relative to this script, which sits at the repo root. This used to be an
# absolute path into one particular machine's home directory, which was wrong
# twice over: it named a user, and it named UnityProjects -- a folder this
# project moved out of in September 2026, so the tool pointed at nothing.
p = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                 'Assets', 'Scripts', 'Level', 'PyramidGenerator.cs')
s = io.open(p, encoding='utf-8').read()

head = s.index('private string[] rows =')
open_brace = s.index('{', head)
close_brace = s.index('};', open_brace)

block = '{\n' + ',\n'.join('            "%s"' % r for r in rows) + '\n        '
s = s[:open_brace] + block + s[close_brace:]

io.open(p, 'w', encoding='utf-8', newline='').write(s)
print('\nwritten into PyramidGenerator.cs')
