# Referensi implementasi frontend

Ada dua sumber tampilan: [prototipe React](../referensi-code-ui-ux/README.md) untuk desain dan
[kode Blazor lama](../referensi-backend/src/Pakar.Web/) untuk integrasi .NET.
Kode Blazor disimpan di solusi .NET yang sama karena memakai ProjectReference ke proyek lain.

| Area | Lokasi kode Blazor |
| --- | --- |
| Halaman parkir | [Pages/Park.razor](../referensi-backend/src/Pakar.Web/Pages/Park.razor) |
| Komponen peta | [Components/CampusMap.razor](../referensi-backend/src/Pakar.Web/Components/CampusMap.razor) |
| Login/register | [Pages](../referensi-backend/src/Pakar.Web/Pages/) |
| Dashboard/halaman admin | [Pages/Admin](../referensi-backend/src/Pakar.Web/Pages/Admin/) |
| Navigasi/layout | [Components/Layout](../referensi-backend/src/Pakar.Web/Components/Layout/) |
| Styling | [wwwroot](../referensi-backend/src/Pakar.Web/wwwroot/) |

Pembagian tugas dari percakapan dan catatan issue sebelumnya:

- Nabil: desain UI/UX, peta/status real-time (#16), pencarian/filter/alternatif (#17).
- Ursula: pelaporan/notifikasi (#18), dashboard admin (#19).
- Deva: fondasi koneksi SignalR, status awal dan form login/register, serta integrasi.

Itu pemetaan pekerjaan, bukan klaim bahwa semua fitur sudah selesai dalam snapshot ini.
Rute lama /park adalah Live Occupancy Test; sejumlah halaman admin kosong.
React dan Blazor tidak otomatis saling terhubung. Gunakan aplikasi utama di src/Pakar.Web
pada root repository untuk melihat implementasi terkini.
