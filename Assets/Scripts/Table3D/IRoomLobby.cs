namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Pintu menu utama ke lobby online (BOB-035). Implementasi mengisi CandyTableDirector.Lobby, atau cukup menjadi
    /// komponen aktif di scene (dicari saat menu utama dibuka). Selama belum ada implementasi atau Ready false, tombol
    /// Buat Room dan Gabung Room tampil abu-abu dan tidak bisa dipilih.
    /// </summary>
    public interface IRoomLobby
    {
        /// <summary>True kalau online siap dipakai (package terpasang, project UGS ter-link, sudah login).</summary>
        bool Ready { get; }

        /// <summary>
        /// Menu utama sudah disembunyikan; lobby mengambil alih meja. Untuk kembali, panggil director.OpenMainMenu().
        /// Exception dari sini ditangkap director dan tampil sebagai pesan di meja.
        /// </summary>
        void CreateRoom(CandyTableDirector director);

        /// <summary>Sama seperti CreateRoom, untuk gabung dengan kode room.</summary>
        void JoinRoom(CandyTableDirector director);
    }
}
