# Referensi cloud dan DevOps

Peran formal Cloud Engineer dan Project Manager: Deva. Infrastruktur juga mendapat kontribusi
dari anggota lain sesuai penugasan issue.

- [infra/docker-compose.yml](infra/docker-compose.yml): contoh SQL Server development lama.
- [infra/bicep](infra/bicep/): kerangka resource Azure; beberapa modul kosong.
- [Dockerfile worker](../referensi-backend/src/Pakar.AiInference/Dockerfile): file kerangka lama.

Struktur internal infra dipertahankan. Ini bukan konfigurasi deployment main terkini yang
menggunakan PostgreSQL/Neon. Periksa infra di root repository untuk konfigurasi terbaru.
Contoh password development di snapshot lama perlu diganti melalui konfigurasi lokal sebelum digunakan.
