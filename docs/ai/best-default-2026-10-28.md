# Default model AI-PAKAR: best.pt

Permintaan Nabil: bagian AI menggunakan model custom best.pt, fitur lain tetap.
Default pipeline sekarang `edge/python-agent/assets/best.pt`. Tidak ada download
atau fallback COCO diam-diam ketika file hilang. Mode auto memilih adapter slot
untuk label empty/occupied, dan adapter kendaraan untuk weights COCO eksplisit.
Parameter file/stream, ROI, debounce2s/3s, frame validation, reconnect, pacing,
EOF, heartbeat, auth dan retry tetap memakai pipeline yang sama.

Custom membutuhkan pemetaan yang berbeda: missing/ambiguous detection menjadi
unknown, bukan tersedia. JSON journal memuat unknown dan jumlahnya. Snapshot
HTTP hanya mengirim slot yang teramati. Backend mempertahankan last-known slot
yang tidak dikirim; UI lama belum menampilkan unknown per slot. Jika seluruh
slot unknown tidak ada heartbeat. Jadi angka UI bukan jaminan seluruh slot
mendapat prediksi baru pada setiap frame. Ini batasan yang didokumentasikan,
bukan pengubahan missing detection menjadi kosong agar hitungan terlihat baik.

Weights tidak dimasukkan GitHub. Letakkan best.pt dari Google Drive pada assets/;
isi tautan di [ASSETS.md](../../edge/python-agent/ASSETS.md). Hash dan provenance
model dicatat di manifest. COCO masih dapat dipakai dengan argumen eksplisit:
`--model edge/python-agent/assets/yolov8n.pt --mode vehicles`.

## Menjalankan AI dari branch ini

Dari root pakar setelah mengunduh weights/video:

```powershell
py -X utf8 edge/python-agent/pipeline.py --source edge/python-agent/assets/parking-demo.mp4 --roi edge/python-agent/configs/real-video-demo.json --replay-speed 2 --events output/ai-validation/best/events.jsonl --summary output/ai-validation/best/summary.json --annotated-video output/ai-validation/best/overlay.mp4
```

Tidak perlu --model; default adalah best.pt. HTTP tetap opt-in lewat backend-url
dan PAKAR_API_KEY. ID9001 dan ROI hanya demo, bukan kalibrasi produksi.

## Verifikasi

- 25 pytest passed, termasuk dua tes tambahan pipeline slot: snapshot parsial
  tidak membuat slot tak teramati menjadi available, dan semua unknown tidak
  menghasilkan heartbeat. Tes COCO sebelumnya tetap lolos.
- Launcher lokal juga diganti ke best.pt; satu putaran nyata selesai pada4x,
  290 sampel,6 payload diterima,0 gagal, EOF normal. DB mencatat7 Detection
  (enam status awal dan perubahan VIDEO-04); status akhir cocok:4 occupied,
  2 available,0 unknown pada observasi akhir.
- [Bukti ringkas](best-default-evidence.json). Ini tes agent/HTTP/DB pada aplikasi
  pendukung lokal; browser live update tidak diulang dalam pengujian switch ini.
  Tidak ada klaim latency kamera-ke-UI atau peningkatan akurasi baru.
- Evaluasi custom sebelumnya tetap berlaku:156 cocok,1 beda,17 unknown dari174
  label sampel; lihat laporan custom. Mengubah default bukan klaim model sempurna.

Commit AI mencakup kode/tes/dokumen. Launcher demo.py dan patch backend/UI masih
lokal dan terpisah dari commit AI, seperti kesepakatan pemisahan sebelumnya.
Demo yang sudah berjalan memakai proses lama perlu dihentikan lalu dijalankan
ulang agar memuat perubahan model default.
