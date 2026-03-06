namespace MV.DomainLayer.DTOs.Product.Response
{
    public class SizeGuideResponse
    {
        public string SizeName { get; set; } = null!;
        public BodyMeasurementRange BodyMeasurements { get; set; } = new();
        public GarmentMeasurements GarmentMeasurements { get; set; } = new();
    }

    public class BodyMeasurementRange
    {
        public MeasurementRange? Bust { get; set; }
        public MeasurementRange? Waist { get; set; }
        public MeasurementRange? Hips { get; set; }
        public MeasurementRange? Weight { get; set; }
    }

    public class MeasurementRange
    {
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
    }

    public class GarmentMeasurements
    {
        public decimal? ChestCm { get; set; }
        public decimal? WaistCm { get; set; }
        public decimal? HipCm { get; set; }
        public decimal? ShoulderCm { get; set; }
        public decimal? LengthCm { get; set; }
        public decimal? SleeveCm { get; set; }
    }
}
