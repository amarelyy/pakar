# Model dan video AI PAKAR — Google Drive

Binary dibagikan melalui Google Drive, tidak dimasukkan ke Git. Setelah upload,
ganti placeholder tautan di bawah. Berikan akses kepada anggota tim yang perlu
mengunduh. Jangan memasukkan password atau API key dalam dokumen ini.

**Folder Google Drive:** [ISI TAUTAN FOLDER DI SINI](GANTI_DENGAN_LINK_FOLDER_GDRIVE)

| File | Tautan unduh Google Drive | Tempat setelah diunduh | Kegunaan |
|---|---|---|---|
| `yolov8n.pt` | [ISI LINK MODEL](GANTI_DENGAN_LINK_MODEL_GDRIVE) | `edge/python-agent/assets/yolov8n.pt` | Weights baseline yang dipakai runtime |
| `parking-demo.mp4` | [ISI LINK VIDEO DEMO](GANTI_DENGAN_LINK_VIDEO_DEMO_GDRIVE) | `edge/python-agent/assets/parking-demo.mp4` | Clip nyata yang sudah cocok dengan enam ROI demo |
| `sample_parkir.mp4` (opsional) | [ISI LINK FIXTURE](GANTI_DENGAN_LINK_VIDEO_FIXTURE_GDRIVE) | `edge/python-agent/assets/sample_parkir.mp4` | Animasi sintetis untuk reproduksi tes lama, bukan evaluasi akurasi |

Untuk menjalankan AI dengan enam ROI, cukup unduh dua file pertama. Unggah
file aslinya, bukan video yang sudah dikompres ulang atau diubah framing-nya.
Tidak perlu membagikan seluruh folder output pengujian.

## File yang Nabil unggah

Ketiganya sudah tersedia pada workstation ini di folder:

```text
C:\Users\Nabil Abrian Aryo P\OneDrive\Dokumen\ChatGPT\Pakar - Senpro\pakar\edge\python-agent\assets\
```

`parking-demo.mp4` adalah clip stabil sumber 00:05–02:30, durasi145 detik,
1280x720, 30FPS, tanpa audio. Waktu di dalam clip dimulai dari0. ROI yang sesuai
adalah `configs/real-video-demo.json` (VIDEO-01..06, ID khusus demo9001..9006).
Video sumber penuh memiliki gerakan pan/zoom dan tidak boleh langsung dipakai
dengan satu konfigurasi ROI ini.

Model adalah **YOLOv8n COCO**, kelas kendaraan car/motorcycle/bus/truck, bukan
model custom yang dilatih Nabil. `best.pt` belum tersedia/diverifikasi; jangan
mengganti weights atau mengaitkan metrik training custom dengan baseline ini.

## Memeriksa file yang diunduh

SHA-256 yang diharapkan:

| File | SHA-256 |
|---|---|
| `yolov8n.pt` | `f59b3d833e2ff32e194b5bb8e08d211dc7c5bdf144b90d2c8412c47ccfc83b36` |
| `parking-demo.mp4` | `bab6a16d4f72231ca352715797d8035a669fc8714a70a2acdba1a64c45cca96e` |
| `sample_parkir.mp4` | `f14d56838d6bc3b244cb06577a5f163790bfbd1073c9be958b09856cb02786d1` |

Dari root checkout `pakar`, cek dengan PowerShell:

```powershell
Get-FileHash edge/python-agent/assets/yolov8n.pt, edge/python-agent/assets/parking-demo.mp4 -Algorithm SHA256
```

## Menjalankan bagian AI saja

Siapkan Python dan dependensi dari README agent, lalu dari root repo:

```powershell
py -X utf8 edge/python-agent/pipeline.py --source edge/python-agent/assets/parking-demo.mp4 --model edge/python-agent/assets/yolov8n.pt --roi edge/python-agent/configs/real-video-demo.json --replay-speed 2 --events output/ai-validation/handoff/events.jsonl --summary output/ai-validation/handoff/summary.json --annotated-video output/ai-validation/handoff/overlay.mp4
```

Perintah tersebut tidak memerlukan backend dan tidak mengirim HTTP. Hasilnya
journal status, summary, dan video overlay ROI. Untuk mengirim hasil ke backend
tim, gunakan kontrak pada README dan mapping ExternalId yang disepakati.
Jangan mengirim ID demo9001 ke database tim sebelum mapping dibuat di sana.

Launcher `demo.py` menjalankan aplikasi lengkap dengan SQLite. Launcher itu
memerlukan perubahan backend/UI pendukung pada checkout demo; tidak otomatis
kompatibel dengan branch tim533008 yang menggunakan SQL Server.
