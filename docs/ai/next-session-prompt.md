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

SCOPE TERBARU DARI NABIL: proyek untuk praktikum/latihan, bukan pemakaian
sehari-hari. AI inti sudah berfungsi untuk lingkup ini; jangan menjadikan RTSP
FT UGM, Azure, cuaca, atau model custom syarat selesai. Launcher py demo.py
sudah tersedia (baca docs/ai/demo-praktikum.md), mengurus build/API/Web/DB/YOLO,
loop video 2x dan cleanup. Ada --smoke-test, --no-browser, --speed, --cycles.
18 pytest passed termasuk pacing. Semua perubahan tetap lokal belum commit/push.
Konteks di bawah menyimpan bukti dan potensi pengembangan lanjutan.

Lanjutkan pekerjaan AI PAKAR untuk Nabil/Brian di checkout existing:
C:\Users\Nabil Abrian Aryo P\OneDrive\Dokumen\ChatGPT\Pakar - Senpro\pakar
Branch codex/ai-local-validation. Periksa Git dari folder pakar, pertahankan
semua perubahan lokal. Belum commit/push/deploy atau menutup issue.

Baca docs/ai/context-and-next-steps.md, real-video-and-backend-2026-10-28.md,
validation-evidence.json bagian followup_2026_10_28, evaluation-protocol.md,
edge/python-agent/README.md dan MODEL_CARD.md. Laporan 27 Oktober dan tinjauan
awal 28 Oktober bersifat historis, bukan status akhir.

Sudah dikerjakan:
- Pipeline Python YOLO COCO, ROI, debounce 2s occupied/3s available, health,
  reconnect, EOF, snapshot retry. 17 pytest lulus.
- Video nyata dari Downloads diperiksa: 316,13s, 1280x720 30FPS, pan/zoom.
- Clip stabil source 5..150s disimpan output/ai-validation/real-video/
  stable-0005-0230.mp4. Hash ada di manifest/bukti. Waktu clip = source -5s.
- Enam ROI demo real-video-demo.json camera/area9001 spots9001..9006,
  VIDEO-01..06. Jangan kirim ke database produksi atau menyebutnya FT UGM.
- Codex memberi label visual 29 frame tiap5s pada enam slot: 172/174 cocok,
  TP129 TN43 FP2 FN0, MAE0,069. Ini sparse, selected easy slots, single annotator,
  belum direview manusia, bukan akurasi umum/independent test.
- VIDEO04 keluar sekitar clip63..64s, available AI66,5s. VIDEO05 false occupied
  106,5..112,5 saat kendaraan melintas. Default tidak dituning dari skor ini.
- Snapshot EF diperbaiki dari target migrasi SQLite terakhir; legacy SQL Server
  delta dilewati pada SQLite saja. Factory mendapat override connection env.
- Ingestion memvalidasi mapping ExternalId dan payload; commit ditunggu sebelum
  memory/event/HTTP200. Failure503 tidak mengubah memory, retry berhasil.
- Build API dan startup DB SQLite fresh berhasil. Tests opt-in validate_backend.py:
  auth, persistence, hub, reconnect/snapshot, invalid mapping, failed commit,
  watchdog120s/poll15s terukur129,32s dan reaktivasi lolos.
- validate_video_backend.py: agent asli->ASP.NET->SQLite->SignalR, 8 payload,
  9 event/Detection, state akhir cocok. Replay dipercepat, bukan latency UI.
- Output binary/log di output/ai-validation diabaikan Git. Semua DB test disposable.

Lanjutan:
1. Minta/review ROI dan anotasi bersama Nabil, kemudian label interval penuh dan
   evaluasi pada clip independen. Jangan tuning lalu klaim skor yang sama held-out.
2. Frontend /park telah diperbaiki (named api client, Api:BaseUrl, interactive,
   Leaflet, scoped slot list, hub events, reconnect refresh, poll4s fallback,
   inactive warning). Browser smoke test VIDEO04 Occupied/Available tanpa reload
   berhasil. Screenshot ui-final.png dan AX logs tersedia. BuildWeb 0error,
   2warning BL0008 existing. Target latency camera-to-UI <=5s dan browser network
   reconnect masih belum diukur. Bootstrap existing404, slot scopedCSS bekerja.
3. Perlu RTSP nyata FT UGM untuk disconnect/reconnect/black/freeze/scene statis.
   Camera never-seen dan snapshot setelah restart masih belum ditangani store.
4. Mapping ExternalId aktual dan kalibrasi FT UGM belum tersedia. Model best.pt,
   kelas/dataset/provenance juga belum ada; baseline COCO tetap terpisah.
5. SQL Server/Azure belum diuji. Migrasi lama lintas provider perlu review tim;
   jangan menerapkan skema ke DB existing tanpa pemeriksaan history/back-up.
6. NuGet memperingatkan Pkcs9.0.11 NU1902/1903. Belum diubah.
7. Preview final dibiarkan berjalan: API5089, Web5091/park. Port5088 dan5090
   sudah dihentikan. Agent replay sudah selesai; UI menampilkan last-known data. Folder bin standard
   terkunci proses lama Pakar.Api PID40820 yang pathnya tak terbaca; gunakan
   artifacts-path output/... untuk build, jangan hentikan proses lain tanpa bukti.

Jangan tutup #13/#15/#20 dari hasil terbatas. Fokus AI/integrasi, forecasting
bukan syarat. Tidak perlu clone ulang atau mengulang tes tanpa perubahan relevan.
