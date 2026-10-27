# Demo praktikum satu perintah

**Cakupan commit:** panduan ini mencatat demo aplikasi lengkap yang diuji lokal.
Launcher `demo.py` dan perubahan backend/UI pendukung belum termasuk commit AI
inti pada branch AI-PAKAR. Untuk menjalankan bagian AI dari commit itu saja,
ikuti [ASSETS.md](../../edge/python-agent/ASSETS.md).

Bagian AI inti untuk scope praktikum sudah berjalan: YOLO COCO -> enam polygon
ROI -> debounce occupied/available -> HTTP -> database -> SignalR -> UI Blazor.
Ini demonstrasi menggunakan video rekaman, bukan klaim deployment CCTV produksi.
Tidak perlu RTSP, Azure, model custom, atau evaluasi semua cuaca untuk menjalankan
praktikum ini. Best.pt hanya diperlukan bila ingin mengklaim memakai model itu.

Dari root checkout `pakar`:

```powershell
py demo.py
```

Atau dari workspace induk `Pakar - Senpro`:

```powershell
py .\pakar\demo.py
```

Launcher membangun API/Web, memilih port localhost yang kosong, membuat database
SQLite baru setiap run, menyamakan key sementara, membuka `/park`, dan menjalankan
YOLO. Semua path diambil relatif lokasi demo.py sehingga cwd tidak berpengaruh.
Port default API5180/Web5181; jika sudah dipakai dipilih port lain tanpa mematikan
service yang sudah ada. URL sebenarnya dicetak di terminal. Tidak perlu mengubah
appsettings atau mengelola tiga terminal. Tidak ada instalasi paket otomatis.

Prasyarat: .NET10 SDK, Python dengan dependensi `edge/python-agent/requirements.txt`,
weights `edge/python-agent/assets/yolov8n.pt`. Pada workstation Nabil semua telah
tersedia. Video stabil disalin otomatis dari output sesi sebelumnya ke
`edge/python-agent/assets/parking-demo.mp4`; jika output tidak ada launcher dapat
memotong file Downloads U7HRKjlXK-Y 720p yang sudah diberikan. Pada komputer lain
sertakan clip yang sama (sumber00:05–02:30), bukan video berbeda dengan ROI ini.
Binary/video tetap diabaikan Git dan perlu dibagikan terpisah ke anggota tim.

## Skenario presentasi

1. Jalankan perintah. Setelah build/model siap, buka zona **VIDEO DEMO ONLY**.
2. Awal: VIDEO-01/02/03/04/06 occupied, VIDEO-05 available (1/6 available).
3. Sekitar33 detik setelah inferensi dimulai pada kecepatan default2x,
   VIDEO-04 menjadi available (2/6 available), sesuai kendaraan keluar.
4. Video diulang otomatis; status kembali awal saat putaran berikutnya mulai.
5. Ctrl+C menghentikan proses API/Web milik launcher. Database/log disimpan di
   `output/ai-validation/demo/<run-id>/` untuk diperiksa, tanpa menghapus run lama.

Untuk mengikuti waktu video asli: `py demo.py --speed 1`. Untuk menjalankan tanpa
membuka browser otomatis: `py demo.py --no-browser`. Pengujian singkat yang keluar
sendiri: `py demo.py --smoke-test --no-browser`.
Untuk berhenti otomatis setelah dua putaran: `py demo.py --cycles 2`.

Demo tetap memakai neural YOLO asli, bukan rectangle stub. Ada false positive
singkat VIDEO-05 ketika mobil melintas sekitar waktu clip106,5–112,5s (sekitar
53–56 detik pada2x). Tampilkan sebagai keterbatasan latihan; jangan menyebut
pipeline ini sempurna. Replay2x bukan pengukuran latency kamera-ke-UI.

ROI, label, laporan dan source disimpan di Git; hasil run/model/video tidak.
Menyelesaikan bagian AI praktikum tidak berarti seluruh pekerjaan tim seperti
login/admin dan desain final aplikasi ikut dinyatakan selesai.
