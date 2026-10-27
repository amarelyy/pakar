# Konteks AI PAKAR — status terkini 28 Oktober 2026

Dokumen ini menggantikan ringkasan sesi lama yang bertumpuk. Laporan pengujian
bertanggal tetap disimpan sebagai bukti historis; jangan menganggap hasil lama
sebagai kondisi runtime terbaru.

## Pengguna, scope, dan repo

Nabil/Brian (Nabil Abrian Aryo Prabowo), AI engineer PAKAR. Proyek adalah
praktikum/latihan membuat aplikasi, bukan layanan parkir sehari-hari. Fokus
kontribusi pekan ini: program memproses MP4 dengan YOLO untuk mendeteksi status
slot, pemetaan ROI, debounce dan penghitungan. Improve model adalah lanjutan;
RTSP FT UGM/Azure/semua cuaca bukan blocker praktikum. Jangan mengklaim semua
TODO tim selesai: branch533008 meminta webcam browser/FrameHub/C#/ONNX/ROI DB,
sedangkan implementasi kita Python dan ROI JSON. Keputusan arsitektur tim belum
mengubah implementasi ini.

Repo https://github.com/amarelyy/pakar ; checkout existing:
C:\Users\Nabil Abrian Aryo P\OneDrive\Dokumen\ChatGPT\Pakar - Senpro\pakar
Workspace induk punya repo Git sendiri. Jalankan semua Git dari folder pakar.

Branch aktif AI-PAKAR, telah dipush ke origin:
- db35f30: kode/tes/konfigurasi/dokumentasi AI awal.
- 945726b: default best.pt dan adapter slot eksplisit; HEAD terakhir terverifikasi.
Tidak ada PR/merge/deploy/penutupan issue yang dilakukan.

Perubahan pendukung aplikasi tetap BELUM di-commit: README bagian demo,
src/Pakar.Api, src/Pakar.Infrastructure, src/Pakar.Web, demo.py dan scoped CSS
CampusMap. Jangan reset/stash/hapus atau ikutkan ke commit AI tanpa meninjau.
Backend lokal lama memakai SQLite; branch tim533008 sudah SQL Server dan
migrasinya berbeda. Jangan cherry-pick seluruh perubahan backend/UI ke sana.
Dokumen ringkasan sesi ini juga baru disimpan lokal, belum commit/push.

## Model yang sekarang dipakai

Default pipeline.py adalah edge/python-agent/assets/best.pt, kelas empty dan
occupied, task detect. Mode auto memilih adapter slot_labels.py. File berasal
Downloads/pakarmodelsenpro.pt dan pakarmodel1.pt yang identik. Best.pt adalah
nama salinan lokal; tidak membuktikan pemilihan epoch terbaik.
SHA256: 1b5b8f3b662146c5259710f83c0eb198bb982f96f815c6d59e9e2681a99c5272.
Metadata menyebut base yolov8n.pt, epochs argument50, imgsz640, dataset
/content/Parking-Space-1/data.yaml; checkpoint epoch=-1. Pengguna menjelaskan
model hasil retraining tim. Metadata diperiksa, tetapi dataset/split, riwayat
training dan mAP training belum direproduksi. Jangan klaim training ulang oleh
Codex. YOLOv8n COCO masih tersedia sebagai baseline opsional melalui --model dan
--mode vehicles; bukan default lagi.

Weights/video tidak di Git. Link Drive masih PLACEHOLDER di
edge/python-agent/ASSETS.md; jangan mengklaim sudah diupload ke Drive. File
lokal assets/: best.pt, yolov8n.pt, parking-demo.mp4, sample_parkir.mp4.
Sample_parkir adalah animasi rectangle CAR, bukan CCTV/evaluasi model nyata.

## Alur dan unknown

MP4/stream -> Python YOLO -> ROI -> debounce2s occupied/3s available -> HTTP POST
/api/ingest/detections dengan X-Api-Key -> backend -> SignalR /hubs/parking -> UI.
Tidak ada HTTP tanpa backend-url. File replay berhenti EOF; --replay-speed2
untuk2x,0 untuk accelerated. Frame health/reconnect/retry tetap ada.

Adapter custom memetakan pusat box ke satu polygon, minIoU0,1, confidence0,10
sejak pemilihan terbaru (hasil historis0,25 tetap dicatat di bawah).
Dua label berlawanan dengan gap confidence<0,1 -> unknown. Tidak ada box cocok
juga unknown, bukan otomatis available atau occupied. Unknown mereset kandidat
pending, journal/count memuat unknown; snapshot HTTP hanya slot teramati.
Backend/UI mempertahankan last-known slot yang tidak dikirim. UI belum menulis
unknown per slot, sehingga tampilan last-known bukan bukti deteksi baru yang
pasti. Jika semua slot unknown, tidak ada heartbeat.

## Video, ROI dan bukti

Input video nyata user: Downloads/YTDown.com_YouTube_Media_U7HRKjlXK-Y_BLK-HDPTZ12-
Security-Camera-Parkng-Lot-Surveillance-Video_002_720p.mp4. Durasi316,13s,
1280x720 30FPS, ada pan/zoom. Clip stabil sumber5..150s =145s tersedia sebagai
assets/parking-demo.mp4, SHA256
bab6a16d4f72231ca352715797d8035a669fc8714a70a2acdba1a64c45cca96e.
Waktu clip dimulai0; source time = clip+5s.

Config configs/real-video-demo.json: camera/area9001, spot9001..9006,
VIDEO-01..06, enam polygon manual. Ini demo, bukan ExternalId FT UGM.
ROI bukan hasil deteksi otomatis. Model membaca seluruh frame, lalu hasil
box dipetakan ke enam slot itu. Polygons tambahan diperlukan untuk slot lain.

Anotasi evaluasi berbeda dari ROI: docs/ai/real-video-sampled-labels.csv adalah
label visual Codex untuk29 frame tiap5s pada enam slot (174 slot-sample), belum
review manusia, bukan label training atau ground truth kontinu.

Hasil perbandingan pada label yang sama:
- COCO:172 cocok,2 beda,0 unknown dari174.
- Custom conf0,25:156 cocok,1 beda,17 unknown; coverage90,23%.
- Accuracy custom99,36% hanya pada157 jawaban tersedia, BUKAN seluruh174.
  Jangan menyebut model custom lebih unggul dari COCO berdasarkan angka itu.
- Custom conf0,35:146 cocok,1 beda,27 unknown; coverage84,48%.
- VIDEO04 berubah available pada66,5s waktu clip. Satu beda65s terkait debounce.
- Banyak unknown VIDEO05 (134 dari290 sampel keseluruhan); sebabnya belum
  dipisahkan antara model, perspektif, kalibrasi ROI, confidence dan konflik.
25 tes otomatis lulus, termasuk adapter unknown dan HTTP snapshot parsial.
Satu putaran custom default145s/290sampel selesai:6 payload HTTP diterima,
0 gagal,7 Detection tersimpan, final4occupied/2available/0unknown.
Browser live update sudah pernah diuji pada COCO; belum diulang setelah switch
custom. Tidak ada pengukuran latency kamera-ke-UI <=5s.

Bukti: docs/ai/custom-model-validation-2026-10-28.md, custom-model-evidence.json,
best-default-2026-10-28.md, best-default-evidence.json, validation-evidence.json.
Output lokal di output/ai-validation/custom-model/: overlay video/frame,
detections.jsonl, observations*.jsonl, comparison.json, custom-evaluation.json.
Bukti runtime custom default di output/ai-validation/demo/20261028-035241-f7de34/.

## Checkpoint TERBARU: dipilih0,10 dan ROI v2, 28 Oktober 2026

User meminta memilih0,15 vs0,10 lalu menyimpan ke branch existing AI-PAKAR.
Dipilih0,10 untuk demo clip:170benar/1beda/3unknown coverage98,28%, dibanding
0,15:168/1/5 coverage97,13%. Unknown05 12 vs40 dari290frame. Tambahan box
umumnya sesuai petak05; ada box meragukan dekat pejalan kaki dan raw empty01
134,5s yang tertahan debounce. Bukan bukti generalisasi di kamera lain.

Baca docs/ai/selected-confidence-2026-10-28.md dan selected-confidence-evidence.json.
Default YOLODetector sekarang berdasarkan kelas model: custom empty/occupied0,10,
COCO tetap0,25. Pipeline/SlotOccupancyAdapter default assignment slots0,10;
vehicles tetap0,35. Argumen eksplisit tetap dapat override. Low-level
SlotLabelEngine tetap0,25 untuk reproduksi eksperimen historis; pipeline selalu
memberi threshold efektif secara eksplisit. Debounce/IoU/margin tidak berubah.
Perintah demo di README agent/ASSETS menggunakan real-video-demo-v2.json;
ROI lama utuh. Launcher demo.py lokal hanya mengganti path ROI ke v2, memakai
threshold baru dari detector/pipeline. Launcher tetap tidak ikut commit AI.

28 tes lulus setelah perubahan runtime. Replay pipeline290 cached frame cocok
persis hasil eksperimen0,10 (state/unknown); smoke inferensi default best.pt4
frame cocok box cache>=0,10. Tidak ada tes HTTP/UI baru. Backend/UI/README root
lokal dipertahankan, tidak disertakan ke commit. Binary/output tetap ignored.
Dokumen/runner diagnosis ROI, confidence, pilihan dan kode AI disiapkan untuk
commit/push branch AI-PAKAR; lihat git log/status untuk hash/status terkini.

## Riwayat: confidence rendah diperiksa sebelum pemilihan default

Baca docs/ai/low-confidence-2026-10-28.md dan low-confidence-evidence.json.
Ini eksperimen BARU terpisah dari ROI-only, menggunakan ROI v2 tetap, best.pt
sama, IoU0,1/margin0,1/debounce2/3s/label174 sama. Satu inferensi baru290frame
pada floor0,05, lalu replay threshold0,25/0,20/0,15/0,10/0,05 dari cache baru.
Seluruh box>=0,25 cocok cache lama; baseline v2 state/raw/unknown/transisi cocok
persis. Audit assignment cocok engine seluruh frame dan kelima threshold.

Hasil benar/beda/unknown dari174 label dan coverage:
-0,25:155/1/18;89,66%; VIDEO05 unknown151/290.
-0,20:163/1/10;94,25%; VIDEO05 unknown84/290.
-0,15:168/1/5;97,13%; VIDEO05 unknown40/290.
-0,10:170/1/3;98,28%; VIDEO05 unknown12/290.
-0,05:171/1/2;98,85%; VIDEO05 unknown4/290.
Satu beda tetap04 di65s karena debounce (available66,5s semua threshold).
Banyak box empty05 memang ada di bawah0,25:45s conf0,1165 IoU0,6336;
110s conf0,1888 IoU0,6473. Keduanya petak05, bukan mobil belakang.
15s box empty0,1523 di petak05, tetapi box0,2246 sempit/bergeser dekat pejalan
kaki juga diterima. Jangan menyamakan semua recovery dengan box berkualitas.
103,5s mobil lewat menghasilkan raw occupied05 (juga pada0,25), state tertahan
debounce; frame tidak berlabel, jangan klaim salah/benar.105s tetap unknown
pada0,05; sebagian pandangan tertutup mobil lewat.0,05 menambah konflik01:
3unknown vs1 pada0,10. Inspeksi fullres15/45/103,5/105/110s.

KESIMPULAN TERBARU: threshold tuning layak didahulukan; retraining belum wajib.
Kandidat0,15/0,10 perlu diperiksa pada clip lain dan label review manusia sebelum
mengganti default.0,05 batas diagnosis, bukan otomatis rekomendasi. Data ini
clip yang sama untuk memilih threshold dan mengevaluasi, tidak held-out.
Output lokal output/ai-validation/low-confidence/ memuat cache, trace setiap
threshold dan overlay. Runner tests/ai/check_low_confidence.py reuse cache
berdasar hash input.25 tes lama tidak diulang, kode runtime tidak diubah.
Best.pt/conf0,25/referensi ROI launcher tetap seperti sebelumnya. Backend/UI/
demo.py dan label tidak disentuh. Tidak ada HTTP/UI/training ulang. Semua
hasil baru lokal BELUM commit/push; HEAD tetap945726b.

## Checkpoint sebelumnya: diagnosis ROI-only, 28 Oktober 2026

Baca docs/ai/roi-review-2026-10-28.md dan roi-review-evidence.json.
Inspeksi frame asli1280x720 pada0/15/65/100/110/135/140s mendukung hipotesis
ROI lama melintasi garis pemisah dua baris. Kandidat floor ROI terpisah:
edge/python-agent/configs/real-video-demo-v2.json. Identitas enam petak depan
VIDEO01..06 tetap; CSV label tidak berubah. Geometri manual, beberapa garis
teroklusi dan diinterpolasi; belum review manusia, bukan ground truth resmi.

Replay cached detections290 sampel (tanpa inference ulang), confidence0,25,
IoU0,1, margin0,1, debounce2/3s dan174 label sama:
- Lama:156 benar/1 beda/17 unknown, coverage90,23%.
- V2:155 benar/1 beda/18 unknown, coverage89,66%.
- VIDEO05 unknown seluruh clip:134 ->151. V2 memulihkan4 unknown05 namun
  kehilangan21 jawaban lain. Kalibrasi tidak cukup menyelesaikan unknown.
- Replay lama cocok persis journal asli; audit assignment independen cocok
  seluruh frame kedua ROI. Runner sekitar5s: tests/ai/review_cached_roi.py.
- Unknown05 lama:101 tanpa box empty cached overlap,29 center_outside,
  4 konflik. Angka29 bukan29 box05 yang pasti bisa diselamatkan: box bisa
  milik04/baris belakang.130/134 unknown lama tidak punya pusat box cached
  di petak05 v2; semua151 unknown05 v2 juga tidak punya pusat box di sana.
-110s: ROI lama05 menerima occupied0,5368 dari mobil belakang, center535,5;
  448,5, IoU0,2103. State masih available karena debounce! V2 menolak dan
  unknown. Empty0,6722 yang tampak adalah petak04. Jangan menyamakan state
  yang cocok label dengan deteksi fisik05 yang benar.

Bukti rinci lokal output/ai-validation/roi-review/: assignment-old/v2.jsonl
(conf/center/IoU/reason/hits/pending), observations-old/v2.jsonl, comparison.json,
raw/old/v2 PNG full-resolution tujuh timestamp. Ringkasan/hash/contoh di docs.
Cache disaring pada0,25, sehingga confidence lebih rendah/NMS/box hilang tidak
bisa dipisahkan dari cache saja. Pending debounce bukan unknown.

Keputusan: model custom butuh peningkatan konsistensi empty atau eksperimen
assignment terpisah; retraining bukan satu-satunya solusi yang terbukti.
Langkah berikut yang disarankan: diagnosis inferensi confidence lebih rendah
sebagai eksperimen BARU pada petak05 fisik v2, tanpa mencampur hasil ROI-only.
Jika box tetap hilang/tidak stabil, review label/dataset tim, tambah contoh
perspektif ini lalu evaluasi clip lain sebelum klaim improvement. Ini bukan
blocker Azure/RTSP/semua cuaca untuk praktikum. Jangan mengubah threshold/model
bersamaan dengan ROI lalu menyebutnya perbaikan ROI.

Best.pt tetap default. ROI lama/referensi launcher belum diganti; v2 kandidat
manual yang belum review manusia dan belum meningkatkan coverage. Backend/UI/
demo.py tidak disentuh sesi diagnosis.25 tes lama tidak diulang; replay/assertion
baru selesai. Tidak ada HTTP/UI/inference panjang baru. Semua hasil baru masih
lokal BELUM commit/push; HEAD tetap945726b. Jangan ikutkan patch aplikasi lokal
ke commit AI tanpa review. Webcam browser/C#/ONNX tetap TODO integrasi tim.

## Menjalankan

Dari checkout pakar:
py -X utf8 -m pytest tests/ai -q
py -X utf8 edge/python-agent/pipeline.py --source edge/python-agent/assets/parking-demo.mp4 --roi edge/python-agent/configs/real-video-demo-v2.json --events output/ai-validation/best/events.jsonl --summary output/ai-validation/best/summary.json --annotated-video output/ai-validation/best/overlay.mp4

Demo lokal lengkap: py demo.py (video2x, loop, buka URL yang dicetak; Ctrl+C stop).
Launcher/patch backend/UI hanya ada di working tree, tidak ikut branch commit
AI. Existing services mungkin masih hidup; launcher memilih port kosong dan
jangan menghentikan proses milik pengguna. File .pt/video diabaikan Git dan
output pengujian juga diabaikan. Jangan clone ulang, menimpa perubahan, membuat
claim produksi, atau menutup issue hanya dari demo.
