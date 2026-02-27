using Microsoft.AspNetCore.Mvc;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs;

namespace MV.PresentationLayer.Controllers
{
    [Route("api/emails")]
    [ApiController]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emailService;

        private static readonly Dictionary<string, (string Otp, DateTime ExpiryTime)> _otpStore = new();
        private static int _otpExpiryMinutes = 5;

        public EmailController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        // Gửi OTP
        [HttpPost("otps")]
        public async Task<IActionResult> SendOtp([FromBody] EmailRequest request)
        {
            if (string.IsNullOrEmpty(request.To))
                throw new ArgumentException("Invalid request data.");

            var otp = new Random().Next(100000, 999999).ToString();
            var expiryTime = DateTime.UtcNow.AddMinutes(_otpExpiryMinutes);
            _otpStore[request.To] = (otp, expiryTime);

            string htmlBody = $@"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
                <meta charset=""UTF-8"" />
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
                <title>IGCSE - Account Verification</title>
                <style>
                    @import url('https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:wght@400;500;600;700&display=swap');
                    body {{
                        margin: 0; padding: 0;
                        background-color: #f8f9fa;
                        font-family: 'Be Vietnam Pro', sans-serif;
                    }}
                    .container {{
                        max-width: 600px; margin: 30px auto;
                        background-color: #ffffff; border-radius: 12px;
                        box-shadow: 0 6px 20px rgba(0,0,0,0.05);
                        overflow: hidden; border: 1px solid #dee2e6;
                    }}
                    .header {{
                        background-color: #14b8a6; padding: 30px 20px;
                        text-align: center; color: white;
                    }}
                    .header img {{ width: 60px; margin-bottom: 10px; }}
                    .header h1 {{ margin: 0; font-size: 28px; font-weight: 700; }}
                    .content {{ padding: 40px 45px; color: #343a40; line-height: 1.6; }}
                    .content h2 {{
                        font-size: 24px; font-weight: 700;
                        margin: 0 0 15px 0; text-align: center; color: #0f766e;
                    }}
                    .content p {{
                        font-size: 16px; color: #495057;
                        text-align: center; margin-bottom: 25px;
                    }}
                    .otp-box {{
                        background-color: #f0fdfa; border-radius: 8px;
                        padding: 15px 20px; font-size: 36px; font-weight: 700;
                        color: #134e4a; letter-spacing: 8px; text-align: center;
                        margin: 20px auto 30px; border: 1px solid #ccfbf1;
                    }}
                    .expiry {{ font-size: 14px; color: #6c757d; text-align: center; }}
                    .footer {{
                        background-color: #f1f3f5; padding: 30px;
                        text-align: center; font-size: 13px; color: #6c757d;
                    }}
                    .footer a {{ color: #14b8a6; text-decoration: none; font-weight: 500; }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <img src=""https://img.icons8.com/plasticine/100/book.png"" alt=""IGCSE Logo""/>
                        <h1>IGCSE</h1>
                    </div>
                    <div class=""content"">
                        <h2>Verify Your Account</h2>
                        <p>Please use the code below to complete your account setup.</p>
                        <div class=""otp-box"">{otp}</div>
                        <p class=""expiry"">This code is valid for <strong>{_otpExpiryMinutes} minutes</strong>.</p>
                    </div>
                    <div class=""footer"">
                        <p>&copy; 2025 IGCSE. All rights reserved.</p>
                    </div>
                </div>
            </body>
            </html>";

            // Gửi email trực tiếp (không qua Kafka)
            await _emailService.SendEmailAsync(request.To, "Verification Code", htmlBody);

            return Ok(new { otp = otp, message = "OTP sent successfully." });
        }

        // Xác thực OTP
        [HttpPost("otps/verify")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Otp))
                throw new ArgumentException("Email and OTP are required.");

            if (!_otpStore.TryGetValue(request.Email, out var otpData))
                throw new KeyNotFoundException("No OTP found for this email.");

            if (DateTime.UtcNow > otpData.ExpiryTime)
            {
                _otpStore.Remove(request.Email);
                throw new TimeoutException($"OTP has expired after {_otpExpiryMinutes} minutes. Please request a new one.");
            }

            if (otpData.Otp != request.Otp)
                throw new UnauthorizedAccessException("Invalid OTP.");

            _otpStore.Remove(request.Email);

            return Ok(new { message = "OTP verified successfully." });
        }
    }
}
