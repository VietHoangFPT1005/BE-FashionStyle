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

            // Split OTP into individual digits for styled display
            var d1 = otp[0]; var d2 = otp[1]; var d3 = otp[2];
            var d4 = otp[3]; var d5 = otp[4]; var d6 = otp[5];

            string htmlBody = $@"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
                <meta charset=""UTF-8"" />
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
                <title>BigSize Fashion - Verification Code</title>
            </head>
            <body style=""margin:0;padding:0;background-color:#F5F0EB;font-family:'Segoe UI',Helvetica,Arial,sans-serif;"">
                <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#F5F0EB;padding:40px 0;"">
                    <tr>
                        <td align=""center"">
                            <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);"">
                                <!-- Header -->
                                <tr>
                                    <td style=""background:linear-gradient(135deg,#1C1C1E 0%,#2C2C2E 50%,#3A3A3C 100%);padding:40px 40px 35px;text-align:center;"">
                                        <h1 style=""margin:0;font-size:28px;font-weight:700;color:#D4A574;letter-spacing:6px;text-transform:uppercase;"">BIGSIZE FASHION</h1>
                                        <p style=""margin:8px 0 0;font-size:12px;color:#8E8E93;letter-spacing:3px;text-transform:uppercase;"">Your Style, Your Confidence</p>
                                    </td>
                                </tr>

                                <!-- Decorative line -->
                                <tr>
                                    <td style=""padding:0 40px;"">
                                        <div style=""height:3px;background:linear-gradient(90deg,#D4A574,#B76E79,#D4A574);border-radius:2px;""></div>
                                    </td>
                                </tr>

                                <!-- Content -->
                                <tr>
                                    <td style=""padding:40px 45px 20px;text-align:center;"">
                                        <h2 style=""margin:0 0 12px;font-size:24px;font-weight:700;color:#1C1C1E;"">Account Verification</h2>
                                        <p style=""margin:0 0 30px;font-size:15px;color:#636366;line-height:1.7;"">
                                            Thank you for choosing <strong style=""color:#1C1C1E;"">BigSize Fashion</strong>.<br/>
                                            Enter the verification code below to secure your account.
                                        </p>

                                        <!-- OTP Digits -->
                                        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" align=""center"" style=""margin:0 auto 25px;"">
                                            <tr>
                                                <td style=""padding:0 4px;""><div style=""width:48px;height:56px;background-color:#1C1C1E;border-radius:10px;text-align:center;line-height:56px;font-size:26px;font-weight:700;color:#D4A574;"">{d1}</div></td>
                                                <td style=""padding:0 4px;""><div style=""width:48px;height:56px;background-color:#1C1C1E;border-radius:10px;text-align:center;line-height:56px;font-size:26px;font-weight:700;color:#D4A574;"">{d2}</div></td>
                                                <td style=""padding:0 4px;""><div style=""width:48px;height:56px;background-color:#1C1C1E;border-radius:10px;text-align:center;line-height:56px;font-size:26px;font-weight:700;color:#D4A574;"">{d3}</div></td>
                                                <td style=""padding:0 8px;""><div style=""width:12px;height:4px;background-color:#D4A574;border-radius:2px;margin-top:26px;""></div></td>
                                                <td style=""padding:0 4px;""><div style=""width:48px;height:56px;background-color:#1C1C1E;border-radius:10px;text-align:center;line-height:56px;font-size:26px;font-weight:700;color:#D4A574;"">{d4}</div></td>
                                                <td style=""padding:0 4px;""><div style=""width:48px;height:56px;background-color:#1C1C1E;border-radius:10px;text-align:center;line-height:56px;font-size:26px;font-weight:700;color:#D4A574;"">{d5}</div></td>
                                                <td style=""padding:0 4px;""><div style=""width:48px;height:56px;background-color:#1C1C1E;border-radius:10px;text-align:center;line-height:56px;font-size:26px;font-weight:700;color:#D4A574;"">{d6}</div></td>
                                            </tr>
                                        </table>

                                        <!-- Timer -->
                                        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" align=""center"" style=""margin:0 auto 30px;"">
                                            <tr>
                                                <td style=""background-color:#FFF8F0;border:1px solid #F0E0D0;border-radius:8px;padding:10px 20px;text-align:center;"">
                                                    <span style=""font-size:13px;color:#B76E79;font-weight:600;"">This code expires in {_otpExpiryMinutes} minutes</span>
                                                </td>
                                            </tr>
                                        </table>

                                        <p style=""margin:0 0 10px;font-size:13px;color:#AEAEB2;line-height:1.6;"">If you didn't request this code, please safely ignore this email.</p>
                                    </td>
                                </tr>

                                <!-- Divider -->
                                <tr>
                                    <td style=""padding:0 45px;"">
                                        <div style=""height:1px;background-color:#E5E5EA;""></div>
                                    </td>
                                </tr>

                                <!-- Support Section -->
                                <tr>
                                    <td style=""padding:25px 45px 30px;"">
                                        <h3 style=""margin:0 0 12px;font-size:15px;font-weight:700;color:#1C1C1E;"">Need Help?</h3>
                                        <p style=""margin:0 0 6px;font-size:13px;color:#636366;"">
                                            Our support team is always here for you:
                                        </p>
                                        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin-top:8px;"">
                                            <tr>
                                                <td style=""padding:4px 0;font-size:13px;color:#636366;"">
                                                    Phone: <a href=""tel:0775743304"" style=""color:#B76E79;text-decoration:none;font-weight:600;"">077 574 3304</a>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style=""padding:4px 0;font-size:13px;color:#636366;"">
                                                    Email: <a href=""mailto:hoangnv10052004@gmail.com"" style=""color:#B76E79;text-decoration:none;font-weight:600;"">hoangnv10052004@gmail.com</a>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style=""padding:4px 0;font-size:13px;color:#636366;"">
                                                    Facebook: <a href=""https://www.facebook.com/viethoang.ng1005"" target=""_blank"" style=""color:#B76E79;text-decoration:none;font-weight:600;"">Message us on Facebook</a>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>

                                <!-- Footer -->
                                <tr>
                                    <td style=""background-color:#1C1C1E;padding:25px 40px;text-align:center;"">
                                        <p style=""margin:0 0 6px;font-size:12px;color:#D4A574;letter-spacing:2px;text-transform:uppercase;font-weight:600;"">BigSize Fashion</p>
                                        <p style=""margin:0;font-size:11px;color:#636366;"">
                                            &copy; 2026 BigSize Fashion. All rights reserved.
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>";

            await _emailService.SendEmailAsync(request.To, "BigSize Fashion - Verification Code", htmlBody);

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
