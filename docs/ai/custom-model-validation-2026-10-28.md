# Uji model custom empty/occupied — 28 Oktober 2026

## Artifact yang benar-benar diuji

Ditemukan di Downloads: `pakarmodelsenpro.pt` dan `pakarmodel1.pt`. Kedua file
identik (6.251.050 byte), SHA-256:
`1b5b8f3b662146c5259710f83c0eb198bb982f96f815c6d59e9e2681a99c5272`.
Satu salinan disimpan lokal sebagai `edge/python-agent/assets/best.pt`; nama
best.pt adalah alias lokal, bukan bukti bahwa ini checkpoint terbaik training.
File sumber dipertahankan, binary tetap diabaikan Git.

Inspeksi Ultralytics: task `detect`, kelas0=`empty`, kelas1=`occupied`.
Metadata mencatat base model `yolov8n.pt`, argumen epochs50, imgsz640,
dataset `/content/Parking-Space-1/data.yaml`. Epoch checkpoint=-1 (tidak memberi
riwayat epoch terbaik). Metadata konsisten dengan fine-tuning slot, tetapi
dataset/split, kepengarangan, jumlah epoch yang sungguh selesai, dan mAP laporan
training belum direproduksi. Tidak mengklaim mAP50 98,5% dari pengujian ini.

## Metode

- Video nyata stabil yang sama: 145s, 1280x720 30FPS; source00:05–02:30.
- Sampling2FPS: seluruh290 sampel selesai diproses, seluruhnya valid, EOF normal.
- CPU, imgsz640, confidence awal0,25. Pemeriksaan tambahan confidence0,35
  menggunakan hasil inferensi yang sama, bukan training ulang.
- Enam ROI dan anotasi29 frame/174 slot-sample yang sama dengan baseline.
  Label dibuat visual oleh Codex, belum review manusia dan tidak padat kontinu.
- Adapter khusus `slot_labels.py`: pusat box harus di dalam ROI dan IoU>=0,1,
  satu box paling banyak satu slot. Confidence tertinggi dipakai per kelas.
  Empty diterjemahkan available, occupied tetap occupied. Dua kelas berlawanan
  dengan selisih confidence<0,1 dianggap unknown.
- Tidak ada box cocok -> **unknown**, bukan available. Unknown mereset kandidat
  debounce, tidak menghapus state internal sebelumnya, dan tidak dihitung
  sebagai prediksi baru yang valid. Debounce2s occupied/3s available; initial
  evidence langsung menetapkan status seperti baseline.
- Ini runner offline eksplisit. CLI vehicle pipeline tetap menolak kelas slot,
  sehingga empty tidak pernah diam-diam dianggap kendaraan.

## Hasil

| Ukuran pada label yang tersedia | COCO + adapter kendaraan sebelumnya | Custom + adapter slot |
|---|---:|---:|
| Slot-sample referensi | 174 | 174 |
| Prediksi tersedia | 174 | 157 |
| Cocok | 172 | 156 |
| Berbeda | 2 | 1 |
| Unknown/tidak terjawab | 0 | 17 |
| Coverage | 100% | 90,23% |
| Accuracy hanya pada prediksi tersedia | 98,85% | 99,36% |
| Fraksi benar dari seluruh174 label | 98,85% | 89,66% |

Angka99,36% **tidak** membuktikan model custom lebih unggul:17 unknown
dikecualikan dari accuracy, dan adapter serta threshold kedua pendekatan
berbeda. Baseline menjawab seluruh174 sampel. Ini evaluasi eksploratif pada
sampel mudah terlihat yang sama, bukan benchmark independen yang terkontrol.

Custom TP128/TN28/FP1/FN0 pada prediksi tersedia. Satu beda pada VIDEO-04 di65s:
label sudah available tetapi debounce masih occupied. Status berubah menjadi
available pada66,5s. Pada sampel110s, false occupied VIDEO-05 yang sebelumnya
muncul pada baseline tidak muncul pada state custom; hal ini juga dipengaruhi
aturan unknown/reset/debounce dan tidak semata-mata perbedaan model.

17 unknown pada frame berlabel: terutama slot kosong VIDEO-05. Pada seluruh290
frame, VIDEO-05 unknown134 kali. Slot kosong masih kurang konsisten dikenali.
Custom tidak menyatakan semua slot kosong hanya karena model melewatkan box.

Dengan confidence0,35:147/174 prediksi tersedia,146 cocok,1 beda,27 unknown;
coverage84,48%. Menaikkan confidence tidak otomatis memperbaiki kemampuan
menjawab status slot kosong. Parameter belum dituning untuk mengejar skor ini.

MAE jumlah okupansi custom=0 hanya pada13 frame referensi yang seluruh enam
slotnya mendapat prediksi. Angka ini tidak berlaku untuk semua29 frame atau
seluruh video. Inferensi CPU mean58,12ms dan p95 98,16ms, bukan latency UI.
Deteksi mentah kumulatif:3587 occupied dan657 empty, bukan jumlah kendaraan unik.

Status final144,5s: VIDEO-01/02/03/06 occupied, VIDEO-04/05 available.
Lima tes adapter ditambah18 tes sebelumnya: **23 passed**.

## Bukti dan reproduksi

Metadata/hasil yang dapat dilacak Git: [custom-model-evidence.json](custom-model-evidence.json).
Output lokal: `output/ai-validation/custom-model/`:

- `custom-overlay.mp4`: video145s dengan box empty/occupied serta state ROI.
- `overlay-000.jpg`, `overlay-065.jpg`, `overlay-110.jpg`, `overlay-140.jpg`.
- `custom-evaluation.json`, `comparison.json`, `observations.jsonl`,
  `observations-conf035.jsonl`, `detections.jsonl`, `pytest.xml`.

Dari root pakar, setelah file tersedia:

```powershell
py -X utf8 tests/ai/validate_custom_model.py --model edge/python-agent/assets/best.pt --video edge/python-agent/assets/parking-demo.mp4 --roi edge/python-agent/configs/real-video-demo.json --labels docs/ai/real-video-sampled-labels.csv --out output/ai-validation/custom-model
py -X utf8 -m pytest tests/ai -q
```

## Kesimpulan dan batas pekerjaan

Uji offline model custom selesai dan artifact terbukti dapat mendeteksi label
slot pada video nyata. Hasil belum konsisten untuk slot kosong sehingga unknown
harus tetap terlihat saat memilih model demo. Pengujian ini tidak memakai test
stub dan tidak melatih ulang atau mengubah weights.

**Demo.py/website belum dialihkan ke model custom.** Pengujian HTTP/DB/SignalR/UI
yang dilaporkan sebelumnya menggunakan COCO, bukan model ini. Integrasi custom
harus menentukan cara menampilkan unknown dan freshness setiap slot; jangan
memaksakan missing detection menjadi available untuk membuat angka terlihat baik.
Tidak ada commit/push tambahan dari sesi pengujian ini.
