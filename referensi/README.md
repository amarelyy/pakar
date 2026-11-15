# Referensi kode PAKAR

Kumpulan referensi dari folder lokal brian-senpro, disusun pada 15 November 2026.
Mulai dari folder sesuai pekerjaanmu. Kode aplikasi utama tetap berada di luar folder ini.

| Folder | Isi | Peran terkait |
| --- | --- | --- |
| [referensi-code-ui-ux](referensi-code-ui-ux/README.md) | Prototipe React/Vite, driver app, dashboard operator, komponen, design tokens, riset desain | UI/UX: Nabil; frontend: tim |
| [referensi-frontend](referensi-frontend/README.md) | Panduan lokasi kode Blazor, peta, login, halaman admin, dan pemetaan tugas frontend | Nabil, Ursula, Deva |
| [referensi-backend](referensi-backend/PANDUAN.md) | Snapshot solusi .NET lama: API, domain, DTO, persistence, shared, Blazor, worker AI | Software Engineer: Ursula; integrasi: tim |
| [referensi-ai](referensi-ai/README.md) | Model .pt, skrip training/export/validasi, ROI calibration, panduan edge | AI Engineer: Nabil |
| [referensi-cloud-devops](referensi-cloud-devops/README.md) | Docker Compose dan Bicep lama | Cloud Engineer: Deva; kontribusi infrastruktur: tim |
| [referensi-manajemen-proyek](referensi-manajemen-proyek/README.md) | Pembagian peran, peta sumber, dokumentasi sistem lama, batas penggunaan | Project Manager: Deva; seluruh tim |

## Cara memakai

Prototipe UI dapat dicoba terpisah:

```powershell
cd referensi/referensi-code-ui-ux
npm install
npm run dev
```

Halaman pengguna ada di / dan operator di /ops. Seluruh angka berasal dari simulasi browser,
belum terhubung API PAKAR. Baca [README prototipe](referensi-code-ui-ux/README.md).

Snapshot .NET ada di referensi-backend. Struktur src dan ProjectReference di dalamnya dipertahankan
agar proyek yang saling bergantung dapat dibaca sebagai satu solusi. Frontend Blazor dan worker C#
tidak diduplikasi: panduan frontend/AI menautkan ke sumber yang sama.

Snapshot ini berasal dari versi September/Oktober 2026, bukan salinan backend main terbaru
atau pipeline Python terbaru di branch AI-PAKAR. Ambil pola/komponen yang relevan, lalu sesuaikan
dengan kontrak API dan arsitektur aplikasi utama. Beberapa file lama masih berupa kerangka kosong.
Docker Compose lama memakai SQL Server, snapshot API memakai SQLite, sedangkan main menggunakan PostgreSQL/Neon.

Seluruh kode dan aset yang ditemukan pada sumber disertakan, termasuk model .pt.
Database lokal, log runtime, dependensi, hasil build, dan metadata .git tidak disertakan.
Asal setiap file, hash SHA-256, commit sumber, dan daftar pengecualian tersedia dalam
[manifest-sumber.json](referensi-manajemen-proyek/manifest-sumber.json).
