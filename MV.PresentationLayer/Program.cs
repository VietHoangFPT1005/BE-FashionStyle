using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.ApplicationLayer.Services;
using MV.DomainLayer.Configuration;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;
using MV.InfrastructureLayer.Repositories;
using MV.PresentationLayer.Hubs; // [CHAT SUPPORT - MỚI THÊM]
using System.Text;

namespace MV.PresentationLayer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Fix PostgreSQL DateTime UTC issue
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            var builder = WebApplication.CreateBuilder(args);

            // TỐI ƯU SỐ 3: Sửa lỗi cấu hình CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });

            // Configure Swagger
            builder.Services.AddSwaggerGen(c =>
            {
                c.EnableAnnotations();
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Lumina Style API",
                    Version = "v1"
                });

                c.UseAllOfToExtendReferenceSchemas();
                c.UseInlineDefinitionsForEnums();

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter: Bearer {your JWT token}"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // Configure JWT Authentication
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
                        )
                    };

                    // [CHAT SUPPORT - MỚI THÊM]
                    // SignalR trên mobile gửi JWT qua query string thay vì header
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            // Chỉ áp dụng cho hub endpoint
                            if (!string.IsNullOrEmpty(accessToken) &&
                                path.StartsWithSegments("/hubs/chat"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            builder.Services.AddAuthorization();

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                });

            // ==============================================
            // TỐI ƯU SỐ 2: Bổ sung các Dependency Injection bị thiếu
            // ==============================================

            // Đọc Settings từ appsettings.json
            builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
            builder.Services.Configure<SePaySettings>(builder.Configuration.GetSection("SePay"));
            builder.Services.Configure<GeminiSettings>(builder.Configuration.GetSection("Gemini"));
            builder.Services.Configure<GoogleSettings>(builder.Configuration.GetSection("Google"));

            // Register DbContext with connection pooling for production performance
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            if (!connectionString!.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase))
                connectionString += ";Maximum Pool Size=20;Minimum Pool Size=1;Connection Idle Lifetime=60";

            builder.Services.AddDbContext<FashionDbContext>(options =>
                options.UseNpgsql(connectionString,
                    npgsqlOptions => npgsqlOptions.MigrationsAssembly("MV.InfrastructureLayer"))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));

            // Repositories - Milestone 1
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IOtpCodeRepository, OtpCodeRepository>();
            builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            builder.Services.AddScoped<IUserAddressRepository, UserAddressRepository>();
            builder.Services.AddScoped<IUserBodyProfileRepository, UserBodyProfileRepository>();

            // Repositories - Milestone 2
            builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
            builder.Services.AddScoped<IProductRepository, ProductRepository>();
            builder.Services.AddScoped<IProductVariantRepository, ProductVariantRepository>();
            builder.Services.AddScoped<IProductImageRepository, ProductImageRepository>();
            builder.Services.AddScoped<ISizeGuideRepository, SizeGuideRepository>();
            builder.Services.AddScoped<ICartItemRepository, CartItemRepository>();
            builder.Services.AddScoped<IWishlistRepository, WishlistRepository>();
            builder.Services.AddScoped<IVoucherRepository, VoucherRepository>();
            builder.Services.AddScoped<IProductReviewRepository, ProductReviewRepository>();

            // Services - Milestone 1
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IBodyProfileService, BodyProfileService>();
            builder.Services.AddScoped<IAddressService, AddressService>();

            // Services - Milestone 2
            builder.Services.AddScoped<ICategoryService, CategoryService>();
            builder.Services.AddScoped<IProductService, ProductService>();
            builder.Services.AddScoped<ICartService, CartService>();
            builder.Services.AddScoped<IWishlistService, WishlistService>();
            builder.Services.AddScoped<IVoucherService, VoucherService>();

            // Repositories - Milestone 3
            builder.Services.AddScoped<IOrderRepository, OrderRepository>();
            builder.Services.AddScoped<IOrderItemRepository, OrderItemRepository>();
            builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
            builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
            builder.Services.AddScoped<ISepayTransactionRepository, SepayTransactionRepository>();

            // Services - Milestone 3
            builder.Services.AddScoped<IOrderService, OrderService>();
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            builder.Services.AddScoped<IReviewService, ReviewService>();

            // Background Services - Payment expiry auto-cancel + SePay polling
            builder.Services.AddHostedService<PaymentExpiryBackgroundService>();
            builder.Services.AddHostedService<SepayPollingBackgroundService>();

            // Repositories - Milestone 4
            builder.Services.AddScoped<IShipperLocationRepository, ShipperLocationRepository>();
            builder.Services.AddScoped<IChatAiHistoryRepository, ChatAiHistoryRepository>();

            // Services - Milestone 4
            builder.Services.AddScoped<IShipperService, ShipperService>();
            builder.Services.AddScoped<IChatAiService, ChatAiService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddScoped<IAdminService, AdminService>();

            // Services - Milestone 5 (Admin Management + Refund)
            builder.Services.AddScoped<IAdminProductService, AdminProductService>();
            builder.Services.AddScoped<IRefundService, RefundService>();

            // [CHAT SUPPORT - MỚI THÊM] Repository + Service cho chat hỗ trợ
            builder.Services.AddScoped<IChatSupportRepository, ChatSupportRepository>();
            builder.Services.AddScoped<IChatSupportService, ChatSupportService>();

            // [CHAT SUPPORT - MỚI THÊM] Đăng ký SignalR
            builder.Services.AddSignalR();

            // HttpClient for external API calls
            builder.Services.AddHttpClient();

            // Prevent background service exceptions from crashing the host
            builder.Services.Configure<HostOptions>(options =>
            {
                options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
            });

            builder.Services.AddEndpointsApiExplorer();

            var app = builder.Build();

            // ==============================================
            // TỐI ƯU SỐ 1: Bật giao diện Swagger
            // ==============================================
            var enableSwagger = app.Configuration.GetValue<bool>("EnableSwagger", false);
            if (app.Environment.IsDevelopment() || enableSwagger)
            {
                app.UseSwagger();
                app.UseSwaggerUI(); // Đã mở comment dòng này để giao diện không bị 404
            }

            // app.UseHttpsRedirection(); // Đã đóng comment để cho phép Flutter HTTP (10.0.2.2) truy cập không bị lỗi 307 Redirect
            app.UseStaticFiles();

            // Enable CORS - must be before Authentication/Authorization
            app.UseCors("AllowFrontend");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            // [CHAT SUPPORT - MỚI THÊM] Map SignalR Hub tại /hubs/chat
            app.MapHub<ChatHub>("/hubs/chat");

            app.Run();
        }
    }
}