# Referensi AI dan edge

Peran AI Engineer: Nabil.

| Lokasi | Isi |
| --- | --- |
| [models/pakarmodelsenpro.pt](models/pakarmodelsenpro.pt) | Model lokal asli, sekitar 6 MB; checksum tercatat di manifest |
| [dl](dl/) | Requirements dan skrip train/export/validate lama; skrip masih kerangka |
| [edge/calibration](edge/calibration/) | ROI mapper HTML lama |
| [edge/android-agent](edge/android-agent/) | Panduan edge lama |
| [assets/sample_parkir.mp4](assets/sample_parkir.mp4) | Berkas bernama .mp4 dari sumber; isinya HTML, bukan video yang valid |
| [worker C#](../referensi-backend/src/Pakar.AiInference/) | Kerangka inferensi ONNX/OpenVINO dalam solusi .NET |

Model disalin byte-for-byte; arsitektur, label kelas, metrik, dan kompatibilitas runtime belum
diverifikasi dalam pengunggahan ini. File .pt adalah model PyTorch, bukan hasil export ONNX.
Jangan menganggap kerangka worker C# sudah dapat menjalankannya.

Pipeline Python/ROI yang lebih baru dibahas di branch AI-PAKAR. Paket ini hanya merekam isi
folder sumber yang diminta, bukan menggantikan pekerjaan pada branch tersebut.

File sample_parkir.mp4 dipertahankan untuk kelengkapan arsip, tetapi pemeriksaan header menemukan
DOCTYPE html. Sediakan video asli terpisah untuk pengujian inferensi.
