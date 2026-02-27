using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.ApplicationLayer.Services;
using MV.DomainLayer.Configuration;
using MV.InfrastructureLayer.DBContext;
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

            // Đọc SmtpSettings từ appsettings.json
            builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));

            // Repositories


            // Services
            builder.Services.AddScoped<IEmailService, EmailService>();


            // Cloudinary (file upload service)


            // HttpClient for external API calls (Location service)
            builder.Services.AddHttpClient();

            // Register DbContext with connection string from appsettings
            builder.Services.AddDbContext<FashionDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
                    npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly("MV.InfrastructureLayer");
                        npgsqlOptions.MapEnum<MV.DomainLayer.Enums.ProductTypeEnum>(
                            "product_type_enum",
                            schemaName: null,
                            nameTranslator: new Npgsql.NameTranslation.NpgsqlNullNameTranslator());
                    })
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning)));

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