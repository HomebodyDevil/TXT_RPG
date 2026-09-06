using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TxTRPG.Gameplay.Players;
using UnityEngine;

namespace TxTRPG.Application.Persistence
{
    public interface IPlayerSaveRepository
    {
        Task<PlayerSaveData> TryLoadAsync(CancellationToken cancellationToken);
        Task SaveAsync(PlayerSaveData saveData, CancellationToken cancellationToken);
    }

    public sealed class LocalPlayerSaveRepository : IPlayerSaveRepository
    {
        public LocalPlayerSaveRepository(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A player save path is required.", nameof(filePath));
            }
            FilePath = Path.GetFullPath(filePath);
        }

        public string FilePath { get; }
        public string BackupPath => FilePath + ".bak";

        public async Task<PlayerSaveData> TryLoadAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(FilePath))
            {
                return null;
            }

            string json;
            using (var stream = new FileStream(
                       FilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                       4096, true))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 4096))
            {
                json = await reader.ReadToEndAsync();
            }
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return PlayerSaveSerializer.Deserialize(json);
            }
            catch (Exception exception) when (
                exception is FormatException ||
                exception is NotSupportedException ||
                exception is ArgumentException)
            {
                throw new InvalidDataException(
                    $"Player save at '{FilePath}' is invalid. The file was preserved.",
                    exception);
            }
        }

        public async Task SaveAsync(
            PlayerSaveData saveData,
            CancellationToken cancellationToken)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var directory = Path.GetDirectoryName(FilePath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("Player save path has no parent directory.");
            }
            Directory.CreateDirectory(directory);

            var temporaryPath = FilePath + ".tmp";
            var json = JsonUtility.ToJson(saveData, true);
            try
            {
                using (var stream = new FileStream(
                           temporaryPath, FileMode.Create, FileAccess.Write,
                           FileShare.None, 4096, true))
                using (var writer = new StreamWriter(
                           stream, new UTF8Encoding(false), 4096))
                {
                    await writer.WriteAsync(json);
                    await writer.FlushAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                    stream.Flush(true);
                }

                PromoteTemporaryFile(temporaryPath);
            }
            catch
            {
                TryDelete(temporaryPath);
                throw;
            }
        }

        private void PromoteTemporaryFile(string temporaryPath)
        {
            if (!File.Exists(FilePath))
            {
                File.Move(temporaryPath, FilePath);
                return;
            }

            try
            {
                File.Replace(temporaryPath, FilePath, BackupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(FilePath, BackupPath, true);
                File.Delete(FilePath);
                File.Move(temporaryPath, FilePath);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
