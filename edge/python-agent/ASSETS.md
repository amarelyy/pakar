# Model dan video AI PAKAR — Google Drive

Binary dibagikan melalui Google Drive, tidak dimasukkan ke Git. Setelah upload,
ganti placeholder tautan di bawah. Berikan akses kepada anggota tim yang perlu
mengunduh. Jangan memasukkan password atau API key dalam dokumen ini.

**Folder Google Drive:** [ISI TAUTAN FOLDER DI SINI](GANTI_DENGAN_LINK_FOLDER_GDRIVE)

| File | Tautan unduh Google Drive | Tempat setelah diunduh | Kegunaan |
|---|---|---|---|
| `yolov8n.pt` | [ISI LINK MODEL](GANTI_DENGAN_LINK_MODEL_GDRIVE) | `edge/python-agent/assets/yolov8n.pt` | Weights baseline opsional untuk perbandingan |
| `best.pt` | [ISI LINK MODEL CUSTOM](GANTI_DENGAN_LINK_MODEL_CUSTOM_GDRIVE) | `edge/python-agent/assets/best.pt` | Model custom default, kelas empty/occupied |
| `parking-demo.mp4` | [ISI LINK VIDEO DEMO](GANTI_DENGAN_LINK_VIDEO_DEMO_GDRIVE) | `edge/python-agent/assets/parking-demo.mp4` | Clip nyata yang sudah cocok dengan enam ROI demo |
| `sample_parkir.mp4` (opsional) | [ISI LINK FIXTURE](GANTI_DENGAN_LINK_VIDEO_FIXTURE_GDRIVE) | `edge/python-agent/assets/sample_parkir.mp4` | Animasi sintetis untuk reproduksi tes lama, bukan evaluasi akurasi |

Untuk baseline, unduh `yolov8n.pt` dan `parking-demo.mp4`. Untuk pengujian model
custom, unduh `best.pt` dan `parking-demo.mp4`. Unggah
file aslinya, bukan video yang sudah dikompres ulang atau diubah framing-nya.
Tidak perlu membagikan seluruh folder output pengujian.

## File yang Nabil unggah

Semua file pada tabel sudah tersedia pada workstation ini di folder:

```text
C:\Users\Nabil Abrian Aryo P\OneDrive\Dokumen\ChatGPT\Pakar - Senpro\pakar\edge\python-agent\assets\
```

`parking-demo.mp4` adalah clip stabil sumber 00:05–02:30, durasi145 detik,
1280x720, 30FPS, tanpa audio. Waktu di dalam clip dimulai dari0. ROI yang sesuai
adalah `configs/real-video-demo-v2.json` (VIDEO-01..06, ID khusus demo9001..9006).
Video sumber penuh memiliki gerakan pan/zoom dan tidak boleh langsung dipakai
dengan satu konfigurasi ROI ini.

`yolov8n.pt` adalah **YOLOv8n COCO**, kelas kendaraan car/motorcycle/bus/truck.
Model custom kini tersedia sebagai `best.pt` (salinan identik dari Downloads
pakarmodelsenpro.pt/pakarmodel1.pt), kelas empty/occupied. Keduanya memakai
pendekatan berbeda. Jangan mengganti weights pada CLI kendaraan secara langsung
atau mengaitkan metrik training custom dengan baseline COCO.

## Memeriksa file yang diunduh

SHA-256 yang diharapkan:

| File | SHA-256 |
|---|---|
| `yolov8n.pt` | `f59b3d833e2ff32e194b5bb8e08d211dc7c5bdf144b90d2c8412c47ccfc83b36` |
| `best.pt` | `1b5b8f3b662146c5259710f83c0eb198bb982f96f815c6d59e9e2681a99c5272` |
| `parking-demo.mp4` | `bab6a16d4f72231ca352715797d8035a669fc8714a70a2acdba1a64c45cca96e` |
| `sample_parkir.mp4` | `f14d56838d6bc3b244cb06577a5f163790bfbd1073c9be958b09856cb02786d1` |

Dari root checkout `pakar`, cek dengan PowerShell:

```powershell
Get-FileHash edge/python-agent/assets/yolov8n.pt, edge/python-agent/assets/parking-demo.mp4 -Algorithm SHA256
```

## Menjalankan bagian AI saja

Siapkan Python dan dependensi dari README agent, lalu dari root repo:

```powershell
py -X utf8 edge/python-agent/pipeline.py --source edge/python-agent/assets/parking-demo.mp4 --model edge/python-agent/assets/best.pt --mode slots --roi edge/python-agent/configs/real-video-demo-v2.json --replay-speed 2 --events output/ai-validation/handoff/events.jsonl --summary output/ai-validation/handoff/summary.json --annotated-video output/ai-validation/handoff/overlay.mp4
```

Perintah di atas memakai model custom default, confidence YOLO/assignment0,10
dan ROI v2. Kalibrasi ini dipilih pada clip yang sama, belum validasi held-out. Baseline COCO tetap bisa dipilih
dengan --model assets/yolov8n.pt (path lengkap relatif root) dan --mode vehicles.
Perintah tersebut tidak memerlukan backend dan tidak mengirim HTTP. Hasilnya
journal status, summary, dan video overlay ROI. Untuk mengirim hasil ke backend
tim, gunakan kontrak pada README dan mapping ExternalId yang disepakati.
Jangan mengirim ID demo9001 ke database tim sebelum mapping dibuat di sana.

Launcher `demo.py` menjalankan aplikasi lengkap dengan SQLite. Launcher itu
memerlukan perubahan backend/UI pendukung pada checkout demo; tidak otomatis
kompatibel dengan branch tim533008 yang menggunakan SQL Server.

Untuk uji custom, jalankan perintah pada
[laporan custom](../../docs/ai/custom-model-validation-2026-10-28.md). Model
custom kini didukung pipeline HTTP dan menjadi default launcher lokal.
Runner validate_custom_model.py tetap khusus offline. File binary custom harus
diunggah terpisah ke Drive; .pt tidak ikut commit/push GitHub.
