using System;
using QRCoder;
using SkiaSharp;

namespace RestaurantManagementSystem.Services
{
    /// <summary>
    /// Service for generating UPI payment QR codes (Cross-platform using QRCoder + SkiaSharp)
    /// </summary>
    public class UPIQRCodeService
    {
        /// <summary>
        /// Generate UPI payment URL
        /// </summary>
        /// <param name="upiId">UPI ID (e.g., restaurant@paytm)</param>
        /// <param name="payeeName">Payee name</param>
        /// <param name="amount">Payment amount</param>
        /// <param name="transactionNote">Transaction note (e.g., Order Number)</param>
        /// <returns>UPI payment URL</returns>
        public static string GenerateUPIPaymentUrl(string upiId, string payeeName, decimal amount, string transactionNote)
        {
            // UPI Deep Link Format
            // upi://pay?pa=<UPI_ID>&pn=<PAYEE_NAME>&am=<AMOUNT>&cu=INR&tn=<TRANSACTION_NOTE>
            return $"upi://pay?pa={Uri.EscapeDataString(upiId)}&pn={Uri.EscapeDataString(payeeName)}&am={amount:F2}&cu=INR&tn={Uri.EscapeDataString(transactionNote)}";
        }

        /// <summary>
        /// Generate QR code image as Base64 string (QRCoder + SkiaSharp - Cross-platform)
        /// </summary>
        /// <param name="upiUrl">UPI payment URL</param>
        /// <param name="pixelsPerModule">Size of each QR module (default 20)</param>
        /// <returns>Base64 encoded PNG image</returns>
        public static string GenerateQRCodeBase64(string upiUrl, int pixelsPerModule = 20)
        {
            using (var qrGenerator = new QRCodeGenerator())
            using (var qrCodeData = qrGenerator.CreateQrCode(upiUrl, QRCodeGenerator.ECCLevel.M))
            {
                // Get the QR code matrix
                var modules = qrCodeData.ModuleMatrix;
                var moduleCount = modules.Count;
                
                // Calculate image size
                var imageSize = moduleCount * pixelsPerModule;
                var info = new SKImageInfo(imageSize, imageSize);
                
                using (var surface = SKSurface.Create(info))
                {
                    var canvas = surface.Canvas;
                    canvas.Clear(SKColors.White);
                    
                    // Draw QR code modules
                    using (var paint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill })
                    {
                        for (int row = 0; row < moduleCount; row++)
                        {
                            for (int col = 0; col < moduleCount; col++)
                            {
                                if (modules[row][col])
                                {
                                    var rect = new SKRect(
                                        col * pixelsPerModule,
                                        row * pixelsPerModule,
                                        (col + 1) * pixelsPerModule,
                                        (row + 1) * pixelsPerModule
                                    );
                                    canvas.DrawRect(rect, paint);
                                }
                            }
                        }
                    }
                    
                    using (var image = surface.Snapshot())
                    using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                    {
                        return Convert.ToBase64String(data.ToArray());
                    }
                }
            }
        }

        /// <summary>
        /// Generate complete UPI QR code as Base64 data URL
        /// </summary>
        /// <param name="upiId">UPI ID</param>
        /// <param name="payeeName">Payee name</param>
        /// <param name="amount">Payment amount</param>
        /// <param name="transactionNote">Transaction note</param>
        /// <param name="pixelsPerModule">QR size</param>
        /// <returns>Data URL for img src</returns>
        public static string GenerateUPIQRCodeDataUrl(string upiId, string payeeName, decimal amount, string transactionNote, int pixelsPerModule = 20)
        {
            string upiUrl = GenerateUPIPaymentUrl(upiId, payeeName, amount, transactionNote);
            string base64 = GenerateQRCodeBase64(upiUrl, pixelsPerModule);
            return $"data:image/png;base64,{base64}";
        }
    }
}
