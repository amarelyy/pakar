# Diagnosis confidence rendah pada ROI v2 — 28 Oktober 2026

**Banyak box empty petak05 sebenarnya ada, tetapi confidence-nya di bawah0,25.**
Menurunkan confidence pada ROI v2 meningkatkan coverage clip ini secara nyata.
Karena itu retraining bukan langkah wajib berikutnya; uji konfigurasi confidence
lebih rendah di video lain layak didahulukan. Default runtime belum diubah.
Kesimpulan ini memperbarui dugaan peningkatan model dalam laporan ROI-only,
tanpa mengubah hasil historis laporan tersebut.

## Metode dan verifikasi

Satu inferensi baru best.pt pada290 frame/2FPS dari clip145s, CPU/imgsz640,
confidence keluaran0,05. Hasil pasca-NMS disimpan di cache baru terpisah.
Cache ini lalu disaring pada0,25/0,20/0,15/0,10/0,05 dengan ROI v2 yang sama.
MinIoU0,1, margin konflik0,1, debounce occupied2s/available3s tidak berubah.
CSV label tetap174 slot-sample/29 frame yang sama, belum review manusia.

Seluruh box>=0,25 cocok dengan cache inferensi lama (toleransi koordinat0,001px,
confidence0,00001; tidak ada frame berbeda). Hasil state/raw/unknown/transisi
conf0,25 cocok persis dengan replay v2 sebelumnya. Audit assignment independen
cocok dengan engine pada seluruh290 sampel untuk kelima threshold.
Jadi peningkatan di sini dapat ditelusuri ke box confidence rendah yang sekarang
diterima, bukan perubahan geometri, weights, atau debounce.

## Hasil pada label yang sama

| Confidence minimum | Benar /174 | Beda | Unknown | Coverage | Unknown05 /290 |
|---|---:|---:|---:|---:|---:|
| 0,25 | 155 | 1 | 18 | 89,66% | 151 |
| 0,20 | 163 | 1 | 10 | 94,25% | 84 |
| 0,15 | 168 | 1 | 5 | 97,13% | 40 |
| 0,10 | 170 | 1 | 3 | 98,28% | 12 |
| 0,05 | 171 | 1 | 2 | 98,85% | 4 |

Satu beda tetap VIDEO-04 pada65s: state masih occupied selama debounce,
berubah available66,5s pada semua threshold. Tidak ada tambahan kesalahan
state pada174 label jarang ini. Ini **bukan** bukti tidak ada false detection
pada frame lain atau seluruh area gambar.

VIDEO05 memulihkan67/111/139/147 sampel unknown pada confidence0,20/0,15/0,10/0,05,
tanpa kehilangan sampel05 yang sebelumnya terjawab pada0,25. Pada0,05 tetap4
unknown05, semuanya no_matching_detection. Namun efek lebih rendah tidak selalu
menguntungkan: VIDEO01 unknown naik dari1 sampel pada0,10 menjadi3 pada0,05
karena konflik empty/occupied (134/134,5/135s).

## Box yang diperiksa pada resolusi penuh

Frame overlay1280×720 diperiksa pada15,45,103,5,105,110s:

- **15s:** ada box empty petak05 confidence0,1523, center(589;508,5),
  IoU0,5673. Ada pula box empty0,2246 yang sempit dan bergeser ke kanan,
  center(642;520,5), dekat pejalan kaki/batas petak. Ini contoh mengapa jawaban
  yang cocok label belum membuktikan setiap box terlokalisasi baik. Pada0,15
  kedua box diterima, engine memakai confidence tertinggi per kelas.
- **45s:** box empty0,1165, center(591,5;510), IoU0,6336, terletak pada petak05.
  Ditolak0,25 maupun0,15, diterima0,10. Ini contoh jelas box benar ada tetapi
  terpotong threshold sebelumnya.
- **110s:** box empty0,1888, center(590,5;509,5), IoU0,6473, petak05.
  V2 pada0,25 unknown; pada0,15 available. Box occupied mobil belakang tetap
  di luar v2; bukan dipakai untuk menyelamatkan status05.
- **103,5s:** mobil bergerak melintasi bagian petak05 menghasilkan raw occupied
  confidence0,3278 bahkan pada baseline0,25. State masih available karena
  debounce. Frame ini tidak punya label CSV; tidak dihitung sebagai kesalahan
  atau jawaban benar di tabel. Jangan klaim seluruh clip selalu empty dari
  label yang hanya tiap5s.
- **105s:** mobil bergerak berada di belakang/dekat petak05 dan sebagian
  menutupi pandangan. Bahkan pada0,05 tidak ada box cocok05. Unknown tetap
  dipertahankan. Tidak disimpulkan bahwa semua sisa unknown disebabkan oklusi.

Di0,05 jumlah box tambahan di seluruh gambar juga lebih banyak, termasuk
lokalisasi yang meragukan. ROI dan debounce membantu menyaring pengaruhnya,
tetapi bukan pengganti validasi visual atau label yang independen.

## Keputusan

Ada bukti kuat threshold0,25 membuang banyak prediksi empty yang berguna pada
clip ini. Kandidat0,15 dan0,10 layak diuji berikutnya;0,05 dipakai sebagai batas
diagnosis, belum menjadi rekomendasi default. Gunakan clip lain yang belum
dipakai memilih threshold dan review manusia terutama empty/occupied, konflik,
mobil lewat, serta batas petak. Untuk praktikum, tidak perlu menambahkan syarat
Azure/RTSP/semua cuaca.

Belum perlu menyimpulkan harus retraining. Jika threshold kandidat tetap banyak
keliru/tidak stabil pada clip lain, barulah review dataset tim dan improvement
model. Evaluasi ini memilih threshold pada clip kalibrasi yang sama sehingga
tidak membuktikan generalisasi atau keunggulan terhadap COCO.

Default best.pt/conf0,25/referensi ROI launcher tetap seperti sebelumnya.
Backend/UI/demo.py dan label tidak diubah. Tidak ada HTTP/UI/training ulang.
Hasil baru dan checkpoint masih lokal, belum commit/push; tes25 lama tidak
diulang karena kode runtime tidak berubah.

## Bukti dan reproduksi

- [Bukti JSON](low-confidence-evidence.json): hash input/cache, kelima hasil,
  contoh box05, perubahan status dan seluruh ketidakcocokan label/unknown.
- [Runner](../../tests/ai/check_low_confidence.py): inferensi pertama, lalu reuse
  cache bila hash input sama; assertion memeriksa baseline dan assignment.
- Lokal `output/ai-validation/low-confidence/`: `detections-conf005.jsonl`,
  `cache-manifest.json`, `comparison.json`, `assignment-*.jsonl`,
  `observations-*.jsonl`, overlay PNG, dan `run-log.json`.

```powershell
py -X utf8 tests/ai/check_low_confidence.py
```

Lower-confidence cache tetap hasil pasca-NMS dengan floor0,05; tidak menjelaskan
prediksi di bawah0,05 atau box yang disupresi NMS. Output/binary diabaikan Git;
laporan, bukti ringkas, dan runner disiapkan di repo tanpa commit/push.
