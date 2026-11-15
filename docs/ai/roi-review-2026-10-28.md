# Diagnosis ROI dan kalibrasi v2 — 28 Oktober 2026

ROI lama memang melintasi garis pemisah dua baris parkir. ROI v2 terpisah
membatasi satu petak depan per polygon. Namun koreksi geometri saja **belum
cukup** mengatasi unknown VIDEO-05. Tidak ada training ulang atau perubahan
weights, threshold, aturan assignment, maupun debounce dalam perbandingan ini.

## Identitas fisik dan geometri

Frame asli 1280×720 diperiksa pada 0, 15, 65, 100, 110, 135, 140 detik clip.
Garis melintang sekitar y472–510 memisahkan dua baris yang berhadapan.
ROI lama mulai sekitar y422–465 sehingga mencakup bagian baris belakang;
ROI v2 dimulai pada garis pemisah dan berakhir di ujung garis petak depan.
Titik tetap perkiraan manual dari citra perspektif, bukan survei kalibrasi.
Bagian garis tertutup kendaraan diinterpolasi dari segmen yang terlihat.

Identitas VIDEO-01..06 tetap urutan enam petak depan dari kiri ke kanan:

| Slot | Referensi fisik pada awal clip | Status referensi |
|---|---|---|
| VIDEO-01 | SUV gelap di kiri, bukan mobil perak di belakangnya | occupied |
| VIDEO-02 | sedan abu-abu menghadap kamera, bukan mobil gelap di belakang | occupied |
| VIDEO-03 | mobil hitam berikutnya | occupied |
| VIDEO-04 | sedan gelap, kemudian keluar sebelum sampel65s | occupied lalu available |
| VIDEO-05 | petak kosong di antara sedan tadi dan sedan putih | available |
| VIDEO-06 | sedan putih, bukan SUV gelap di belakangnya | occupied |

Koordinat pixel v2, berurutan mulai kiri-atas (resolusi1280×720):

| Slot | Empat titik polygon |
|---|---|
| 01 | (130,510), (232,507), (233,560), (117,564) |
| 02 | (232,507), (332,504), (351,556), (233,560) |
| 03 | (332,504), (430,500), (469,550), (351,556) |
| 04 | (430,500), (525,493), (576,543), (469,550) |
| 05 | (525,493), (625,481), (676,535), (576,543) |
| 06 | (625,481), (723,472), (779,520), (676,535) |

Label CSV tetap utuh; pemeriksaan ini mendukung identitas slot dan perubahan04,
bukan review ulang seluruh29 frame atau penetapan ground truth manusia.
Jangan mengubah label agar cocok dengan model. Baik label maupun v2 masih
memerlukan review manusia bila akan dipakai sebagai benchmark resmi.

## Perbandingan terkendali

Replay290 sampel dari `custom-model/detections.jsonl` yang sama, tanpa YOLO baru.
Confidence0,25, minIoU0,1, margin konflik0,1, debounce occupied2s/available3s.
Label174 slot-sample/29 frame sama. Hash cache/ROI/video/label disimpan di
[bukti JSON](roi-review-evidence.json). Replay ROI lama cocok **persis** dengan
journal lama untuk seluruh state/raw/unknown/perubahan pada290 sampel.
Audit assignment independen juga cocok dengan engine untuk kedua ROI.

| Metrik | ROI lama | ROI v2 |
|---|---:|---:|
| Benar /174 | 156 | 155 |
| Berbeda | 1 | 1 |
| Unknown | 17 | 18 |
| Coverage | 90,23% | 89,66% |
| Benar / semua174 | 89,66% | 89,08% |
| Accuracy hanya jawaban tersedia | 99,36% (156/157) | 99,36% (155/156) |
| VIDEO-05 unknown /290 | 134 | 151 |

Satu perbedaan tetap VIDEO-04 pada65s, dengan state baru available66,5s karena
debounce. V2 memulihkan4 sampel VIDEO-05 yang sebelumnya unknown tetapi membuat
21 sampel lain menjadi unknown; tidak boleh melaporkan hanya4 pemulihan.
Pada label jarang, VIDEO-04 bertambah1 jawaban benar dan VIDEO-05 berkurang2.

## Mengapa ada box empty tetapi status unknown?

Audit lokal `assignment-old.jsonl` dan `assignment-v2.jsonl` memuat seluruh
pasangan deteksi/slot: confidence, xyxy, center setelah integer clipping,
center_inside, intersection_pixels, IoU, reason (`center_outside`,
`iou_below_min`, `below_conf`, `assigned_other_slot`, `assigned`). `hits`
menyimpan confidence tertinggi per kelas, dan unknown serta pending debounce
dicatat terpisah. Contoh ringkas terpilih juga masuk bukti JSON yang disiapkan untuk Git (belum commit).

Untuk134 unknown VIDEO-05 pada ROI lama:

- 101 tidak punya box empty cached yang beririsan dengan ROI lama.
- 29 punya box empty beririsan tetapi pusatnya di luar ROI.
- 4 mengalami konflik empty/occupied.
- Tidak ada kasus yang terklasifikasi sebagai penolakan minIoU saja.

Kategori ini diagnostik geometri, **bukan130+ deteksi petak05 yang pasti bisa
diselamatkan**. Box beririsan sedikit bisa milik petak04 atau baris belakang.
130 dari134 unknown lama tidak punya pusat box cached kelas apa pun di dalam
petak05 fisik v2. Seluruh151 unknown05 pada v2 juga tidak punya pusat box cached
di petak05. Jadi memperpanjang ROI untuk meraih box tetangga bukan perbaikan.

Contoh yang dapat diperiksa:

- **0s:** empty0,3047, center(596,5;510), benar-benar petak05. IoU lama0,4832,
  v2 0,6657; kedua ROI menerima. Ini bukti model bisa mendeteksi petak kosong05.
- **15s:** tidak ada box empty cached di petak05. Box empty yang tampak di
  kanan adalah petak lain. Kedua ROI unknown05.
- **65s:** box empty0,6412, center(489;518,5), milik petak04 yang baru kosong.
  Sedikit irisan dengan v2 petak05 (39pixel, IoU0,0035) bukan bukti deteksi05.
- **110s:** box occupied0,5368 dari mobil **belakang**, center(535,5;448,5),
  diterima ROI lama05 (IoU0,2103). V2 menolaknya. State lama05 masih available,
  walaupun raw occupied; debounce menahan perubahan. Box empty0,6722 dengan
  center(497,5;519,5) adalah petak04. V2 jujur unknown05 pada frame ini.
- **135s:** box empty0,2673, center(589,5;510,5), benar petak05; kedua ROI
  menerima. Box empty0,7088 yang lebih tinggi confidence milik petak04.

ROI lama menerima66 box untuk05 yang pusatnya di luar petak05 v2 sepanjang
clip (bisa beberapa box per frame). Ini bukti kontaminasi mapping, bukan66
kesalahan state: sebagian tertekan konflik/debounce dan state bisa kebetulan
cocok label. Contoh110s membuktikan mengapa coverage/state saja tidak cukup.

Cache sudah disaring YOLO pada confidence0,25. Kita **tidak bisa** membedakan
box hilang, confidence di bawah0,25, atau suppression dari cache ini saja.
Pending debounce bukan unknown: bila masih ada evidence valid, engine dapat
mengembalikan state sebelumnya selama menunggu perubahan. Unknown menghilangkan
slot dari snapshot HTTP; UI tetap last-known sesuai batasan implementasi lama.

## Keputusan dan langkah berikutnya

Kalibrasi diperlukan untuk identitas petak yang benar, tetapi hasil ini menolak
kesimpulan bahwa memperbaiki ROI saja akan menyelesaikan unknown. Model custom
masih perlu peningkatan konsistensi deteksi petak kosong, atau eksperimen
assignment terpisah yang divalidasi secara fisik. **Retraining belum terbukti
sebagai satu-satunya solusi.** Sebelum memutuskan training, inspeksi inferensi
confidence lebih rendah pada petak05 sebagai eksperimen berikut yang diberi
label terpisah; jangan mencampurnya dengan hasil ROI-only ini. Bila box petak05
tetap hilang atau tidak stabil, prioritaskan contoh empty dari perspektif ini,
review label/dataset tim, lalu evaluasi model di clip lain.

V2 disimpan sebagai kandidat kalibrasi fisik terpisah di
[real-video-demo-v2.json](../../edge/python-agent/configs/real-video-demo-v2.json).
ROI lama dipertahankan; referensi launcher/default belum dialihkan karena v2
belum direview manusia dan coverage belum meningkat. Best.pt tetap default.
Tidak ada perubahan backend/UI/demo.py, inference ulang145s, atau tes integrasi
baru. Jalur webcam browser/C#/ONNX tetap TODO integrasi tim.

## Reproduksi dan bukti lokal

```powershell
py -X utf8 tests/ai/review_cached_roi.py
```

Runner ini selesai sekitar5s dan mengassert replay lama serta setiap hasil
assignment audit terhadap engine; tidak mengulang25 tes atau inferensi panjang.
Output di `output/ai-validation/roi-review/`: comparison.json, assignment-*.jsonl,
observations-*.jsonl, raw-*.png, old-*.png, v2-*.png (tujuh timestamp di atas).
PNG1280×720 mempertahankan resolusi penuh. Output/binary tetap diabaikan Git.
Ringkasan/hash/contoh dan script reproduksi disimpan di repo, belum commit/push. Evaluasi menggunakan
clip kalibrasi yang sama, bukan bukti generalisasi atau keunggulan atas COCO.
