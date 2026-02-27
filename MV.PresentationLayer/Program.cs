using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.ApplicationLayer.Services;
using MV.DomainLayer.Configuration;
using MV.InfrastructureLayer.DBContext;
using MV.InfrastructureLayer.Interfaces;
using MV.InfrastructureLayer.Repositories;
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
                    policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
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

            // Register DbContext
            builder.Services.AddDbContext<FashionDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

            // Services - Milestone 3
            builder.Services.AddScoped<IOrderService, OrderService>();
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            builder.Services.AddScoped<IReviewService, ReviewService>();

            // HttpClient for external API calls
            builder.Services.AddHttpClient();

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

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            // Enable CORS - must be before Authentication/Authorization
            app.UseCors("AllowFrontend");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}