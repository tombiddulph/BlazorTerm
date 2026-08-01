#!/usr/bin/env python3
"""Generate a privacy-filtered GeoJSON activity map from the Strava SQLite database."""

import argparse
import json
import math
import sqlite3
from collections import Counter
from pathlib import Path

EARTH_RADIUS_METERS = 6_378_137
GEOMETRY_PRECISION_METERS = 100
PRIVATE_ROUTE_END_METERS = 1_500
SIMPLIFICATION_TOLERANCE_METERS = 50


def decode_polyline(value):
    points = []
    index = latitude = longitude = 0

    while index < len(value):
        changes = []
        for _ in range(2):
            result = shift = 0
            while True:
                byte = ord(value[index]) - 63
                index += 1
                result |= (byte & 0x1F) << shift
                shift += 5
                if byte < 0x20:
                    break
            changes.append(~(result >> 1) if result & 1 else result >> 1)

        latitude += changes[0]
        longitude += changes[1]
        points.append((latitude / 100_000, longitude / 100_000))

    return points


def distance_between(first, second):
    first_latitude, second_latitude = map(math.radians, (first[0], second[0]))
    latitude_delta = second_latitude - first_latitude
    longitude_delta = math.radians(second[1] - first[1])
    haversine = (
        math.sin(latitude_delta / 2) ** 2
        + math.cos(first_latitude) * math.cos(second_latitude) * math.sin(longitude_delta / 2) ** 2
    )
    return EARTH_RADIUS_METERS * 2 * math.asin(min(1, math.sqrt(haversine)))


def snap_point(point):
    latitude, longitude = point
    latitude = max(-85.0511, min(85.0511, latitude))
    x = EARTH_RADIUS_METERS * math.radians(longitude)
    y = EARTH_RADIUS_METERS * math.log(math.tan(math.pi / 4 + math.radians(latitude) / 2))
    x = round(x / GEOMETRY_PRECISION_METERS) * GEOMETRY_PRECISION_METERS
    y = round(y / GEOMETRY_PRECISION_METERS) * GEOMETRY_PRECISION_METERS
    longitude = math.degrees(x / EARTH_RADIUS_METERS)
    latitude = math.degrees(2 * math.atan(math.exp(y / EARTH_RADIUS_METERS)) - math.pi / 2)
    return [round(longitude, 5), round(latitude, 5)]


def project_point(point):
    longitude, latitude = point
    latitude = max(-85.0511, min(85.0511, latitude))
    return (
        EARTH_RADIUS_METERS * math.radians(longitude),
        EARTH_RADIUS_METERS * math.log(math.tan(math.pi / 4 + math.radians(latitude) / 2)),
    )


def simplify_line(points):
    if len(points) <= 2:
        return points

    projected = [project_point(point) for point in points]
    keep = {0, len(points) - 1}
    pending = [(0, len(points) - 1)]
    tolerance_squared = SIMPLIFICATION_TOLERANCE_METERS ** 2

    while pending:
        start, end = pending.pop()
        start_x, start_y = projected[start]
        end_x, end_y = projected[end]
        segment_x = end_x - start_x
        segment_y = end_y - start_y
        segment_squared = segment_x ** 2 + segment_y ** 2
        furthest_index = -1
        furthest_distance = 0

        for index in range(start + 1, end):
            point_x, point_y = projected[index]
            if segment_squared == 0:
                distance_squared = (point_x - start_x) ** 2 + (point_y - start_y) ** 2
            else:
                position = max(0, min(1, ((point_x - start_x) * segment_x + (point_y - start_y) * segment_y) / segment_squared))
                nearest_x = start_x + position * segment_x
                nearest_y = start_y + position * segment_y
                distance_squared = (point_x - nearest_x) ** 2 + (point_y - nearest_y) ** 2

            if distance_squared > furthest_distance:
                furthest_index = index
                furthest_distance = distance_squared

        if furthest_distance > tolerance_squared:
            keep.add(furthest_index)
            pending.append((start, furthest_index))
            pending.append((furthest_index, end))

    return [point for index, point in enumerate(points) if index in keep]


def activity_line(polyline):
    try:
        points = decode_polyline(polyline)
    except (IndexError, ValueError):
        return [], 0

    distances = [0.0]
    for first, second in zip(points, points[1:]):
        distances.append(distances[-1] + distance_between(first, second))

    total_distance = distances[-1]
    line = [
        snap_point(point)
        for point, distance in zip(points, distances)
        if distance >= PRIVATE_ROUTE_END_METERS and total_distance - distance >= PRIVATE_ROUTE_END_METERS
    ]
    line = [point for index, point in enumerate(line) if index == 0 or point != line[index - 1]]
    return simplify_line(line), max(0, total_distance - (PRIVATE_ROUTE_END_METERS * 2))


def generate(database_path):
    database_uri = Path(database_path).resolve().as_uri() + "?mode=ro"
    lines = []

    with sqlite3.connect(database_uri, uri=True) as connection:
        rows = connection.execute(
            """SELECT polyline, sportType, elevation FROM Activity
               WHERE polyline IS NOT NULL
                 AND length(polyline) > 0
                 AND coalesce(markedForDeletion, 0) = 0
               ORDER BY activityId"""
        )
        for polyline, sport_type, elevation in rows:
            line, visible_distance = activity_line(polyline)
            if len(line) >= 2:
                lines.append((line, visible_distance, sport_type, max(0, elevation or 0)))

    # Spatial ordering avoids leaking the source activity chronology through array position.
    lines.sort(key=lambda item: (item[0][0], item[0][-1], len(item[0])))
    sports = Counter(sport_type for _, _, sport_type, _ in lines)

    return {
        "type": "FeatureCollection",
        "activityCount": len(lines),
        "distanceKilometers": round(sum(distance for _, distance, _, _ in lines) / 1_000),
        "elevationMeters": round(sum(elevation for _, _, _, elevation in lines)),
        "geometryPrecisionMeters": GEOMETRY_PRECISION_METERS,
        "privateRouteEndMeters": PRIVATE_ROUTE_END_METERS,
        "sports": [
            {"name": name, "count": count}
            for name, count in sorted(sports.items(), key=lambda item: (-item[1], item[0]))
        ],
        "features": [
            {
                "type": "Feature",
                "properties": {},
                "geometry": {"type": "LineString", "coordinates": line},
            }
            for line, _, _, _ in lines
        ],
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("database", type=Path, help="Path to the source Strava SQLite database")
    parser.add_argument("output", type=Path, help="Path for the generated GeoJSON file")
    arguments = parser.parse_args()
    data = generate(arguments.database)
    arguments.output.write_text(json.dumps(data, separators=(",", ":")), encoding="utf-8")
    print(f"Published {len(data['features'])} generalized activity lines to {arguments.output}")


if __name__ == "__main__":
    main()
