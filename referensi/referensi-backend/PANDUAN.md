# Referensi backend dan solusi .NET lama

Pemilik peran software engineering: Ursula. Integrasi backend, AI, dan UI dikerjakan bersama.

| Lokasi | Fungsi |
| --- | --- |
| src/Pakar.Api | Endpoint, ingest, hubs SignalR, middleware, seed data |
| src/Pakar.Application | DTO dan lapisan aplikasi |
| src/Pakar.Domain | Entitas dan kalkulasi okupansi |
| src/Pakar.Infrastructure | EF Core, migrasi SQLite, storage, camera timeout |
| src/Pakar.Shared | DTO/enums lintas proyek |
| src/Pakar.Web | Frontend Blazor; lihat [panduan frontend](../referensi-frontend/README.md) |
| src/Pakar.AiInference | Kerangka worker C#/ONNX; lihat [panduan AI](../referensi-ai/README.md) |

Pakar.slnx, Directory.Build.props, global.json, dan path relatif src dipertahankan.
Ini snapshot lama, bukan backend PostgreSQL main. Database runtime tidak diunggah; gunakan migrasi
jika mencoba versi ini. Beberapa halaman admin dan layanan AI masih kosong/kerangka.

File lama README.md dipertahankan sebagai sumber historis. Dokumentasi yang tadinya berada
di docs dipindah ke [dokumentasi-lama](../referensi-manajemen-proyek/dokumentasi-lama/).
Training/edge ada di [referensi-ai](../referensi-ai/); infra ada di
[referensi-cloud-devops](../referensi-cloud-devops/). Sesuaikan perintah lama dengan lokasi tersebut.
Konfigurasi lama berisi contoh kredensial development, bukan konfigurasi deployment.
