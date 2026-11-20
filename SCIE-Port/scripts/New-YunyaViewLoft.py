"""V4: retain the approved envelope; refine eaves, loft circulation and side views."""
import argparse
import importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location('yunya_loft', Path(__file__).with_name('New-YunyaLoft.py'))
loft = importlib.util.module_from_spec(spec)
spec.loader.exec_module(loft)
base = loft.base


def build():
    h = loft.build()
    original_bounds = tuple((min(p[a] for p in h.cells), max(p[a] for p in h.cells)) for a in range(3))

    # A small clear 2x2 view opening, separated from the existing platform door by a pier.
    # Move the obstructing shelf columns to the rear of that door, keeping the view clear.
    h.clear(16, 16, 1, 4, 8, 9)
    h.group = 'right_bookshelves'
    for v in (12, 13):
        for y in (1, 2, 3):
            h.put(16, y, v, 'BookshelfBlock', 9)
    h.group = 'right_view_window'
    for v in (8, 9):
        for y in (2, 3):
            h.detail(17, y, v, 'Window', 0, 1)
    # Clear a two-wide approach inside the one-cell doorway, with no display furniture.
    h.clear(15, 16, 1, 2, 10, 11)

    # Keep the roof ridge/slope, but halve the backing thickness at the sealed edges.
    # The existing variant-5 wedges still close the triangular front/back roof faces.
    for u in range(7, 19):
        yy = 12 - abs(u - 12)
        for v in (4, 16):
            h.group = 'roof'
            h.put(u, yy - 1, v, 'WoodenSlabBlock', 51)

    # Replace the chunky tea lean-to with thin tiled flats and two connecting pitches.
    for p in [p for p, group in h.groups.items() if group == 'tea_roof']:
        del h.cells[p]
        del h.groups[p]
    h.clear(2, 2, 5, 5, 7, 14)
    h.group = 'tea_roof'
    courses = ((1, 5, 2), (2, 5, 5), (3, 6, 2), (4, 6, 2), (5, 6, 5), (6, 7, 2))
    for u, y, variant in courses:
        for v in range(6, 16):
            h.detail(u, y, v, 'Roof', variant, 1)
    # U7 belongs to the continuous main pitch; restore the part overwritten by V2's roof.
    for v in range(6, 16):
        h.detail(7, 7, v, 'Roof', 0, 1)
    h.group = 'ceiling_beams'
    for u in (3, 5):
        h.log(u, u, 5, 5, 7, 14)
    h.log(9, 16, 5, 5, 8, 8)
    h.log(15, 16, 5, 5, 12, 12)
    h.group = 'gable_beam'
    h.log(10, 14, 6, 6, 6, 6)

    # Widen the upper turn without raising or extending the house. Move the edge guard
    # to the new supported outer edge, leaving a 2x2 landing behind the last stair.
    h.clear(10, 10, 6, 6, 12, 13)
    h.group = 'loft_floor'
    h.box(9, 9, 5, 5, 12, 13, 'PlanksBlock')
    h.group = 'loft_beams'
    for v in (12, 13):
        h.log(9, 9, 4, 4, v, v)
    h.group = 'loft_rail'
    for v in (12, 13):
        h.detail(9, 6, v, 'Timber', 4, 1)
    h.group = 'platform'
    h.log(13, 20, -1, -1, 5, 5)
    for v in (8, 11):
        h.log(18, 20, -1, -1, v, v)

    assert original_bounds == tuple((min(p[a] for p in h.cells), max(p[a] for p in h.cells)) for a in range(3))
    assert h.cells[(17, 1, 11)][0] == 'WoodenDoorBlock'
    for v in (8, 9):
        for y in (2, 3):
            assert h.cells[(17, y, v)][0] == 'AncientWindowBlock'
            assert (16, y, v) not in h.cells
    for u in (10, 11):
        for v in (11, 12):
            assert h.cells[(u, 5, v)][0] in ('PlanksBlock', 'WoodenStairsBlock')
            assert all((u, y, v) not in h.cells for y in (6, 7))
    return h


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=base.ROOT / 'artifacts/yunya-view-loft-v4')
    args = parser.parse_args()
    base.write(build(), args.output, name='云崖书屋·观景阁楼版 V4', filename='yunya-view-loft-v4.json',
        additional_landmarks={'loft': (11, 5, 11), 'loftTurn': (10, 5, 12),
                              'loftRead': (12, 5, 12), 'rightDoorAccess': (17, 0, 11),
                              'windowView': (16, 0, 8)},
        floor_levels=(0, 5), glass_side='-X (small 2x2 view window; platform door retained)',
        scope='Same V2/V3 envelope; small right view window with clear interior sightline; door and shelves retained; thinner tiled tea eaves, half-thickness roof-edge backing, wider loft turn and supported lighting; no exterior stairs or scenery.')
