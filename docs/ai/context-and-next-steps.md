Update default model: pipeline.py sekarang default assets/best.pt dan mode auto
memilih adapter slot empty/occupied. COCO tetap bisa dipilih eksplisit. Adapter
HTTP mengirim hanya slot yang teramati, unknown tidak dijadikan available dan
backend mempertahankan last-known. Demo.py lokal juga default best.pt tetapi
launcher/backend/UI tetap di luar commit AI. File best.pt di Drive, tidak diGit.
25 tes lulus. Lihat docs/ai/best-default-2026-10-28.md untuk bukti terbaru.
Bagian di bawah berisi riwayat sebelum perubahan default ini.

Pembaruan model custom (28 Oktober): file Downloads pakarmodelsenpro.pt dan
pakarmodel1.pt identik; disalin ke assets/best.pt. Task detect, kelas empty/occupied,
metadata baseyolov8n/epochs50 (bukan verifikasi riwayat training). Adapter offline
slot_labels.py dan runner validate_custom_model.py ditambahkan. Uji145s/290sampel
selesai:156 benar,1 beda,17 unknown dari174 label; coverage90,23%. 23 tes lulus.
Baca custom-model-validation-2026-10-28.md dan custom-model-evidence.json.
Demo.py/website masih COCO, custom belum diuji HTTP/UI. File .pt diabaikan Git.
Perubahan custom-model belum di-commit/push; commit AI sebelumnya db35f30 tetap.

# Konteks AI PAKAR untuk Nabil/Brian

## Scope praktikum yang dikonfirmasi Nabil

Tujuan proyek adalah latihan membuat aplikasi yang berjalan, bukan layanan
parkir sehari-hari/produksi. Bagian AI inti dianggap terpenuhi untuk praktikum:
video nyata -> YOLO -> ROI/debounce -> backend/SQLite -> SignalR -> Blazor.
RTSP FT UGM, Azure, semua cuaca dan model custom bukan syarat tambahan selesai.
Keterbatasan tetap dicatat sebagai batasan, bukan blocker praktikum.

Launcher satu perintah tersedia: `py demo.py` dari root pakar (atau panggil
path absolut demo.py dari mana saja). Launcher membuka UI dan mengulang video
2x, memakai DB baru serta port kosong, Ctrl+C menghentikan proses miliknya.
Panduan: [demo-praktikum.md](demo-praktikum.md). Tes pacing baru membuat total
pytest menjadi 18 passed. Binary diperlukan lokal, tidak dimasukkan Git.


## Pembaruan terakhir: 28 Oktober 2026

Lihat [laporan video nyata dan backend](real-video-and-backend-2026-10-28.md)
sebelum memakai status historis di bawah. Enam ROI demo dan 29 frame berlabel
visual Codex tersedia; 172/174 slot-sample cocok (eksploratif, belum review
manusia). Agent nyata -> ASP.NET -> SQLite -> SignalR sudah diuji: 8 payload,
9 event/Detection, state akhir cocok. Backend startup/migrasi SQLite diperbaiki;
commit gagal ->503 tanpa perubahan memory, lalu recovery lolos. Watchdog
120s/poll15s terukur 129,32s dan reaktivasi lolos. 17 pytest lulus.

UI /park kemudian diperbaiki dan diverifikasi di browser: peta, status slot,
perubahan VIDEO-04, koneksi live dan peringatan inactive. API/Web build lulus.
Preview http://127.0.0.1:5091/park memakai DB disposable.

Masih belum: RTSP nyata, latency kamera-ke-UI, never-seen camera/restart,
mapping FT UGM, clip independen, motor/malam/hujan dan best.pt. Tidak ada issue
ditutup, commit/push/deploy. ID 9001..9006 hanya demo, jangan ke database produksi.

Bagian berikut menyimpan konteks sesi awal 27 Oktober, bukan status terbaru.

PAKAR (Parkir Akurat & Real-Time) adalah aplikasi parkir FT UGM yang memanfaatkan
CCTV, computer vision, backend .NET 10/EF Core, serta frontend Blazor. Target
deployment Azure. Nabil Abrian Aryo Prabowo bertugas sebagai AI engineer dan
UI/UX designer. Fokus sesi ini hanya pekerjaan AI. Ursula/Rere menangani backend;
Deva membantu integrasi real-time/frontend dan koordinasi.

Visi produk mencakup forecasting, tetapi FR 1-17 lebih konkret pada status
real-time, pencarian/alternatif, laporan, autentikasi, serta admin. Forecasting
memerlukan keputusan scope tersendiri; jangan menambahkannya sebagai syarat
untuk menyelesaikan ingestion/occupancy saat ini.

Alur implementasi: video -> Python YOLO -> ROI per slot -> HTTP ingestion
`/api/ingest/detections` menggunakan `X-Api-Key` -> backend -> SignalR
`/hubs/parking` -> UI. AI tidak langsung membuka WebSocket ke backend.

## Ruang kerja

- Repo publik: https://github.com/amarelyy/pakar
- Checkout aktif yang dibuat sesi ini:
  `C:\Users\Nabil Abrian Aryo P\OneDrive\Dokumen\ChatGPT\Pakar - Senpro\pakar`
- Branch lokal: `codex/ai-local-validation`, basis `a19fed6`.
- Workspace induk memiliki Git kosong sendiri; pastikan perintah Git dijalankan
  di subfolder checkout `pakar`, bukan direktori induk.
- Source AI semula:
  `C:\Users\Nabil Abrian Aryo P\Downloads\pakar-ai-realtime\pakar-ai-realtime`
- Kode integrasi lokal: `edge/python-agent/`; pengujian: `tests/ai/`.
- Semua perubahan masih lokal, belum commit/push. GitHub issues belum diubah.

## Prioritas 1: selaraskan model

Sudah disiapkan: baseline YOLOv8n COCO yang nyata tersedia, verifikasi kelas,
hash weights, dan dokumentasi keputusan. Model custom empty/occupied yang
dilaporkan sebelumnya belum tersedia untuk diperiksa. Jangan menggunakan angka
mAP50 laporan training untuk menyatakan akurasi model COCO atau video nyata.

Berikutnya: peroleh model custom bila diperlukan dan rekaman nyata; evaluasi
model detection, ketepatan slot, error jumlah okupansi, dan latensi secara terpisah.

## Prioritas 2: masukkan kode ke repo

Sudah disiapkan: modul occupancy/ROI, pipeline yang finite untuk video lokal,
frame validation/reconnect, contoh konfigurasi ber-ID konsisten dengan subset
seeder, requirements, manifest model, README, tes, dan bukti hasil.

Berikutnya: tinjau diff untuk kesiapan commit/push dan integrasi tim. Source
Python ini tidak otomatis membuat scaffold C# AiInference menjadi implementasi
ONNX; port tersebut membutuhkan keputusan arsitektur tersendiri.

## Pengujian #13 dan #15

14 tes otomatis lulus. Video lokal ternyata sintetis: dua kotak slot dan satu
kotak merah CAR. Neural YOLO dipisahkan dari test stub. Fixture menghasilkan
enam perubahan status yang sesuai jadwal; tujuh payload diterima HTTP loopback.
Frame hitam/beku, akhir video, dan reconnect terkontrol diuji. Penerima HTTP
pengujian tidak memverifikasi ASP.NET, SQL, SignalR, atau UI.

Masih perlu: CCTV/RTSP nyata, ground truth, kalibrasi seluruh slot, pemeriksaan
scene statis vs freeze, watchdog backend 120 detik, serta latensi end-to-end.
Default debounce baru 2 detik occupied / 3 detik available belum membuktikan
target keseluruhan 5 detik tercapai. #13 dan #15 belum layak ditutup penuh.

Baca [laporan pengujian](validation-2026-10-27.md),
[bukti terstruktur](validation-evidence.json),
dan [prompt sesi berikutnya](next-session-prompt.md).
