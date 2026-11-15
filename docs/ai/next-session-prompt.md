Lanjutkan AI PAKAR Nabil/Brian di checkout existing:
C:\Users\Nabil Abrian Aryo P\OneDrive\Dokumen\ChatGPT\Pakar - Senpro\pakar
Git dari pakar, branch existing AI-PAKAR. Jangan clone/reset/stash. Pertahankan
patch backend/UI/README root/demo.py yang lokal; launcher tidak ikut commit AI.
Lihat git log/status untuk commit terbaru, jangan berasumsi HEAD945726b lagi.

Baca docs/ai/context-and-next-steps.md, selected-confidence-2026-10-28.md dan
selected-confidence-evidence.json, lalu low-confidence dan roi-review beserta
JSON evidence masing-masing, pipeline.py, slot_labels.py, ROI lama dan v2.

User memilihkan tugas0,15 vs0,10; SUDAH dipilih0,10 untuk demo clip dan diterapkan.
YOLODetector default custom0,10 / COCO0,25, assignment slots0,10 / vehicles0,35.
Model best.pt tetap. Perintah demo pakai ROI v2. Lokal demo.py hanya ganti path
ROI v2; otomatis detector/pipeline pakai0,10, tetapi launcher tidak ikut commit.
Low-level SlotLabelEngine tetap0,25 untuk reproduksi historis; runtime pipeline
memberi nilai efektif eksplisit. Jangan ubah angka eksperimen historis.

0,10:170benar/1beda/3unknown dari174, coverage98,28%;0,15:168/1/5,97,13%.
Unknown05 seluruh290frame12 vs40. Satu beda04 di65s karena debounce, berubah
66,5s. Box rendah beberapa meragukan, raw empty01 di134,5s tertahan debounce;
jangan klaim setiap state benar berasal box sempurna.103,5s mobil lewat raw
occupied05 juga tertahan debounce, frame tidak berlabel.105s tetap unknown
bahkan0,05. Threshold dipilih/evaluasi clip sama, bukan held-out/review manusia.
28 tests passed setelah kode berubah; pipeline replay cached290frame cocok
hasil0,10, smoke inferensi default4frame box cocok cache. Tidak ada HTTP/UI baru.

Diagnosis ROI-only sebelumnya: ROI lama melintasi dua baris; v2 floor polygons
terpisah, identitas01..06 sama.0,25 lama156/1/17;v2 155/1/18. Cache0,05 baru
menemukan box empty05 seperti45s0,1165 dan110s0,1888. Jangan mengklaim improvement
threshold sebagai improvement ROI atau model baru. Retraining belum wajib.
Unknown HTTP diomit, UI last-known belum unknown per slot. Video/weights lokal
ignored, Drive placeholder. Proyek praktikum/fokus AI; webcam/C#/ONNX tetap
TODO integrasi tim. Azure/RTSP/semua cuaca bukan blocker. Jangan ulang inference
panjang/tes tanpa perubahan atau kebutuhan baru. Evaluasi clip lain bila ada
untuk menilai pilihan0,10; jangan tambah tugas integrasi tanpa arahan user.
