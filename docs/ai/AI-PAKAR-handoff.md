# Penyerahan bagian AI — branch AI-PAKAR

Default terbaru adalah **best.pt** (model custom empty/occupied), dengan adapter
slot dan HTTP snapshot parsial. COCO tetap pilihan pembanding. Binary model
dibagikan melalui Google Drive, bukan GitHub. Lihat
[laporan switch default](best-default-2026-10-28.md).

Branch `AI-PAKAR` dibuat dari `codex/ai-local-validation` untuk penyerahan
pekerjaan AI Nabil. Branch sudah dipublikasikan. Commit AI inti berisi kode
Python, tes, konfigurasi, dokumentasi dan aturan ignore. Perubahan backend/UI
serta launcher demo dipertahankan secara lokal di luar commit AI ini.

## Yang disiapkan sebagai bagian AI

- `edge/python-agent/`: deteksi YOLO, ROI polygon, debounce, pembacaan video/
  stream, validasi frame, HTTP client, konfigurasi, manifest dan dependensi.
- `tests/ai/conftest.py`, `test_agent.py`, `test_evaluate.py`, `validate_local.py`:
  tes dan evaluasi komponen Python. `validate_backend.py` serta
  `validate_video_backend.py` adalah pengujian integrasi pendukung.
- `docs/ai/`: konteks, protokol evaluasi, anotasi sampel, laporan dan bukti.
- `.gitignore`: binary input, cache dan output pengujian tetap tidak dilacak.

**Tautan Google Drive diisi pada
[edge/python-agent/ASSETS.md](../../edge/python-agent/ASSETS.md).**

Tidak ada binary yang di-upload ke GitHub/Drive secara otomatis. Model `.pt`
dan clip video dibagikan Nabil secara terpisah melalui tautan tersebut.

## Pemisahan sebelum push

Perubahan `src/Pakar.Api/`, `src/Pakar.Infrastructure/`, `src/Pakar.Web/`, dan
`demo.py` mendukung aplikasi demonstrasi lama dan tidak termasuk commit AI
inti. Jangan menerapkannya langsung ke branch533008 yang sudah memiliki backend
dan migrasi SQL Server lebih baru. Tidak ada file lama yang dibuang atau di-reset.

Kontrak saat ini: Python mengirim status ke `/api/ingest/detections` dengan
`X-Api-Key`. Ini belum jalur webcam browser -> FrameHub -> C#/ONNX yang diminta
TODO tim. Status yang tepat: implementasi AI Python berfungsi untuk praktikum,
penyesuaian arsitektur tim masih perlu kesepakatan.

## Bukti singkat

- Video nyata stabil145s; enam ROI khusus demo.
- Evaluasi visual Codex pada29 frame:172/174 slot-sample cocok, belum review
  manusia dan bukan klaim akurasi umum.
- 18 pytest lulus; alur AI sampai aplikasi telah diuji pada database disposable.
- Detail: [laporan](real-video-and-backend-2026-10-28.md) dan
  [panduan demo](demo-praktikum.md).
