"""Configure the four video fixtures on a local PAKAR demo API."""

import argparse
import hashlib
import json
import os
from pathlib import Path
from urllib.error import HTTPError
from urllib.parse import urlparse
from urllib.request import Request, urlopen
from uuid import uuid4

import cv2

MANIFEST = Path(__file__).resolve().parents[1] / "docs/ai/four-zone-demo.json"


def request(base, method, path, token=None, data=None, content_type="application/json"):
    headers = {"Content-Type": content_type}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if isinstance(data, dict):
        data = json.dumps(data).encode("utf-8")
    try:
        with urlopen(Request(f"{base}{path}", data, headers, method=method), timeout=25) as response:
            body = response.read()
            return json.loads(body) if body and "application/json" in response.headers["Content-Type"] else body
    except HTTPError as error:
        raise RuntimeError(f"{method} {path} returned HTTP {error.code}: {error.read().decode(errors='replace')}") from error


def reference_jpeg(video):
    capture = cv2.VideoCapture(str(video))
    try:
        read, frame = capture.read()
        if not read:
            raise RuntimeError(f"Cannot read the first frame of {video.name}")
        encoded, jpeg = cv2.imencode(".jpg", frame)
        if not encoded:
            raise RuntimeError(f"Cannot encode the reference frame for {video.name}")
        return jpeg.tobytes()
    finally:
        capture.release()


def multipart_jpeg(jpeg):
    boundary = uuid4().hex
    prefix = (f"--{boundary}\r\n"
              'Content-Disposition: form-data; name="file"; filename="reference.jpg"\r\n'
              "Content-Type: image/jpeg\r\n\r\n").encode()
    return prefix + jpeg + f"\r\n--{boundary}--\r\n".encode(), f"multipart/form-data; boundary={boundary}"


def same_polygons(left, right):
    left_points = {entry["spotId"]: entry["points"] for entry in left}
    right_points = {entry["spotId"]: entry["points"] for entry in right}
    # The API normalizes to six decimal places using .NET midpoint rounding.
    return left_points.keys() == right_points.keys() and all(
        len(points) == len(right_points[spot_id]) and all(
            abs(lx - rx) <= 0.000001 and abs(ly - ry) <= 0.000001
            for (lx, ly), (rx, ry) in zip(points, right_points[spot_id])
        )
        for spot_id, points in left_points.items()
    )


def configure(base, video_dir, email, password):
    base = base.rstrip("/")
    url = urlparse(base)
    if url.scheme not in {"http", "https"} or url.hostname not in {"127.0.0.1", "localhost", "::1"} or url.path:
        raise ValueError("Use the base address of a local PAKAR demo API.")
    fixtures = json.loads(MANIFEST.read_text(encoding="utf-8"))
    media = {}
    for fixture in fixtures:
        video = video_dir / fixture["videoFile"]
        with video.open("rb") as stream:
            if hashlib.file_digest(stream, "sha256").hexdigest() != fixture["sha256"]:
                raise ValueError(f"Video checksum mismatch: {video.name}")
        if fixture["capacity"] != len(fixture["slots"]):
            raise ValueError(f"Inconsistent polygon fixture: {fixture['zone']}")
        media[fixture["zone"]] = reference_jpeg(video)

    token = request(base, "POST", "/api/auth/login", data={"email": email, "password": password})["token"]
    cameras = request(base, "GET", "/api/cameras", token)
    zones = request(base, "GET", "/api/zones", token)
    fixtures_by_zone = {entry["zone"]: entry for entry in fixtures}
    if {zone["name"] for zone in zones} != fixtures_by_zone.keys():
        raise ValueError("The active zones must match the four-zone demo seed.")

    prepared = []
    for zone in zones:
        fixture = fixtures_by_zone[zone["name"]]
        matches = [camera for camera in cameras if camera["zoneId"] == zone["id"]]
        if len(matches) != 1 or matches[0]["name"] != f"Video {zone['name']}":
            raise ValueError(f"Expected one seeded video source for {zone['name']}")
        camera = matches[0]
        url = f"/api/cameras/{camera['id']}"
        current = request(base, "GET", f"{url}/rois", token)
        ids = {spot["name"]: spot["id"] for spot in current["spots"]}
        slots = [{"spotId": ids[entry["spotNumber"]], "points": entry["points"]} for entry in fixture["slots"]]
        if camera["videoFile"] not in ("", fixture["videoFile"]):
            raise ValueError(f"A different video is configured for {zone['name']}")
        if current["slots"] and not same_polygons(current["slots"], slots):
            raise ValueError(f"Different polygons are already configured for {zone['name']}")
        if camera["hasReferenceFrame"] and camera["videoFile"] != fixture["videoFile"]:
            raise ValueError(f"A reference image is already configured for {zone['name']}")
        prepared.append((zone, fixture, camera, url, slots, bool(current["slots"])))

    for zone, fixture, camera, url, slots, has_polygons in prepared:
        if not camera["videoFile"]:
            request(base, "PUT", url, token,
                    {"name": camera["name"], "zoneId": zone["id"], "videoFile": fixture["videoFile"]})
        if not camera["hasReferenceFrame"]:
            body, content_type = multipart_jpeg(media[zone["name"]])
            request(base, "PUT", f"{url}/reference-frame", token, body, content_type)
        if not has_polygons:
            request(base, "PUT", f"{url}/rois", token, {"slots": slots})
        saved = request(base, "GET", f"{url}/rois", token)
        if not same_polygons(saved["slots"], slots):
            raise RuntimeError(f"The API did not retain the expected polygons for {zone['name']}")
        print(f"{zone['name']}: {fixture['videoFile']}, {len(slots)}/{len(saved['spots'])} slots calibrated")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--api", default="http://127.0.0.1:5249")
    parser.add_argument("--videos", required=True, type=Path)
    args = parser.parse_args()
    configure(args.api, args.videos, os.environ["PAKAR_DEMO_ADMIN_EMAIL"], os.environ["PAKAR_DEMO_ADMIN_PASSWORD"])
