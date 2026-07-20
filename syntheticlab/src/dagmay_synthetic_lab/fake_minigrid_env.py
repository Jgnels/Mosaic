from __future__ import annotations


class FakeMiniGridEnv:
    """Small Gymnasium-shaped environment for adapter contract tests.

    It is intentionally not presented as an external validation result.
    """

    def __init__(self):
        self.width = 5
        self.height = 5
        self.goal = (3, 3)
        self.max_steps = 80
        self.position = (1, 1)
        self.direction = 0
        self.step_count = 0
        self.closed = False

    def _cell(self, x, y):
        if (
            x <= 0
            or y <= 0
            or x >= self.width - 1
            or y >= self.height - 1
        ):
            return [2, 5, 0]  # wall-like opaque encoding
        if (x, y) == self.goal:
            return [8, 1, 0]  # goal-like opaque encoding
        if (x, y) == self.position:
            return [10, 0, 0]  # agent-like opaque encoding
        return [1, 0, 0]  # empty-like opaque encoding

    def _observation(self):
        px, py = self.position
        image = []
        for dy in (-1, 0, 1):
            row = []
            for dx in (-1, 0, 1):
                row.append(
                    self._cell(
                        px + dx,
                        py + dy,
                    )
                )
            image.append(row)

        return {
            "direction": self.direction,
            "image": image,
            "mission": (
                "get to the green goal square"
            ),
        }

    def reset(self, seed=None):
        seed = 0 if seed is None else int(seed)
        self.position = (
            1 + (seed % 2),
            1,
        )
        self.direction = seed % 4
        self.step_count = 0
        return self._observation(), {
            "seed": seed
        }

    def step(self, action):
        action = int(action)
        self.step_count += 1

        if action == 0:
            self.direction = (
                self.direction - 1
            ) % 4
        elif action == 1:
            self.direction = (
                self.direction + 1
            ) % 4
        elif action == 2:
            dx_dy = {
                0: (1, 0),
                1: (0, 1),
                2: (-1, 0),
                3: (0, -1),
            }
            dx, dy = dx_dy[
                self.direction
            ]
            nx = self.position[0] + dx
            ny = self.position[1] + dy
            if (
                0 < nx < self.width - 1
                and 0 < ny < self.height - 1
            ):
                self.position = (nx, ny)

        terminated = (
            self.position == self.goal
        )
        truncated = (
            self.step_count >= self.max_steps
            and not terminated
        )
        reward = (
            1.0
            if terminated
            else 0.0
        )

        return (
            self._observation(),
            reward,
            terminated,
            truncated,
            {},
        )

    def close(self):
        self.closed = True
