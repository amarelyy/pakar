# Evaluasi awal video nyata dan integrasi backend — 28 Oktober 2026

## Bukti dan lingkup

YOLO COCO berhasil dijalankan pada video kendaraan nyata yang diberikan Nabil.
Dipilih clip sumber 00:05–02:30, 145 detik, 1280x720, 30 FPS. Ini video parkir
eksternal, bukan kamera FT UGM. Sumber asli tetap utuh. Binary dan keluaran
lokal di `output/ai-validation/real-video/`, tidak dilacak Git.

Enam ROI demo dibuat di `edge/python-agent/configs/real-video-demo.json`.
Camera/area ExternalId=9001, spot ExternalId=9001..9006, nama VIDEO-01..06.
ID ini hanya untuk database uji; bukan H-01/H-02 atau mapping produksi.
Referensi visual: `roi-reference.jpg`. ROI dipilih sebelum melihat skor.

Codex melakukan anotasi visual pada 29 frame, setiap 5 detik clip 0..140.
Label tersedia dalam `real-video-sampled-labels.csv`. Interval setengah detik
dipakai untuk mencakup tepat satu sampel pipeline 2 FPS pada setiap frame
berlabel. Jeda antar-frame belum diberi label; ini bukan ground truth kontinu.
Anotasi belum ditinjau manusia/annotator kedua. Slot yang dipilih relatif
mudah terlihat; hasil mengandung bias pemilihan dan korelasi temporal.
Montage bukti lokal: `annotation-sheet-0.jpg` sampai `annotation-sheet-2.jpg`.

## Hasil baseline per slot

Pipeline memproses 290 sampel pada 2 FPS, seluruhnya lolos frame health.
Model/threshold/debounce default tidak dituning berdasarkan skor ini.

| Ukuran pada frame berlabel saja | Hasil |
|---|---:|
| Slot-sample | 174 |
| Cocok | 172 |
| TP / TN / FP / FN | 129 / 43 / 2 / 0 |
| Accuracy eksploratif | 98,85% |
| Precision occupied | 98,47% |
| Recall occupied | 100% |
| Coverage prediksi pada label tersedia | 100% |
| MAE jumlah okupansi enam slot | 0,069 kendaraan/frame berlabel |

Ini **bukan** akurasi umum model, detection mAP, full-video accuracy, atau
angka produksi PAKAR. Bukti terstruktur di validation-evidence.json bagian
followup_2026_10_28. Masih perlu review label, clip independen dan kondisi lain.

VIDEO-04 ditandai available pada 66,5s. Inspeksi 1 detik menunjukkan kendaraan
masih berada di slot pada 63s dan sudah keluar pada 64s; keterlambatan yang
teramati terhadap bracket tersebut sekitar 2,5–3,5s **dalam waktu video**,
bukan kamera-ke-UI. Selisih sampel 65s merupakan efek debounce yang diharapkan.

VIDEO-05 yang kosong menjadi occupied sementara pada 106,5–112,5s ketika
kendaraan melintas dekat/melalui ROI. Sampel 110s menjadi false positive.
Ini keterbatasan occupancy berdasarkan overlap/dwell: kendaraan melintas dapat
memenuhi kriteria. Jangan menghapus error atau menaikkan debounce tanpa uji
clip terpisah. Review geometri ROI dan definisi occupied terhadap lintasan
kendaraan diperlukan. Status akhir: VIDEO-01/02/03/06 occupied, 04/05 available.

## Perbaikan backend

1. Snapshot EF diselaraskan dengan target model `SqliteInitialSchema` terakhir:
   ExternalId Camera/Zone dan LastActiveTime dikembalikan; deklarasi duplikat
   SQL Server yang salah dihapus. Tidak ada tabel existing dihapus.
2. Migrasi SQL Server lama UpdateAuthAndZoneModels dilewati hanya pada provider
   SQLite, karena SQLite memiliki migrasi initial sendiri. Jalur SQL Server
   tidak diubah oleh guard ini, tetapi Azure/SQL Server belum diverifikasi.
3. Design-time factory membaca environment connection override agar tooling
   bisa diarahkan ke database disposable. Import DbSeeder diperbaiki sebelumnya.
4. Ingestion memvalidasi status/confidence/ID/type dan mapping ExternalId
   camera-area-spot serta nama slot. Mapping tidak ditemukan/ambigu ditolak 400.
5. Commit database ditunggu sebelum mutasi memory dan publikasi hub. Kegagalan
   commit menghasilkan 503, sehingga agent dapat mengirim snapshot ulang.
   Gate serialisasi mengurutkan ingestion pada satu instance; bukan mekanisme
   koordinasi multi-instance Azure. Deduplikasi detection memakai state memory;
   restart/out-of-order request belum diselesaikan oleh perubahan ini.

Build API final berhasil (0 error). Startup/migrasi SQLite database baru dan
seeding berhasil. Peringatan NuGet NU1902/NU1903 Pkcs 9.0.11 tetap ada. Tidak ada
database lama/produksi dimodifikasi. Build memakai artifacts-path di output
karena proses API dari sesi lama mengunci folder bin standar.

## Pengujian backend langsung

`tests/ai/validate_backend.py` terhadap database disposable integration-v3.db:

- API key salah -> 401.
- Occupied/available -> event hub dan baris database sesuai.
- Koneksi SignalR ditutup lalu dibuat ulang -> GetSnapshot dan JoinArea berhasil.
- Mapping/type/status invalid -> 400 tanpa mengubah snapshot.
- Trigger SQLite yang sengaja menggagalkan commit -> 503, state memory dan
  occupancy DB tidak berubah. Trigger dihapus; retry berhasil.
- Watchdog default 120 detik dengan polling 15 detik benar-benar ditunggu;
  event inactive diterima, lalu heartbeat memicu active kembali. Waktu aktual
  dicatat di validation-evidence.json; bukan pengujian dipercepat.

`tests/ai/validate_video_backend.py` menjalankan clip melalui agent asli dan
backend yang sama menggunakan enam ID demo pada database disposable:

- 8 payload diterima; 0 kegagalan HTTP.
- 9 event SpotStatusChanged diterima lewat SignalR dan 9 Detection tersimpan.
- Status akhir database dan snapshot backend cocok dengan journal Python.
- Replay dipercepat; hasil ini tidak mengukur latensi kamera sampai UI.

Tes otomatis Python final: **17 passed**. Integration scripts bersifat opt-in,
bukan bagian pytest default; memerlukan requests dan websocket-client.

## Integrasi UI Blazor yang kemudian diselesaikan

Celah frontend diperbaiki: named HTTP client `api` dengan `Api:BaseUrl`,
interactive server render pada /park, implementasi Leaflet CampusMap.js,
case path script yang benar, daftar slot berwarna dengan teks status, dan link
Parking. Component berlangganan ZoneUpdated dan CameraStatusChanged; setelah
reconnect mengambil ulang data DB. Poll 4 detik tetap menjadi fallback dan
mencoba koneksi hub kembali. Kamera inactive memunculkan peringatan status lama.
Slot demo tanpa koordinat nyata (0,0) hanya muncul di daftar, tidak dipetakan.

Build Web final berhasil 0 error; 2 warning BL0008 pada form Login/Register
existing belum diubah. Bootstrap lokal existing tidak tersedia (404); tampilan
slot memakai scoped CSS sendiri. Browser lokal /park benar-benar dibuka dan
menampilkan koneksi live, peta, enam slot demo, serta peringatan kamera inactive.

Uji UI menggunakan agent nyata: frame pertama mengubah VIDEO-04 menjadi
Occupied (available 1/6), lalu replay penuh mengembalikannya menjadi Available
(available 2/6) tanpa reload halaman. Pada build final, payload hasil inferensi
awal/akhir juga direplay untuk memverifikasi kedua status secara eksplisit.
Bukti AX dan screenshot: ui-before-ax.txt, ui-after-ax.txt, ui-initial.png,
ui-before.png, ui-final.png di output/ai-validation/real-video. CUA/browser dipakai
untuk observasi UI, bukan penerima HTTP pengganti. Rekoneksi browser setelah
jaringan putus dan latency terukur belum diuji. Fallback polling dan event hub
sama-sama aktif saat observasi; tidak diklaim jalur event eksklusif.

Preview lokal dibiarkan berjalan pada http://127.0.0.1:5091/park, backend5089,
DB disposable integration-v3.db; API5088 dan Web5090 lama sudah dihentikan.
Tidak ada agent yang terus memproses video setelah replay selesai.

## Pekerjaan yang masih terbuka

- UI telah lolos smoke test di atas; belum pengukuran latency kamera-ke-UI,
  browser reconnect saat jaringan putus, atau verifikasi produksi/Azure.
- Store backend masih memory-only, belum memuat kamera yang belum pernah
  mengirim dan belum memulihkan snapshot saat restart. Watchdog yang lolos
  hanya untuk kamera yang sudah mengirim. Kamera offline tetap mempertahankan
  last-known slot state; UI kini memberi peringatan global saat kamera yang dikenal inactive, tetapi
  belum membedakan freshness setiap slot/kamera yang belum pernah mengirim.
- Belum ada RTSP nyata, black/freeze pada kamera nyata, atau validasi scene
  statis vs freeze. Fixture terdahulu tetap bukti komponen saja.
- Belum ada evaluasi motor, malam/hujan, oklusi berat, calibration FT UGM,
  best.pt, dan measurement kamera-ke-UI <=5 detik.
- Belum commit/push/deploy dan tidak ada issue yang ditutup. #13/#15/#20
  memperoleh bukti tambahan tetapi belum selesai penuh.

## Reproduksi (database uji baru saja)

Lihat README agent bagian integrasi. Jangan jalankan fixture ke backend
produksi atau database shared. File input/model/clip harus tersedia lokal.
`validate_video_backend.py` sengaja menolak ID demo yang sudah ada; gunakan
database disposable baru saat mengulang seluruh prosedur.
