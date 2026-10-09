"""Player engineer: brass helmet and goggles, overalls in the player's slot colour.
4 directions x 3 frames (idle, step A, step B)."""
from palette import *
from pixel import Canvas


def engineer(direction, frame, slot):
    cloth = SLOTS[slot]
    c = Canvas(16)
    step = 0 if frame == 0 else (1 if frame == 1 else -1)

    # Legs and boots (alternate when walking).
    if direction in ('down', 'up'):
        ly, ry = 13 + (step > 0), 13 + (step < 0)
        c.rect(5, 11, 2, ly - 11, cloth[0])
        c.rect(9, 11, 2, ry - 11, cloth[0])
        c.rect(5, ly, 2, 2, WOOD[0])
        c.rect(9, ry, 2, 2, WOOD[0])
    else:
        fx = 1 if direction == 'right' else -1
        c.rect(6 + step * fx, 11, 2, 3, cloth[0])
        c.rect(8 - step * fx, 11, 2, 3, cloth[0])
        c.rect(6 + step * fx, 14, 2, 1, WOOD[0])
        c.rect(8 - step * fx, 14, 2, 1, WOOD[0])

    # Body / overalls with a brass buckle.
    c.rect(4, 7, 8, 5, cloth[1])
    c.rect(4, 7, 1, 5, cloth[2])
    c.rect(11, 7, 1, 5, cloth[0])
    if direction == 'down':
        c.px(6, 8, BRASS[3])
        c.px(9, 8, BRASS[3])
        c.rect(6, 10, 4, 1, cloth[0])
    elif direction == 'up':
        # Brass tank backpack.
        c.bevel(5, 7, 6, 5, BRASS[2], BRASS[3], BRASS[0])
    # Arms swing opposite the legs.
    if direction in ('down', 'up'):
        c.rect(3, 8 - (step > 0), 1, 3, cloth[1])
        c.rect(12, 8 - (step < 0), 1, 3, cloth[1])
        c.px(3, 11 - (step > 0), SKIN[1])
        c.px(12, 11 - (step < 0), SKIN[1])
    else:
        ax = 7 + step * (1 if direction == 'right' else -1)
        c.rect(ax, 8, 2, 3, cloth[2])
        c.px(ax, 11, SKIN[1])

    # Head: face, goggles, brass helmet.
    c.rect(5, 2, 6, 5, SKIN[1])
    c.rect(5, 6, 6, 1, SKIN[0])
    c.rect(4, 1, 8, 2, BRASS[2])
    c.rect(5, 0, 6, 1, BRASS[3])
    c.px(6, 1, BRASS[4])
    if direction == 'down':
        c.rect(5, 3, 2, 2, GLASS[1])
        c.rect(9, 3, 2, 2, GLASS[1])
        c.rect(7, 3, 2, 1, IRON[1])
        c.px(5, 3, GLASS[2])
        c.px(9, 3, GLASS[2])
    elif direction == 'up':
        c.rect(5, 2, 6, 4, WOOD[1])   # hair under the helmet
        c.rect(4, 3, 8, 1, IRON[1])   # goggle strap
    else:
        front = 10 if direction == 'right' else 5
        back = 5 if direction == 'right' else 10
        c.rect(front - (1 if direction == 'right' else 0), 3, 2, 2, GLASS[1])
        c.px(front - (1 if direction == 'right' else 0), 3, GLASS[2])
        c.rect(back, 2, 1, 4, WOOD[1])

    c.outline(OUTLINE)
    return c


DIRECTIONS = ('down', 'up', 'left', 'right')
