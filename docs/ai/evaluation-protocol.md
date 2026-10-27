# Evaluasi rekaman nyata

Simpan rekaman di `edge/python-agent/assets/`. Jangan memakai animasi sebagai
bukti akurasi. Catat SHA-256, durasi, FPS, resolusi, kamera, waktu pengambilan,
kondisi cahaya, jenis kendaraan, serta izin penggunaan rekaman. Pisahkan clip
untuk tuning dari clip evaluasi final; jangan memilih threshold pada data final.

Kalibrasi ROI pada frame kamera yang sama, lalu cocokkan camera_id, area_id,
spot_id terhadap ExternalId database aktual. Simpan juga daftar slot yang tidak
terlihat. Dua ROI contoh bukan cakupan sepuluh slot zona H.

## Ground truth manual

Buat CSV UTF-8 dengan kolom berikut (header saja; isi berdasarkan video nyata):

```csv
spot_number,start_seconds,end_seconds,status
```

Satu baris untuk satu interval status slot. Waktu relatif terhadap awal video,
interval `[start,end)`, status `occupied`, `available`, atau `unknown` jika tidak
bisa dipastikan karena oklusi. Interval satu slot tidak boleh tumpang tindih.
Anotasi semua slot dan seluruh durasi yang hendak dinilai. Periksa ulang anotasi
secara manual, terutama transisi, motor, oklusi, dan perubahan pencahayaan.
Simpan catatan kondisi per clip; jalankan laporan terpisah untuk tiap kondisi.

Jalankan pipeline dengan `--events`, lalu dari root repo:

```powershell
py -X utf8 edge/python-agent/evaluate.py --events output/ai-validation/real-events.jsonl --labels edge/python-agent/assets/real-labels.csv --out output/ai-validation/real-evaluation.json
```

Evaluator menghasilkan confusion matrix agregat/per slot, precision/recall
occupied, accuracy, cakupan sampel, dan MAE jumlah okupansi pada frame dengan
semua slot anotasi terlihat serta diprediksi. MAE menghitung cakupan anotasi,
bukan otomatis seluruh zona. Frame rejected/missing prediction mengurangi
coverage; unknown dikecualikan. Accuracy hanya atas prediksi tersedia sehingga
wajib dibaca bersama coverage. Hasil berbobot sampel, bukan durasi. Waktu yang
tidak ada dalam journal (misalnya RTSP terputus) belum masuk denominator;
laporkan downtime terpisah. Metrik ini bukan detection mAP atau latensi UI.

## Integrasi #13 / #20

Gunakan database pengujian terpisah. Rekam initial snapshot, perubahan occupied
dan available, SQL row/Detection, event SignalR setelah JoinArea, dan UI aktual.
Putus HTTP lalu pulihkan: pastikan snapshot terbaru dikirim. Putus RTSP,
black/freeze nyata, serta scene statis sehat harus diuji terpisah. Ukur kamera
inactive setelah >120 detik pada tick watchdog berikutnya (15 detik); periksa
reaktivasi dan kasus kamera belum pernah mengirim data.

Untuk latensi <=5 detik gunakan jam tersinkron, timestamp pengambilan frame,
hasil inferensi, penerimaan backend, event hub, dan render UI. Timestamp payload
saat ini dibuat saat pengiriman, bukan pengambilan frame; replay dipercepat
tidak mengukur latensi tersebut. Laporkan p50/p95/maksimum dan jumlah sampel.

Model custom memerlukan file best.pt, hash, kelas, konfigurasi/dataset split,
serta provenance training. Periksa kontrak sebelum integrasi; label slot
empty/occupied belum didukung adapter vehicle-ROI saat ini.
