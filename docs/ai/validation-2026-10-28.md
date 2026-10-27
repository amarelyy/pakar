# Tinjauan lanjutan — 28 Oktober 2026

**Pembaruan setelah laporan awal ini:** blocker startup SQLite sudah diperbaiki
dan integrasi backend/video nyata sudah diuji. Lihat
[laporan terbaru](real-video-and-backend-2026-10-28.md). Isi di bawah adalah
catatan temuan awal, bukan status akhir sesi.

Branch terverifikasi `codex/ai-local-validation`. Perubahan sesi sebelumnya
dipertahankan, belum commit/push. Tidak ada issue ditutup.

- Enam dokumen konteks, kode runtime/ROI/occupancy/ingestion, kontrak backend,
  state store, watchdog, dan seeder ditinjau. `git diff --check` bersih.
- Pencarian asset di folder agent dan sumber Downloads yang disebut pengguna
  hanya menemukan sample sintetis dan yolov8n.pt; best.pt/rekaman nyata tidak
  ditemukan pada lokasi tersebut.
- Ditambahkan `evaluate.py` dan protokol anotasi/evaluasi rekaman nyata.
  Tiga tes baru memeriksa confusion/coverage, batas interval, unknown, input
  invalid, dan perbedaan error hitungan vs error status slot.
- Pytest: **17 passed**, bukti lokal
  `output/ai-validation/pytest-2026-10-28.xml`. Tes evaluator memakai data kecil
  terkontrol, bukan hasil akurasi kendaraan.
- Build API pertama gagal CS0103 DbSeeder; ditambahkan import
  `Pakar.Api.Data` pada Program.cs. Build ulang berhasil, log lokal
  `output/ai-validation/backend-build-2026-10-28.log`.
- NuGet mengeluarkan NU1902/NU1903 untuk System.Security.Cryptography.Pkcs
  9.0.11. Paket tidak diubah dalam pekerjaan ini.
- Startup API dicoba pada 127.0.0.1:5087 dengan database khusus di output,
  bukan database existing. Gagal di DbSeeder.MigrateAsync: snapshot EF
  `AppDbContextModelSnapshot.cs:85` membuat indeks Camera.ExternalId tanpa
  deklarasi tipe properti. Integrasi DB/SignalR/UI/watchdog belum berjalan.

Temuan review untuk tindak lanjut backend: endpoint memulai persist task lalu
`Task.WhenAny(persistTask, Task.CompletedTask)` sehingga HTTP 200 tidak
menjamin commit database; exception persistence hanya dilog. State store hanya
mengenali kamera yang sudah mengirim ingestion, sehingga kamera belum pernah
mengirim tidak ikut MarkStaleCameras. Kedua temuan adalah review kode, belum
pengukuran runtime. Perbaikan migrasi perlu menjaga konsistensi model, snapshot,
dan history database; tidak dilakukan pengubahan skema secara spekulatif.

#13/#15/#20 tetap belum selesai. Diperlukan rekaman nyata dan ground truth,
mapping ExternalId aktual, akses RTSP untuk fault/recovery, serta backend yang
bisa startup untuk melanjutkan pengujian. Lihat evaluation-protocol.md.
