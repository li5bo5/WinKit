using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using WinKit.Clipboard.Models;
using WinKit.Common;

namespace WinKit.Clipboard.Services
{
    /// <summary>
    /// 图片剪贴板处理服务：位图提取、5MB 大小限制、54px 缩略图生成、哈希计算与无锁加载
    /// </summary>
    public static class ImageProcessingService
    {
        public const long MaxImageFileSizeBytes = 5 * 1024 * 1024; // 5 MB 上限
        public const int ThumbnailHeight = 54; // 固定高度 54px

        /// <summary>
        /// 将内存位图持久化保存为原图与 54px 缩略图，超限返回 null
        /// </summary>
        public static ClipboardItem? ProcessAndSaveImage(Image originalImage, string? sourceApp = null)
        {
            if (originalImage == null) return null;

            try
            {
                AppPaths.EnsureDirectories();

                // 1. 编码原图为 PNG 内存字节流，计算哈希并检查 5MB 上限
                using var memoryStream = new MemoryStream();
                originalImage.Save(memoryStream, ImageFormat.Png);
                byte[] imageBytes = memoryStream.ToArray();

                if (imageBytes.Length > MaxImageFileSizeBytes)
                {
                    System.Diagnostics.Debug.WriteLine($"ImageProcessingService: 图片超过 5MB 上限 ({imageBytes.Length} 字节)，已丢弃");
                    return null;
                }

                string hash = ComputeSha256(imageBytes);
                string fileGuid = Guid.NewGuid().ToString("N");

                string originalFileName = $"{fileGuid}.png";
                string thumbnailFileName = $"{fileGuid}_thumb.png";

                string originalPath = Path.Combine(AppPaths.ClipboardImagesDir, originalFileName);
                string thumbnailPath = Path.Combine(AppPaths.ClipboardThumbnailsDir, thumbnailFileName);

                // 2. 写入原图文件
                File.WriteAllBytes(originalPath, imageBytes);

                // 3. 生成固定高度 54px 的等比缩略图
                int origW = originalImage.Width;
                int origH = originalImage.Height;
                int thumbH = ThumbnailHeight;
                int thumbW = Math.Max(1, (int)Math.Round((double)origW * thumbH / origH));

                using (var thumbBmp = new Bitmap(thumbW, thumbH, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(thumbBmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.Clear(Color.Transparent);

                        g.DrawImage(originalImage, new Rectangle(0, 0, thumbW, thumbH),
                            0, 0, origW, origH, GraphicsUnit.Pixel);
                    }
                    thumbBmp.Save(thumbnailPath, ImageFormat.Png);
                }

                return new ClipboardItem
                {
                    Id = Guid.NewGuid(),
                    Type = ClipboardItemType.Image,
                    ContentType = "Image",
                    Content = originalPath,
                    ImagePath = originalPath,
                    ThumbnailPath = thumbnailPath,
                    ImageHash = hash,
                    ImageSize = imageBytes.Length,
                    ImageResolution = $"{origW}×{origH}",
                    SourceApp = sourceApp,
                    CreatedAt = DateTimeOffset.Now
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageProcessingService: 处理图片失败 ({ex.Message})");
                return null;
            }
        }

        /// <summary>
        /// 以非锁定模式加载 BitmapImage（读取为内存流后断开文件句柄）
        /// </summary>
        public static BitmapImage? LoadBitmapWithoutLock(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                using var ms = new MemoryStream(bytes);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze(); // 允许跨线程访问
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageProcessingService: 加载图片失败 {filePath} ({ex.Message})");
                return null;
            }
        }

        /// <summary>
        /// 计算字节数组的 SHA-256 哈希
        /// </summary>
        public static string ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(data);
            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// 安全删除磁盘图片文件（含原图与缩略图）
        /// </summary>
        public static void SafeDeleteFiles(ClipboardItem item)
        {
            if (item == null) return;
            SafeDeleteFile(item.ImagePath);
            SafeDeleteFile(item.ThumbnailPath);
        }

        public static void SafeDeleteFile(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageProcessingService: 删除图片文件失败 {path} ({ex.Message})");
            }
        }
    }
}
