using Microsoft.EntityFrameworkCore;
using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Admin.Request;
using MV.DomainLayer.DTOs.Admin.Response;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Voucher.Response;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.DBContext;

namespace MV.ApplicationLayer.Services
{
    public class AdminProductService : IAdminProductService
    {
        private readonly FashionDbContext _context;

        public AdminProductService(FashionDbContext context)
        {
            _context = context;
        }

        #region Product CRUD

        public async Task<ApiResponse<PaginatedResponse<AdminProductResponse>>> GetProductsAsync(
            int page, int pageSize, string? search, int? categoryId, bool? isActive)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 50) pageSize = 50;

            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages.Where(img => img.IsPrimary == true))
                .Include(p => p.ProductVariants)
                .Where(p => p.IsDeleted != true)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(s)
                    || (p.BrandName != null && p.BrandName.ToLower().Contains(s)));
            }

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            if (isActive.HasValue)
                query = query.Where(p => p.IsActive == isActive.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var response = new PaginatedResponse<AdminProductResponse>
            {
                Items = items.Select(MapToAdminProductResponse).ToList(),
                Pagination = new PaginationInfo
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = totalCount,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    HasNext = page * pageSize < totalCount,
                    HasPrevious = page > 1
                }
            };

            return ApiResponse<PaginatedResponse<AdminProductResponse>>.SuccessResponse(response);
        }

        public async Task<ApiResponse<AdminProductDetailResponse>> GetProductDetailAsync(int productId)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages.OrderBy(img => img.SortOrder))
                .Include(p => p.ProductVariants)
                .Include(p => p.SizeGuides)
                .FirstOrDefaultAsync(p => p.Id == productId && p.IsDeleted != true);

            if (product == null)
                return ApiResponse<AdminProductDetailResponse>.ErrorResponse("Product not found.");

            var response = new AdminProductDetailResponse
            {
                ProductId = product.Id,
                Name = product.Name,
                Slug = product.Slug,
                Description = product.Description,
                DetailDescription = product.DetailDescription,
                Material = product.Material,
                CareInstructions = product.CareInstructions,
                Gender = product.Gender,
                BrandName = product.BrandName,
                Tags = product.Tags,
                Price = product.Price,
                SalePrice = product.SalePrice,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name,
                AverageRating = product.AverageRating,
                TotalReviews = product.TotalReviews,
                ViewCount = product.ViewCount,
                SoldCount = product.SoldCount,
                IsActive = product.IsActive,
                IsFeatured = product.IsFeatured,
                VariantCount = product.ProductVariants.Count,
                TotalStock = product.ProductVariants.Sum(v => v.StockQuantity ?? 0),
                PrimaryImageUrl = product.ProductImages.FirstOrDefault(i => i.IsPrimary == true)?.ImageUrl
                    ?? product.ProductImages.FirstOrDefault()?.ImageUrl,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                Variants = product.ProductVariants.Select(v => new AdminVariantResponse
                {
                    VariantId = v.Id,
                    Sku = v.Sku,
                    Size = v.Size,
                    Color = v.Color,
                    StockQuantity = v.StockQuantity,
                    PriceAdjustment = v.PriceAdjustment,
                    IsActive = v.IsActive
                }).ToList(),
                Images = product.ProductImages.Select(i => new AdminProductImageResponse
                {
                    ImageId = i.Id,
                    ImageUrl = i.ImageUrl,
                    AltText = i.AltText,
                    IsPrimary = i.IsPrimary,
                    SortOrder = i.SortOrder
                }).ToList(),
                SizeGuides = product.SizeGuides.Select(sg => new AdminSizeGuideResponse
                {
                    SizeGuideId = sg.Id,
                    SizeName = sg.SizeName,
                    MinBust = sg.MinBust,
                    MaxBust = sg.MaxBust,
                    MinWaist = sg.MinWaist,
                    MaxWaist = sg.MaxWaist,
                    MinHips = sg.MinHips,
                    MaxHips = sg.MaxHips,
                    MinWeight = sg.MinWeight,
                    MaxWeight = sg.MaxWeight,
                    ChestCm = sg.ChestCm,
                    WaistCm = sg.WaistCm,
                    HipCm = sg.HipCm,
                    ShoulderCm = sg.ShoulderCm,
                    LengthCm = sg.LengthCm,
                    SleeveCm = sg.SleeveCm
                }).ToList()
            };

            return ApiResponse<AdminProductDetailResponse>.SuccessResponse(response);
        }

        public async Task<ApiResponse<AdminProductDetailResponse>> CreateProductAsync(CreateProductRequest request)
        {
            // Check slug unique
            if (await _context.Products.AnyAsync(p => p.Slug == request.Slug && p.IsDeleted != true))
                return ApiResponse<AdminProductDetailResponse>.ErrorResponse("Product slug already exists.");

            // Check category exists
            if (request.CategoryId.HasValue)
            {
                if (!await _context.Categories.AnyAsync(c => c.Id == request.CategoryId.Value))
                    return ApiResponse<AdminProductDetailResponse>.ErrorResponse("Category not found.");
            }

            var product = new Product
            {
                Name = request.Name,
                Slug = request.Slug,
                Description = request.Description,
                DetailDescription = request.DetailDescription,
                Material = request.Material,
                CareInstructions = request.CareInstructions,
                Gender = request.Gender ?? "UNISEX",
                BrandName = request.BrandName,
                Tags = request.Tags,
                Price = request.Price,
                SalePrice = request.SalePrice,
                CategoryId = request.CategoryId,
                IsFeatured = request.IsFeatured ?? false,
                IsActive = true,
                IsDeleted = false,
                AverageRating = 0,
                TotalReviews = 0,
                ViewCount = 0,
                SoldCount = 0,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return await GetProductDetailAsync(product.Id);
        }

        public async Task<ApiResponse<AdminProductDetailResponse>> UpdateProductAsync(int productId, UpdateProductRequest request)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId && p.IsDeleted != true);

            if (product == null)
                return ApiResponse<AdminProductDetailResponse>.ErrorResponse("Product not found.");

            // Check slug unique if changed
            if (!string.IsNullOrEmpty(request.Slug) && request.Slug != product.Slug)
            {
                if (await _context.Products.AnyAsync(p => p.Slug == request.Slug && p.Id != productId && p.IsDeleted != true))
                    return ApiResponse<AdminProductDetailResponse>.ErrorResponse("Product slug already exists.");
            }

            // Check category exists
            if (request.CategoryId.HasValue)
            {
                if (!await _context.Categories.AnyAsync(c => c.Id == request.CategoryId.Value))
                    return ApiResponse<AdminProductDetailResponse>.ErrorResponse("Category not found.");
            }

            if (request.Name != null) product.Name = request.Name;
            if (request.Slug != null) product.Slug = request.Slug;
            if (request.Description != null) product.Description = request.Description;
            if (request.DetailDescription != null) product.DetailDescription = request.DetailDescription;
            if (request.Material != null) product.Material = request.Material;
            if (request.CareInstructions != null) product.CareInstructions = request.CareInstructions;
            if (request.Gender != null) product.Gender = request.Gender;
            if (request.BrandName != null) product.BrandName = request.BrandName;
            if (request.Tags != null) product.Tags = request.Tags;
            if (request.Price.HasValue) product.Price = request.Price.Value;
            if (request.SalePrice.HasValue) product.SalePrice = request.SalePrice.Value;
            if (request.CategoryId.HasValue) product.CategoryId = request.CategoryId.Value;
            if (request.IsActive.HasValue) product.IsActive = request.IsActive.Value;
            if (request.IsFeatured.HasValue) product.IsFeatured = request.IsFeatured.Value;

            product.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return await GetProductDetailAsync(product.Id);
        }

        public async Task<ApiResponse<object>> DeleteProductAsync(int productId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId && p.IsDeleted != true);

            if (product == null)
                return ApiResponse<object>.ErrorResponse("Product not found.");

            // Soft delete
            product.IsDeleted = true;
            product.IsActive = false;
            product.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return ApiResponse<object>.SuccessResponse("Product deleted successfully.");
        }

        #endregion

        #region Variant CRUD

        public async Task<ApiResponse<AdminVariantResponse>> CreateVariantAsync(int productId, CreateVariantRequest request)
        {
            if (!await _context.Products.AnyAsync(p => p.Id == productId && p.IsDeleted != true))
                return ApiResponse<AdminVariantResponse>.ErrorResponse("Product not found.");

            // Check SKU unique
            if (await _context.ProductVariants.AnyAsync(v => v.Sku == request.Sku))
                return ApiResponse<AdminVariantResponse>.ErrorResponse("SKU already exists.");

            // Check size+color unique for this product
            if (await _context.ProductVariants.AnyAsync(v =>
                v.ProductId == productId && v.Size == request.Size && v.Color == request.Color))
                return ApiResponse<AdminVariantResponse>.ErrorResponse("Variant with this size and color already exists for this product.");

            var variant = new ProductVariant
            {
                ProductId = productId,
                Sku = request.Sku,
                Size = request.Size,
                Color = request.Color,
                StockQuantity = request.StockQuantity ?? 0,
                PriceAdjustment = request.PriceAdjustment ?? 0,
                IsActive = true
            };

            _context.ProductVariants.Add(variant);
            await _context.SaveChangesAsync();

            return ApiResponse<AdminVariantResponse>.SuccessResponse(new AdminVariantResponse
            {
                VariantId = variant.Id,
                Sku = variant.Sku,
                Size = variant.Size,
                Color = variant.Color,
                StockQuantity = variant.StockQuantity,
                PriceAdjustment = variant.PriceAdjustment,
                IsActive = variant.IsActive
            }, "Variant created successfully.");
        }

        public async Task<ApiResponse<AdminVariantResponse>> UpdateVariantAsync(int variantId, UpdateVariantRequest request)
        {
            var variant = await _context.ProductVariants.FindAsync(variantId);
            if (variant == null)
                return ApiResponse<AdminVariantResponse>.ErrorResponse("Variant not found.");

            // Check SKU unique if changed
            if (!string.IsNullOrEmpty(request.Sku) && request.Sku != variant.Sku)
            {
                if (await _context.ProductVariants.AnyAsync(v => v.Sku == request.Sku && v.Id != variantId))
                    return ApiResponse<AdminVariantResponse>.ErrorResponse("SKU already exists.");
            }

            if (request.Sku != null) variant.Sku = request.Sku;
            if (request.Size != null) variant.Size = request.Size;
            if (request.Color != null) variant.Color = request.Color;
            if (request.StockQuantity.HasValue) variant.StockQuantity = request.StockQuantity.Value;
            if (request.PriceAdjustment.HasValue) variant.PriceAdjustment = request.PriceAdjustment.Value;
            if (request.IsActive.HasValue) variant.IsActive = request.IsActive.Value;

            await _context.SaveChangesAsync();

            return ApiResponse<AdminVariantResponse>.SuccessResponse(new AdminVariantResponse
            {
                VariantId = variant.Id,
                Sku = variant.Sku,
                Size = variant.Size,
                Color = variant.Color,
                StockQuantity = variant.StockQuantity,
                PriceAdjustment = variant.PriceAdjustment,
                IsActive = variant.IsActive
            }, "Variant updated successfully.");
        }

        public async Task<ApiResponse<object>> DeleteVariantAsync(int variantId)
        {
            var variant = await _context.ProductVariants.FindAsync(variantId);
            if (variant == null)
                return ApiResponse<object>.ErrorResponse("Variant not found.");

            // Check if variant has order items
            if (await _context.OrderItems.AnyAsync(oi => oi.ProductVariantId == variantId))
            {
                // Soft delete by deactivating
                variant.IsActive = false;
                await _context.SaveChangesAsync();
                return ApiResponse<object>.SuccessResponse("Variant deactivated (has existing orders).");
            }

            _context.ProductVariants.Remove(variant);
            await _context.SaveChangesAsync();

            return ApiResponse<object>.SuccessResponse("Variant deleted successfully.");
        }

        #endregion

        #region Image CRUD

        public async Task<ApiResponse<AdminProductImageResponse>> CreateProductImageAsync(
            int productId, CreateProductImageRequest request)
        {
            if (!await _context.Products.AnyAsync(p => p.Id == productId && p.IsDeleted != true))
                return ApiResponse<AdminProductImageResponse>.ErrorResponse("Product not found.");

            // If this is set as primary, unset other primary images
            if (request.IsPrimary == true)
            {
                await _context.ProductImages
                    .Where(pi => pi.ProductId == productId && pi.IsPrimary == true)
                    .ExecuteUpdateAsync(s => s.SetProperty(pi => pi.IsPrimary, false));
            }

            var image = new ProductImage
            {
                ProductId = productId,
                ImageUrl = request.ImageUrl,
                AltText = request.AltText,
                IsPrimary = request.IsPrimary ?? false,
                SortOrder = request.SortOrder ?? 0,
                CreatedAt = DateTime.Now
            };

            _context.ProductImages.Add(image);
            await _context.SaveChangesAsync();

            return ApiResponse<AdminProductImageResponse>.SuccessResponse(new AdminProductImageResponse
            {
                ImageId = image.Id,
                ImageUrl = image.ImageUrl,
                AltText = image.AltText,
                IsPrimary = image.IsPrimary,
                SortOrder = image.SortOrder
            }, "Image added successfully.");
        }

        public async Task<ApiResponse<object>> DeleteProductImageAsync(int imageId)
        {
            var image = await _context.ProductImages.FindAsync(imageId);
            if (image == null)
                return ApiResponse<object>.ErrorResponse("Image not found.");

            _context.ProductImages.Remove(image);
            await _context.SaveChangesAsync();

            return ApiResponse<object>.SuccessResponse("Image deleted successfully.");
        }

        #endregion

        #region Size Guide

        public async Task<ApiResponse<object>> UpsertSizeGuideAsync(int productId, CreateSizeGuideRequest request)
        {
            if (!await _context.Products.AnyAsync(p => p.Id == productId && p.IsDeleted != true))
                return ApiResponse<object>.ErrorResponse("Product not found.");

            // Remove existing size guides
            var existingGuides = await _context.SizeGuides
                .Where(sg => sg.ProductId == productId)
                .ToListAsync();
            _context.SizeGuides.RemoveRange(existingGuides);

            // Add new size guides
            var newGuides = request.Items.Select(item => new SizeGuide
            {
                ProductId = productId,
                SizeName = item.SizeName,
                MinBust = item.MinBust,
                MaxBust = item.MaxBust,
                MinWaist = item.MinWaist,
                MaxWaist = item.MaxWaist,
                MinHips = item.MinHips,
                MaxHips = item.MaxHips,
                MinWeight = item.MinWeight,
                MaxWeight = item.MaxWeight,
                ChestCm = item.ChestCm,
                WaistCm = item.WaistCm,
                HipCm = item.HipCm,
                ShoulderCm = item.ShoulderCm,
                LengthCm = item.LengthCm,
                SleeveCm = item.SleeveCm
            }).ToList();

            _context.SizeGuides.AddRange(newGuides);
            await _context.SaveChangesAsync();

            return ApiResponse<object>.SuccessResponse(new { count = newGuides.Count },
                $"Size guide updated with {newGuides.Count} sizes.");
        }

        #endregion

        #region Category CRUD

        public async Task<ApiResponse<object>> CreateCategoryAsync(CreateCategoryRequest request)
        {
            // Check slug unique
            if (await _context.Categories.AnyAsync(c => c.Slug == request.Slug))
                return ApiResponse<object>.ErrorResponse("Category slug already exists.");

            // Check parent exists
            if (request.ParentId.HasValue)
            {
                if (!await _context.Categories.AnyAsync(c => c.Id == request.ParentId.Value))
                    return ApiResponse<object>.ErrorResponse("Parent category not found.");
            }

            var category = new Category
            {
                Name = request.Name,
                Slug = request.Slug,
                Description = request.Description,
                ImageUrl = request.ImageUrl,
                ParentId = request.ParentId,
                SortOrder = request.SortOrder ?? 0,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return ApiResponse<object>.SuccessResponse(new
            {
                categoryId = category.Id,
                name = category.Name,
                slug = category.Slug,
                parentId = category.ParentId,
                isActive = category.IsActive
            }, "Category created successfully.");
        }

        public async Task<ApiResponse<object>> UpdateCategoryAsync(int categoryId, UpdateCategoryRequest request)
        {
            var category = await _context.Categories.FindAsync(categoryId);
            if (category == null)
                return ApiResponse<object>.ErrorResponse("Category not found.");

            // Check slug unique if changed
            if (!string.IsNullOrEmpty(request.Slug) && request.Slug != category.Slug)
            {
                if (await _context.Categories.AnyAsync(c => c.Slug == request.Slug && c.Id != categoryId))
                    return ApiResponse<object>.ErrorResponse("Category slug already exists.");
            }

            // Prevent self-referencing parent
            if (request.ParentId.HasValue && request.ParentId.Value == categoryId)
                return ApiResponse<object>.ErrorResponse("Category cannot be its own parent.");

            if (request.Name != null) category.Name = request.Name;
            if (request.Slug != null) category.Slug = request.Slug;
            if (request.Description != null) category.Description = request.Description;
            if (request.ImageUrl != null) category.ImageUrl = request.ImageUrl;
            if (request.ParentId.HasValue) category.ParentId = request.ParentId.Value;
            if (request.SortOrder.HasValue) category.SortOrder = request.SortOrder.Value;
            if (request.IsActive.HasValue) category.IsActive = request.IsActive.Value;

            await _context.SaveChangesAsync();

            return ApiResponse<object>.SuccessResponse(new
            {
                categoryId = category.Id,
                name = category.Name,
                slug = category.Slug,
                isActive = category.IsActive
            }, "Category updated successfully.");
        }

        public async Task<ApiResponse<object>> DeleteCategoryAsync(int categoryId)
        {
            var category = await _context.Categories
                .Include(c => c.InverseParent)
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == categoryId);

            if (category == null)
                return ApiResponse<object>.ErrorResponse("Category not found.");

            // Check if has children
            if (category.InverseParent.Any())
                return ApiResponse<object>.ErrorResponse("Cannot delete category with sub-categories. Remove sub-categories first.");

            // Check if has products
            if (category.Products.Any(p => p.IsDeleted != true))
                return ApiResponse<object>.ErrorResponse("Cannot delete category with active products. Move or delete products first.");

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return ApiResponse<object>.SuccessResponse("Category deleted successfully.");
        }

        #endregion

        #region Voucher CRUD

        public async Task<ApiResponse<PaginatedResponse<VoucherResponse>>> GetVouchersAsync(
            int page, int pageSize, bool? isActive)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 50) pageSize = 50;

            var query = _context.Vouchers.AsQueryable();

            if (isActive.HasValue)
                query = query.Where(v => v.IsActive == isActive.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var response = new PaginatedResponse<VoucherResponse>
            {
                Items = items.Select(v => new VoucherResponse
                {
                    VoucherId = v.Id,
                    Code = v.Code,
                    Description = v.Description,
                    DiscountType = v.DiscountType,
                    DiscountValue = v.DiscountValue,
                    MinOrderAmount = v.MinOrderAmount,
                    MaxDiscountAmount = v.MaxDiscountAmount,
                    StartDate = v.StartDate,
                    EndDate = v.EndDate,
                    UsageLimit = v.UsageLimit,
                    UsedCount = v.UsedCount,
                    IsActive = v.IsActive,
                    CreatedAt = v.CreatedAt
                }).ToList(),
                Pagination = new PaginationInfo
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = totalCount,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    HasNext = page * pageSize < totalCount,
                    HasPrevious = page > 1
                }
            };

            return ApiResponse<PaginatedResponse<VoucherResponse>>.SuccessResponse(response);
        }

        public async Task<ApiResponse<VoucherResponse>> CreateVoucherAsync(CreateVoucherRequest request)
        {
            // Check code unique
            if (await _context.Vouchers.AnyAsync(v => v.Code == request.Code))
                return ApiResponse<VoucherResponse>.ErrorResponse("Voucher code already exists.");

            if (request.EndDate <= request.StartDate)
                return ApiResponse<VoucherResponse>.ErrorResponse("End date must be after start date.");

            var discountType = request.DiscountType.ToUpper();
            if (discountType != "PERCENTAGE" && discountType != "FIXED_AMOUNT")
                return ApiResponse<VoucherResponse>.ErrorResponse("Discount type must be PERCENTAGE or FIXED_AMOUNT.");

            if (discountType == "PERCENTAGE" && request.DiscountValue > 100)
                return ApiResponse<VoucherResponse>.ErrorResponse("Percentage discount cannot exceed 100%.");

            var voucher = new Voucher
            {
                Code = request.Code.ToUpper(),
                Description = request.Description,
                DiscountType = discountType,
                DiscountValue = request.DiscountValue,
                MinOrderAmount = request.MinOrderAmount ?? 0,
                MaxDiscountAmount = request.MaxDiscountAmount,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                UsageLimit = request.UsageLimit ?? 100,
                UsedCount = 0,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _context.Vouchers.Add(voucher);
            await _context.SaveChangesAsync();

            return ApiResponse<VoucherResponse>.SuccessResponse(MapToVoucherResponse(voucher),
                "Voucher created successfully.");
        }

        public async Task<ApiResponse<VoucherResponse>> UpdateVoucherAsync(int voucherId, UpdateVoucherRequest request)
        {
            var voucher = await _context.Vouchers.FindAsync(voucherId);
            if (voucher == null)
                return ApiResponse<VoucherResponse>.ErrorResponse("Voucher not found.");

            if (request.DiscountType != null)
            {
                var dt = request.DiscountType.ToUpper();
                if (dt != "PERCENTAGE" && dt != "FIXED_AMOUNT")
                    return ApiResponse<VoucherResponse>.ErrorResponse("Discount type must be PERCENTAGE or FIXED_AMOUNT.");
                voucher.DiscountType = dt;
            }

            if (request.Description != null) voucher.Description = request.Description;
            if (request.DiscountValue.HasValue) voucher.DiscountValue = request.DiscountValue.Value;
            if (request.MinOrderAmount.HasValue) voucher.MinOrderAmount = request.MinOrderAmount.Value;
            if (request.MaxDiscountAmount.HasValue) voucher.MaxDiscountAmount = request.MaxDiscountAmount.Value;
            if (request.StartDate.HasValue) voucher.StartDate = request.StartDate.Value;
            if (request.EndDate.HasValue) voucher.EndDate = request.EndDate.Value;
            if (request.UsageLimit.HasValue) voucher.UsageLimit = request.UsageLimit.Value;
            if (request.IsActive.HasValue) voucher.IsActive = request.IsActive.Value;

            await _context.SaveChangesAsync();

            return ApiResponse<VoucherResponse>.SuccessResponse(MapToVoucherResponse(voucher),
                "Voucher updated successfully.");
        }

        public async Task<ApiResponse<object>> DeleteVoucherAsync(int voucherId)
        {
            var voucher = await _context.Vouchers
                .Include(v => v.Orders)
                .FirstOrDefaultAsync(v => v.Id == voucherId);

            if (voucher == null)
                return ApiResponse<object>.ErrorResponse("Voucher not found.");

            // If voucher has been used in orders, just deactivate
            if (voucher.Orders.Any())
            {
                voucher.IsActive = false;
                await _context.SaveChangesAsync();
                return ApiResponse<object>.SuccessResponse("Voucher deactivated (has existing orders).");
            }

            _context.Vouchers.Remove(voucher);
            await _context.SaveChangesAsync();

            return ApiResponse<object>.SuccessResponse("Voucher deleted successfully.");
        }

        #endregion

        #region Helpers

        private AdminProductResponse MapToAdminProductResponse(Product p)
        {
            return new AdminProductResponse
            {
                ProductId = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Description = p.Description,
                Material = p.Material,
                Gender = p.Gender,
                BrandName = p.BrandName,
                Tags = p.Tags,
                Price = p.Price,
                SalePrice = p.SalePrice,
                CategoryId = p.CategoryId,
                CategoryName = p.Category?.Name,
                AverageRating = p.AverageRating,
                TotalReviews = p.TotalReviews,
                ViewCount = p.ViewCount,
                SoldCount = p.SoldCount,
                IsActive = p.IsActive,
                IsFeatured = p.IsFeatured,
                VariantCount = p.ProductVariants.Count,
                TotalStock = p.ProductVariants.Sum(v => v.StockQuantity ?? 0),
                PrimaryImageUrl = p.ProductImages.FirstOrDefault()?.ImageUrl,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            };
        }

        private VoucherResponse MapToVoucherResponse(Voucher v)
        {
            return new VoucherResponse
            {
                VoucherId = v.Id,
                Code = v.Code,
                Description = v.Description,
                DiscountType = v.DiscountType,
                DiscountValue = v.DiscountValue,
                MinOrderAmount = v.MinOrderAmount,
                MaxDiscountAmount = v.MaxDiscountAmount,
                StartDate = v.StartDate,
                EndDate = v.EndDate,
                UsageLimit = v.UsageLimit,
                UsedCount = v.UsedCount,
                IsActive = v.IsActive,
                CreatedAt = v.CreatedAt
            };
        }

        #endregion
    }
}
