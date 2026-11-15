"""
Tool gambar ROI (jalankan LOKAL, bukan di Colab — butuh jendela OpenCV).

  python roi_tool.py --image frame.jpg --camera-id 1 --area-id 1 --out roi_config.json
  (ambil frame dari CCTV: ffmpeg -i rtsp://... -frames:v 1 frame.jpg)

Kontrol:
  klik kiri : tambah titik polygon        z : hapus titik terakhir
  Enter     : selesai 1 slot (lalu isi spot_id & spot_number di terminal)
  s         : simpan & keluar              q : keluar tanpa simpan
"""
import argparse

import cv2
import numpy as np

from roi_mapping import CameraROIConfig, SpotROI


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--image", required=True)
    ap.add_argument("--camera-id", type=int, required=True)
    ap.add_argument("--area-id", type=int, required=True)
    ap.add_argument("--out", default="roi_config.json")
    a = ap.parse_args()

    img = cv2.imread(a.image)
    if img is None:
        raise SystemExit(f"Gagal baca gambar: {a.image}")
    h, w = img.shape[:2]
    cfg = CameraROIConfig(a.camera_id, a.area_id, [], ref_width=w, ref_height=h)
    current: list[tuple[int, int]] = []

    def on_mouse(event, x, y, *_):
        if event == cv2.EVENT_LBUTTONDOWN:
            current.append((x, y))

    cv2.namedWindow("ROI tool")
    cv2.setMouseCallback("ROI tool", on_mouse)

    while True:
        view = img.copy()
        for s in cfg.spots:
            pts = s.to_pixels(w, h)
            cv2.polylines(view, [pts], True, (0, 200, 0), 2)
            cv2.putText(view, s.spot_number, tuple(pts[0]), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 200, 0), 2)
        if current:
            cv2.polylines(view, [np.array(current, np.int32)], False, (0, 255, 255), 2)
            for p in current:
                cv2.circle(view, p, 4, (0, 255, 255), -1)
        cv2.imshow("ROI tool", view)
        k = cv2.waitKey(30) & 0xFF

        if k == ord("z") and current:
            current.pop()
        elif k == 13 and len(current) >= 3:  # Enter
            sid = int(input("spot_id (sesuai ParkingSpot.spot_id di DB): "))
            num = input("spot_number (mis. A-01): ").strip()
            poly = np.array(current, np.float32) / np.array([w, h], np.float32)
            cfg.spots.append(SpotROI(sid, num, poly))
            current.clear()
        elif k == ord("s"):
            cfg.validate()
            cfg.save(a.out)
            print(f"Tersimpan: {a.out} ({len(cfg.spots)} slot)")
            break
        elif k == ord("q"):
            break
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
