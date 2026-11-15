# PAKAR — konteks dan rencana lanjutan untuk progres 17 November 2026

Pembaruan 16 November 2026: perubahan UI/UX dan pendukung demo dilanjutkan pada branch `ui-ux-pakar`, dibuat dari `ai-dotnet`. Konteks di bawah adalah catatan historis 15 November. Lihat [progres UI/UX](ui-progress-17-november-2026.md) dan [validasi lokal terbaru](ai/local-validation-2026-11-16.md) untuk status lanjutan.

Disimpan 15 November 2026, Asia/Jakarta. Dokumen ini merupakan ringkasan konteks,
keputusan, implementasi, bukti, dan pekerjaan berikutnya; bukan transkrip chat.
**Tidak memuat password, API key, atau connection string.**

## 1. Tujuan pengguna dan posisi pekerjaan

Pengguna adalah Nabil/Brian (`nabilabrianap06`); keduanya orang yang sama.
Rere/Ursula adalah `amarelyy`; Deva adalah `Devzka`.
Pemeriksaan progres tim berikutnya: **17 November 2026**.

Pengguna meminta pekerjaan AI tahap 1–7 dikerjakan berurutan tanpa berhenti untuk
meminta konfirmasi di setiap tahap. Implementasi dan pengujian lokal sudah
dilaksanakan. Pembahasan berikutnya beralih ke integrasi dan UI/UX yang tersisa.

Pernyataan status yang tepat:

> Inti AI .NET sudah berfungsi dan teruji lokal, sehingga siap masuk tahap
> integrasi bersama tim. Ini belum berarti seluruh acceptance, peta, watchdog,
> database bersama, dan deployment sudah selesai.

Pengguna membutuhkan penjelasan sederhana dan konkret. Bedakan pekerjaan AI,
backend, database, ParkingHub, dan tampilan peta. Hindari menyebut seluruh tugas
selesai hanya karena kode AI sudah bisa didemokan.

## 2. Checkout, branch, dan PR

Checkout aktif:

```text
C:\Users\Nabil Abrian Aryo P\.codex\worktrees\pakar-dotnet-ai\pakar
```

- **Branch aktif: `ai-dotnet`**, upstream `origin/ai-dotnet`.
- Base implementasi: main `e410db3`.
- Commit implementasi: `deaa6359020967bfcf0b63ac30d49899d838556b`.
- Commit pembaruan nama branch pada dokumen: `ddeb383101b07eef48efc6f928a46ccff5a4788a`.
- **PR aktif: https://github.com/amarelyy/pakar/pull/38**.
- Pada pemeriksaan terakhir, PR #38 masih open dan belum merge. Periksa ulang.
- PR #37 ditutup otomatis oleh GitHub ketika nama branch sumber diganti;
  deskripsinya sudah menautkan PR pengganti #38.
- Pengguna memilih nama sederhana `ai-dotnet`. Referensi nama branch sebelumnya
  sudah diganti pada empat dokumen repo dan PR terkait. Gunakan nama sekarang.
- Setelah dokumen handoff ini dibuat, akan ada file Markdown baru yang belum
  di-commit. Pembuatan dokumen ini tidak mengubah source aplikasi.

`AI-PAKAR` menyimpan pipeline Python dan bukti historis sebagai referensi.
`ai-dotnet` berasal dari main terbaru saat implementasi dan membawa runtime
.NET/ONNX beserta API/editor pendukung. Model sumbernya sama; sesi ini tidak
melatih model baru. Branch baru bukan salinan lengkap seluruh eksperimen Python.

Checkout lama yang harus dijaga beserta WIP-nya:

```text
C:\Users\Nabil Abrian Aryo P\OneDrive\Dokumen\ChatGPT\Pakar - Senpro\pakar
```

Checkout referensi Python:

```text
C:\Users\Nabil Abrian Aryo P\.codex\worktrees\issue15-review\pakar
```

Jangan reset/menimpa WIP lama atau memasukkan patch migrasi/backend/UI lama ke
implementasi PostgreSQL. Perbedaan branch bukan berarti ada dua produk PAKAR.

## 3. Sumber instruksi dan pembagian tugas

- https://github.com/amarelyy/pakar/blob/main/TODO.md
- https://github.com/amarelyy/pakar/blob/main/docs/status-and-issues.md
- https://github.com/amarelyy/pakar/blob/main/docs/api-reference.md
- Lampiran instruksi awal berada di:

```text
C:\Users\Nabil Abrian Aryo P\.codex\attachments\70fe2e92-b946-47f5-8e16-51b4351349a4\Pasted text.txt
```

Pesan WA berisi daftar prioritas **seluruh tim**, bukan seluruhnya kewajiban Nabil.
Sebagian kata/status WA tertinggal dari arsitektur dan source terbaru.

TODO main bagian 0 menetapkan **video rekaman loop, bukan RTSP/kamera nyata**, dan
runtime aplikasi memakai .NET/Blazor. Python dipertahankan untuk export/validasi
offline dan referensi, bukan proses runtime aplikasi produksi.

P3 dan P6 adalah subjudul dalam TODO GitHub, bukan nomor daftar WA:

- **P3:** ONNX model and inference engine — export, C# inference, parity.
- **P5:** Reading the video and mapping slots — worker, ROI, occupancy.
- **P6:** Slot polygon editor — Blazor SVG dan penyimpanan API.
- **P4 / #19:** dashboard admin, dikerjakan bersama Deva/Brian.
- **P7 / #16:** peta realtime, tugas Nabil.
- **P8 / #17:** pencarian dan alternatif zona, masih perlu dikerjakan.
- **P9 / #18:** frontend laporan pengguna, pemilik belum disepakati.
- **P10 / #20:** integrasi penuh sampai peta, Deva dengan tim.

Main terbaru yang terakhir diperiksa masih `e410db3`. Pada main, auth #9,
guest status/search/alternate #11, reports #12, seed, dan CRUD zona/sumber video
sudah tersedia. Jangan mengulang backend tersebut berdasarkan checklist lama.

TODO terbaru menugaskan API ROI/config/reference-frame (2a–2c) kepada Nabil.
Implementasinya sudah ada pada `ai-dotnet`. Persistensi watchdog/IsActive dan
backend zone override ditugaskan kepada Deva. Rere menangani review/merge dan
penerapan migrasi ke lingkungan bersama sesuai proses tim.

## 4. Implementasi yang sudah dilakukan

### Tahap 1–3: baseline, export, engine

- Checkout implementasi dari main; WIP lama dipertahankan.
- Exporter `dl/scripts/export_onnx.py`, manifest, model/input/output hashes.
- Model aktual adalah custom YOLOv8n dengan label `empty` dan `occupied`.
- Input FP32 `[1,3,640,640]`, output `[1,6,8400]`, opset 17.
- `OnnxInferenceEngine`: letterbox, RGB/NCHW/normalisasi, decode, class-aware NMS,
  koordinat dikembalikan ke frame asli. DTO box tetap normalized 0–1.
- Placeholder engine diganti; diagnostic HTTP inference benar-benar berjalan.

### Tahap 4: ROI dan occupancy C#

- Polygon normalized, boundary-inclusive point-in-polygon, satu box maksimal satu slot.
- `empty -> available`, `occupied -> occupied`; missing/conflict -> undecided.
- Confidence .10, NMS IoU .70, assignment IoU .10, conflict margin .10.
- Baseline debounce occupied 2 detik / available 3 detik; observasi awal yang
  decisive langsung menginisialisasi status seperti referensi Python.
- Candidate reset pada gap/loop; slot undecided tidak dikirim sebagai kosong.
- Slot changes dipertahankan saat total occupied tidak berubah; ringkasan
  mempertimbangkan ROI coverage, kapasitas zona, pending dan unknown.

### Tahap 5: API dan database pendukung AI

- `GET/PUT /api/cameras/{id}/rois` — admin, validasi polygon dan keanggotaan zona.
- `GET /api/internal/video-sources` — ingest API key, ExternalId numerik untuk worker.
- `GET/PUT /api/cameras/{id}/reference-frame` — admin upload PNG/JPEG <=2 MB.
- Pengguna secara eksplisit menyetujui screenshot diunggah admin dan disimpan DB.
- DTO admin camera memiliki `hasReferenceFrame`.
- Satu migrasi baru **`AddCameraFrames`**, hanya menambahkan tabel tersebut.
  Sudah diperiksa dan diuji pada PostgreSQL lokal; belum diterapkan ke Neon tim.
- Health `IsActive` tidak dipakai sebagai enable flag config worker, agar sumber
  yang belum aktif tetap bisa mulai dijalankan.

### Tahap 6: video worker

- `VideoSourceWorker` .NET BackgroundService, native OpenCvSharp, sampling ~2 FPS.
- Loop EOF, refresh config, API key ingest, heartbeat, retry dan fresh snapshot resync.
- Debounce memakai monotonic clock; timestamp payload adalah UTC send/observation.
- Valid all-undecided frame boleh mengirim heartbeat kosong tanpa mengubah last-known.
- Invalid/missing/corrupt frame tidak membuat slot otomatis available.
- Pemulihan API restart, missing/corrupt file, loop dan cancellation diuji.
- Dockerfile disiapkan dan Linux-x64 publish berhasil; container belum dijalankan.

### Tahap 7: editor dan integrasi lokal

- `/admin/polygons`: editor Blazor SVG/MouseEventArgs, pilih source/spot, upload
  gambar, draw/drag, undo/delete/clear, save/reload, validasi dan admin gate.
- Resize/scale dan penyimpanan melalui API nyata diuji dengan browser.
- Bug race pemulihan UserSession diperbaiki; komponen menunggu restore yang sama.
- Client SignalR diperbarui ke 10.0.13 setelah warning dependency aktual ditemukan.
- Video -> .NET -> ingest -> PostgreSQL lokal dan ParkingHub benar-benar diuji;
  bukti HTTP, persistensi dan event diamati terpisah.

ParkingHub adalah bagian backend `Pakar.Api`, route `/hubs/parking`, memakai
SignalR untuk mengirim pembaruan ke frontend. ParkingHub sudah ada sebelumnya;
kita menguji pipeline AI menghasilkan event di sana. Ini belum membuktikan peta UI
sudah mendengarkan event dan berubah otomatis.

## 5. Bukti dan batas pengujian

Dokumen tracked:

- `docs/ai/dotnet-pipeline-progress.md` — laporan utama.
- `docs/ai/runtime-validation.md` — cara menjalankan/mengulang pengujian.
- `docs/ai/model-runtime.md`, `onnx-manifest.json` — export dan model.
- `box-parity.json`, `slot-parity.json`, `dotnet-label-evaluation.json`.
- `roi-api-evidence.json`, `detection-http-evidence.json`.
- `worker-evidence.json`, `editor-evidence.json`, `pipeline-integration-evidence.json`.

Hasil yang sudah dibuktikan:

- Build solution tanpa warning/error pada run tercatat.
- **113 tes .NET total**: 88 backend/domain/application, 23 AI/worker, 2 web/session.
  Angka ini mencakup tes backend yang sudah ada, bukan 113 tes AI baru.
- **43 pemeriksaan API nyata** pada PostgreSQL lokal, ditambah diagnostic HTTP
  boxes/400/401/415/503.
- Parity 290 frame: zero unmatched boxes, toleransi 1 px/.001 confidence;
  max error sekitar .00103 px dan .00000416 confidence.
- 1.740 stable/raw slot observations dan 290 zone counts: zero differences
  terhadap referensi Python.
- Sparse visual labels: 170 cocok / 1 debounce mismatch / 3 undecided dari 174.
- Video loop/recovery/empty-heartbeat preservation dan editor browser tests lulus.
- Enam event ParkingHub cocok dengan database lokal, count 5 occupied/1 available
  pada clip integrasi pendek yang terpisah.

Run performa pada laporan repo: mean 89.58 ms, p95 112.22 ms. Run ulang untuk
bukti visual: mean 96.56 ms, p95 121.85 ms. **Jangan menjamin setiap frame <100 ms.**
Latensi event pertama dari startup worker: sekitar 1.26 s pada laporan repo dan
2.19 s pada run ulang visual. Ini **bukan** pengukuran video-change -> map UI.

Model/video dan labels bukan held-out evaluation. Label visual belum direview
manusia; jangan menyebut akurasi umum 100% dari angka parity.

Issue #13/#14/#15 masih memiliki checklist lama/tidak sepenuhnya tersinkronkan.
Sebagian checkbox #20 sudah dicentang oleh tim, tetapi itu bukan bukti baru sesi
ini untuk latensi UI. Cocokkan task dengan source dan scope terbaru sebelum
menandai selesai atau menutup issue; review juga acceptance event/count FR11.

## 6. Foto/video bukti yang sudah diberikan ke pengguna

Folder lokal (ignored Git):

```text
C:\Users\Nabil Abrian Aryo P\.codex\worktrees\pakar-dotnet-ai\pakar\artifacts\visual-proof
```

- `01-ai-deteksi-lokal.png`: frame 0 s, 5 terisi / 1 kosong.
- `02-perubahan-status-slot.png`: frame 70 s, 4 terisi / 2 kosong.
- `03-editor-polygon.png`: screenshot pengujian editor Blazor aktual.
- `bukti-ai-dotnet-lokal.mp4`: H.264, ~32.2 detik, 1440x1160, ~6.9 MB.
- `bukti-ai-dotnet-lokal.zip`: paket foto/video + README + evidence + hashes, ~8.3 MB.
- `bukti-uji-lokal.json`, `integration.json`, `integration-console.txt`, `manifest.json`.

**Video adalah replay visualisasi output inferensi .NET aktual**, bukan rekaman
inferensi live atau UI peta produk. Kartu backend pada video merangkum uji
integrasi lokal terpisah, bukan sambungan live replay ke database. Label batas
ini sudah tertera pada visual dan README. Jangan mengubah narasinya menjadi
"map sudah selesai" atau "Neon sudah terintegrasi".

Pengujian ulang memakai PostgreSQL lokal terisolasi, port 55439, dan API test
loopback 5259. Server uji tersebut sudah dihentikan setelah selesai. Bukti baru
disimpan di artifacts; evidence tracked semula dipertahankan. Tidak ada perubahan
data Neon dari pengujian ini.

## 7. Database tim: informasi dan hasil cek terakhir

Database kelompok menggunakan **PostgreSQL di Neon**. Pengguna memberikan
connection string untuk database **`pakardb`**. Kredensial sengaja tidak disalin
ke dokumen ini dan tidak boleh dimasukkan ke Git, log, atau foto/video.

Hasil probe read-only:

- Koneksi berhasil, TLS aktif, PostgreSQL 18.7.
- Transaksi pemeriksaan read-only.
- Schema **`public` tidak memiliki tabel aplikasi**, termasuk tabel PAKAR dan
  `__EFMigrationsHistory`.
- Ini belum membuktikan database/schema lain kosong; hasil cek hanya public.
- Belum menjalankan migrasi, seed, worker, atau penulisan data pada Neon ini.
- Connection string belum dikonfigurasi permanen untuk menjalankan aplikasi.

Sebelumnya main/docs tim menyebut database bersama sudah berisi zona, kamera,
spot dan admin. Karena hasil database `pakardb` berbeda, **konfirmasikan target
database/branch Neon yang benar dengan pengguna/Rere sebelum inisialisasi**.
Jangan berasumsi DB kosong itu pasti DB bersama yang dimaksud.

`Admin:Password` adalah password akun aplikasi, berbeda dari password PostgreSQL.
`dotnet user-secrets` tersimpan per komputer dan tidak ikut repo. Pemeriksaan
terakhir menunjukkan user-secrets Pakar.Api pada komputer ini masih kosong dan
tidak ada DATABASE_URL di environment/konfigurasi project yang diperiksa.

Pooled connection tidak menerima semua startup `options`; probe berhasil setelah
readonly/statement_timeout diatur melalui session/transaction. Untuk migrasi,
ikuti proses tim dan gunakan koneksi direct/unpooled yang tepat jika diperlukan.

## 8. UI/UX yang masih perlu dikerjakan

Pada `ai-dotnet`/main yang terakhir diperiksa:

- Admin `Users.razor`, `Reports.razor`, `Zones.razor`, `Cameras.razor` masih kosong.
- Home masih template "Hello, world!".
- CampusMap masih memakai PeriodicTimer/polling; belum dialihkan penuh ke ParkingHub.
- Editor polygon sudah tersedia, tetapi bukan pengganti dashboard admin umum.

### Dashboard admin #19 — bersama Deva

- Users: list/search/paging, edit nama/role, deactivate/reactivate, safeguard errors.
- Reports: queue/filter/paging/detail, resolve/dismiss dan catatan.
- Zones: list/add/edit/remove melalui API yang sudah ada.
- Video Sources: list/add/edit/remove, zona/file, status, link ke editor polygon.
- Override control setelah endpoint backend resmi tersedia.
- Setiap flow: admin protection, API nyata, loading, empty, error, validasi,
  success message dan tampilan layar kecil.

**Belum ada pembagian halaman resmi** antara Nabil/Brian dan Deva di percakapan
ini. Usulan Users vs Reports sebelumnya bukan penugasan. Jangan mengirim pesan
ke teman melalui tool tanpa instruksi eksplisit.

Pada fetch terakhir branch `origin/546873` bergerak ke `6603c98`. Pesan commit
menyebut Zones/AdminApiClient, tetapi tree yang diperiksa masih menunjukkan
Zones kosong dan tambahan AdminApiClient 0 byte. Periksa ulang source/PR dan
perubahan lokal Deva, jangan menganggap deskripsi commit sebagai bukti halaman
sudah berfungsi atau langsung mengerjakan halaman yang sama.

### Peta #16 dan pencarian #17

- Tentukan DoD issue #16, jadikan map halaman utama.
- Status zona available/almost_full/full, jumlah tersedia/kapasitas, legenda.
- Data awal API + ParkingHub; snapshot/resync saat reconnect.
- Mapping Guid vs ExternalId benar; indikator sumber inactive/data lama.
- Buktikan update tanpa reload dan ukur sampai UI <=5 detik bersama Deva.
- Search/filter zona; alternatif saat zona full; target search <2 s dan alternate <3 s.

### Laporan pengguna #18

Owner belum disepakati. Jangan otomatis menambahkannya sebagai kewajiban Nabil.
Pisahkan form submit pengguna dari halaman review laporan admin #19.

## 9. Urutan kerja berikutnya yang disarankan

1. Baca handoff ini, cek git status/branch/worktrees, fetch, lalu baca TODO/docs
   main dan issue/PR terbaru. Pertahankan `ai-dotnet` dan seluruh WIP yang ada.
2. Selesaikan kejelasan integrasi: review PR #38, konfirmasi database Neon target,
   lokasi distribusi ONNX/video, dan konfigurasi API/worker. Jangan menerapkan
   migrasi ke database bersama sebelum proses review/merge tim terpenuhi.
3. Sepakati pembagian dashboard #19 dengan Deva melalui keputusan pengguna/tim.
   Pilih satu halaman yang belum diambil dan buat benar-benar bekerja melalui API.
   Pilihan halaman masih terbuka; jangan menganggap Users sudah ditugaskan.
4. Lanjutkan peta #16 dan search/alternate #17 memakai endpoint yang sudah ada.
5. Integrasikan AI dengan map dan watchdog persisten bersama Deva, lalu ukur
   seluruh jalur dan tes reconnect/restart. HTTP 200 bukan bukti persistensi saja.
6. Update checklist issue dan docs sesuai bukti per subtask; bedakan implemented
   locally, reviewed/merged, deployed, dan fully accepted. Jangan menutup issue
   gabungan dari unit test atau bukti Python saja.

Target minimum yang disarankan untuk presentasi 17 November: **demo AI .NET
yang berfungsi beserta bukti + satu halaman admin yang berfungsi melalui API**.
Ini target kerja, bukan janji seluruh daftar tim selesai tanggal tersebut.

User menyukai nama branch singkat dan mudah dipahami. Pembuatan branch implementasi
terpisah sebelumnya sempat membingungkan; jelaskan alasan pengelolaan branch bila
memang diperlukan, dan jangan membuat branch baru secara otomatis untuk lanjut
membaca atau mengerjakan pekerjaan di checkout yang sesuai.

## 10. Kalimat laporan singkat yang aman

> Guys, inti AI .NET sudah jalan dan teruji lokal di branch ai-dotnet. Video loop,
> mapping ROI, editor polygon dan pengiriman sampai database lokal/ParkingHub
> sudah diuji. Kode dan bukti ada di PR #38. Berikutnya review/merge, integrasi
> lingkungan tim dan peta, serta penyelesaian watchdog. Dashboard admin umum dan
> peta/pencarian belum aku klaim selesai.

## 11. Prompt untuk percakapan berikutnya

```text
Lanjutkan proyek PAKAR untuk progres 17 November 2026.
Baca docs/handoff-progres-17-november-2026.md terlebih dahulu, lalu cek git status,
fetch origin, TODO/docs main, issue dan PR terbaru.
Nabil dan Brian adalah orang yang sama. Branch aktif ai-dotnet, PR #38.
AI .NET, worker, ROI APIs, reference-frame upload dan editor SVG sudah diuji lokal;
belum semua acceptance/deployment/latensi UI selesai. Pertahankan WIP lama dan
AI-PAKAR sebagai referensi Python. Gunakan nama ai-dotnet pada dokumentasi.
Jangan salin kredensial dari riwayat chat ke dokumen atau output.
Neon pakardb pernah berhasil diakses read-only tetapi public kosong; konfirmasi
target DB bersama dengan pengguna/Rere sebelum migrasi/seed/penulisan data.
Fokus selanjutnya: pastikan pembagian #19 dengan Deva, kerjakan satu halaman admin
yang disepakati melalui API, lalu lanjut #16 peta realtime dan #17 pencarian.
#18 belum ditetapkan pemiliknya. Watchdog persisten/override dan integrasi #20
perlu koordinasi dengan Deva. Bukti visual artifacts/visual-proof adalah replay
hasil tes aktual, bukan peta produk/live Neon.
Laporkan hasil, batas pengujian dan sisa pekerjaan dengan bahasa sederhana.
```
