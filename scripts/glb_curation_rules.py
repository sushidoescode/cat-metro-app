#!/usr/bin/env python3
"""Pure geometric predicates for the two frozen GLB-CURATION operations."""

from __future__ import annotations

import math
from collections.abc import Mapping, Sequence


LOAF_CUT_HEIGHT_RATIO = 0.08
LOAF_SELECTED_FOOTPRINT_MINIMUM = 0.95
LOAF_RETAINED_FOOTPRINT_MAXIMUM = 0.80
WAVE_THIN_SPAN_RATIO = 0.07
WAVE_MIN_Y_LOCATION_RATIO = 0.01


class CurationRuleError(ValueError):
    """Raised when geometry does not satisfy the frozen curation contract."""


def _finite_number(value: object, label: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise CurationRuleError(f"{label} must be numeric")
    rendered = float(value)
    if not math.isfinite(rendered):
        raise CurationRuleError(f"{label} must be finite")
    return rendered


def _point(value: object, label: str) -> tuple[float, float, float]:
    if (
        not isinstance(value, Sequence)
        or isinstance(value, (str, bytes, bytearray))
        or len(value) != 3
    ):
        raise CurationRuleError(f"{label} must be a three-value point")
    return tuple(
        _finite_number(component, f"{label}[{index}]")
        for index, component in enumerate(value)
    )


def _bounds(
    value: object, label: str
) -> tuple[tuple[float, float, float], tuple[float, float, float]]:
    if not isinstance(value, Mapping):
        raise CurationRuleError(f"{label} must be an object")
    minimum = _point(value.get("minimum"), f"{label}.minimum")
    maximum = _point(value.get("maximum"), f"{label}.maximum")
    if any(lower > upper for lower, upper in zip(minimum, maximum)):
        raise CurationRuleError(f"{label} has inverted bounds")
    return minimum, maximum


def _spans(
    minimum: tuple[float, float, float],
    maximum: tuple[float, float, float],
) -> tuple[float, float, float]:
    return tuple(upper - lower for lower, upper in zip(minimum, maximum))


def loaf_cutoff(minimum_y: float, maximum_y: float) -> float:
    """Return the strict upper boundary of the ruled min-Y plinth slab."""

    lower = _finite_number(minimum_y, "minimum_y")
    upper = _finite_number(maximum_y, "maximum_y")
    if upper <= lower:
        raise CurationRuleError("loaf Y height must be positive")
    return lower + LOAF_CUT_HEIGHT_RATIO * (upper - lower)


def validate_loaf_footprints(
    *,
    selected_width_ratio: float,
    selected_depth_ratio: float,
    retained_width_ratio: float,
    retained_depth_ratio: float,
) -> None:
    """Require the frozen disc-wide selection and contracted cat footprint."""

    values = {
        "selected_width_ratio": selected_width_ratio,
        "selected_depth_ratio": selected_depth_ratio,
        "retained_width_ratio": retained_width_ratio,
        "retained_depth_ratio": retained_depth_ratio,
    }
    checked = {
        name: _finite_number(value, name) for name, value in values.items()
    }
    if any(value < 0.0 for value in checked.values()):
        raise CurationRuleError("loaf footprint ratios must be non-negative")
    if (
        checked["selected_width_ratio"]
        < LOAF_SELECTED_FOOTPRINT_MINIMUM
        or checked["selected_depth_ratio"]
        < LOAF_SELECTED_FOOTPRINT_MINIMUM
    ):
        raise CurationRuleError("loaf min-Y slab does not span the display disc")
    if (
        checked["retained_width_ratio"]
        >= LOAF_RETAINED_FOOTPRINT_MAXIMUM
        or checked["retained_depth_ratio"]
        >= LOAF_RETAINED_FOOTPRINT_MAXIMUM
    ):
        raise CurationRuleError("loaf retained footprint does not contract enough")


def select_wave_fragments(
    components: object,
    full_bounds: object,
) -> list[int]:
    """Return components satisfying both frozen wave-fragment predicates."""

    if (
        not isinstance(components, Sequence)
        or isinstance(components, (str, bytes, bytearray))
    ):
        raise CurationRuleError("wave components must be a list")
    full_minimum, full_maximum = _bounds(full_bounds, "full_bounds")
    full_spans = _spans(full_minimum, full_maximum)
    full_max_span = max(full_spans)
    full_y_height = full_spans[1]
    if full_max_span <= 0.0 or full_y_height <= 0.0:
        raise CurationRuleError("wave full bounds must have positive span")
    location_limit = full_minimum[1] + WAVE_MIN_Y_LOCATION_RATIO * full_y_height

    selected: list[int] = []
    for index, component in enumerate(components):
        if not isinstance(component, Mapping):
            raise CurationRuleError(f"components[{index}] must be an object")
        triangles = component.get("triangles")
        if isinstance(triangles, bool) or not isinstance(triangles, int) or triangles < 1:
            raise CurationRuleError(
                f"components[{index}].triangles must be a positive integer"
            )
        minimum, maximum = _bounds(component, f"components[{index}]")
        spans = _spans(minimum, maximum)
        thin_ratio = min(spans) / full_max_span
        if (
            thin_ratio < WAVE_THIN_SPAN_RATIO
            and minimum[1] <= location_limit
        ):
            selected.append(index)
    return selected
