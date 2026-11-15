# Pilihan demo: best.pt, ROI v2, confidence0,10

Dipilih **0,10** dibanding0,15 untuk clip praktikum ini. Dengan ROI v2 dan
aturan lainnya tetap,0,10 menghasilkan170 cocok/1beda/3unknown dari174 label
(coverage98,28%), dibanding168/1/5 (97,13%) pada0,15. Unknown VIDEO05 pada
seluruh290 sampel turun40 menjadi12. Kedua pilihan tetap memiliki satu beda
VIDEO04 di65s akibat debounce; transisi available terjadi66,5s.

Tambahan jawaban0,10 terhadap0,15 berjumlah31 slot-sample di seluruh clip:
28 pada05,1 pada04,2 pada01. Banyak box05 memiliki center sekitar589–598,
507–511 dan IoU sekitar0,54–0,67, sesuai petak05. Contoh45s empty0,1165
terlihat benar di petak05. Ada juga box bergeser dekat pejalan kaki di awal
clip, dan raw empty01 confidence0,1075 pada134,5s yang tertahan debounce.
Karena itu pilihan ini memakai coverage **bersama** audit lokasi, raw state,
dan debounce, tanpa menganggap setiap box atau state sempurna.

## Perubahan runtime

- `YOLODetector` tanpa override memakai0,10 bila kelas model empty/occupied;
  model kendaraan tetap memakai inference floor0,25.
- `pipeline.py` dan `SlotOccupancyAdapter` memakai assignment default0,10 untuk
  slot. Kendaraan tetap0,35; IoU0,1, margin konflik0,1, debounce2/3s tetap.
- `--conf` tetap override floor inferensi; `--min-conf` override assignment.
  Untuk reproduksi lama gunakan keduanya0,25 dan ROI lama.
- Low-level `SlotLabelEngine` tetap default0,25 agar runner historis tetap
  dapat direproduksi. Pipeline selalu memberikan threshold efektif eksplisit.
- README agent dan ASSETS memilih `real-video-demo-v2.json`. ROI lama utuh.
  CLI tetap meminta `--roi`; tidak ada kalibrasi otomatis untuk kamera lain.
- Launcher `demo.py` lokal hanya mengganti path ROI ke v2. Detector yang sudah
  dipakai launcher kini otomatis memilih0,10. Launcher tetap lokal/tidak masuk
  commit AI bersama patch backend/UI yang sebelumnya terpisah.
- Summary runtime kini mencatat `inference_conf` dan `min_conf` agar dua filter
  itu dapat diperiksa secara terpisah.

## Verifikasi

**28 tes lulus** setelah perubahan:25 sebelumnya dan3 kasus model/override
confidence. Tes pipeline slot kini memakai confidence0,12 sehingga membuktikan
box di bawah ambang lama benar-benar diterima, dengan snapshot parsial tetap.

`verify_selected_confidence.py` menjalankan pipeline290 frame memakai deteksi
nyata cached yang disaring0,10. State dan unknown cocok persis dengan hasil
eksperimen0,10 sebelumnya. Ini memeriksa jalur CLI/engine/validator/journal
tanpa mengulang inferensi seluruh clip. Empat frame juga diinferensi ulang
best.pt memakai default aktual0,10; seluruh box cocok dengan cache>=0,10.
Tidak ada HTTP/backend/UI baru yang diuji.

[Bukti pilihan dan verifikasi](selected-confidence-evidence.json),
[eksperimen confidence](low-confidence-2026-10-28.md),
[diagnosis ROI-only](roi-review-2026-10-28.md).

```powershell
py -X utf8 edge/python-agent/pipeline.py --source edge/python-agent/assets/parking-demo.mp4 --roi edge/python-agent/configs/real-video-demo-v2.json --replay-speed 2 --events output/ai-validation/selected/events.jsonl --summary output/ai-validation/selected/summary.json --annotated-video output/ai-validation/selected/overlay.mp4
```

Ini kalibrasi pada clip yang sama dengan evaluasi, label visual Codex belum
review manusia, belum held-out. Hasil tidak membuktikan threshold0,10 terbaik
untuk semua kamera, tidak membuktikan keunggulan atas COCO, dan tidak mengubah
model. Evaluasi clip lain layak menjadi pekerjaan lanjutan, bukan syarat
tambahan infrastruktur praktikum. Unknown tetap diomit dari HTTP dan UI masih
last-known. Weights/video/output tidak masuk Git, Drive masih placeholder.
