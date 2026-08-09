using Avalonia.Media.Imaging;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Gui;

public sealed class AvaloniaArtworkImageProcessor : IArtworkImageProcessor
{
    public Task SavePortraitAsync(
        ReadOnlyMemory<byte> source,
        string destinationPath,
        int maximumWidth,
        int maximumHeight,
        CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var sourceStream = new MemoryStream(source.ToArray(), writable: false);
            using var original = new Bitmap(sourceStream);
            var widthScale = (double)maximumWidth / original.PixelSize.Width;
            var heightScale = (double)maximumHeight / original.PixelSize.Height;
            var scale = Math.Min(1d, Math.Min(widthScale, heightScale));

            Bitmap output = original;
            if (scale < 1d)
            {
                sourceStream.Position = 0;
                var width = Math.Max(1, (int)Math.Round(original.PixelSize.Width * scale));
                output = Bitmap.DecodeToWidth(
                    sourceStream,
                    width,
                    BitmapInterpolationMode.HighQuality);
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var destination = new FileStream(
                    destinationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                output.Save(destination, PngBitmapEncoderOptions.Default);
                destination.Flush(flushToDisk: true);
            }
            finally
            {
                if (!ReferenceEquals(output, original))
                {
                    output.Dispose();
                }
            }
        }, cancellationToken);
}
