# Pemeriksaan video parkir nyata — 28 Oktober 2026

Input diberikan pengguna dari Downloads; metadata dan hash disimpan pada
output/ai-validation/real-video/. Metadata: 1280x720, 30 FPS, 9484 frame,
durasi 316,13 detik. Binary tidak disalin ke Git.

Pemeriksaan visual frame awal/25%/50%/75%/99% memperlihatkan kendaraan nyata,
slot bergaris, serta perubahan framing/zoom kamera. ROI konfigurasi sintetis
H-01/H-02 sengaja tidak digunakan: posisi slot berbeda dan video bukan zona H.
Satu ROI tetap untuk seluruh video akan menghasilkan pemetaan keliru.

YOLOv8n COCO CPU dijalankan pada sampel setiap 1 detik, imgsz=640,
confidence>=0.35, kelas car/motorcycle/bus/truck. Ini pemeriksaan kelayakan
deteksi, bukan evaluasi status slot. Preview detection-preview.mp4 diputar
5x waktu asli. Bounding box bukan polygon slot.

Temuan visual: banyak mobil terdeteksi; beberapa kendaraan kecil terlewat atau
confidence berubah. Kendaraan besar dapat berubah label bus/truck. Frame
0/30/60 detik menunjukkan framing serupa; ini kandidat segmen awal untuk ROI,
bukan bukti seluruh interval stabil. Kamera kemudian melakukan perubahan
pandangan. Slot tepi bawah terpotong dan overlay menutup sebagian gambar.

Output lokal: metadata.json, detection-summary.json, detections.jsonl,
detection-preview.mp4, frame-*.jpg, detected-*.jpg, check_video.py.
Tidak ada request ke backend, tidak ada ground truth, tidak ada klaim accuracy,
detection mAP, jumlah kendaraan unik, okupansi zona, atau latensi UI.

Lanjutan: pilih batas segmen kamera diam secara teliti, buat ROI beberapa slot
yang utuh dan identitas khusus video uji, anotasi status manual termasuk
unknown, lalu jalankan pipeline dan evaluator. Video ini belum memberikan
bukti pengujian motor, malam/hujan, CCTV FT UGM, atau RTSP putus/pulih.

Hasil run selesai: 317 sampel; 6704 deteksi kumulatif (kendaraan yang sama
terhitung berulang), rentang 0–36 deteksi/sampel, mean 21,15. Inferensi CPU
mean 73,02 ms, p95 93,81 ms; tidak termasuk capture/HTTP/UI. Kelas teramati:
car 6508, truck 150, bus 46; tidak ada deteksi motorcycle pada threshold ini.
Sampel tanpa deteksi perlu diperiksa, tidak otomatis berarti parkiran kosong.
SHA-256 video: cb206a38d655cef2e5983fffcf9c86d5f1ff3af211f1ac7460a434a417c2b2e7

## Pemilihan segmen kamera stabil

Gerakan latar diperiksa setiap 1 detik menggunakan Lucas-Kanade optical flow
pada bagian atas gambar dan affine RANSAC. Kandidat memerlukan >=30 inlier,
fraksi inlier >=0,5, median pergeseran <=1,5 piksel pada resolusi 1280, serta
perubahan skala <=0,2% per pasangan. Sampling ini dapat melewatkan gerakan
singkat di antara sampel; bukan jaminan stabil setiap frame.

Kandidat panjang: 0–68s, 69–155s, 251–316s. Pemisahan 68/69s merupakan
kegagalan kriteria kualitas pelacakan; tidak otomatis gerakan kamera.
Montage 5/65/75/110/150s memperlihatkan framing awal serupa. Gerakan nyata
terdeteksi mulai pasangan yang berakhir pada 156s. Interval 169–205s tampak
stabil tetapi zoom ke tiang dan sedikit slot, sehingga kurang cocok untuk ROI.

Dipilih 00:05–02:30 (145 detik), dengan margin sebelum pan/zoom. Diperiksa juga
registrasi ORB/RANSAC terhadap frame 5s pada interval 5 detik untuk memeriksa
drift kumulatif, bukan hanya pergeseran antar-frame berturutan. Bukti numerik:
output/ai-validation/real-video/stable-reference-check.json.

Clip: output/ai-validation/real-video/stable-0005-0230.mp4. Re-encode mp4v,
30 FPS, tanpa audio; sumber asli tidak diubah. Manifest mencatat hash,
jumlah frame dan offset: waktu sumber = waktu clip + 5 detik. Ground truth
untuk clip harus dimulai pada 0, bukan 5 detik. Segmen akhir 04:15–05:10 bisa
menjadi kandidat tambahan dengan ROI berbeda; belum dipotong.

Belum dilakukan anotasi occupancy atau pemeriksaan transisi parkir lengkap.
Langkah selanjutnya: ROI beberapa slot utuh, inspeksi ground truth manual,
dan jalankan pipeline/evaluator pada clip. Identitas video bukan ExternalId
produksi FT UGM; jangan mengirim ID fixture ke backend produksi.
