# Kesesuaian progres Nabil/Brian dengan arsitektur video .NET

Diperiksa 15 November 2026 terhadap `origin/main` commit
`e08e7b3e43251c63b13f35dce5b7e30e279d8638`, body issue GitHub, PR #34, dan
hasil pekerjaan Python yang sudah diuji. Pengguna mengonfirmasi bahwa **Brian
adalah Nabil**, GitHub `nabilabrianap06`; bukan anggota tim keempat.

## Keputusan yang mengubah penilaian progres

[TODO terbaru](https://github.com/amarelyy/pakar/blob/e08e7b3e43251c63b13f35dce5b7e30e279d8638/TODO.md)
bagian 0 menetapkan sumber video rekaman yang diputar berulang, runtime
Blazor/.NET, model ONNX, mapper C#, dan editor polygon Blazor SVG. Python hanya
dipakai untuk training/export serta referensi perilaku dan evaluasi.
Bagian 0.3 E secara eksplisit menempatkan `edge/python-agent` sebagai referensi,
bukan runtime yang dimasukkan ke main.

Dengan scope ini, kesimpulan lama “#15 selesai” hanya menjelaskan implementasi
referensi Python. **#15 untuk aplikasi yang sekarang direncanakan belum selesai.**
Hasil tes lama tetap valid untuk bahasa/runtime yang benar-benar diuji:
47 tes Python dan 50 unit test backend tidak membuktikan implementasi AI .NET.

Alur target:

```text
video loop -> VideoSourceWorker (.NET) -> OnnxInferenceEngine
           -> RoiMapper C# + occupancy rules
           -> HTTP ingest -> ParkingHub -> peta Blazor
editor polygon Blazor SVG -> API/database -> konfigurasi worker
```

## Apa yang sudah berguna dari pekerjaan sebelumnya

- Model custom `best.pt` dan kontrak kelas empty/occupied sudah diketahui.
- Confidence 0,10, ROI normalisasi 0..1, assignment dan debounce sudah memiliki
  implementasi serta bukti perilaku yang dapat dijadikan acuan porting.
- Dataset evaluasi sampel, evaluator, dan kasus error/unknown tersedia sebagai
  baseline parity test ONNX/.NET.
- Snapshot parsial mengomit slot undecided; ini sejalan dengan kontrak ingest
  terbaru yang tetap hanya menerima occupied/available.
- Kasus masuk/keluar, slot swap dengan jumlah tetap, unknown/recovery, dan
  kandidat transisi batal sudah diuji di Python; skenario ini dapat dipakai
  untuk membuat tes .NET.

Yang belum tersedia dari referensi tersebut: runtime C#, model ONNX yang
divalidasi, worker loop .NET, editor SVG, dan integrasi konfigurasi ROI dari API.
Rentang count dan penanda pending pada journal Python adalah hasil referensi;
TODO menempatkan fitur uncertain/age pada map sebagai peningkatan lanjutan.
Jangan menambahkan status `unknown` baru ke kontrak ingest tanpa perubahan tim.

## Progres tugas Nabil/Brian

| Prioritas TODO | Tugas | Status berdasarkan kode | Sisa / dependensi |
| --- | --- | --- | --- |
| P3 / #14 | Export ONNX dan engine .NET | Belum selesai. `export_onnx.py` masih placeholder; program mendaftarkan `UnavailableInferenceEngine`. | Export `best.pt`, manifest/hash/distribusi model, letterbox/normalisasi/confidence/NMS di C#, parity test terhadap baseline Python. Bisa mulai tanpa P1/P2. |
| P4 / #19, bersama Deva | Dashboard admin | Belum tersedia. Users, Reports, Zones, Cameras masing-masing 0 byte; dashboard hanya kerangka. | Users/Reports dapat mulai memakai endpoint yang ada. Zona/video source lengkap dan override menunggu tambahan API Ursula. |
| P5 / #13 | VideoSourceWorker | Belum tersedia di .NET. Reader dan replay Python adalah referensi, bukan implementasi worker yang diminta. | Sampling sekitar 2 FPS, loop EOF, ambil konfigurasi API, heartbeat dan HTTP ingest. Bergantung P2/P3. Persistensi status sumber adalah pekerjaan backend Ursula. |
| P5 / #15 | RoiMapper dan occupancy rules C# | Belum diimplementasikan; mapper C# masih placeholder. Logika Python tersedia sebagai referensi. | Point-in-polygon, normalisasi koordinat, confidence/debounce, omit undecided, hitung zona, tes .NET. |
| P6 / #15 | Editor polygon admin Blazor SVG | Belum dibuat. `roi_tool.py` OpenCV tidak memenuhi editor Razor yang diminta. | Pilih spot, gambar/edit polygon dengan `MouseEventArgs`, simpan koordinat 0..1 melalui API; menunggu endpoint/frame referensi P2. |
| P7 / #16 | Peta sebagai home dan real-time | Sebagian. Main masih `/park`, polling 4 s, home template. Patch aplikasi lama ada di checkout asal dan belum menjadi fitur main. | Home, ParkingHub, warna zona, inactive-source indicator, DoD dan verifikasi UI. |
| P8 / #17 | UI pencarian dan alternatif | Belum dibuat; endpoint guest backend sudah ada. | Search/filter, hasil, saran alternate dan pemeriksaan batas waktu. Bisa mulai sekarang. |

P9 / #18 (report form) masih **belum ditentukan pemiliknya**. Itu tidak otomatis
menjadi tugas Nabil/Brian. P10 / #20 menjadi tanggung jawab Deva dengan
dependensi worker dan map dari Nabil; mendukung integrasi bukan berarti semua
pekerjaan #20 berpindah pemilik.

## Apa yang perlu diselaraskan pada tracking

- #15 tetap open; checklist Python terdahulu perlu dibedakan dari checklist
  implementasi .NET dan editor SVG. Jangan menyatakan scope terbaru selesai.
- PR #34 tetap draft dan menjadi referensi Python. Usulan merge Python sebagai
  runtime ke main tidak lagi sesuai TODO terbaru.
- #14 masih closed padahal engine .NET belum ada; perlu dibuka kembali atau
  diganti issue baru sesuai mekanisme tim.
- #13 masih menyebut RTSP. Scope terbaru adalah worker video dan status sumber,
  sehingga pengujian RTSP nyata tidak lagi menjadi syarat proyek ini.
- Assignee #19 di GitHub masih Ursula; rencana tim menetapkannya untuk Deva dan
  Nabil/Brian. Assignee #18 juga belum mencerminkan keputusan pemilik yang final.
- Owner table TODO perlu menggabungkan Nabil/Brian dan memakai satu handle
  `nabilabrianap06`. Koreksi identitas ini disiapkan pada branch referensi.

## Backend: yang sudah siap dan yang masih menjadi dependensi

[Dokumen status](https://github.com/amarelyy/pakar/blob/e08e7b3e43251c63b13f35dce5b7e30e279d8638/docs/status-and-issues.md)
mencatat seed/migrasi Neon berhasil. Seed bukan lagi blocker yang belum ditulis.
Auth, guest search/alternate, dan API pengguna/review laporan tersedia.

Namun skema/API untuk arsitektur video belum dibuat: `SpotRoi`, status sumber
persisten, list/edit zona dan video source, ROI GET/PUT, frame referensi,
`GET /api/internal/video-sources`, dan FR17 override masih pekerjaan P1/P2.
[API reference](https://github.com/amarelyy/pakar/blob/e08e7b3e43251c63b13f35dce5b7e30e279d8638/docs/api-reference.md)
mendokumentasikan endpoint yang benar-benar ada sekarang; endpoint pada tabel
refactoring TODO adalah target, bukan bukti endpoint tersebut sudah tersedia.

Commit main terbaru hanya mengubah tiga dokumen; perubahan rencana arsitektur
belum sama dengan implementasi refactoring di source aplikasi.

## Urutan yang masuk akal untuk progres 17 November

1. Nabil: mulai P3, export/validasi ONNX dan engine .NET; gunakan baseline Python
   tanpa menganggap model .pt dapat dijalankan langsung dari C#.
2. Nabil/Brian bersama Deva: bagi halaman admin #19. Users/Reports dapat dikerjakan
   sekarang; jangan menunggu API zona/video source lengkap untuk dua halaman ini.
3. Peta #16 dan search #17 dapat berjalan dengan endpoint guest yang tersedia.
4. Ursula menyediakan P1/P2. Sesudah kontraknya tersedia, lanjut mapper/rules,
   worker dan editor SVG; lalu Deva menguji keseluruhan jalur #20.

Tanggal 17 November pada TODO adalah **pemeriksaan progres tim**. Dokumen tidak
menetapkan bahwa semua P3–P8 sudah harus lulus seluruh acceptance pada tanggal
tersebut. Capaian yang dilaporkan harus menyebut hasil yang benar-benar tersedia
beserta blocker, bukan menyatakan keseluruhan bagian Nabil sudah selesai.

## Batas tindakan audit ini

Audit ini tidak mengimplementasikan port C#/ONNX, mengubah aplikasi backend,
menjalankan migrasi Neon, atau menutup issue. Catatan Python dipertahankan sebagai
bukti historis; update tracking membedakan hasil referensi dari scope baru.
