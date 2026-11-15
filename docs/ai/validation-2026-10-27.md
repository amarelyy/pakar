# Pengujian lokal AI PAKAR - 27 Oktober 2026

Pengujian komponen Python berhasil. Penerimaan penuh issue #13 dan #15 masih
memerlukan kamera/video nyata dan integrasi backend. Tidak ada pengujian akurasi
model CCTV maupun latensi kamera sampai UI dalam hasil ini.

## Lingkup dan keputusan model

- Checkout: `https://github.com/amarelyy/pakar`, basis `a19fed6`.
- Branch lokal: `codex/ai-local-validation`; perubahan belum di-commit/push.
- Runtime Python disiapkan di `edge/python-agent/`, meneruskan kode lokal Nabil
  dari `Downloads/pakar-ai-realtime/pakar-ai-realtime/ai`.
- Baseline yang benar-benar dimuat: `yolov8n.pt`, COCO 80 kelas; runtime memilih
  `car`, `motorcycle`, `bus`, `truck` dan memakai ROI polygon per slot.
- `best.pt` dengan label `empty/occupied` yang disebut laporan HW3 belum tersedia
  pada sumber yang diperiksa. mAP50 98,5% dalam laporan tidak direproduksi dan
  tidak berlaku untuk baseline COCO ini.
- Model slot-label sekarang ditolak secara eksplisit; tidak ada konversi diam-diam
  menjadi deteksi kendaraan. Lihat `edge/python-agent/MODEL_CARD.md`.

## Perubahan yang disiapkan

1. Memindahkan runtime, ROI tool, konfigurasi contoh, dependensi, model manifest,
   serta panduan menjalankan ke struktur repositori.
2. Memperbaiki replay file: sampling berdasarkan waktu video dan berhenti saat
   EOF. Prototipe lama melewati frame berdasarkan kecepatan komputer dan
   melakukan reconnect terus-menerus ketika file berakhir.
3. Menambahkan penolakan frame hitam/kosong dan frame yang identik terlalu lama;
   frame invalid tidak menghasilkan occupancy update/heartbeat. Scene statis
   yang sah tetap dapat menyerupai stream beku, sehingga validasi kamera nyata
   atau metadata timestamp masih diperlukan.
4. Mempertahankan last-known occupancy ketika stream terganggu, membatalkan
   kandidat debounce yang belum stabil, lalu mengirim snapshot setelah reconnect.
5. Mengganti default available debounce dari 6 menjadi 3 detik; occupied tetap
   2 detik. Ini menyediakan anggaran waktu untuk FR 5 detik, bukan bukti latensi
   end-to-end terpenuhi. Parameter tetap bisa diubah setelah evaluasi CCTV.
6. Menambahkan fail-fast model/ID/ROI validation dan pengiriman ulang snapshot
   terbaru setelah kegagalan HTTP agar transisi lama tidak diputar ulang.
7. Mengabaikan binary weights/video, cache, dan keluaran pengujian melalui Git.

## Input dan lingkungan

Video asli lokal: `sample_parkir.mp4`, 640x480, 15 FPS, 450 frame, 30 detik.
Ini **animasi dua slot dengan kotak merah berlabel CAR**, bukan rekaman kendaraan.
ROI fixture memakai camera ExternalId 1, area ExternalId 1, spot ExternalId 1/2
(`H-01`, `H-02`). Kapasitas fixture dua slot bukan kapasitas zona database yang
berisi sepuluh slot; cakupan kamera produksi belum dikalibrasi.

Raw fixture H-01: kosong 0-4 detik; kotak muncul 4-8, 12-16, 20-24, dan 28-30
detik. H-02 selalu kosong. Jadwal ini ditetapkan sebagai referensi test fixture.

Python 3.13 pada workstation pengguna sudah memiliki paket yang diperlukan;
tidak ada instalasi paket global yang dilakukan. Versi paket dan SHA-256 semua
input dicatat dalam [validation-evidence.json](validation-evidence.json).

## Hasil

| Pemeriksaan | Hasil | Arti dan batasan |
|---|---|---|
| Pytest | 14 passed | Kontrak model, validasi ROI/ID, debounce, oklusi, confidence/class filter, resolusi, 1 deteksi/1 slot, frame health, EOF/media time, reconnect terkontrol, HTTP retry dan auth rejection. |
| YOLO asli | 60 sampel; 0 deteksi kendaraan | Smoke test runtime. Kotak merah bukan mobil; hasil ini bukan ukuran akurasi atau kegagalan deteksi CCTV. |
| Waktu inferensi YOLO CPU | Rata-rata 94,19 ms, p95 45,15 ms | Rata-rata memasukkan warm-up/outlier awal; p95 dapat lebih rendah daripada rata-rata. Tidak membuktikan semua frame <100 ms atau latensi UI <5 detik. |
| ROI/debounce dengan rectangle stub | Semua enam perubahan status sesuai jadwal | Menguji logika dengan input terkontrol, bukan neural inference. Stub hanya berada pada kode pengujian. |
| HTTP loopback | 7 payload diterima | Initial snapshot dan enam perubahan status. Endpoint/header/ID diperiksa; penerima ini bukan ASP.NET/DB/SignalR. |
| Video hitam turunan | 8/8 sampel ditolak; 0 payload | Tidak ada heartbeat yang membuat kamera rusak seolah aktif. |
| Video beku turunan | 4 valid, 4 frozen; hanya initial payload | Ambang repeat dipercepat menjadi 1 detik untuk tes; default runtime 30 detik. |
| EOF video | Berhenti normal | Tidak mencoba reconnect file yang selesai. |
| Reconnect live | Lolos menggunakan capture stub | Backoff/pemulihan dan batas retry diuji; bukan koneksi RTSP nyata. |

Perubahan stabil H-01 yang diharapkan dan diperoleh: occupied pada 6, 14, 22
detik; available pada 11, 19, 27 detik. Initial state available pada 0 detik.
Kemunculan kotak terakhir pada 28 detik belum stabil sebelum video berakhir,
sehingga tidak ada event occupied keempat. H-02 selalu available.

Keluaran lokal lengkap ada pada `output/ai-validation/`: events JSONL,
summary JSON, `pytest.xml`, overlay YOLO, overlay fixture, video hitam/beku,
dan `comparison.png`. Folder ini sengaja tidak dilacak Git.

## Status pekerjaan

| Pekerjaan | Status |
|---|---|
| Prioritas 1: selaraskan model/pipeline | Baseline kompatibel dipilih dan dimuat; keputusan tentang model custom menunggu artifact serta evaluasi nyata. |
| Prioritas 2: masukkan kode ke repo | Kode, tes, dan dokumentasi siap ditinjau pada branch lokal; belum commit/push ke GitHub. |
| #13: ingestion dan camera timeout | Komponen file replay, health gate, dan retry diuji. RTSP asli, black/freeze pada kamera nyata, dan watchdog backend 120 detik belum diuji. |
| #15: ROI dan occupancy | Geometri, counting, debounce, event, serta ID fixture diuji. Akurasi pada video kendaraan nyata dan cakupan semua slot belum diverifikasi. |
| #20: integrasi real-time | Kontrak HTTP diuji dengan receiver lokal. Persistensi ASP.NET, SignalR, dan UI belum diuji. |

Full .NET solution build/deployment tidak dijalankan dalam pengujian Python ini.
Kode backend produksi tidak diubah. Watchdog backend yang sudah ada memeriksa
setiap 15 detik dengan timeout 120 detik; kamera dapat ditandai setelah melewati
120 detik pada pemeriksaan berikutnya. Kamera yang belum pernah mengirim data
juga perlu menjadi kasus pengujian tersendiri.

## Reproduksi

Jalankan dari root checkout `pakar`, menggunakan Python yang memiliki dependensi:

```powershell
py -X utf8 -m pytest tests/ai -q --junitxml=output/ai-validation/pytest.xml
py -X utf8 tests/ai/validate_local.py `
  --video edge/python-agent/assets/sample_parkir.mp4 `
  --model edge/python-agent/assets/yolov8n.pt `
  --out output/ai-validation
```

Reproduksi pada clone lain memerlukan input binary yang sama. Unit tests
membuat clip kecil sendiri dan dapat berjalan tanpa weights/video lokal.

## Lanjutan yang diperlukan

1. Dapatkan video CCTV/kamera nyata beserta ground truth; lokasi source sintetis
   tidak cukup untuk mengukur akurasi kendaraan.
2. Dapatkan artifact `best.pt` dan daftar kelasnya bila tim tetap ingin memakai
   hasil training custom; bandingkan pendekatan tanpa mencampur metrik.
3. Kalibrasi seluruh ROI dan samakan ExternalId dengan database aktual.
4. Uji RTSP putus/pulih, black/freeze nyata, waktu tunggu kamera, serta risiko
   scene statis; jangan hanya mengandalkan identitas pixel untuk produksi.
5. Bersama backend/integrasi, uji watchdog 120 detik, persistensi, reconnect,
   SignalR/UI, dan ukur latensi kamera sampai tampilan.
6. Tinjau diff sebelum commit/push; jangan menutup #13/#15 hanya dari fixture.
