# -*- coding: utf-8 -*-
"""Builds the school floor plan programmatically and checks it before it ever
reaches Unity: every row the same length, every room connected to the corridor,
and the power cell candidates spread away from the motor."""
import io
import os

W = 42          # columns
ROOM_SPLITS = [10, 16, 23, 30, 36]   # wall columns dividing the wings into rooms

# rows 1-5 north classrooms, 6 wall, 7-9 corridor, 10 wall, 11-15 south, 16 wall
ROWS = 17


def blank_row(fill='.'):
    return ['#'] + [fill] * (W - 2) + ['#']


grid = [blank_row() for _ in range(ROWS)]

# outer walls
grid[0] = ['#'] * W
grid[ROWS - 1] = ['#'] * W
for r in range(ROWS):
    grid[r][0] = '#'
    grid[r][W - 1] = '#'

# --- room divider walls in both wings -------------------------------------
for c in ROOM_SPLITS:
    for r in list(range(1, 6)) + list(range(11, 16)):
        grid[r][c] = '#'

# --- the two corridor walls ------------------------------------------------
for c in range(1, W - 1):
    grid[6][c] = '#'
    grid[10][c] = '#'


def room_spans():
    """Column ranges of each room, north and south are the same."""
    spans = []
    start = 1
    for c in ROOM_SPLITS + [W - 1]:
        spans.append((start, c - 1))
        start = c + 1
    return spans


SPANS = room_spans()

# A door per room, in the middle of its span, on both corridor walls. This is the
# fix for the first layout: one door per wing meant fourteen of seventeen spawns
# were walled off from the player.
for (a, b) in SPANS:
    mid = (a + b) // 2
    grid[6][mid] = 'D'
    grid[10][mid] = 'D'

# --- lockers, in short banks inside the corridor ---------------------------
# They line the corridor rather than forming a wall across it: cover to break the
# sightline down a straight hall, with the middle row always clear to walk.
def blocks_a_door(row, c):
    """A locker in the corridor must never stand in front of a classroom door."""
    wall = 6 if row == 7 else 10
    return any(grid[wall][x] == 'D' for x in (c - 1, c, c + 1, c + 2) if 0 <= x < W)


for i, c in enumerate(range(3, W - 4, 6)):
    row = 7 if i % 2 == 0 else 9
    if blocks_a_door(row, c):
        continue
    grid[row][c] = 'L'
    grid[row][c + 1] = 'L'

# --- contents ---------------------------------------------------------------
PROPS = 'KTBYS.'          # a marker may replace furniture; the furniture just moves aside


def put(r, c, ch):
    current = grid[r][c]
    assert current in PROPS, 'cell %d,%d already holds %r' % (r, c, current)
    grid[r][c] = ch


# gym: the westmost north room (cols 1-9). Equipment around the edges only — a gym
# is mostly floor, and a grid of it every other cell left a spawn boxed into a
# 0.4 m gap that no agent could path out of.
for (r, c) in [(1, 2), (1, 5), (1, 8), (3, 2), (3, 8), (4, 5)]:
    if grid[r][c] == '.':
        grid[r][c] = 'Y'
for c in range(2, 7):
    grid[5][c] = 'S'          # the stage along the gym's south side

# classrooms with desks: north rooms 2, 3, 4
for (a, b) in [SPANS[1], SPANS[2], SPANS[3]]:
    for r in range(1, 5):
        for c in range(a + 1, b, 2):
            if grid[r][c] == '.':
                grid[r][c] = 'K'

# library: north room 5
a, b = SPANS[4]
for r in range(1, 5, 2):
    for c in range(a, b + 1, 2):
        if grid[r][c] == '.':
            grid[r][c] = 'B'

# cafeteria: south rooms 3 and 4
for (a, b) in [SPANS[2], SPANS[3]]:
    for r in range(11, 15):
        for c in range(a + 1, b, 2):
            if grid[r][c] == '.':
                grid[r][c] = 'T'

# more classrooms: south room 2
a, b = SPANS[1]
for r in range(11, 15):
    for c in range(a + 1, b, 2):
        if grid[r][c] == '.':
            grid[r][c] = 'K'

# --- keep the doorways clear ------------------------------------------------
# A prop standing in a doorway seals the room behind it, and it does it silently:
# the plan looks connected because the door is there. The stage did exactly this to
# the gym, which was the level's only remaining unreachable spawn.
SOLID_PROPS = 'LBSY'

# Clearing only the cell either side of the door is not enough: the gym's stage sat
# one cell further in and sealed it just as effectively. Carve the whole column of
# each door through its room, so every door opens onto a channel rather than into a
# pocket.
for r in (6, 10):
    for c in range(W):
        if grid[r][c] != 'D':
            continue

        room_rows = range(1, 6) if r == 6 else range(11, 16)
        for rr in room_rows:
            if grid[rr][c] in SOLID_PROPS:
                grid[rr][c] = '.'

        for rr in (r - 1, r + 1):
            if grid[rr][c] in SOLID_PROPS:
                grid[rr][c] = '.'

# --- markers ----------------------------------------------------------------
put(8, 2, 'P')                       # front hall, west end of the corridor
grid[15][W - 2] = 'E'                # fire door, south-east corner
put(15, W - 4, 'G')                  # motor beside it

# power cell candidates: one per wing-end room, all far from the motor
for (r, c) in [(2, 12), (4, 25), (2, 38), (13, 3), (11, 20)]:
    put(r, c, 'W')

# children hiding
for (r, c) in [(3, 38), (13, 33), (12, 6)]:
    put(r, c, 'R')

# supplies
for (r, c) in [(1, 13), (12, 13), (4, 33), (14, 21)]:
    put(r, c, 'A')
for (r, c) in [(2, 20), (14, 4), (11, 33), (3, 27)]:
    put(r, c, 'M')
for (r, c) in [(13, 25), (1, 32)]:
    put(r, c, 'X')
for (r, c) in [(4, 18), (12, 27), (14, 38), (1, 3)]:
    put(r, c, 'V')

# zombie spawn markers, spread through every room and the corridor
for (r, c) in [(3, 5), (1, 18), (3, 21), (1, 25), (4, 28), (2, 32), (4, 39),
               (11, 3), (13, 13), (11, 18), (14, 25), (12, 31), (14, 34), (11, 38),
               (8, 15), (8, 29), (9, 35)]:
    put(r, c, 'Z')

rows = [''.join(r) for r in grid]

# --- checks before this ever reaches Unity ---------------------------------
assert all(len(r) == W for r in rows), 'ragged rows'

# Flood fill from the player start: everything walkable must be reachable, and a
# locker cell counts as blocked because a bank of lockers is solid.
start = None
for r, row in enumerate(rows):
    for c, ch in enumerate(row):
        if ch == 'P':
            start = (r, c)
assert start, 'no player start'

# Anything that fills most of a two-metre cell blocks an agent: lockers, bookshelves,
# the stage. Desks and cafeteria tables are narrow enough to walk around inside a cell.
BLOCKED = set('#LBSY')
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

unreached = []
for r, row in enumerate(rows):
    for c, ch in enumerate(row):
        if ch in BLOCKED:
            continue
        if (r, c) not in seen:
            unreached.append((r, c, ch))

if unreached:
    print('UNREACHABLE CELLS:', unreached[:20])
    raise SystemExit('layout is not fully connected')

print('plan is %d x %d, fully connected from the player start' % (ROWS, W))
for i, row in enumerate(rows):
    print('%2d %s' % (i, row))

# --- write it into the generator -------------------------------------------
# Relative to this script, which sits at the repo root. This used to be an
# absolute path into one particular machine's home directory, which was wrong
# twice over: it named a user, and it named UnityProjects -- a folder this
# project moved out of in September 2026, so the tool pointed at nothing.
p = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                 'Assets', 'Scripts', 'Level', 'SchoolGenerator.cs')
s = io.open(p, encoding='utf-8').read()

head = s.index('private string[] rows =')
open_brace = s.index('{', head)
close_brace = s.index('};', open_brace)

block = '{\n' + ',\n'.join('            "%s"' % r for r in rows) + '\n        '
s = s[:open_brace] + block + s[close_brace:]

io.open(p, 'w', encoding='utf-8', newline='').write(s)
print('\nwritten into SchoolGenerator.cs')
