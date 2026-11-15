# Validasi AI dan pengaman lokal — 16 November 2026

Pekerjaan ini menindaklanjuti pemeriksaan demo untuk progres 17 November. Ruang lingkupnya adalah pengujian inferensi, konsistensi data/peta, serta pemeriksaan keamanan lokal terarah. Hak penggunaan rekaman tidak ditinjau ulang sesuai arahan pengguna. Integrasi bersama tim dan deployment ditunda. Tidak ada perubahan model, pelatihan baru, perubahan database bersama, commit, push, atau deployment dalam pemeriksaan ini.

## Hasil dan batas pemakaian

Demo lokal dapat digunakan untuk **mobil, rekaman siang hari, dan ROI yang telah dikonfigurasi**. Ini bukan bukti keandalan pada motor, malam, hujan, atau lokasi kampus yang sesungguhnya. Tampilan peta sekarang menyatakan batas demo mobil/siang hari.

Empat zona masih memakai empat potongan dari satu rekaman sumber, dengan 17 ROI dan kapasitas 6/5/3/3. Potongan tersebut memutar ulang dua peristiwa keberangkatan nyata; bukan empat lokasi atau empat peristiwa independen. Pengujian fungsional empat sumber berhasil, sedangkan evaluasi AI menghindari menghitung ulang potongan yang saling tumpang tindih sebagai dataset independen.

## Evaluasi inferensi

Evaluasi memakai runtime C# ONNX, pemetaan ROI, debounce, dan pemeriksaan kualitas gambar yang sama dengan worker. Dua interval demo disampel 2 FPS; dua rekaman pembanding disampel 1 FPS. Label interval dibuat melalui pemeriksaan visual oleh agen, belum ditinjau manusia secara independen. Interval gerakan/oklusi yang ambigu dikecualikan secara eksplisit sebelum hasil diukur. Satu sampel berarti satu status slot pada satu waktu, bukan satu kendaraan unik.

| Kasus | Frame | Sampel slot berlabel | Cocok | Salah diterbitkan | Belum diketahui | Cakupan keputusan |
|---|---:|---:|---:|---:|---:|---:|
| Demo awal, 145 detik | 290 | 1.686 | 1.671 | 0 | 15 | 99,11% |
| Demo akhir, 55 detik | 110 | 318 | 313 | 0 | 5 | 98,43% |
| Pembanding CHAD depan | 281 | 1.405 | 1.183 | 0 | 222 | 84,20% |
| Pembanding CHAD miring | 251 | 753 | 503 | 0 | 250 | 66,80% |

Gabungan dua interval demo: **2.004 sampel, 1.984 cocok, 20 belum diketahui, dan tidak ada status kosong keliru yang diterbitkan pada sampel berlabel yang diuji**. Cakupan 99,00% adalah hasil pada rekaman demo yang sebelumnya dipakai menyetel ROI, bukan estimasi akurasi umum. Inferensi mentah sempat menghasilkan satu status kosong keliru; debounce menahannya sebagai belum diketahui. Interval ambigu tetap di luar penyebut.

Kriteria pemeriksaan demo adalah tidak ada status kosong keliru yang diterbitkan dan cakupan keputusan minimal 95%. Kedua interval demo memenuhi kriteria ini; kedua pembanding gagal pada cakupan. Pembanding miring hanya memiliki slot kosong dan tidak membuktikan kemampuan mendeteksi kendaraan terisi. Hubungan rekaman pembanding dengan dataset pelatihan tidak diketahui; hasil ini bukan benchmark independen.

Uji stres menggelapkan dua interval demo menjadi 8% tingkat terang: **400 dari 400 frame ditolak**, sehingga 2.004 sampel slot menjadi belum diketahui dan tidak dihitung kosong. Ini menguji perilaku penolakan, bukan validasi malam nyata. Rekaman berlabel motor, hujan, malam, dan kondisi kampus belum tersedia dalam evaluasi ini.

Spesifikasi, label, ROI, hash aset/model, dan hasil terukur tersedia di:

- [occupancy-evaluation-spec.json](occupancy-evaluation-spec.json)
- [occupancy-evaluation-assets.json](occupancy-evaluation-assets.json)
- [occupancy-evaluation-results.json](occupancy-evaluation-results.json)

## Perbaikan yang diterapkan

- Worker menolak gambar hampir hitam atau tanpa detail sebagai belum diketahui. Pemeriksaan ini konservatif dan tidak menjamin semua gambar berkualitas buruk akan terdeteksi.
- Publikasi gambar dan metadata terbaru diserialkan, sehingga frame terlambat tidak dapat menggantikan hasil yang lebih baru. Status pending tetap belum diketahui.
- Perubahan ROI/sumber video serta penghapusan kamera segera membatalkan gambar dan pengamatan lama. UI menghapus preview ketika API tidak lagi menyediakan gambar yang sah.
- Payload frame diperiksa sampai struktur bersarang, nilai numerik, identitas slot/ROI, geometri, nama file, format/dimensi JPEG, waktu, dan ukuran badan permintaan. Batas ukuran berlaku juga untuk permintaan chunked.
- Ingest lama sekarang memeriksa kamera, zona, ID/nama slot, status, confidence, waktu, duplikasi, dan data null. Payload lintas zona atau slot yang tidak terdaftar ditolak.
- Gambar tetap khusus admin; endpoint internal memerlukan API key. Metadata publik tidak memuat JPEG, nama file video, atau polygon.

## Bukti pengujian lokal

- Build solusi: **0 warning, 0 error**. Total tes otomatis **135 lulus**: 100 API/domain/application, 25 inference/worker, 10 web. Setelah build solusi dan 134 tes lulus, satu tes konkurensi publikasi ditambahkan dan suite API dijalankan ulang: 100 lulus. Tidak ada perubahan runtime setelah build terakhir.
- Pemeriksaan HTTP: tamu 401, pengguna biasa 403, admin 200 pada frame privat; signature token palsu 401; API key wajib; frame lama/sumber salah 409; input cacat 400; payload terlalu besar termasuk chunked 413.
- Pemeriksaan perubahan ROI: gambar lama langsung hilang, slot berubah menjadi belum diketahui, dan frame lama tidak dapat mengembalikan konfigurasi sebelumnya.
- Uji gambar hitam dan abu-abu polos pada worker nyata: seluruh ROI menjadi belum diketahui dan tidak dihitung kosong; sumber asli dikembalikan setelah pengujian.
- Uji akhir browser/API pada build terbaru: 17 slot/kapasitas selaras, empat frame worker dengan ROI tampil, dan semua empat sumber memperlihatkan occupied/available dari perubahan video nyata. Pengamatan browser 160 detik membuktikan perubahan peta tanpa reload.
- Saat worker dihentikan terkontrol, semua 17 slot kedaluwarsa menjadi belum diketahui, pencarian tempat kosong tidak mengembalikan zona, preview diberi label data lama, lalu pembacaan pulih setelah worker dinyalakan kembali.

Ringkasan pengaman yang disanitasi: [local-security-results.json](local-security-results.json). Ini pemeriksaan lokal terarah, bukan audit keamanan menyeluruh atau persetujuan produksi.

## Reproduksi dan lingkungan

Runtime terakhir: `artifacts/local-audit-verified`. Database sandbox terisolasi: `pakar_ui_20261115_v2`. Web/API/worker dibiarkan berjalan pada loopback port **5379 / 5359 / 5369**. Kredensial dan PID tetap berada di file sesi yang diabaikan Git; tidak dimasukkan ke laporan.

CLI evaluasi tersedia di `tools/Pakar.AiValidation/RoiEvaluation.cs`:

```powershell
dotnet build tools/Pakar.AiValidation --artifacts-path artifacts/ai-eval-build
dotnet artifacts/validation/debug/Pakar.AiValidation.dll --evaluate-roi artifacts/models/best.onnx docs/ai/occupancy-evaluation-spec.json artifacts/ai-eval/reproduced
```

Proyek validasi mengatur output executable sendiri ke `artifacts/validation/debug`. Semua video/model dalam spesifikasi harus tersedia lokal dengan hash yang sesuai; media tidak ditambahkan ke Git. Kode keluar nol berarti pengukuran berhasil dijalankan, bukan seluruh kasus memenuhi kriteria.

Pemeriksaan keamanan yang dapat diulang: `tests/ai/validate_local_security.py`. Skrip hanya menerima sandbox loopback tersebut, menghentikan worker yang PID-nya tercatat, menguji konfigurasi sementara, lalu memulihkan sumber dan worker; jalankan terpisah dari uji outage/browser lain. Dependensi utilitas offline: Python, psutil, OpenCV, NumPy. Runtime aplikasi tetap .NET/Blazor/OpenCvSharp/ONNX; PostgreSQL tetap database aplikasi.

Bukti rinci yang diabaikan Git: `artifacts/ai-eval/results/*.jsonl`, `artifacts/ai-eval/security-local.json`, `artifacts/roi-demo/accept-fixed.json`, `fixed-live-samples.json`, serta tangkapan layar worker/peta. Data hasil lokal terakhir dibuat 16 November 2026, Asia/Jakarta.

Untuk memperluas klaim ke motor, cuaca buruk, malam, atau kampus nyata, diperlukan rekaman berlabel yang mewakili kondisi tersebut dan evaluasi terpisah; model atau data pelatihan mungkin perlu diperbaiki berdasarkan hasilnya. Mengganti label zona atau menambahkan efek gelap tidak menggantikan bukti tersebut.
