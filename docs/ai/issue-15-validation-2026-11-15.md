# Issue #15 — ROI mapping dan occupancy counting

Tanggal: 15 November 2026. Scope: komponen AI Python pada branch `AI-PAKAR`,
enam ROI demo pada rekaman nyata 145 detik. Integrasi dengan backend PostgreSQL
terbaru, SignalR/UI, RTSP nyata, editor ROI dalam dashboard, dan port C#/ONNX
tetap merupakan pekerjaan integrasi/fitur lain. Laporan ini tidak menyatakan
semua FR sistem sudah lulus atau detektor selalu akurat.

## Checklist task asli

- [x] Confidence thresholding dan NMS: inferensi custom memakai confidence 0,10;
  class-aware NMS IoU 0,7 diteruskan eksplisit ke Ultralytics. Test memakai fungsi
  NMS aktual untuk membuktikan duplicate sekelas dan confidence rendah dibuang,
  sedangkan kelas berbeda tetap tersedia untuk pemeriksaan konflik.
- [x] Pusat box dipetakan ke ROI yang ditentukan operator/admin: polygon JSON
  ternormalisasi 0..1, point-in-polygon, assignment IoU minimal 0,1, satu box
  maksimal satu slot. `roi_tool.py` yang sudah ada dapat membuat konfigurasi.
  Nama dan ID spot harus unik; nama duplikat kini ditolak agar dua slot tidak
  diam-diam tergabung ketika dihitung.
- [x] Hitungan zona dan status: `available`, `almost_full` pada okupansi >=90%,
  dan `full` untuk observasi lengkap yang sudah stabil. Missing/conflicting
  detection atau kandidat transisi yang masih pending menghasilkan `unknown`
  serta rentang jumlah; tidak dianggap slot kosong atau angka pasti.
- [x] Event jumlah zona hanya pada perubahan jumlah yang sudah terkonfirmasi:
  `zone_snapshot` untuk baseline lengkap pertama; `zone_count_changed` untuk
  perubahan occupied berikutnya. Perubahan per-slot tetap diteruskan meskipun
  jumlah zona tetap, agar perpindahan A ke B tidak membuat peta ketinggalan.
- [x] Fungsi counting diuji pada video nyata: inferensi baru 290 frame, evaluasi
  29 frame berlabel pada enam slot, hasil per-frame dan batasan dicatat di bawah.
  Acceptance fungsional ini menguji angka pasti ketika lengkap/stabil serta
  rentang eksplisit ketika belum pasti; bukan klaim angka tepat di setiap frame.

## Perubahan kode

`edge/python-agent/zone_occupancy.py` menghitung status/rentang dan event jumlah
secara terpisah dari transisi per-slot. `slot_labels.py` dan `occupancy.py`
mengekspos jumlah kandidat masuk/keluar yang masih menunggu debounce.

Contoh: dua slot terisi, satu tampak kosong tetapi belum melewati debounce.
Status slot tetap stabil sementara zona menampilkan rentang 1–2 occupied,
`pending=1`, `complete=false`. Sesudah konfirmasi, zona menjadi tepat satu
occupied dan mengeluarkan satu event perubahan. Jika kandidat dibatalkan atau
dua slot bertukar status dengan jumlah tetap, tidak ada event jumlah palsu.

Pada frame unknown, `occupiedMin`/`occupiedMax` merupakan rentang yang konsisten
dengan status yang diketahui dan kandidat transisi, bukan interval kepercayaan
statistik terhadap seluruh kesalahan model. Salah klasifikasi yang tidak
terdeteksi tetap dapat menghasilkan angka salah pada rekaman lain.

Kontrak HTTP `cameraId`, `areaId`, `type`, `timestamp`, `spots` dipertahankan.
Event jumlah zona AI ada dalam journal `--events`; integrasi event ini dengan
SignalR backend terbaru termasuk #20. Perubahan ini tidak mengubah broadcast
`ZoneUpdated` backend utama atau memasukkan patch backend/UI lokal ke commit AI.

## Pengujian

- **47 pytest lulus**, termasuk NMS aktual, filter confidence, ROI center,
  nama slot ambigu, masuk/keluar, debounce, kandidat batal, unknown/recovery,
  pertukaran dua slot dengan total tetap, dan bentuk payload HTTP yang kompatibel.
- Inferensi baru memakai `best.pt`, kelas empty/occupied, confidence 0,10,
  NMS class-aware 0,7, ROI v2, assignment IoU 0,1, margin konflik 0,1,
  debounce occupied 2 detik / available 3 detik, sampling 2 FPS.
- Model, ROI, threshold assignment, dan CSV label lama tidak diubah untuk
  meningkatkan skor. Perbaikan utamanya adalah cara menyatakan ketidakpastian
  serta membedakan perubahan slot dan jumlah zona.

| Ukuran | Hasil pada 29 frame berlabel / enam slot |
| --- | --- |
| Status slot cocok / berbeda / unknown | 170 / 1 / 3 dari 174 slot-sample |
| Coverage status slot | 98,28% |
| Hitungan pasti, lengkap dan tidak pending | 25 frame; seluruhnya cocok |
| Hitungan ditandai pending | 1 frame, detik 65; rentang 4–5 |
| Hitungan ditandai unknown | 3 frame, detik 105, 115, 135 |
| Frame yang jumlah manualnya berada dalam rentang keluaran | 29 dari 29 frame berlabel |
| Event perubahan jumlah zona | 1 event, 5 -> 4 pada detik 66,5 |

Metrik lama terhadap **status slot yang distabilkan** tetap dilaporkan:
26 frame memiliki semua status slot, 25 hitungannya cocok dan satu selisih satu
kendaraan, MAE 1/26 = 0,03846. Frame detik 65 tidak dihapus dari metrik tersebut.
Ringkasan zona baru mengenali transisinya sebagai pending dan menampilkan
rentang, sehingga tidak menyajikan angka lima sebagai jumlah pasti saat itu.
Coverage hitungan pasti baru adalah 25/29 = 86,21%; jangan menggantinya dengan
coverage status slot 98,28% atau menyebut detektor akurat 100%.

## Review empat kasus bermasalah

| Waktu | Bukti review | Penanganan |
| --- | --- | --- |
| 65 s, VIDEO-04 | Frame 63 s masih menunjukkan mobil; 64 s petak sudah kosong. Raw empty mulai 63,5 s; saat 65 s baru bertahan 1,5 s dari syarat 3 s. | Pending, rentang 4–5. Konfirmasi available dan event 5 -> 4 pada 66,5 s. Debounce tidak dipendekkan untuk mengejar skor. |
| 105 s, VIDEO-05 | Petak kosong terlihat, tetapi tidak ada box di atas threshold yang memenuhi pusat dan IoU petak. Box occupied sekitar 0,184 memiliki pusat di luar ROI dan berasal dari area belakang. | Unknown, rentang 4–5. ROI tidak diperluas agar box area belakang ikut terhitung. |
| 115 s, VIDEO-05 | Petak tampak kosong; box yang overlap memiliki pusat di luar petak, bukan bukti empty yang dapat diterima. | Unknown, rentang 4–5. Tidak menganggap ketidakadaan box sebagai kosong. |
| 135 s, VIDEO-01 | Mobil masih di petak, pejalan kaki melintas di depannya. Ada occupied 0,375 dan empty 0,333 yang keduanya memenuhi mapping; selisih 0,043 < margin 0,1. | Unknown, rentang 3–4. Tidak memaksa salah satu label hanya supaya cocok dengan CSV. |

Review visual dilakukan Codex pada frame asli dan overlay. Ini pemeriksaan
ulang oleh agen yang sama, bukan review manusia atau anotator independen.
Tidak ada bukti bahwa retraining diperlukan untuk memperbaiki logika event.
Meningkatkan konsistensi deteksi pada oklusi/kamera lain tetap pekerjaan model
lanjutan dan memerlukan data evaluasi lain.

## Reproduksi dan artefak

Dari root repo, dengan assets lokal yang sesuai:

```powershell
py -X utf8 -m pytest tests/ai -q --junitxml=output/ai-validation/issue15-2026-11-15/pytest.xml
py -X utf8 tests/ai/validate_issue15.py
```

Output lokal di `output/ai-validation/issue15-2026-11-15/`:

- `events.jsonl`, `summary.json`, `evidence.json`, `pytest.xml`.
- `overlay.mp4`: tampilan jumlah/status zona termasuk unknown dan pending.
- `review-*-original.png` dan `review-*-overlay.png`: frame temporal dan kasus bermasalah.

[Bukti ringkas yang dilacak Git](issue-15-evidence-2026-11-15.json) memuat hash
input, parameter, hasil, dan kasus per-frame. Model/video/output binary tidak
dimasukkan ke Git. Link distribusi assets masih perlu dilengkapi terpisah.

## Batas integrasi

- Kapasitas enam ROI adalah scope demo, bukan seluruh kendaraan dalam gambar
  atau kapasitas zona kampus yang sebenarnya.
- Sampel evaluasi jarang dan berasal dari clip yang juga dipakai memilih
  threshold; bukan held-out accuracy, detection mAP, atau bukti generalisasi.
- ROI JSON adalah konfigurasi operator/admin komponen ini. Penyimpanan ROI di
  DB dan editor SVG dashboard memerlukan fitur tambahan sesuai TODO tim.
- Belum ada uji baru RTSP, PostgreSQL, SignalR/UI, atau latency kamera-ke-UI <=5 s.
- Backend `main` perlu memakai ExternalId yang sesuai dan menyelaraskan
  perlakuan unknown/pending jika ingin menampilkan ringkasan zona baru ini.
- Integrasi arsitektur Python versus jalur C#/ONNX/FrameHub tetap perlu
  diselaraskan bersama tim; laporan ini tidak mengklaim jalur C# sudah dibuat.
