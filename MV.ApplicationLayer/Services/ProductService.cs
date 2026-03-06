using MV.ApplicationLayer.ServiceInterfaces;
using MV.DomainLayer.DTOs.Common;
using MV.DomainLayer.DTOs.Product.Response;
using MV.DomainLayer.Entities;
using MV.InfrastructureLayer.Interfaces;

namespace MV.ApplicationLayer.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IProductVariantRepository _variantRepository;
        private readonly IProductImageRepository _imageRepository;
        private readonly ISizeGuideRepository _sizeGuideRepository;
        private readonly IProductReviewRepository _reviewRepository;
        private readonly IUserBodyProfileRepository _bodyProfileRepository;

        public ProductService(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IProductVariantRepository variantRepository,
            IProductImageRepository imageRepository,
            ISizeGuideRepository sizeGuideRepository,
            IProductReviewRepository reviewRepository,
            IUserBodyProfileRepository bodyProfileRepository)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _variantRepository = variantRepository;
            _imageRepository = imageRepository;
            _sizeGuideRepository = sizeGuideRepository;
            _reviewRepository = reviewRepository;
            _bodyProfileRepository = bodyProfileRepository;
        }

        #region Product Listing

        public async Task<ApiResponse<PaginatedResponse<ProductListResponse>>> GetProductsAsync(
            int page, int pageSize,
            int? categoryId, string? gender, string? search,
            string? tags, decimal? minPrice, decimal? maxPrice,
            string sortBy, string sortOrder, bool? isFeatured)
        {
            // Clamp pageSize
            if (pageSize < 1) pageSize = 12;
            if (pageSize > 50) pageSize = 50;
            if (page < 1) page = 1;

            var (items, totalCount) = await _productRepository.GetProductsPagedAsync(
                page, pageSize, categoryId, gender, search, tags,
                minPrice, maxPrice, sortBy, sortOrder, isFeatured);

            var response = BuildPaginatedProductList(items, page, pageSize, totalCount);
            return ApiResponse<PaginatedResponse<ProductListResponse>>.SuccessResponse(response);
        }

        public async Task<ApiResponse<List<ProductSearchResponse>>> SearchProductsAsync(string keyword, int limit)
        {
            if (string.IsNullOrWhiteSpace(keyword) || keyword.Length < 2)
                return ApiResponse<List<ProductSearchResponse>>.ErrorResponse(
                    "Search keyword must be at least 2 characters.");

            if (limit < 1) limit = 10;
            if (limit > 20) limit = 20;

            var products = await _productRepository.SearchProductsAsync(keyword, limit);

            var response = products.Select(p => new ProductSearchResponse
            {
                ProductId = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Price = p.Price,
                SalePrice = p.SalePrice,
                PrimaryImage = p.ProductImages.FirstOrDefault(img => img.IsPrimary == true)?.ImageUrl
            }).ToList();

            return ApiResponse<List<ProductSearchResponse>>.SuccessResponse(response);
        }

        public async Task<ApiResponse<PaginatedResponse<ProductListResponse>>> GetProductsByCategoryAsync(
            int categoryId, int page, int pageSize,
            string? gender, string? search,
            string? tags, decimal? minPrice, decimal? maxPrice,
            string sortBy, string sortOrder, bool? isFeatured)
        {
            // Verify category exists
            var category = await _categoryRepository.GetByIdAsync(categoryId);
            if (category == null)
                return ApiResponse<PaginatedResponse<ProductListResponse>>.ErrorResponse(
                    "Category not found.");

            // Clamp pageSize
            if (pageSize < 1) pageSize = 12;
            if (pageSize > 50) pageSize = 50;
            if (page < 1) page = 1;

            // Get child category IDs for parent category
            var childIds = await _categoryRepository.GetChildCategoryIdsAsync(categoryId);
            var allCategoryIds = new List<int> { categoryId };
            allCategoryIds.AddRange(childIds);

            var (items, totalCount) = await _productRepository.GetProductsPagedAsync(
                page, pageSize, null, gender, search, tags,
                minPrice, maxPrice, sortBy, sortOrder, isFeatured,
                categoryIds: allCategoryIds);

            var response = BuildPaginatedProductList(items, page, pageSize, totalCount);
            return ApiResponse<PaginatedResponse<ProductListResponse>>.SuccessResponse(response);
        }

        #endregion

        #region Product Detail

        public async Task<ApiResponse<ProductDetailResponse>> GetProductDetailAsync(int productId)
        {
            var product = await _productRepository.GetDetailByIdAsync(productId);
            if (product == null)
                return ApiResponse<ProductDetailResponse>.ErrorResponse("Product not found.");

            // Increment view count (fire and forget)
            await _productRepository.IncrementViewCountAsync(productId);

            var response = new ProductDetailResponse
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
                Category = product.Category != null ? new ProductCategoryInfo
                {
                    CategoryId = product.Category.Id,
                    Name = product.Category.Name
                } : null,
                Images = product.ProductImages.Select(img => new ProductImageResponse
                {
                    ImageId = img.Id,
                    ImageUrl = img.ImageUrl,
                    AltText = img.AltText,
                    IsPrimary = img.IsPrimary == true,
                    SortOrder = img.SortOrder ?? 0
                }).ToList(),
                Variants = product.ProductVariants.Select(v => new ProductVariantResponse
                {
                    VariantId = v.Id,
                    Sku = v.Sku,
                    Size = v.Size,
                    Color = v.Color,
                    StockQuantity = v.StockQuantity ?? 0,
                    PriceAdjustment = v.PriceAdjustment ?? 0,
                    InStock = (v.StockQuantity ?? 0) > 0
                }).ToList(),
                HasSizeGuide = product.SizeGuides.Any(),
                AverageRating = product.AverageRating,
                TotalReviews = product.TotalReviews ?? 0,
                ViewCount = (product.ViewCount ?? 0) + 1, // Include the increment
                SoldCount = product.SoldCount ?? 0,
                InStock = product.ProductVariants.Any(v => (v.StockQuantity ?? 0) > 0),
                IsFeatured = product.IsFeatured == true,
                CreatedAt = product.CreatedAt
            };

            return ApiResponse<ProductDetailResponse>.SuccessResponse(response);
        }

        public async Task<ApiResponse<VariantListResponse>> GetProductVariantsAsync(int productId)
        {
            if (!await _productRepository.ExistsAndActiveAsync(productId))
                return ApiResponse<VariantListResponse>.ErrorResponse("Product not found.");

            var variants = await _variantRepository.GetByProductIdAsync(productId);

            var response = new VariantListResponse
            {
                Colors = variants.Select(v => v.Color).Distinct().OrderBy(c => c).ToList(),
                Sizes = variants.Select(v => v.Size).Distinct().ToList(),
                Variants = variants.Select(v => new ProductVariantResponse
                {
                    VariantId = v.Id,
                    Sku = v.Sku,
                    Size = v.Size,
                    Color = v.Color,
                    StockQuantity = v.StockQuantity ?? 0,
                    PriceAdjustment = v.PriceAdjustment ?? 0,
                    InStock = (v.StockQuantity ?? 0) > 0
                }).ToList()
            };

            return ApiResponse<VariantListResponse>.SuccessResponse(response);
        }

        public async Task<ApiResponse<List<ProductImageResponse>>> GetProductImagesAsync(int productId)
        {
            if (!await _productRepository.ExistsAndActiveAsync(productId))
                return ApiResponse<List<ProductImageResponse>>.ErrorResponse("Product not found.");

            var images = await _imageRepository.GetByProductIdAsync(productId);

            var response = images.Select(img => new ProductImageResponse
            {
                ImageId = img.Id,
                ImageUrl = img.ImageUrl,
                AltText = img.AltText,
                IsPrimary = img.IsPrimary == true,
                SortOrder = img.SortOrder ?? 0
            }).ToList();

            return ApiResponse<List<ProductImageResponse>>.SuccessResponse(response);
        }

        #endregion

        #region Size Guide & AI Fit-Check

        public async Task<ApiResponse<List<SizeGuideResponse>>> GetSizeGuideAsync(int productId)
        {
            if (!await _productRepository.ExistsAndActiveAsync(productId))
                return ApiResponse<List<SizeGuideResponse>>.ErrorResponse("Product not found.");

            var sizeGuides = await _sizeGuideRepository.GetByProductIdAsync(productId);
            if (!sizeGuides.Any())
                return ApiResponse<List<SizeGuideResponse>>.ErrorResponse(
                    "This product does not have a size guide yet.");

            var response = sizeGuides.Select(sg => new SizeGuideResponse
            {
                SizeName = sg.SizeName,
                BodyMeasurements = new BodyMeasurementRange
                {
                    Bust = (sg.MinBust.HasValue || sg.MaxBust.HasValue) ? new MeasurementRange { Min = sg.MinBust, Max = sg.MaxBust } : null,
                    Waist = (sg.MinWaist.HasValue || sg.MaxWaist.HasValue) ? new MeasurementRange { Min = sg.MinWaist, Max = sg.MaxWaist } : null,
                    Hips = (sg.MinHips.HasValue || sg.MaxHips.HasValue) ? new MeasurementRange { Min = sg.MinHips, Max = sg.MaxHips } : null,
                    Weight = (sg.MinWeight.HasValue || sg.MaxWeight.HasValue) ? new MeasurementRange { Min = sg.MinWeight, Max = sg.MaxWeight } : null
                },
                GarmentMeasurements = new GarmentMeasurements
                {
                    ChestCm = sg.ChestCm,
                    WaistCm = sg.WaistCm,
                    HipCm = sg.HipCm,
                    ShoulderCm = sg.ShoulderCm,
                    LengthCm = sg.LengthCm,
                    SleeveCm = sg.SleeveCm
                }
            }).ToList();

            return ApiResponse<List<SizeGuideResponse>>.SuccessResponse(response);
        }

        public async Task<ApiResponse<RecommendSizeResponse>> RecommendSizeAsync(int productId, int userId)
        {
            // Check product exists
            if (!await _productRepository.ExistsAndActiveAsync(productId))
                return ApiResponse<RecommendSizeResponse>.ErrorResponse("Product not found.");

            // Check user has body profile
            var bodyProfile = await _bodyProfileRepository.GetByUserIdAsync(userId);
            if (bodyProfile == null)
                return ApiResponse<RecommendSizeResponse>.ErrorResponse(
                    "You have not entered your body measurements yet. Please update your Body Profile first for accurate AI size recommendation.");

            // Check product has size guide
            var sizeGuides = await _sizeGuideRepository.GetByProductIdAsync(productId);
            if (!sizeGuides.Any())
                return ApiResponse<RecommendSizeResponse>.ErrorResponse(
                    "This product does not have a size guide yet.");

            // Get variants for stock check
            var variants = await _variantRepository.GetByProductIdAsync(productId);

            // AI Fit-Check Algorithm
            var sizeComparisons = new List<SizeComparisonItem>();

            foreach (var sg in sizeGuides)
            {
                var fitScore = CalculateFitScore(bodyProfile, sg);
                var fitLevel = GetFitLevel(fitScore);
                var details = GenerateFitDetails(bodyProfile, sg, fitScore);
                var inStock = variants.Any(v => v.Size == sg.SizeName && (v.StockQuantity ?? 0) > 0);

                sizeComparisons.Add(new SizeComparisonItem
                {
                    SizeName = sg.SizeName,
                    FitScore = fitScore,
                    FitLevel = fitLevel,
                    Details = details,
                    InStock = inStock
                });
            }

            // Apply FitPreference adjustment
            ApplyFitPreference(sizeComparisons, bodyProfile.FitPreference, sizeGuides);

            // Sort by fitScore descending, pick best
            sizeComparisons = sizeComparisons.OrderByDescending(s => s.FitScore).ToList();
            var bestSize = sizeComparisons.First();

            var suggestion = GenerateSuggestion(bodyProfile, bestSize);

            var response = new RecommendSizeResponse
            {
                RecommendedSize = bestSize.SizeName,
                FitScore = bestSize.FitScore,
                FitLevel = bestSize.FitLevel,
                UserProfile = new UserBodyInfo
                {
                    Bust = bodyProfile.Bust,
                    Waist = bodyProfile.Waist,
                    Hips = bodyProfile.Hips,
                    Weight = bodyProfile.Weight
                },
                SizeComparison = sizeComparisons,
                Suggestion = suggestion
            };

            return ApiResponse<RecommendSizeResponse>.SuccessResponse(response);
        }

        #endregion

        #region Reviews

        public async Task<ApiResponse<ProductReviewListResponse>> GetProductReviewsAsync(
            int productId, int page, int pageSize, int? rating, string sortBy)
        {
            if (!await _productRepository.ExistsAndActiveAsync(productId))
                return ApiResponse<ProductReviewListResponse>.ErrorResponse("Product not found.");

            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;
            if (page < 1) page = 1;

            // Get product for averageRating + totalReviews
            var product = await _productRepository.GetByIdAsync(productId);

            var (reviews, totalCount) = await _reviewRepository.GetByProductIdPagedAsync(
                productId, page, pageSize, rating, sortBy);

            var ratingDistribution = await _reviewRepository.GetRatingDistributionAsync(productId);

            var response = new ProductReviewListResponse
            {
                Summary = new ReviewSummary
                {
                    AverageRating = product?.AverageRating ?? 0,
                    TotalReviews = product?.TotalReviews ?? 0,
                    RatingDistribution = ratingDistribution
                },
                Reviews = reviews.Select(r => new ReviewItemResponse
                {
                    ReviewId = r.Id,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    ReviewImageUrl = r.ReviewImageUrl,
                    SizeOrdered = r.SizeOrdered,
                    BodyInfo = r.ShowBodyInfo == true ? new ReviewBodyInfo
                    {
                        HeightCm = r.HeightCm,
                        WeightKg = r.WeightKg,
                        ShowBodyInfo = true
                    } : null,
                    User = new ReviewUserInfo
                    {
                        UserId = r.User.Id,
                        FullName = r.User.FullName ?? "Anonymous",
                        AvatarUrl = r.User.AvatarUrl
                    },
                    CreatedAt = r.CreatedAt
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

            return ApiResponse<ProductReviewListResponse>.SuccessResponse(response);
        }

        #endregion

        #region Private Helpers

        private PaginatedResponse<ProductListResponse> BuildPaginatedProductList(
            List<Product> items, int page, int pageSize, int totalCount)
        {
            return new PaginatedResponse<ProductListResponse>
            {
                Items = items.Select(p => new ProductListResponse
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    Slug = p.Slug,
                    Price = p.Price,
                    SalePrice = p.SalePrice,
                    Gender = p.Gender,
                    BrandName = p.BrandName,
                    Tags = p.Tags,
                    PrimaryImage = p.ProductImages.FirstOrDefault(img => img.IsPrimary == true)?.ImageUrl,
                    Category = p.Category != null ? new ProductCategoryInfo
                    {
                        CategoryId = p.Category.Id,
                        Name = p.Category.Name
                    } : null,
                    AverageRating = p.AverageRating,
                    TotalReviews = p.TotalReviews ?? 0,
                    SoldCount = p.SoldCount ?? 0,
                    InStock = p.ProductVariants.Any(v => (v.StockQuantity ?? 0) > 0),
                    IsFeatured = p.IsFeatured == true,
                    AvailableSizes = p.ProductVariants
                        .Where(v => (v.StockQuantity ?? 0) > 0)
                        .Select(v => v.Size)
                        .Distinct()
                        .ToList()
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
        }

        /// <summary>
        /// AI Fit-Check: Calculate how well user's body measurements fit a size guide entry.
        /// Score is 0-100 based on percentage of measurements within range.
        /// </summary>
        private int CalculateFitScore(UserBodyProfile profile, SizeGuide sg)
        {
            var scores = new List<double>();

            // Check Bust
            if (profile.Bust.HasValue && sg.MinBust.HasValue && sg.MaxBust.HasValue)
            {
                scores.Add(CalculateMeasurementScore(profile.Bust.Value, sg.MinBust.Value, sg.MaxBust.Value));
            }

            // Check Waist
            if (profile.Waist.HasValue && sg.MinWaist.HasValue && sg.MaxWaist.HasValue)
            {
                scores.Add(CalculateMeasurementScore(profile.Waist.Value, sg.MinWaist.Value, sg.MaxWaist.Value));
            }

            // Check Hips
            if (profile.Hips.HasValue && sg.MinHips.HasValue && sg.MaxHips.HasValue)
            {
                scores.Add(CalculateMeasurementScore(profile.Hips.Value, sg.MinHips.Value, sg.MaxHips.Value));
            }

            // Check Weight
            if (profile.Weight.HasValue && sg.MinWeight.HasValue && sg.MaxWeight.HasValue)
            {
                scores.Add(CalculateMeasurementScore(profile.Weight.Value, sg.MinWeight.Value, sg.MaxWeight.Value));
            }

            if (!scores.Any())
                return 50; // Default if no measurements to compare

            return (int)Math.Round(scores.Average());
        }

        /// <summary>
        /// Calculate score for a single measurement.
        /// 100 = perfectly within range, decreases as value moves outside range.
        /// </summary>
        private double CalculateMeasurementScore(decimal value, decimal min, decimal max)
        {
            if (value >= min && value <= max)
            {
                // Within range: score 80-100 based on how centered
                var mid = (min + max) / 2;
                var range = max - min;
                if (range == 0) return 100;
                var deviation = Math.Abs(value - mid) / (range / 2);
                return 100 - (double)deviation * 20; // 80-100
            }
            else
            {
                // Outside range: penalty based on how far
                var rangeSpan = max - min;
                if (rangeSpan == 0) rangeSpan = 1;

                decimal overflow;
                if (value < min)
                    overflow = min - value;
                else
                    overflow = value - max;

                var penaltyRatio = (double)(overflow / rangeSpan);
                var score = 70 - penaltyRatio * 50; // Decreases from 70

                return Math.Max(0, score);
            }
        }

        private string GetFitLevel(int fitScore)
        {
            return fitScore switch
            {
                >= 85 => "Very suitable",
                >= 70 => "Suitable",
                >= 55 => "Slightly loose",
                >= 40 => "Tight",
                _ => "Not recommended"
            };
        }

        private string GenerateFitDetails(UserBodyProfile profile, SizeGuide sg, int fitScore)
        {
            var details = new List<string>();

            if (profile.Bust.HasValue && sg.MinBust.HasValue && sg.MaxBust.HasValue)
            {
                if (profile.Bust.Value > sg.MaxBust.Value)
                    details.Add($"Bust exceeds {profile.Bust.Value - sg.MaxBust.Value:0.#}cm over size {sg.SizeName}");
                else if (profile.Bust.Value < sg.MinBust.Value)
                    details.Add($"Bust is {sg.MinBust.Value - profile.Bust.Value:0.#}cm under size {sg.SizeName}");
            }

            if (profile.Waist.HasValue && sg.MinWaist.HasValue && sg.MaxWaist.HasValue)
            {
                if (profile.Waist.Value > sg.MaxWaist.Value)
                    details.Add($"Waist exceeds {profile.Waist.Value - sg.MaxWaist.Value:0.#}cm over size {sg.SizeName}");
                else if (profile.Waist.Value < sg.MinWaist.Value)
                    details.Add($"Waist is {sg.MinWaist.Value - profile.Waist.Value:0.#}cm under size {sg.SizeName}");
            }

            if (profile.Hips.HasValue && sg.MinHips.HasValue && sg.MaxHips.HasValue)
            {
                if (profile.Hips.Value > sg.MaxHips.Value)
                    details.Add($"Hips exceeds {profile.Hips.Value - sg.MaxHips.Value:0.#}cm over size {sg.SizeName}");
                else if (profile.Hips.Value < sg.MinHips.Value)
                    details.Add($"Hips is {sg.MinHips.Value - profile.Hips.Value:0.#}cm under size {sg.SizeName}");
            }

            if (!details.Any())
            {
                if (fitScore >= 85)
                    return $"Measurements are within the standard range of size {sg.SizeName}. Great fit with your preference.";
                else
                    return $"Measurements are close to size {sg.SizeName} range.";
            }

            return string.Join(". ", details) + ".";
        }

        private void ApplyFitPreference(List<SizeComparisonItem> comparisons, string? fitPreference,
            List<SizeGuide> sizeGuides)
        {
            if (string.IsNullOrWhiteSpace(fitPreference))
                return;

            // Sort size guides by size name to establish order
            var sizeOrder = sizeGuides
                .Select(sg => sg.SizeName)
                .Distinct()
                .OrderBy(s => GetSizeOrderIndex(s))
                .ToList();

            foreach (var comp in comparisons)
            {
                var sizeIndex = sizeOrder.IndexOf(comp.SizeName);
                if (sizeIndex < 0) continue;

                switch (fitPreference.ToUpper())
                {
                    case "TIGHT":
                        // Prefer smaller sizes: bonus for lower index
                        comp.FitScore = Math.Min(100, comp.FitScore + (sizeOrder.Count - 1 - sizeIndex) * 3);
                        break;
                    case "LOOSE":
                        // Prefer larger sizes: bonus for higher index
                        comp.FitScore = Math.Min(100, comp.FitScore + sizeIndex * 3);
                        break;
                    // REGULAR: no adjustment needed
                }

                // Recalculate fit level after adjustment
                comp.FitLevel = GetFitLevel(comp.FitScore);
            }
        }

        private int GetSizeOrderIndex(string sizeName)
        {
            var sizeMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "XS", 0 }, { "S", 1 }, { "M", 2 }, { "L", 3 },
                { "XL", 4 }, { "2XL", 5 }, { "XXL", 5 },
                { "3XL", 6 }, { "XXXL", 6 },
                { "4XL", 7 }, { "5XL", 8 }, { "6XL", 9 }
            };

            return sizeMap.TryGetValue(sizeName, out var index) ? index : 99;
        }

        private string GenerateSuggestion(UserBodyProfile profile, SizeComparisonItem bestSize)
        {
            var bust = profile.Bust.HasValue ? $"{profile.Bust.Value:0.#}" : "N/A";
            var waist = profile.Waist.HasValue ? $"{profile.Waist.Value:0.#}" : "N/A";
            var hips = profile.Hips.HasValue ? $"{profile.Hips.Value:0.#}" : "N/A";
            var weight = profile.Weight.HasValue ? $"{profile.Weight.Value:0.#}kg" : "N/A";

            var preference = profile.FitPreference?.ToUpper() switch
            {
                "TIGHT" => "snug fit",
                "LOOSE" => "relaxed fit",
                _ => "regular fit"
            };

            return $"With measurements {bust}/{waist}/{hips} and weight {weight}, " +
                   $"size {bestSize.SizeName} is the best choice for you ({bestSize.FitLevel}). " +
                   $"This recommendation is based on your {preference} preference.";
        }

        #endregion
    }
}
